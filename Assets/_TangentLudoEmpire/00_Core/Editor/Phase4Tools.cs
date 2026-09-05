using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using TangentLudoEmpire.Core;
using TangentLudoEmpire.Wallet;
using TangentLudoEmpire.Multiplayer;
using TangentLudoEmpire.Tournament;
using TangentLudoEmpire.Social;
using TangentLudoEmpire.Services;

namespace TangentLudoEmpire.Core.EditorTools
{
    /// <summary>
    /// <c>Tangent Ludo Empire/Phase 4/*</c>. Unlike every Phase 3.x tool (which reads/writes the
    /// encrypted files DIRECTLY because MoneyWallet.Instance doesn't exist outside Play mode), this one
    /// spins up a full throwaway manager stack via AddComponent - RoomManager/TournamentManager have no
    /// persisted file at all (see their class remarks), so there is nothing to hand-roll crypto for; it's
    /// simpler and more faithful to the real runtime path to just run the real components. Confirmed safe
    /// by the Phase 3.3 AntiFraudManager fix: every manager touched here uses lazy Ensure-style init
    /// rather than assuming Awake() already ran, so calling methods immediately after AddComponent works.
    /// </summary>
    public static class Phase4Tools
    {
        private const string TournamentConfigPath = "Assets/_TangentLudoEmpire/Resources/TournamentConfig.asset";

