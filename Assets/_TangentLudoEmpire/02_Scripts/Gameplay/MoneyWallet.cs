using System;
using System.Collections.Generic;
using UnityEngine;
using TangentLudoEmpire.Core;
using TangentLudoEmpire.Services;
using TangentLudoEmpire.Kyc;
using TangentLudoEmpire.Payments;

namespace TangentLudoEmpire.Wallet
{
    /// <summary>
    /// The real-money wallet (distinct from <see cref="TangentLudoEmpire.Core.WalletManager"/>, which is
    /// the in-game <b>coin</b> façade). Server-authoritative in intent: every credit path is gated, every
    /// change is persisted encrypted + hashed + audited, and the balance here is reconciled against the
    /// backend by <see cref="AntiCheatManager.ValidateWallet"/>.
    ///
    /// Money is ALWAYS <see cref="decimal"/>. No <c>float</c>/<c>double</c> anywhere in this file.
    ///
    /// Persistence: <see cref="SaveService.SaveEncrypted{T}"/> / <see cref="SaveService.LoadEncrypted{T}"/>
    /// with file <c>tle_wallet_v1.enc</c> (AES-256 + SHA-256 envelope, same as the profile).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-150)] // after SecurityManager(-250) / SaveService(-200), before gameplay
    public class MoneyWallet : MonoBehaviour
    {
        public const string WalletFile = "tle_wallet_v1.enc";

        public static MoneyWallet Instance { get; private set; }

        /// <summary>Fired after any successful balance change, with the new balance.</summary>
        public event Action<decimal> OnBalanceChanged;
        /// <summary>Fired when a transaction is recorded (success or failure).</summary>
        public event Action<Transaction> OnTransaction;

        private WalletData _data;
        private string _checksum = "";

        /// <summary>Phase 5.1: set by <see cref="TangentLudoEmpire.Security.WalletSecurityBridge.FreezeAccount"/>
        /// when anti-cheat catches an illegal move / speed hack. Not persisted on purpose - a freeze is a
        /// live session-safety gate on top of the account's server-side status, not a durable wallet field;
        /// a real backend integration would ALSO carry this server-side so it survives app restarts and a
        /// re-login on a different device still sees the account frozen (see MIGRATION_NOTES Phase 5 Deferred).
        /// Checked by <see cref="RBAC.CanSpend"/> so it actually blocks DeductFunds/RequestWithdraw.</summary>
        public bool IsFrozen { get; private set; }

        /// <summary>See <see cref="IsFrozen"/>. Idempotent either direction; audits every transition.</summary>
        public void SetFrozen(bool frozen, string reason = "")
        {
            if (IsFrozen == frozen) return;
            IsFrozen = frozen;
            AuditLog.Log(frozen ? "WALLET_FROZEN" : "WALLET_UNFROZEN", reason ?? "", $"balance={(_data?.Balance ?? 0m):0.00}");
            Debug.LogWarning($"[MoneyWallet] {(frozen ? "FROZEN" : "unfrozen")} - {reason}");
        }

