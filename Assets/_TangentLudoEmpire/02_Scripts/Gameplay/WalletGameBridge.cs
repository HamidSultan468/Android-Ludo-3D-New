using System;
using UnityEngine;
using TangentLudoEmpire.Wallet;

namespace TangentLudoEmpire.Bridge
{
    /// <summary>
    /// FINAL wallet &lt;-&gt; gameplay bridge (Phase 3.4). Supersedes the Phase 3 scaffold that lived at
    /// <c>Phase3/Wallet/WalletGameBridge.cs</c> (a MonoBehaviour singleton with instance forwarders,
    /// deleted this phase) - this one is the static class Task 1 asked for, so gameplay code can call
    /// <see cref="TryEnterGame"/>/<see cref="ReportWin"/> with no object reference to hold onto.
    ///
    /// Does not touch <c>LudoEmpire.Ludo</c>. Actually firing these from inside the match lifecycle is
    /// <see cref="LudoWalletHooks"/>'s job - a separate, Inspector-friendly MonoBehaviour a human drops
    /// on a gameplay object; see its class remarks for the two manual calls still needed inside
    /// <c>LudoBoardLogic</c> / <c>LudoVictoryScreenController</c> (both off-limits to this session).
    /// </summary>
    public static class WalletGameBridge
    {
        /// <summary>Fired the instant an entry-fee deduction is attempted, BEFORE the wallet is touched -
        /// a listener (<see cref="GameWalletConnector"/>) can read the pre-deduction balance to decide
        /// whether to show a low-balance popup.</summary>
        public static event Action<decimal> OnGameEntryRequested;

        /// <summary>Fired after a win has been credited.</summary>
        public static event Action<decimal> OnGameWinReported;

        /// <summary>Phase 5.1: playerId, reason. Fired by <see cref="ReportSuspiciousActivity"/> -
        /// <see cref="TangentLudoEmpire.Security.WalletSecurityBridge"/> listens on this to freeze the account.</summary>
        public static event Action<string, string> OnSuspiciousActivity;

        /// <summary>Phase 5.2: amount, requestId. Fired by <see cref="ReportWithdrawSuccess"/> once a
        /// withdrawal actually pays out - <see cref="TangentLudoEmpire.Analytics.AnalyticsBridge"/> hooks
        /// this to log the "withdraw" analytics event.</summary>
        public static event Action<decimal, string> OnWithdrawSuccess;

        /// <summary>
        /// Phase 5.1: called by <see cref="TangentLudoEmpire.Security.GameplayAntiCheatManager"/> when a
        /// move fails validation (illegal move) or trips the speed-hack timer. This bridge does not itself
        /// decide what happens next - it only fans the report out to whoever is listening
        /// (<see cref="WalletSecurityBridge"/> freezes the account); kept here rather than calling
        /// WalletSecurityBridge directly so Security -> Bridge stays a one-way dependency, same shape as
        /// every other hook in this class.
        /// </summary>
        public static void ReportSuspiciousActivity(string playerId, string reason)
        {
            Debug.LogWarning($"[WalletGameBridge] Suspicious activity: player={playerId} reason={reason}");
            OnSuspiciousActivity?.Invoke(playerId ?? "", reason ?? "");
        }

        /// <summary>Phase 5.2: called by <see cref="MoneyWallet.RunPayout"/>'s success branch AFTER the
        /// gateway confirms payout (i.e. real money already left, not merely requested) - see
        /// <see cref="OnWithdrawSuccess"/>.</summary>
        public static void ReportWithdrawSuccess(decimal amount, string requestId)
        {
            OnWithdrawSuccess?.Invoke(amount, requestId ?? "");
        }

        /// <summary>Attempts to pay a match's entry fee out of the real-money wallet. Returns false (no
        /// deduction made) if the wallet doesn't exist yet, the fee is invalid, RBAC blocks spending, or
        /// the balance is short - see <see cref="MoneyWallet.DeductFunds"/> for the exact audit trail.</summary>
        public static bool TryEnterGame(decimal fee)
        {
            OnGameEntryRequested?.Invoke(fee);

            var wallet = MoneyWallet.Instance;
            if (wallet == null)
            {
                Debug.LogWarning("[WalletGameBridge] TryEnterGame called before MoneyWallet exists - entry denied.");
                return false;
            }
            return wallet.DeductFunds(fee, "ENTRY_FEE");
        }

        /// <summary>Credits a match win. A missing wallet is a logged no-op, never a throw - a stray call
        /// from gameplay can't crash the game.</summary>
        public static void ReportWin(decimal amount)
        {
            OnGameWinReported?.Invoke(amount);

            var wallet = MoneyWallet.Instance;
            if (wallet == null)
            {
                Debug.LogWarning("[WalletGameBridge] ReportWin called before MoneyWallet exists - win not credited.");
                return;
            }
            wallet.AddFunds(amount, "GAME_WIN"); // AddFunds tags TxType.GameWin for exactly this source string
        }
    }
}
