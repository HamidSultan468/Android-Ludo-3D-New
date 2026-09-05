using System.Collections.Generic;
using UnityEngine;
using TangentLudoEmpire.Bridge;
using TangentLudoEmpire.Wallet;
using TangentLudoEmpire.Tournament;

namespace TangentLudoEmpire.Analytics
{
    /// <summary>
    /// Task B.3. Static class - hooks the existing gameplay/wallet bridges and forwards them to
    /// <see cref="FirebaseManager.LogEvent"/>. Spec named exactly three hook points
    /// (WalletGameBridge.OnGameEntry -&gt; "game_start", OnWin -&gt; "game_win", OnWithdrawSuccess -&gt;
    /// "withdraw") but also asked for "deposit" and "tournament_join" events, which have no matching
    /// WalletGameBridge member to hook - resolved by also listening to
    /// <see cref="MoneyWallet.OnTransaction"/> (fires for every successful deposit) and the new
    /// <see cref="TournamentManager.OnPlayerRegistered"/> (Phase 5 addition, see its class remarks)
    /// rather than inventing events on WalletGameBridge that nothing else needed.
    /// </summary>
    public static class AnalyticsBridge
    {
        private static bool _subscribed;

        /// <summary>Call once MoneyWallet/TournamentManager/FirebaseManager exist (GameServices.EnsureManagers
        /// does this, in dependency order). Idempotent.</summary>
        public static void EnsureSubscribed()
        {
            if (_subscribed) return;
            _subscribed = true;

            WalletGameBridge.OnGameEntryRequested += OnGameEntry;
            WalletGameBridge.OnGameWinReported += OnGameWin;
            WalletGameBridge.OnWithdrawSuccess += OnWithdrawSuccess;

            if (MoneyWallet.Instance != null) MoneyWallet.Instance.OnTransaction += OnWalletTransaction;
            else Debug.LogWarning("[AnalyticsBridge] MoneyWallet not ready yet - 'deposit' events will not be logged this session.");

            if (TournamentManager.Instance != null) TournamentManager.Instance.OnPlayerRegistered += OnTournamentJoin;
            else Debug.LogWarning("[AnalyticsBridge] TournamentManager not ready yet - 'tournament_join' events will not be logged this session.");
        }

        private static void OnGameEntry(decimal fee) =>
            Log("game_start", new Dictionary<string, object> { { "entry_fee", fee } });

        /// <summary>Task B.3 named method - literal signature match for WalletGameBridge.OnGameWinReported
        /// (Action&lt;decimal&gt;). "prize" is the parameter name given in the spec's own example
        /// ({ "entry_fee": 100, "prize": 560 }).</summary>
        public static void OnGameWin(decimal prize) =>
            Log("game_win", new Dictionary<string, object> { { "prize", prize } });

        private static void OnWithdrawSuccess(decimal amount, string requestId) =>
            Log("withdraw", new Dictionary<string, object> { { "amount", amount }, { "request_id", requestId } });

        private static void OnWalletTransaction(Transaction tx)
        {
            if (tx == null || tx.Type != TxType.Deposit || tx.Status != TxStatus.Success) return;
            Log("deposit", new Dictionary<string, object> { { "amount", tx.Amount } });
        }

        private static void OnTournamentJoin(string playerId, decimal fee, string tournamentId) =>
            Log("tournament_join", new Dictionary<string, object> { { "entry_fee", fee }, { "tournament_id", tournamentId } });

        private static void Log(string eventName, Dictionary<string, object> parameters)
        {
            var fb = FirebaseManager.Instance;
            if (fb == null) { Debug.LogWarning($"[AnalyticsBridge] FirebaseManager unavailable - '{eventName}' not logged."); return; }
            fb.LogEvent(eventName, parameters);
        }
    }
}
