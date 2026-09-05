using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TangentLudoEmpire.Core;
using TangentLudoEmpire.Services;

namespace TangentLudoEmpire.Social
{
    [Serializable]
    internal class LeaderboardData
    {
        public PlayerStat Local = PlayerStat.New("local", "You");
    }

    /// <summary>
    /// Task C.1. Persisted at <c>tle_leaderboard_v1.enc</c> - but this device can only ever know THIS
    /// player's own stats; there is no backend to aggregate anyone else's. See <see cref="GetTop10"/>'s
    /// remarks - a real cross-player leaderboard needs a live backend query, not a local encrypted file.
    /// </summary>
    [DisallowMultipleComponent]
    public class LeaderboardManager : MonoBehaviour
    {
        public const string LeaderboardFile = "tle_leaderboard_v1.enc";

        public static LeaderboardManager Instance { get; private set; }

        private LeaderboardData _data;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject); // Editor/test code (headless tools) would otherwise hit InvalidOperationException here and abort the rest of Awake()
            EnsureData();
        }

        /// <summary>Lazy-init guard rather than trusting Awake() already ran - see AntiFraudManager's
        /// class remarks (Phase 3.3) for why this pattern exists everywhere it touches _data now.</summary>
        private void EnsureData()
        {
            if (_data != null) return;
            _data = SaveService.Instance != null ? SaveService.Instance.LoadEncrypted<LeaderboardData>(LeaderboardFile) : null;
            _data ??= new LeaderboardData();
            if (SaveService.Instance != null && SaveService.Instance.Profile != null && !string.IsNullOrEmpty(SaveService.Instance.Profile.displayName))
                _data.Local.Name = SaveService.Instance.Profile.displayName;
        }

        /// <summary>Task C.1. Call after any match resolves (win or loss) - Games always increments, Wins
        /// and TotalWinnings only on a win.</summary>
        public void UpdateStats(decimal winAmount, bool won)
        {
            EnsureData();
            _data.Local.Games++;
            if (won)
            {
                _data.Local.Wins++;
                _data.Local.TotalWinnings += winAmount;
            }
            Persist();
            AuditLog.Log("LEADERBOARD_UPDATE", _data.Local.Id, $"games={_data.Local.Games} wins={_data.Local.Wins} total={_data.Local.TotalWinnings:0.00}");
        }

        public PlayerStat LocalStat { get { EnsureData(); return _data.Local; } }

        /// <summary>
        /// TODO (Firebase/backend): a real leaderboard needs a live query against every player's stats,
        /// filtered server-side by the requested time window. This mock can only ever see the LOCAL
        /// player.
        ///
        /// Phase 6.1 ("remove ALL demo/test/mock data"): the 9 deterministic "Player_####" placeholder
        /// rows this method used to pad the list with are now gated behind
        /// <see cref="BuildConfigManager.IsProduction"/> - in a production build (IsProduction=true) this
        /// returns ONLY the local player's real entry, never fabricated rows. The padding still renders in
        /// dev/mock builds (IsProduction=false) purely so the UI has something to demo. A real Top 10 needs
        /// the live backend query above - this method cannot invent one.
        ///
        /// DAILY/WEEKLY are approximated from GameHistoryManager's timestamped records when available
        /// (falls back to the same AllTime figure if GameHistoryManager isn't up yet).
        /// </summary>
        public List<PlayerStat> GetTop10(LeaderboardType type)
        {
            EnsureData();
            var list = new List<PlayerStat>();

            decimal windowWinnings = _data.Local.TotalWinnings;
            int windowWins = _data.Local.Wins;
            var history = GameHistoryManager.Instance != null ? GameHistoryManager.Instance.GetHistory() : null;
            if (history != null && type != LeaderboardType.AllTime)
            {
                var cutoff = type == LeaderboardType.Daily ? DateTime.UtcNow.AddDays(-1) : DateTime.UtcNow.AddDays(-7);
                var windowed = history.Where(r => r.Time >= cutoff).ToList();
                windowWinnings = windowed.Where(r => r.IsWin).Sum(r => r.Win);
                windowWins = windowed.Count(r => r.IsWin);
            }

            var localCopy = PlayerStat.New(_data.Local.Id, _data.Local.Name);
            localCopy.Wins = windowWins;
            localCopy.Games = _data.Local.Games;
            localCopy.TotalWinnings = windowWinnings;
            list.Add(localCopy);

            // Phase 6.1: mock padding only in dev/mock builds - see method remarks. A production build
            // never fabricates other-player rows; it shows the local player's real entry alone until a
            // real backend leaderboard query exists.
            if (!BuildConfigManager.Load().IsProduction)
            {
                for (int i = 0; i < 9; i++)
                {
                    var fake = PlayerStat.New($"mock_{i}", $"Player_{1000 + i * 137}");
                    fake.Wins = Mathf.Max(0, 9 - i) * 2;
                    fake.Games = fake.Wins + i + 3;
                    fake.TotalWinnings = (9 - i) * 87.50m;
                    list.Add(fake);
                }
            }

            return list.OrderByDescending(p => p.TotalWinnings).Take(10).ToList();
        }

        private void Persist()
        {
            bool ok = SaveService.Instance != null && SaveService.Instance.SaveEncrypted(LeaderboardFile, _data);
            if (!ok) Debug.LogError("[LeaderboardManager] SaveEncrypted failed - stats not persisted!");
        }
    }
}
