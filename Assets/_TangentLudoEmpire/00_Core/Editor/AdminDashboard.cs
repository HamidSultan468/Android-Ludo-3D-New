using System;
using UnityEditor;
using UnityEngine;
using TangentLudoEmpire.Core;
using TangentLudoEmpire.Wallet;
using TangentLudoEmpire.Kyc;
using TangentLudoEmpire.Services;

namespace TangentLudoEmpire.Core.EditorTools
{
    /// <summary>
    /// Task 5. <b>Scope note:</b> this is a LOCAL, single-player dev/testing admin tool - it reads and
    /// writes the SAME encrypted files (<c>tle_wallet_v1.enc</c> / <c>tle_kyc_v1.enc</c>) any other
    /// Phase 3.x Editor tool does, via <see cref="Phase3Tools"/>'s shared crypto helpers. A real
    /// production admin panel needs the backend from <c>BACKEND_WEBHOOK_SPEC.md</c> plus a proper
    /// multi-user data store (a database of every player's requests, auth, roles) - none of which exists
    /// yet. Building that is out of scope for an Editor window; this is the dev-loop equivalent, useful
    /// for exactly the same reason <c>Phase3Tools</c>' menu items are.
    ///
    /// In Play mode (<see cref="MoneyWallet.Instance"/> up), Approve/Reject drive the live wallet - a real
    /// payout gateway call happens, with its own 5s/95% mock timing. In Edit mode they resolve
    /// instantly and deterministically against the file, like every other Phase 3.x Edit-mode tool.
    /// </summary>
    public class AdminDashboard : EditorWindow
    {
        [MenuItem("Tangent Ludo Empire/Admin/Dashboard")]
        public static void Open()
        {
            var win = GetWindow<AdminDashboard>("Tangent Admin");
            win.minSize = new Vector2(520, 420);
            win.Refresh();
        }

        private WalletData _wallet;
        private KycData _kyc;
        private Vector2 _scroll;
        private bool _showLedger;
        private string _status = "";

        private void OnEnable() => Refresh();

