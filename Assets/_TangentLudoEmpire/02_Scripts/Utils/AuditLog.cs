using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TangentLudoEmpire.Services
{
    /// <summary>One recorded action. Kept small and value-typed so the queue is cheap on mobile.</summary>
    [Serializable]
    public struct ActionLog
    {
        public string time;    // ISO-8601 UTC
        public string userId;
        public string action;  // short tag: COINS_ADD, RBAC_DENY, TAMPER, AD_VALIDATE, ...
        public string before;
        public string after;
        public string ip;      // filled by the backend on ingest; "" client-side
        public string device;

        public string ToCsvRow()
        {
            static string Q(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
            return string.Join(",", Q(time), Q(userId), Q(action), Q(before), Q(after), Q(ip), Q(device));
        }
    }

    /// <summary>
    /// Append-only local action log with a 30s auto-flush to the backend. Everything meaningful the
    /// player (or the app on their behalf) does is recorded here: currency changes, RBAC denials,
    /// tamper events, ad validations. The client copy is advisory - the server keeps the authoritative
    /// ledger.
    /// </summary>
    [DisallowMultipleComponent]
    public class AuditLog : MonoBehaviour
    {
        public static AuditLog Instance { get; private set; }

        [SerializeField] private float flushIntervalSeconds = 30f;
        [SerializeField] private int maxQueue = 500;

        private readonly Queue<ActionLog> _queue = new Queue<ActionLog>();
        private Coroutine _flushLoop;

        public int Pending => _queue.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject); // Editor/test code (headless tools) would otherwise hit InvalidOperationException here and abort the rest of Awake()
        }

        private void OnEnable()
        {
            _flushLoop ??= StartCoroutine(FlushLoop());
        }

        private void OnDisable()
        {
            if (_flushLoop != null) { StopCoroutine(_flushLoop); _flushLoop = null; }
        }

        // ---- static convenience (null-safe: no-ops if the manager isn't up yet) ----

        public static void Log(string action, string before = "", string after = "")
            => Instance?.Enqueue(action, before, after);

        // ---- instance ----

        public void Enqueue(string action, string before, string after)
        {
            var sec = TangentLudoEmpire.Core.SecurityManager.Instance;
            var entry = new ActionLog
            {
                time = DateTime.UtcNow.ToString("o"),
                userId = BackendService.Instance != null ? BackendService.Instance.UserId : "local",
                action = action ?? "",
                before = before ?? "",
                after = after ?? "",
                ip = "",
                device = sec != null ? sec.DeviceId : SystemInfo.deviceUniqueIdentifier
            };

            _queue.Enqueue(entry);
            while (_queue.Count > maxQueue) _queue.Dequeue(); // drop oldest under pressure

#if UNITY_EDITOR
            Debug.Log($"[AuditLog] {entry.action} | {entry.before} -> {entry.after}");
#endif
        }

        private IEnumerator FlushLoop()
        {
            var wait = new WaitForSecondsRealtime(Mathf.Max(5f, flushIntervalSeconds));
            while (true)
            {
                yield return wait;
                FlushToServer();
            }
        }

        /// <summary>Sends the queued entries to the backend. On failure they stay queued for the next
        /// cycle (bounded by <see cref="maxQueue"/>).</summary>
        public void FlushToServer()
        {
            if (_queue.Count == 0) return;

            var backend = BackendService.Instance;
            if (backend == null || !backend.IsOnline)
                return; // keep buffering

            var batch = new List<ActionLog>(_queue);
            string payload = JsonUtility.ToJson(new Wrapper { entries = batch.ToArray() });

            backend.ApiPost("audit/batch", payload, ok =>
            {
                if (ok)
                {
                    for (int i = 0; i < batch.Count && _queue.Count > 0; i++) _queue.Dequeue();
#if UNITY_EDITOR
                    Debug.Log($"[AuditLog] flushed {batch.Count} entries.");
#endif
                }
            });
        }

        /// <summary>Dumps the current queue as CSV (for support / local inspection).</summary>
        public string ExportCSV()
        {
            var sb = new StringBuilder();
            sb.AppendLine("time,userId,action,before,after,ip,device");
            foreach (var e in _queue) sb.AppendLine(e.ToCsvRow());
            return sb.ToString();
        }

        [Serializable]
        private class Wrapper { public ActionLog[] entries; }
    }
}