        [MenuItem("Tangent Ludo Empire/Phase 4/Ensure Tournament Config")]
        public static void EnsureTournamentConfig()
        {
            Directory.CreateDirectory("Assets/_TangentLudoEmpire/Resources");
            var cfg = AssetDatabase.LoadAssetAtPath<TournamentConfig>(TournamentConfigPath);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<TournamentConfig>();
                AssetDatabase.CreateAsset(cfg, TournamentConfigPath);
                EditorUtility.SetDirty(cfg);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Phase4] Created {TournamentConfigPath} (70/20/10 defaults).");
            }
        }

        /// <summary>Task D.2. Test 1: Create Room 20 -&gt; Join -&gt; Win 40 -&gt; check Balance,
        /// Leaderboard, History. Test 2: Tournament 100 -&gt; 8 Players -&gt; Winner gets 560.
        /// Runs a full throwaway manager stack (see class remarks) rather than Play mode, so it's
        /// headless and reproducible - torn down at the end regardless of pass/fail.</summary>
        [MenuItem("Tangent Ludo Empire/Phase 4/Run Full Test")]
        public static void RunFullTest()
        {
            EnsureTournamentConfig();
            int pass = 0, fail = 0;
            void Check(bool ok, string label)
            {
                if (ok) { pass++; Debug.Log($"[Phase4] PASS  {label}"); }
                else    { fail++; Debug.LogError($"[Phase4] FAIL  {label}"); }
            }

            DeleteTestFiles();
            MockRoomBackend.Editor_ClearAll();

            var spawned = new List<GameObject>();
            T Spawn<T>() where T : MonoBehaviour
            {
                var go = new GameObject(typeof(T).Name);
                spawned.Add(go);
                var comp = go.AddComponent<T>();
                // DIAGNOSED (this run): in this headless -executeMethod context, AddComponent does NOT
                // reliably invoke Awake() synchronously - confirmed by MoneyWallet.Instance being null
                // even after several subsequent lines of code ran. Unity likely queues Awake for the
                // next player-loop tick, which never comes before -quit in a single synchronous
                // executeMethod call. Force it now via SendMessage (safe/idempotent even if Awake DID
                // already run - every manager's Awake starts with the singleton guard
                // "if (Instance != null && Instance != this)", which is false on a second call to the
                // SAME instance, so this never re-Destroys or mis-fires).
                go.SendMessage("Awake", SendMessageOptions.DontRequireReceiver);
                return comp;
            }

            try
            {
                // Same dependency order as GameServices.EnsureManagers().
                Spawn<SecurityManager>();
                Spawn<AuditLog>();
                var save = Spawn<SaveService>();
                var wallet = Spawn<MoneyWallet>();
                var history = Spawn<GameHistoryManager>();
                var leaderboard = Spawn<LeaderboardManager>();
                var roomMgr = Spawn<RoomManager>();
                var tourneyMgr = Spawn<TournamentManager>();

                wallet.Editor_Reset();
                wallet.Editor_GrantBonus(1000m);
                Check(wallet.GetBalance() == 1000m, "starting balance 1000.00");

                // ---- Test 1: Room ----
                string roomId = roomMgr.CreateRoom(20m);
                Check(!string.IsNullOrEmpty(roomId), $"room created ({roomId})");

                bool joined = roomMgr.JoinRoom(roomId, out string joinErr);
                Check(joined, $"joined room" + (joinErr != null ? $" ({joinErr})" : ""));
                decimal afterJoin = wallet.GetBalance();
                Check(afterJoin == 980m, $"balance 980.00 after 20 entry fee (was {afterJoin:0.00})");

                RoomGameBridge.OnGameEnd(roomId, roomMgr.LocalPlayerId, 40m);
                decimal afterWin = wallet.GetBalance();
                Check(afterWin == 1020m, $"balance 1020.00 after winning 40 (was {afterWin:0.00})");

                history.AddRecord(20m, 40m, true);
                leaderboard.UpdateStats(40m, true);
                Check(leaderboard.LocalStat.Wins == 1 && leaderboard.LocalStat.Games == 1, "leaderboard: 1 win / 1 game recorded");
                Check(leaderboard.LocalStat.TotalWinnings == 40m, "leaderboard: total winnings 40.00");
                Check(history.GetHistory().Count == 1 && history.GetHistory()[0].IsWin, "history: 1 win record");

                var top10 = leaderboard.GetTop10(LeaderboardType.AllTime);
                Check(top10.Count == 10 && top10.Exists(p => p.Id == "local" && p.TotalWinnings == 40m),
                     "leaderboard: local player's real stat (40.00) appears in the Top 10 list (rest is mock padding)");

                // room expiry + refund path (separate room, backdated - no real 120s wait)
                string expireRoomId = roomMgr.CreateRoom(15m);
                roomMgr.JoinRoom(expireRoomId);
                decimal balanceBeforeExpiry = wallet.GetBalance();
                MockRoomBackend.Editor_ForceExpire(expireRoomId);
                MockRoomBackend.PruneExpiredRooms();
                Check(wallet.GetBalance() == balanceBeforeExpiry + 15m, "expired room refunds its entry fee");

                // ---- Test 2: Tournament ----
                tourneyMgr.Editor_Reset();
                bool registered = tourneyMgr.RegisterTournament(100m, out string regErr);
                Check(registered, "tournament: local player registered" + (regErr != null ? $" ({regErr})" : ""));
                // afterWin (1020.00) - the expired room's join(-15)+refund(+15) net to zero - then -100 entry fee.
                Check(wallet.GetBalance() == afterWin - 100m, $"tournament: 100 entry fee deducted (balance {wallet.GetBalance():0.00})");

                for (int i = 0; i < 7; i++) tourneyMgr.Editor_AddMockPlayer($"mock_player_{i}", 100m);
                Check(tourneyMgr.Current != null && tourneyMgr.Current.Status == TournamentStatus.Active, "tournament auto-started at 8 players");
                Check(tourneyMgr.Current != null && tourneyMgr.Current.PrizePool == 800m, $"prize pool 800.00 (8 x 100)");
                Check(tourneyMgr.Bracket.Count == 4, "bracket has 4 Round-1 matchups for 8 players");

                decimal beforeTourneyWin = wallet.GetBalance();
                tourneyMgr.DeclareWinner(roomMgr.LocalPlayerId);
                decimal afterTourneyWin = wallet.GetBalance();
                Check(afterTourneyWin == beforeTourneyWin + 560m, $"tournament winner credited 560.00 (800 x 0.7) - balance now {afterTourneyWin:0.00}");
                Check(tourneyMgr.Current.Status == TournamentStatus.Completed, "tournament marked Completed");
            }
            finally
            {
                foreach (var go in spawned) Object.DestroyImmediate(go);
                MockRoomBackend.Editor_ClearAll();
            }

            Debug.Log($"Phase 4 Full Test: {pass} passed, {fail} failed.");
            if (fail > 0) EditorUtility.DisplayDialog("Phase 4 Full Test", $"{fail} check(s) FAILED - see Console.", "OK");
        }

        private static void DeleteTestFiles()
        {
            string[] files =
            {
                MoneyWallet.WalletFile,
                "tle_profile_v1.json", // legacy Phase-1 upgrade path SaveService also checks
                LeaderboardManager.LeaderboardFile,
                GameHistoryManager.HistoryFile,
            };
            foreach (var f in files)
            {
                string p = Path.Combine(Application.persistentDataPath, f);
                TryDelete(p); TryDelete(p + ".bak"); TryDelete(p + ".tmp");
            }
        }

        private static void TryDelete(string p) { try { if (File.Exists(p)) File.Delete(p); } catch { } }
    }
}