        private void Refresh()
        {
            var cfg = Phase3Tools.LoadConfig();
            if (cfg == null) { _status = "SecurityConfig missing - run Tangent Ludo Empire/Phase 2/Validate Security first."; return; }

            _wallet = Phase3Tools.ReadEncrypted<WalletData>(cfg, Phase3Tools.WalletPath);
            _kyc = Phase3Tools.ReadEncrypted<KycData>(cfg, Phase3Tools.KycPath);
            _status = _wallet == null ? "No wallet save found yet (nothing deposited/withdrawn)." : "";
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70))) Refresh();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(Application.isPlaying && MoneyWallet.Instance != null ? "LIVE (Play mode)" : "Edit-mode file view",
                                       EditorStyles.miniLabel, GUILayout.Width(140));
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_status)) { EditorGUILayout.HelpBox(_status, MessageType.Info); }
            if (_wallet == null) { if (_kyc == null) return; }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawKycSection();
            EditorGUILayout.Space(10);
            DrawWithdrawSection();
            EditorGUILayout.Space(10);
            DrawLedgerSection();

            EditorGUILayout.EndScrollView();
        }

        // ---------------------------------------------------------------- KYC ----

        private void DrawKycSection()
        {
            EditorGUILayout.LabelField("Pending KYC", EditorStyles.boldLabel);
            if (_kyc == null) { EditorGUILayout.LabelField("(no KYC record yet)"); return; }

            using (new EditorGUILayout.HorizontalScope("box"))
            {
                EditorGUILayout.LabelField(_kyc.IsVerified
                    ? $"Verified - CNIC {_kyc.MaskedCnic}, phone {_kyc.PhoneNumber}, at {_kyc.VerifiedAt}"
                    : "NOT verified - withdrawals are blocked (MoneyWallet.RequestWithdraw checks KycManager.CanWithdraw()).");

                if (!_kyc.IsVerified)
                {
                    if (GUILayout.Button("Approve", GUILayout.Width(80))) SetKyc(true);
                }
                else
                {
                    if (GUILayout.Button("Revoke", GUILayout.Width(80))) SetKyc(false);
                }
            }
        }

        private void SetKyc(bool verified)
        {
            if (Application.isPlaying && KycManager.Instance != null)
            {
                KycManager.Instance.Editor_SetVerified(verified);
                Debug.Log($"[AdminDashboard] Live KycManager set verified={verified}.");
                Refresh();
                return;
            }

            var cfg = Phase3Tools.LoadConfig();
            if (cfg == null) return;
            _kyc ??= KycData.CreateNew();
            _kyc.IsVerified = verified;
            if (verified)
            {
                if (string.IsNullOrEmpty(_kyc.CNIC)) _kyc.CNIC = "35202-1234567-1";
                if (string.IsNullOrEmpty(_kyc.PhoneNumber)) _kyc.PhoneNumber = "03000000000";
                _kyc.VerifiedAt = DateTime.UtcNow;
            }
            Phase3Tools.WriteEncrypted(cfg, Phase3Tools.KycPath, _kyc);
            Debug.Log($"[AdminDashboard] KYC {(verified ? "approved" : "revoked")} (edit-mode file write).");
            Refresh();
        }

        // ---------------------------------------------------------------- withdraw requests ----

        private void DrawWithdrawSection()
        {
            EditorGUILayout.LabelField("Pending Withdraw Requests", EditorStyles.boldLabel);
            if (_wallet == null || _wallet.WithdrawRequests == null || _wallet.WithdrawRequests.Count == 0)
            {
                EditorGUILayout.LabelField("(none)");
                return;
            }

            bool any = false;
            foreach (var req in _wallet.WithdrawRequests)
            {
                if (req.Status != WithdrawStatus.Pending) continue;
                any = true;
                using (new EditorGUILayout.HorizontalScope("box"))
                {
                    EditorGUILayout.LabelField($"{req.RequestId}  {req.Amount:0.00} via {req.Provider} -> {req.Account}  ({req.CreatedAtUtc:u})");
                    if (GUILayout.Button("Approve", GUILayout.Width(80))) Resolve(req.RequestId, approve: true);
                    if (GUILayout.Button("Reject", GUILayout.Width(80))) Resolve(req.RequestId, approve: false);
                }
            }
            if (!any) EditorGUILayout.LabelField("(none pending)");
        }

        private void Resolve(string requestId, bool approve)
        {
            if (Application.isPlaying && MoneyWallet.Instance != null)
            {
                if (approve)
                    MoneyWallet.Instance.ApproveWithdraw(requestId, (result, refOrErr) =>
                        Debug.Log($"[AdminDashboard] Live approve {requestId}: {result} ({refOrErr})"));
                else
                    MoneyWallet.Instance.RejectWithdraw(requestId, "ADMIN_REJECTED", (result, refOrErr) =>
                        Debug.Log($"[AdminDashboard] Live reject {requestId}: {result}"));
                Refresh();
                return;
            }

            var cfg = Phase3Tools.LoadConfig();
            if (cfg == null || _wallet == null) return;

            var req = _wallet.WithdrawRequests.Find(r => r.RequestId == requestId && r.Status == WithdrawStatus.Pending);
            if (req == null) return;

            if (approve)
            {
                // Deterministic Edit-mode simulation - a live approval would call PayoutGatewayFactory
                // for real; here (no coroutine/thread ticking outside Play) it just resolves instantly.
                req.Status = WithdrawStatus.Paid;
                req.PayoutRef = "PAYOUT_ADMIN_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                AuditLog.Log("WITHDRAW_ADMIN_APPROVE", requestId, "(edit-mode)");
            }
            else
            {
                req.Status = WithdrawStatus.Rejected;
                req.Note = "ADMIN_REJECTED";
                _wallet.Balance += req.Amount; // refund
                _wallet.AddTransaction(TangentLudoEmpire.Wallet.Transaction.New(TxType.Refund, req.Amount, TxStatus.Success, requestId));
                AuditLog.Log("WITHDRAW_ADMIN_REJECT", requestId, "(edit-mode)");
            }

            Phase3Tools.WriteEncrypted(cfg, Phase3Tools.WalletPath, _wallet);
            Debug.Log($"[AdminDashboard] {(approve ? "Approved" : "Rejected")} {requestId} (edit-mode file write).");
            Refresh();
        }

        // ---------------------------------------------------------------- ledger ----

        private void DrawLedgerSection()
        {
            _showLedger = EditorGUILayout.Foldout(_showLedger, "View Ledger", true);
            if (!_showLedger || _wallet == null) return;

            EditorGUILayout.LabelField($"Balance: {_wallet.Balance:0.00}   ({_wallet.History.Count} transactions, {_wallet.WithdrawRequests.Count} withdraw requests)");
            foreach (var t in _wallet.History)
                EditorGUILayout.LabelField($"  {t.TimestampUtc:u}  {t.Type,-9} {t.Amount,10:0.00}  {t.Status}  ref={t.GatewayRef}");
            foreach (var r in _wallet.WithdrawRequests)
                EditorGUILayout.LabelField($"  WD {r.CreatedAtUtc:u}  {r.Amount,10:0.00}  {r.Status}  {r.Provider}->{r.Account}  {r.PayoutRef}");
        }
    }
}
