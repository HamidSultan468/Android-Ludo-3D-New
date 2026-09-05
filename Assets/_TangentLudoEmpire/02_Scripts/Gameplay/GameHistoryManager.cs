using System;
using System.Collections.Generic;
using UnityEngine;
using TangentLudoEmpire.Core;
using TangentLudoEmpire.Services;

namespace TangentLudoEmpire.Social
{
    [Serializable]
    internal class GameHistoryData
    {
        public List<GameRecord> Records = new List<GameRecord>();
    }

    /// <summary>Task C.4. Persisted at <c>tle_gamehistory_v1.enc</c>. Capped like WalletData.History so
    /// the file doesn't grow unbounded on a long-lived install.</summary>
    [DisallowMultipleComponent]
    public class GameHistoryManager : MonoBehaviour
    {
        public const string HistoryFile = "tle_gamehistory_v1.enc";
        public const int MaxRecords = 200;

        public static GameHistoryManager Instance { get; private set; }

        private GameHistoryData _data;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject); // Editor/test code (headless tools) would otherwise hit InvalidOperationException here and abort the rest of Awake()
            EnsureData();
        }

        /// <summary>Lazy-init guard rather than trusting Awake() already ran - see AntiFraudManager's
        /// class remarks (Phase 3.3) for the real NullReferenceException this exact pattern already
        /// caught once; GameHistoryManager.AddRecord hit the same bug during Phase 4 testing.</summary>
        private void EnsureData()
        {
            if (_data != null) return;
            _data = SaveService.Instance != null ? SaveService.Instance.LoadEncrypted<GameHistoryData>(HistoryFile) : null;
            _data ??= new GameHistoryData();
        }

        public void AddRecord(decimal entry, decimal win, bool isWin)
        {
            EnsureData();
            var record = GameRecord.New(entry, win, isWin);
            _data.Records.Add(record);
            int overflow = _data.Records.Count - MaxRecords;
            if (overflow > 0) _data.Records.RemoveRange(0, overflow);
            Persist();
            AuditLog.Log("GAME_HISTORY_ADD", record.Id, $"entry={entry:0.00} win={win:0.00} isWin={isWin}");
        }

        public IReadOnlyList<GameRecord> GetHistory() { EnsureData(); return _data.Records; }

        private void Persist()
        {
            bool ok = SaveService.Instance != null && SaveService.Instance.SaveEncrypted(HistoryFile, _data);
            if (!ok) Debug.LogError("[GameHistoryManager] SaveEncrypted failed - history not persisted!");
        }
    }
}
