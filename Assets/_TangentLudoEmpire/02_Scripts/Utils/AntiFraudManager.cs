using System;
using System.Collections.Generic;
using UnityEngine;
using TangentLudoEmpire.Core;

namespace TangentLudoEmpire.Services
{
    /// <summary>Persisted, per-UTC-day running totals (Task 4). Its own small encrypted file
    /// (<c>tle_fraud_v1.enc</c>) rather than piggy-backing on UserProfile's int-typed counters
    /// (<see cref="AntiCheatManager"/>'s ad-count mechanism) - money totals need decimal precision, which
    /// an int counter would silently truncate.</summary>
    [Serializable]
    internal class FraudTrackerData
    {
        public string DateUtc = ""; // "yyyyMMdd" - totals reset when this rolls over
        [SerializeField] private string withdrawTotalRaw = "0";
        [SerializeField] private string depositTotalRaw = "0";

        public decimal WithdrawTotal
        {
            get => decimal.TryParse(withdrawTotalRaw, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0m;
            set => withdrawTotalRaw = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        public decimal DepositTotal
        {
            get => decimal.TryParse(depositTotalRaw, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0m;
            set => depositTotalRaw = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Task 4: rate/amount limiting, distinct from <see cref="AntiCheatManager"/> (payment-callback
    /// validation, speed-hack detection, wallet-drift reconciliation). This one asks one question - "is
    /// this specific transaction within today's/this hour's allowance?" - and nothing else; it grants no
    /// rewards and never touches the wallet directly.
    ///
    /// <see cref="CanTransact"/> is a PURE pre-flight check (no side effects) - it does not consume any
    /// of the allowance by itself. Call <see cref="RecordTransact"/> only after the transaction actually
    /// succeeds. If CanTransact itself recorded usage, a transaction that passed the fraud check but then
    /// failed for an unrelated reason (insufficient balance, RBAC, a declined gateway) would have wrongly
    /// eaten into the daily limit / hourly count for money that never moved.
    /// </summary>
    [DisallowMultipleComponent]
    public class AntiFraudManager : MonoBehaviour
    {
        public static AntiFraudManager Instance { get; private set; }

        [Header("Limits (Task 4)")]
        [SerializeField] private decimal dailyWithdrawLimit = 50000m;
        [SerializeField] private decimal dailyDepositLimit = 200000m;
        [SerializeField] private int maxTxPerHour = 10;

        public const string FraudFile = "tle_fraud_v1.enc";

        private FraudTrackerData _data;
        private readonly Queue<float> _txTimesLastHour = new Queue<float>(); // in-memory - a rate limiter resetting on relaunch is conventional, unlike money totals

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject); // Editor/test code (headless tools) would otherwise hit InvalidOperationException here and abort the rest of Awake()
            RollDayIfNeeded(); // also lazily creates _data via EnsureData()
        }

        /// <summary>Lazy-init guard, called at the top of every method that reads <see cref="_data"/>
        /// rather than trusted to have already run via Awake() - a component created via AddComponent()
        /// and used immediately in the same call (as Phase3Tools' headless test does) is not guaranteed
        /// to have finished Awake by the time the very next line runs in every Editor-mode context;
        /// relying on that produced a real NullReferenceException here, caught by that same headless test.</summary>
        private void EnsureData()
        {
            if (_data != null) return;
            _data = SaveService.Instance != null ? SaveService.Instance.LoadEncrypted<FraudTrackerData>(FraudFile) : null;
            _data ??= new FraudTrackerData { DateUtc = Today() };
        }

        private static string Today() => DateTime.UtcNow.ToString("yyyyMMdd");

        private void RollDayIfNeeded()
        {
            EnsureData();
            string today = Today();
            if (_data.DateUtc == today) return;
            _data.DateUtc = today;
            _data.WithdrawTotal = 0m;
            _data.DepositTotal = 0m;
            Persist();
        }

        /// <summary>Pure check - see class remarks. <paramref name="type"/> is "withdraw" or "deposit"
        /// (case-insensitive); anything else is allowed through untouched (this manager only knows about
        /// those two limited types).</summary>
        public bool CanTransact(string type, decimal amount)
        {
            RollDayIfNeeded();
            PruneHourWindow();

            if (_txTimesLastHour.Count >= maxTxPerHour)
            {
                AuditLog.Log("FRAUD_RATE_LIMIT", type ?? "", $"{_txTimesLastHour.Count} tx in the last hour (max {maxTxPerHour})");
                return false;
            }

            if (string.Equals(type, "withdraw", StringComparison.OrdinalIgnoreCase))
            {
                if (_data.WithdrawTotal + amount > dailyWithdrawLimit)
                {
                    AuditLog.Log("FRAUD_DAILY_LIMIT", "withdraw", $"today={_data.WithdrawTotal:0.00} +{amount:0.00} > {dailyWithdrawLimit:0.00}");
                    return false;
                }
            }
            else if (string.Equals(type, "deposit", StringComparison.OrdinalIgnoreCase))
            {
                if (_data.DepositTotal + amount > dailyDepositLimit)
                {
                    AuditLog.Log("FRAUD_DAILY_LIMIT", "deposit", $"today={_data.DepositTotal:0.00} +{amount:0.00} > {dailyDepositLimit:0.00}");
                    return false;
                }
            }

            return true;
        }

        /// <summary>Call only after the transaction actually succeeded - see class remarks.</summary>
        public void RecordTransact(string type, decimal amount)
        {
            RollDayIfNeeded();
            _txTimesLastHour.Enqueue(Time.realtimeSinceStartup);

            if (string.Equals(type, "withdraw", StringComparison.OrdinalIgnoreCase)) _data.WithdrawTotal += amount;
            else if (string.Equals(type, "deposit", StringComparison.OrdinalIgnoreCase)) _data.DepositTotal += amount;

            Persist();
        }

        private void PruneHourWindow()
        {
            float now = Time.realtimeSinceStartup;
            while (_txTimesLastHour.Count > 0 && now - _txTimesLastHour.Peek() > 3600f)
                _txTimesLastHour.Dequeue();
        }

        private void Persist()
        {
            bool ok = SaveService.Instance != null && SaveService.Instance.SaveEncrypted(FraudFile, _data);
            if (!ok) Debug.LogError("[AntiFraudManager] SaveEncrypted failed - today's totals not persisted!");
        }
    }
}
