using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace TangentLudoEmpire.Analytics
{
    /// <summary>One logged analytics call - kept in-memory (queryable by the Phase 5 security/analytics
    /// test) as well as appended to <see cref="FirebaseManager"/>'s local log file.</summary>
    [Serializable]
    public class AnalyticsEventRecord
    {
        public string EventName;
        public string ParamsJson; // flattened "key=value;key2=value2" - avoids pulling in a JSON lib for a mock
        public long TimestampUtcMs;

        public AnalyticsEventRecord(string eventName, IReadOnlyDictionary<string, object> parameters)
        {
            EventName = eventName ?? "";
            TimestampUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var sb = new StringBuilder();
            if (parameters != null)
                foreach (var kv in parameters)
                {
                    if (sb.Length > 0) sb.Append(';');
                    sb.Append(kv.Key).Append('=').Append(Convert.ToString(kv.Value, CultureInfo.InvariantCulture));
                }
            ParamsJson = sb.ToString();
        }

        /// <summary>Reads back a single numeric/string param by key for test assertions -
        /// e.g. <c>GetParam("prize")</c> after logging {"prize": 90}.</summary>
        public string GetParam(string key)
        {
            if (string.IsNullOrEmpty(ParamsJson)) return null;
            foreach (var pair in ParamsJson.Split(';'))
            {
                int eq = pair.IndexOf('=');
                if (eq > 0 && pair.Substring(0, eq) == key) return pair.Substring(eq + 1);
            }
            return null;
        }

        public override string ToString() => $"{EventName}({ParamsJson})";
    }

    /// <summary>
    /// Task B.1. MOCK FACADE - no Firebase SDK is installed in this project (Packages/manifest.json is not
    /// touched by this session without the ability to verify a package resolve headlessly). This class
    /// exposes the exact public surface a real Firebase.Analytics integration would have
    /// (Initialize-on-Awake singleton, LogEvent, SetUserProperty), logs every call to console + a local
    /// file + an in-memory list (so <see cref="TangentLudoEmpire.Analytics.AnalyticsBridge"/> and the
    /// Phase 5 security test can verify events fired), and marks every real-SDK call site with TODO.
    ///
    /// TODO(real Firebase SDK) - steps to swap this mock for the real thing:
    /// Step 1: add com.google.firebase:firebase-analytics / the Firebase Unity SDK via Package Manager
    /// (or a .unitypackage import). Step 2: drop google-services.json into Assets/. Step 3: in Awake,
    /// replace the body of <see cref="InitializeFirebaseApp"/> with
    /// <c>Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(...)</c>. Step 4: in
    /// <see cref="LogEvent"/>, replace the mock logging with
    /// <c>Firebase.Analytics.FirebaseAnalytics.LogEvent(eventName, ConvertParameters(parameters))</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public class FirebaseManager : MonoBehaviour
    {
        public static FirebaseManager Instance { get; private set; }

        private const string LogFileName = "tle_firebase_events_log.txt";

        private readonly List<AnalyticsEventRecord> _events = new List<AnalyticsEventRecord>();
        public IReadOnlyList<AnalyticsEventRecord> Events => _events;

        private bool _initialized;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject); // Editor/test code would otherwise hit InvalidOperationException here and abort the rest of Awake()
            InitializeFirebaseApp();
        }

        private void InitializeFirebaseApp()
        {
            if (_initialized) return;
            _initialized = true;
            // TODO: Add google-services.json for Android, then swap this for the real
            // Firebase.FirebaseApp.CheckAndFixDependenciesAsync() bootstrap.
            Debug.Log("[FirebaseManager] Mock FirebaseApp initialized (no Firebase SDK installed - see class remarks).");
        }

        /// <summary>Logs an analytics event. Never throws - a bad/missing parameters dict degrades to an
        /// event with no params rather than crashing the caller (analytics must never break gameplay).</summary>
        public void LogEvent(string eventName, Dictionary<string, object> parameters)
        {
            if (!_initialized) InitializeFirebaseApp(); // lazy guard - same EnsureData()-style pattern as every other Phase manager, in case Awake hasn't run yet in a headless test context
            if (string.IsNullOrEmpty(eventName)) { Debug.LogWarning("[FirebaseManager] LogEvent called with empty eventName - ignored."); return; }

            var rec = new AnalyticsEventRecord(eventName, parameters);
            _events.Add(rec);
            AppendToLogFile(rec);
            // TODO(real SDK): Firebase.Analytics.FirebaseAnalytics.LogEvent(eventName, ConvertParameters(parameters));
            Debug.Log($"[FirebaseManager] LogEvent: {rec}");
        }

        public void SetUserProperty(string name, string value)
        {
            if (!_initialized) InitializeFirebaseApp();
            if (string.IsNullOrEmpty(name)) return;
            AppendToLogFile(new AnalyticsEventRecord("SET_USER_PROPERTY", new Dictionary<string, object> { { name, value ?? "" } }));
            // TODO(real SDK): Firebase.Analytics.FirebaseAnalytics.SetUserProperty(name, value);
            Debug.Log($"[FirebaseManager] SetUserProperty: {name}={value}");
        }

        private static void AppendToLogFile(AnalyticsEventRecord rec)
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, LogFileName);
                File.AppendAllText(path, $"{DateTimeOffset.FromUnixTimeMilliseconds(rec.TimestampUtcMs):o}\t{rec}{Environment.NewLine}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[FirebaseManager] Failed to write {LogFileName}: {e.Message}");
            }
        }

#if UNITY_EDITOR
        /// <summary>EDITOR ONLY - compiled out of every player build.</summary>
        public static string Editor_LogPath => Path.Combine(Application.persistentDataPath, LogFileName);
        public void Editor_Reset() { _events.Clear(); try { File.Delete(Editor_LogPath); } catch { } }
#endif
    }
}