        // ---------------------------------------------------------------- lifecycle ----

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject); // Editor/test code (headless tools) would otherwise hit InvalidOperationException here and abort the rest of Awake()
            LoadOrCreate();
        }

        private void LoadOrCreate()
        {
            string playerId = BackendService.Instance != null ? BackendService.Instance.UserId
                            : (SaveService.Instance != null && SaveService.Instance.Profile != null
                                ? SaveService.Instance.Profile.profileId : "local");

            _data = SaveService.Instance != null
                ? SaveService.Instance.LoadEncrypted<WalletData>(WalletFile)
                : null;

            if (_data == null)
            {
                _data = WalletData.CreateNew(playerId);
                Persist(0m, "WALLET_INIT");
                Debug.Log("[MoneyWallet] Fresh encrypted wallet created.");
            }
            else
            {
                if (string.IsNullOrEmpty(_data.PlayerId)) _data.PlayerId = playerId;
                _checksum = SecurityManager.Instance != null ? SecurityManager.Instance.GetHash(_data.IntegrityString()) : "";
                Debug.Log($"[MoneyWallet] Loaded wallet: balance {_data.Balance:0.00}, {_data.History.Count} tx.");
            }
        }

        // ---------------------------------------------------------------- reads ----

        public decimal GetBalance() => _data?.Balance ?? 0m;

        public System.Collections.Generic.IReadOnlyList<Transaction> GetHistory() =>
            (System.Collections.Generic.IReadOnlyList<Transaction>)_data?.History
            ?? Array.Empty<Transaction>();

        // ---------------------------------------------------------------- credits ----

        /// <summary>
        /// Add funds. Only ever called from a <see cref="TangentLudoEmpire.Payments.IPaymentGateway"/>
        /// callback (deposit) or an integration hook (<see cref="OnGameWin"/>, bonus).
        ///
        /// When <paramref name="gatewayRef"/> is supplied (a deposit), the ref is first run past
        /// <see cref="AntiCheatManager.ValidatePayment"/> - "never trust the client": the settlement id
        /// must be server-verified before a single unit is credited.
        /// </summary>
        public void AddFunds(decimal amount, string source, string gatewayRef = null, Action<bool> done = null)
        {
            amount = decimal.Round(amount, 2);
            if (amount <= 0m)
            {
                AuditLog.Log("WALLET_ADD_REJECT", source ?? "", $"bad amount {amount}");
                done?.Invoke(false);
                return;
            }

            if (!string.IsNullOrEmpty(gatewayRef))
            {
                RunPaymentGate(gatewayRef, ok =>
                {
                    if (!ok)
                    {
                        RecordFailed(TxType.Deposit, amount, gatewayRef);
                        AuditLog.Log("DEPOSIT_REJECTED", source ?? "", gatewayRef);
                        done?.Invoke(false);
                        return;
                    }
                    ApplyCredit(amount, TxType.Deposit, source, gatewayRef);
                    done?.Invoke(true);
                });
                return;
            }

            // No gateway ref => internal credit (game win / tournament win / refund / bonus). Amount is
            // decided elsewhere (server reward path, or - Phase 4 - RoomGameBridge/TournamentManager's own
            // config-driven math); we just record and tag it.
            var type = TagFor(source);
            ApplyCredit(amount, type, source, "");
            done?.Invoke(true);
        }

        /// <summary>Maps an AddFunds <c>source</c> tag to the ledger TxType (Phase 4 additions:
        /// TOURNAMENT_WIN/ROOM_WIN read as a game win, *_REFUND as a refund; anything else is Bonus).</summary>
        private static TxType TagFor(string source)
        {
            if (string.Equals(source, "GAME_WIN", StringComparison.OrdinalIgnoreCase)) return TxType.GameWin;
            if (string.Equals(source, "TOURNAMENT_WIN", StringComparison.OrdinalIgnoreCase)) return TxType.GameWin;
            if (string.Equals(source, "ROOM_WIN", StringComparison.OrdinalIgnoreCase)) return TxType.GameWin;
            if (!string.IsNullOrEmpty(source) && source.EndsWith("_REFUND", StringComparison.OrdinalIgnoreCase)) return TxType.Refund;
            return TxType.Bonus;
        }

        private void ApplyCredit(decimal amount, TxType type, string source, string gatewayRef)
        {
            _data.Balance += amount;
            var tx = Transaction.New(type, amount, TxStatus.Success, gatewayRef);
            _data.AddTransaction(tx);
            Persist(amount, $"+{source}");
            OnTransaction?.Invoke(tx);
            Debug.Log($"[MoneyWallet] +{amount:0.00} ({type}/{source}) -> {_data.Balance:0.00}");
        }

        // ---------------------------------------------------------------- pending deposits (Phase 3.2) ----
        //
        // A WebView-based deposit (JazzCash/Easypaisa) takes real wall-clock time between "user tapped
        // Confirm" and "gateway redirected back with a result" - during which the app can be backgrounded,
        // killed, or the device can lose connectivity. Recording the attempt as a Pending transaction
        // BEFORE opening the browser (BeginPendingDeposit), and only ever crediting the balance once, the
        // first time ConfirmPendingDeposit finds that specific still-Pending row, means:
        //   * a lost/duplicated callback can't double-credit (the second call finds no Pending row left);
        //   * a closed app mid-payment leaves an honest Pending row in history instead of silently
        //     losing or double-counting money (reconciling stale Pending rows against the backend is a
        //     production concern, not solved here - see MIGRATION_NOTES Phase 3.2 Deferred).

        /// <summary>Records a deposit as started (Status = Pending) BEFORE the WebView opens. Returns the
        /// transaction's internal TxId, or null if the amount/ref was invalid.</summary>
        public string BeginPendingDeposit(string pendingTxId, decimal amount, string source)
        {
            amount = decimal.Round(amount, 2);
            if (amount <= 0m || string.IsNullOrEmpty(pendingTxId))
            {
                AuditLog.Log("DEPOSIT_PENDING_REJECT", source ?? "", $"amount={amount} ref={pendingTxId}");
                return null;
            }

            var tx = Transaction.New(TxType.Deposit, amount, TxStatus.Pending, pendingTxId);
            _data.AddTransaction(tx);
            Persist(0m, $"PENDING:{source}:{pendingTxId}"); // no balance change yet
            OnTransaction?.Invoke(tx);
            Debug.Log($"[MoneyWallet] Pending deposit opened: {pendingTxId} ({amount:0.00}, {source}).");
            return tx.TxId;
        }

        /// <summary>Called only once a gateway callback CONFIRMS success for <paramref name="pendingTxId"/>.
        /// Finds the matching Pending row, runs it through the SAME anti-cheat payment gate as
        /// <see cref="AddFunds"/>, and only then credits the balance and flips the row to Success.
        /// Idempotent: a second call for the same ref finds no Pending row left and is a safe no-op -
        /// this is the double-credit guard. Credits the amount RECORDED at BeginPendingDeposit, not
        /// <paramref name="amount"/> (a caller-supplied figure is a claim, not a grant - if the two
        /// disagree that's logged, and the originally-recorded amount wins).</summary>
        public void ConfirmPendingDeposit(string pendingTxId, decimal amount, Action<bool> done = null) =>
            ConfirmPendingDeposit(pendingTxId, (decimal?)amount, done);

        /// <summary>Phase 3.5: same as above, for callers that genuinely don't know the amount (a deep
        /// link like <c>tle://callback?orderId=...&amp;status=000</c> carries no amount field) - the
        /// pending row's recorded amount is what gets credited either way, so this simply skips the
        /// mismatch-warning check that comparing against an unknown value would otherwise spam.</summary>
        public void ConfirmPendingDeposit(string pendingTxId, Action<bool> done = null) =>
            ConfirmPendingDeposit(pendingTxId, (decimal?)null, done);

        private void ConfirmPendingDeposit(string pendingTxId, decimal? amount, Action<bool> done)
        {
            var tx = FindPending(pendingTxId);
            if (tx == null)
            {
                AuditLog.Log("DEPOSIT_CONFIRM_MISS", pendingTxId ?? "",
                    "no matching Pending transaction (already confirmed, expired, or unknown ref)");
                done?.Invoke(false);
                return;
            }

            if (amount.HasValue && decimal.Round(amount.Value, 2) != tx.Amount)
                Debug.LogWarning($"[MoneyWallet] ConfirmPendingDeposit amount mismatch for {pendingTxId}: " +
                                  $"caller said {amount.Value:0.00}, pending row says {tx.Amount:0.00} - crediting the pending row's amount.");

            RunPaymentGate(pendingTxId, ok =>
            {
                if (!ok)
                {
                    tx.Status = TxStatus.Failed;
                    Persist(0m, $"DEPOSIT_CONFIRM_DENIED:{pendingTxId}");
                    OnTransaction?.Invoke(tx);
                    done?.Invoke(false);
                    return;
                }

                _data.Balance += tx.Amount;
                tx.Status = TxStatus.Success;
                Persist(tx.Amount, $"DEPOSIT_CONFIRMED:{pendingTxId}");
                OnTransaction?.Invoke(tx);
                Debug.Log($"[MoneyWallet] Pending deposit confirmed: {pendingTxId} +{tx.Amount:0.00} -> balance {_data.Balance:0.00}.");
                done?.Invoke(true);
            });
        }

        /// <summary>Called when the WebView flow fails or times out. Flips the Pending row to Failed -
        /// no balance change, nothing left to double-credit later.</summary>
        public void FailPendingDeposit(string pendingTxId, string reason)
        {
            var tx = FindPending(pendingTxId);
            if (tx == null) return; // already resolved, or never existed - nothing to fail
            tx.Status = TxStatus.Failed;
            Persist(0m, $"DEPOSIT_FAILED:{pendingTxId}:{reason}");
            OnTransaction?.Invoke(tx);
            AuditLog.Log("DEPOSIT_FAILED", pendingTxId, reason ?? "");
            Debug.LogWarning($"[MoneyWallet] Pending deposit failed: {pendingTxId} ({reason}).");
        }

        private Transaction FindPending(string pendingTxId)
        {
            if (string.IsNullOrEmpty(pendingTxId) || _data?.History == null) return null;
            for (int i = _data.History.Count - 1; i >= 0; i--)
            {
                var t = _data.History[i];
                if (t.GatewayRef == pendingTxId && t.Status == TxStatus.Pending) return t;
            }
            return null;
        }

        // ---------------------------------------------------------------- debits ----

        /// <summary>
        /// Spend from the wallet. Returns false (and audits) if RBAC forbids spending or the balance is
        /// short - it never goes negative. Used for withdrawals and game entry fees.
        /// <paramref name="gatewayRef"/> (Phase 3.3) lets a caller tag the Transaction for later lookup -
        /// <see cref="RequestWithdraw"/> passes its WithdrawRequest's id so the two records cross-reference.
        /// </summary>
        public bool DeductFunds(decimal amount, string reason, string gatewayRef = null)
        {
            amount = decimal.Round(amount, 2);
            if (amount <= 0m) { AuditLog.Log("WALLET_DEDUCT_REJECT", reason ?? "", $"bad amount {amount}"); return false; }

            if (!RBAC.CanSpend())
            {
                AuditLog.Log("SPEND_BLOCKED", reason ?? "", $"role={RBAC.CurrentRole} walletLocked={(AntiCheatManager.Instance != null && AntiCheatManager.Instance.WalletLocked)}");
                return false;
            }

            if (_data.Balance < amount)
            {
                AuditLog.Log("INSUFFICIENT_FUNDS", reason ?? "", $"need {amount:0.00} have {_data.Balance:0.00}");
                RecordFailed(reason != null && reason.StartsWith("WITHDRAW", StringComparison.OrdinalIgnoreCase)
                    ? TxType.Withdraw : TxType.GameLoss, amount, gatewayRef ?? "");
                return false;
            }

            _data.Balance -= amount;
            var type = reason != null && reason.StartsWith("WITHDRAW", StringComparison.OrdinalIgnoreCase)
                ? TxType.Withdraw : TxType.GameLoss;
            var tx = Transaction.New(type, amount, TxStatus.Success, gatewayRef ?? "");
            _data.AddTransaction(tx);
            Persist(-amount, $"-{reason}");
            OnTransaction?.Invoke(tx);
            Debug.Log($"[MoneyWallet] -{amount:0.00} ({type}/{reason}) -> {_data.Balance:0.00}");
            return true;
        }

        // ---------------------------------------------------------------- withdraw requests (Phase 3.3, Task 2) ----
        //
        // The balance is deducted (via DeductFunds, above) the instant a request is created, whatever its
        // eventual outcome - see WithdrawRequest's class remarks for why (prevents the same reserved money
        // being withdrawn twice while a >10000 request sits waiting for admin approval).

        private const decimal AutoPayoutThreshold = 10000m; // Task 2: "if amount > 10000 -> PENDING else call Payout"

        /// <summary>
        /// Task 2/6: validates (KYC, anti-fraud, RBAC + balance via DeductFunds) and reserves a withdrawal.
        /// Amounts &gt; <see cref="AutoPayoutThreshold"/> stay Pending for <see cref="ApproveWithdraw"/> /
        /// <see cref="RejectWithdraw"/> (AdminDashboard); amounts at or under it pay out immediately
        /// through <see cref="PayoutGatewayFactory"/>. These checks are the AUTHORITY, not just a UX
        /// fast-path - WithdrawPopup pre-checks the same things for instant UI feedback, but this method
        /// never trusts that it already did.
        /// </summary>
        public void RequestWithdraw(decimal amount, string provider, string account, Action<WithdrawResult, string> done = null)
        {
            amount = decimal.Round(amount, 2);
            if (amount <= 0m) { done?.Invoke(WithdrawResult.Rejected, "INVALID_AMOUNT"); return; }

            if (KycManager.Instance == null || !KycManager.Instance.CanWithdraw())
            {
                AuditLog.Log("WITHDRAW_REQUEST_REJECT", "", "KYC not verified");
                done?.Invoke(WithdrawResult.Rejected, "KYC_REQUIRED");
                return;
            }

            if (AntiFraudManager.Instance != null && !AntiFraudManager.Instance.CanTransact("withdraw", amount))
            {
                done?.Invoke(WithdrawResult.Rejected, "FRAUD_LIMIT");
                return;
            }

            var req = WithdrawRequest.New(amount, provider, account);
            if (!DeductFunds(amount, "WITHDRAW_REQUEST", req.RequestId)) // RBAC + balance check + audit, tags the Transaction with req.RequestId
            {
                done?.Invoke(WithdrawResult.Rejected, "INSUFFICIENT_FUNDS_OR_BLOCKED");
                return;
            }

            _data.WithdrawRequests.Add(req);
            Persist(0m, $"WITHDRAW_REQUEST_OPENED:{req.RequestId}");
            AntiFraudManager.Instance?.RecordTransact("withdraw", amount); // only after DeductFunds actually succeeded

            if (amount > AutoPayoutThreshold)
            {
                AuditLog.Log("WITHDRAW_PENDING", req.RequestId, $"{amount:0.00} via {provider} - needs admin approval");
                done?.Invoke(WithdrawResult.Pending, req.RequestId);
                return;
            }

            req.Status = WithdrawStatus.Approved;
            Persist(0m, $"WITHDRAW_AUTO_APPROVE:{req.RequestId}");
            RunPayout(req, done);
        }

        /// <summary>AdminDashboard: approve a Pending request (any amount - not just the &gt;10000 ones)
        /// and trigger its payout.</summary>
        public bool ApproveWithdraw(string requestId, Action<WithdrawResult, string> done = null)
        {
            var req = FindWithdraw(requestId, WithdrawStatus.Pending);
            if (req == null) { done?.Invoke(WithdrawResult.Rejected, "NOT_FOUND"); return false; }

            req.Status = WithdrawStatus.Approved;
            Persist(0m, $"WITHDRAW_ADMIN_APPROVE:{requestId}");
            AuditLog.Log("WITHDRAW_ADMIN_APPROVE", requestId, "");
            RunPayout(req, done);
            return true;
        }

        /// <summary>AdminDashboard: reject a Pending request and refund the reserved amount.</summary>
        public bool RejectWithdraw(string requestId, string reason, Action<WithdrawResult, string> done = null)
        {
            var req = FindWithdraw(requestId, WithdrawStatus.Pending);
            if (req == null) { done?.Invoke(WithdrawResult.Rejected, "NOT_FOUND"); return false; }

            req.Status = WithdrawStatus.Rejected;
            req.Note = reason ?? "";
            req.ProcessedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            RefundReservedAmount(req, $"WITHDRAW_ADMIN_REJECT:{requestId}:{reason}");
            AuditLog.Log("WITHDRAW_ADMIN_REJECT", requestId, reason ?? "");
            done?.Invoke(WithdrawResult.Rejected, reason);
            return true;
        }

        private void RunPayout(WithdrawRequest req, Action<WithdrawResult, string> done)
        {
            var provider = ParseProvider(req.Provider);
            var gateway = PayoutGatewayFactory.GetGateway(provider);
            gateway.Payout(req.Amount, req.Account, (ok, refOrErr) =>
            {
                req.ProcessedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (ok)
                {
                    req.Status = WithdrawStatus.Paid;
                    req.PayoutRef = refOrErr;
                    Persist(0m, $"WITHDRAW_PAID:{req.RequestId}");
                    AuditLog.Log("WITHDRAW_PAID", req.RequestId, refOrErr);
                    TangentLudoEmpire.Bridge.WalletGameBridge.ReportWithdrawSuccess(req.Amount, req.RequestId); // Phase 5.2: AnalyticsBridge hook
                    done?.Invoke(WithdrawResult.Paid, refOrErr);
                }
                else
                {
                    // Payout failed after the funds were already reserved - refund. Rejected is the
                    // closest of the four spec'd states to "this withdrawal did not happen."
                    req.Status = WithdrawStatus.Rejected;
                    req.Note = refOrErr;
                    RefundReservedAmount(req, $"WITHDRAW_PAYOUT_FAILED:{req.RequestId}:{refOrErr}");
                    done?.Invoke(WithdrawResult.Rejected, refOrErr);
                }
            });
        }

        private void RefundReservedAmount(WithdrawRequest req, string reason)
        {
            _data.Balance += req.Amount;
            var tx = Transaction.New(TxType.Refund, req.Amount, TxStatus.Success, req.RequestId);
            _data.AddTransaction(tx);
            Persist(req.Amount, reason);
            OnTransaction?.Invoke(tx);
            AuditLog.Log("WITHDRAW_REFUNDED", req.RequestId, reason);
        }

        private WithdrawRequest FindWithdraw(string requestId, WithdrawStatus mustBe)
        {
            if (string.IsNullOrEmpty(requestId) || _data?.WithdrawRequests == null) return null;
            foreach (var r in _data.WithdrawRequests)
                if (r.RequestId == requestId && r.Status == mustBe) return r;
            return null;
        }

        private static PaymentProvider ParseProvider(string s) =>
            Enum.TryParse<PaymentProvider>(s, true, out var p) ? p : PaymentProvider.JazzCash;

        public IReadOnlyList<WithdrawRequest> GetWithdrawRequests() =>
            (IReadOnlyList<WithdrawRequest>)_data?.WithdrawRequests ?? Array.Empty<WithdrawRequest>();

        // ---------------------------------------------------------------- ledger ----

        /// <summary>Append a caller-built transaction (history is capped at
        /// <see cref="WalletData.MaxHistory"/>). Does NOT move the balance.</summary>
        public void AddTransaction(Transaction tx)
        {
            if (tx == null) return;
            _data.AddTransaction(tx);
            Persist(0m, "TX_APPEND");
            OnTransaction?.Invoke(tx);
        }

        private void RecordFailed(TxType type, decimal amount, string gatewayRef)
        {
            var tx = Transaction.New(type, amount, TxStatus.Failed, gatewayRef);
            _data.AddTransaction(tx);
            Persist(0m, "TX_FAILED");
            OnTransaction?.Invoke(tx);
        }

        // ---------------------------------------------------------------- integration hooks ----
        // FlowManager lives in the do-not-touch LudoEmpire.Ludo layer, so it cannot call AddFunds/
        // DeductFunds directly. Phase 3.4: TangentLudoEmpire.Bridge.WalletGameBridge.TryEnterGame /
        // ReportWin are the supported entry points (they call AddFunds/DeductFunds above directly) -
        // LudoWalletHooks forwards the game's win/entry events into that bridge.

        // ---------------------------------------------------------------- persistence + integrity ----

        private void Persist(decimal delta, string note)
        {
            string integ = _data.IntegrityString();
            _checksum = SecurityManager.Instance != null ? SecurityManager.Instance.GetHash(integ) : "";

            bool ok = SaveService.Instance != null && SaveService.Instance.SaveEncrypted(WalletFile, _data);
            if (!ok) Debug.LogError("[MoneyWallet] SaveEncrypted failed - wallet change not persisted!");

            // SecurityManager.ValidateChecksum: prove the in-memory state still matches the hash we just took.
            if (SecurityManager.Instance != null && !SecurityManager.Instance.ValidateChecksum(integ, _checksum))
                Debug.LogError("[MoneyWallet] Checksum self-check FAILED after write.");

            AuditLog.Log("WALLET_CHANGE",
                before: note,
                after: $"delta={delta:0.00} balance={_data.Balance:0.00} device={(SecurityManager.Instance != null ? SecurityManager.Instance.DeviceId : "")}");

            OnBalanceChanged?.Invoke(_data.Balance);
        }

        private void RunPaymentGate(string gatewayRef, Action<bool> done)
        {
            var ac = AntiCheatManager.Instance;
            if (ac != null) { ac.ValidatePayment(gatewayRef, done); return; }
#if UNITY_EDITOR
            Debug.LogWarning("[MoneyWallet] AntiCheatManager unavailable - editor allow for payment gate.");
            done(true);
#else
            AuditLog.Log("PAYMENT_GATE_NO_ANTICHEAT", gatewayRef, "deny");
            done(false);
#endif
        }

        /// <summary>Decrypt-and-verify plus a ledger-consistency check. Used by the editor tool and by
        /// anti-cheat spot-checks. Returns true only if the on-disk wallet decrypts, its SHA-256 envelope
        /// matches, and the balance equals the signed sum of its successful transactions.</summary>
        public bool ValidateIntegrity(out string report)
        {
            if (SaveService.Instance == null) { report = "SaveService not available."; return false; }

            var disk = SaveService.Instance.LoadEncrypted<WalletData>(WalletFile);
            if (disk == null) { report = "wallet file missing, unreadable, or SHA-256 envelope mismatch (tamper)."; return false; }

            decimal ledger = 0m;
            foreach (var t in disk.History)
            {
                if (t.Status != TxStatus.Success) continue;
                switch (t.Type)
                {
                    case TxType.Deposit:
                    case TxType.GameWin:
                    case TxType.Bonus:
                    case TxType.Refund:    ledger += t.Amount; break;
                    case TxType.Withdraw:
                    case TxType.GameLoss:  ledger -= t.Amount; break;
                }
            }
            ledger = decimal.Round(ledger, 2);

            bool consistent = ledger == decimal.Round(disk.Balance, 2);
            report = consistent
                ? $"OK - balance {disk.Balance:0.00} == ledger {ledger:0.00}, {disk.History.Count} tx, envelope hash verified."
                : $"MISMATCH - stored balance {disk.Balance:0.00} != ledger sum {ledger:0.00}.";
            return consistent;
        }

        // ---------------------------------------------------------------- editor tools support ----
#if UNITY_EDITOR
        /// <summary>EDITOR ONLY - compiled out of every player build. Used by Phase3Tools.</summary>
        public void Editor_Reset()
        {
            _data = WalletData.CreateNew(_data != null ? _data.PlayerId : "local");
            Persist(0m, "EDITOR_RESET");
        }

        /// <summary>EDITOR ONLY - compiled out of every player build. Used by Phase3Tools.</summary>
        public void Editor_GrantBonus(decimal amount) => AddFunds(amount, "EDITOR_GRANT");
#endif
    }
}
