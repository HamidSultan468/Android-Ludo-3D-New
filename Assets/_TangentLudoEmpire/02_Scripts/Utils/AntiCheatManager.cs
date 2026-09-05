using System;
using System.Collections.Generic;
using UnityEngine;
using TangentLudoEmpire.Core;

namespace TangentLudoEmpire.Services
{
    /// <summary>
    /// Client-side heuristics that FLAG suspicious activity and route real reward decisions to the
    /// server. Nothing here grants currency - it only permits or denies, and reports.
    ///
    ///  * <see cref="ValidateAd"/> - a watched-ad reward is only allowed if
    ///    <see cref="BackendService.ValidateReward"/>("ad", amount) says so, and if today's ad count is
    ///    under <see cref="SecurityConfig.dailyAdCap"/>.
    ///  * <see cref="CheckSpeedHack"/> - more than N game-starts in a short window looks automated.
    ///  * <see cref="ValidateWallet"/> - compares the local balance to the server's; drift beyond the
    ///    configured tolerance locks the wallet (server-confirmed).
    /// </summary>
    [DisallowMultipleComponent]
    public class AntiCheatManager : MonoBehaviour
    {
        public static AntiCheatManager Instance { get; private set; }

        public bool WalletLocked { get; private set; }
        public bool Flagged { get; private set; }

        private SecurityConfig _cfg;
        private readonly Queue<float> _gameStartTimes = new Queue<float>();

        private const string AdCountKeyPrefix = "adcount_"; // stored inside the encrypted profile, NOT PlayerPrefs

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject); // Editor/test code (headless tools) would otherwise hit InvalidOperationException here and abort the rest of Awake()
            _cfg = SecurityConfig.Load();
        }

        // ---------------------------------------------------------------- ads ----

        /// <summary>Call when a rewarded ad reports complete. <paramref name="onResult"/> = true only if
        /// the server validates AND the daily cap allows it.</summary>
        public void ValidateAd(string adId, long claimedAmount, Action<bool> onResult)
        {
            int todayCount = GetTodayAdCount();
            if (todayCount >= _cfg.dailyAdCap)
            {
                AuditLog.Log("AD_CAP_HIT", adId, todayCount.ToString());
                onResult?.Invoke(false);
                return;
            }

            var backend = BackendService.Instance;
            if (backend == null) { onResult?.Invoke(false); return; }

            backend.ValidateReward("ad", claimedAmount, ok =>
            {
                if (ok)
                {
                    SetTodayAdCount(todayCount + 1);
                    AuditLog.Log("AD_VALIDATED", adId, claimedAmount.ToString());
                }
                else
                {
                    AuditLog.Log("AD_DENIED", adId, claimedAmount.ToString());
                }
                onResult?.Invoke(ok);
            });
        }

        // ---------------------------------------------------------------- payments ----

        /// <summary>
        /// Phase 3 wallet gate. Before <see cref="TangentLudoEmpire.Wallet.MoneyWallet"/> credits a
        /// deposit it must clear this: the gateway's settlement id has to be confirmed by the backend
        /// ("never trust the client" - the ref could be forged/replayed). Fails closed when offline.
        /// In the Editor, with no backend configured, it allows so the mock flow is testable.
        /// </summary>
        public void ValidatePayment(string gatewayRef, Action<bool> result)
        {
            if (string.IsNullOrEmpty(gatewayRef))
            {
                AuditLog.Log("PAYMENT_VALIDATE_DENY", "", "empty-ref");
                result?.Invoke(false);
                return;
            }

            var backend = BackendService.Instance;
            if (backend == null || !backend.IsOnline)
            {
#if UNITY_EDITOR
                AuditLog.Log("PAYMENT_VALIDATE_MOCK", gatewayRef, "editor-allow (no backend)");
                result?.Invoke(true);
#else
                AuditLog.Log("PAYMENT_VALIDATE_DENY_OFFLINE", gatewayRef, "");
                result?.Invoke(false);
#endif
                return;
            }

            backend.ValidateReward("payment", 0, ok =>
            {
                AuditLog.Log(ok ? "PAYMENT_VALIDATE_OK" : "PAYMENT_VALIDATE_DENY", gatewayRef, ok ? "" : "server-reject");
                result?.Invoke(ok);
            });
        }

        private static string TodayKey() => AdCountKeyPrefix + DateTime.UtcNow.ToString("yyyyMMdd");

        private int GetTodayAdCount()
        {
            var p = SaveService.Instance != null ? SaveService.Instance.Profile : null;
            return p != null ? p.GetCounter(TodayKey()) : 0;
        }

        private void SetTodayAdCount(int n)
        {
            if (SaveService.Instance == null) return;
            SaveService.Instance.Profile.SetCounter(TodayKey(), n);
            SaveService.Instance.SaveProfile(SaveService.Instance.Profile);
        }

        // ---------------------------------------------------------------- speed hack ----

        /// <summary>Call once at the start of every match. Returns true if the recent start rate looks
        /// like automation.</summary>
        public bool CheckSpeedHack()
        {
            float now = Time.realtimeSinceStartup;
            _gameStartTimes.Enqueue(now);
            while (_gameStartTimes.Count > 0 && now - _gameStartTimes.Peek() > _cfg.speedHackWindowSeconds)
                _gameStartTimes.Dequeue();

            if (_gameStartTimes.Count > _cfg.speedHackGameLimit)
            {
                Flagged = true;
                AuditLog.Log("SPEEDHACK_FLAG", $"{_gameStartTimes.Count} starts", $"in {_cfg.speedHackWindowSeconds}s");
                BackendService.Instance?.ApiPost("security/flag",
                    JsonUtility.ToJson(new FlagReq { reason = "speedhack", count = _gameStartTimes.Count }));
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- wallet ----

        /// <summary>Compares local coins to the server's. Beyond the tolerance -> lock the wallet
        /// (only when the server actually answers, so offline play can't self-lock).</summary>
        public void ValidateWallet()
        {
            var backend = BackendService.Instance;
            if (backend == null || !backend.IsOnline) return;

            long local = WalletManager.Instance != null ? WalletManager.Instance.GetCoins()
                        : (SaveService.Instance != null ? SaveService.Instance.Profile.coins : 0);

            backend.GetServerCoins(server =>
            {
                if (server < 0) return; // unknown - do nothing
                long drift = Math.Abs(local - server);
                long allowed = (long)Math.Ceiling(server * _cfg.walletToleranceFraction);

                if (drift > allowed)
                {
                    WalletLocked = true;
                    Flagged = true;
                    AuditLog.Log("WALLET_LOCK", $"local={local}", $"server={server} drift={drift} allowed={allowed}");
                    backend.ApiPost("security/flag",
                        JsonUtility.ToJson(new FlagReq { reason = "wallet_mismatch", count = (int)drift }));
                }
            });
        }

        [Serializable] private class FlagReq { public string reason; public int count; }
    }
}
