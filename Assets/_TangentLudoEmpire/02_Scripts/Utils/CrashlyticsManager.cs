using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TangentLudoEmpire.Analytics
{
    /// <summary>
    /// Task B.2. MOCK FACADE, same rationale as <see cref="FirebaseManager"/> - no Crashlytics SDK is
    /// installed. Static class per spec: LogException/SetUserId, plus auto-catching every unhandled
    /// exception via <see cref="Application.logMessageReceived"/> (the standard cross-platform way to
    /// observe uncaught exceptions/errors in Unity without a native SDK).
    ///
    /// TODO(real Firebase Crashlytics SDK): once installed, replace <see cref="AppendToLogFile"/>'s body
    /// in <see cref="LogException"/> with <c>Firebase.Crashlytics.Crashlytics.LogException(e)</c>, and
    /// <see cref="SetUserId"/>'s with <c>Firebase.Crashlytics.Crashlytics.SetUserId(userId)</c>.
    /// </summary>
    public static class CrashlyticsManager
    {
        private const string LogFileName = "tle_crashlytics_log.txt";
        private static string _userId = "";
        private static bool _hooked;

        /// <summary>Auto-installs before any scene loads, so an exception thrown during boot is still
        /// caught. NOTE: RuntimeInitializeOnLoadMethod fires on entering Play mode / a real Player - it
        /// does NOT fire for a plain <c>-batchmode -executeMethod</c> Editor run that never enters Play
        /// mode (same class of gap as AddComponent's Awake() not running synchronously there - see
        /// Phase4Tools remarks), so headless test tools call <see cref="EnsureHooked"/> directly instead
        /// of relying on this attribute firing.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoHook() => EnsureHooked();

        /// <summary>Idempotent - safe to call repeatedly (real boot via the attribute above, and/or a
        /// headless test harness explicitly).</summary>
        public static void EnsureHooked()
        {
            if (_hooked) return;
            _hooked = true;
            Application.logMessageReceived += OnLogMessageReceived;
            Debug.Log("[CrashlyticsManager] Auto-catch hooked (Application.logMessageReceived).");
        }

        private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error) return;
            // NOTE: a manual LogException() call below also routes back through here via Debug.LogException,
            // so a deliberately-caught exception is logged twice (once by the explicit call, once by this
            // auto-catch) - accepted and documented rather than adding fragile re-entrancy suppression;
            // real Crashlytics SDKs likewise distinguish "handled" vs "unhandled" records rather than dedup.
            AppendToLogFile($"AUTO-CAUGHT [{type}] user={_userId} {condition}\n{stackTrace}");
        }

        /// <summary>Associates subsequent crash/exception reports with a player id.</summary>
        public static void SetUserId(string userId)
        {
            _userId = userId ?? "";
            AppendToLogFile($"SET_USER_ID user={_userId}");
            // TODO(real SDK): Firebase.Crashlytics.Crashlytics.SetUserId(userId);
        }

        /// <summary>Manually reports a caught (handled) exception - the app didn't crash, but the error
        /// is still worth knowing about in production.</summary>
        public static void LogException(Exception e)
        {
            if (e == null) return;
            string entry = $"[{DateTime.UtcNow:o}] user={_userId} EXCEPTION: {e.GetType().Name}: {e.Message}\n{e.StackTrace}";
            AppendToLogFile(entry);
            Debug.LogException(e); // surfaces in the Console like any other exception (and feeds the auto-catch path above, too)
            // TODO(real SDK): Firebase.Crashlytics.Crashlytics.LogException(e);
            FirebaseManager.Instance?.LogEvent("app_exception", new Dictionary<string, object>
            {
                { "type", e.GetType().Name },
                { "message", e.Message },
            });
        }

        private static void AppendToLogFile(string line)
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, LogFileName);
                File.AppendAllText(path, line + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CrashlyticsManager] Failed to write {LogFileName}: {ex.Message}");
            }
        }

#if UNITY_EDITOR
        /// <summary>EDITOR ONLY - compiled out of every player build.</summary>
        public static string Editor_LogPath => Path.Combine(Application.persistentDataPath, LogFileName);
        public static void Editor_Reset() { try { File.Delete(Editor_LogPath); } catch { } }
#endif
    }
}
