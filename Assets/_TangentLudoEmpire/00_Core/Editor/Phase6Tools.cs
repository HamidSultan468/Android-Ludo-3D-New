using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using TangentLudoEmpire.Core;
using TangentLudoEmpire.Services;
using TangentLudoEmpire.Wallet;
using TangentLudoEmpire.Social;
using TangentLudoEmpire.Polish;

namespace TangentLudoEmpire.Core.EditorTools
{
    /// <summary>Task D.2. <c>Tangent Ludo Empire/Phase 6/Final Release Checklist</c>. Every check reads
    /// real state (a file on disk, a loaded assembly, a live manager's actual output) - none of them are
    /// hardcoded to pass.</summary>
    public static class Phase6Tools
    {
        private const string AabPath = "Builds/TangentLudoEmpire.aab";

        [MenuItem("Tangent Ludo Empire/Phase 6/Final Release Checklist")]
        public static void RunFinalReleaseChecklist()
        {
            int pass = 0, fail = 0;
            void Check(bool ok, string label, string detail = "")
            {
                if (ok) { pass++; Debug.Log($"[Phase6] PASS  {label}" + (detail.Length > 0 ? $" - {detail}" : "")); }
                else    { fail++; Debug.LogError($"[Phase6] FAIL  {label}" + (detail.Length > 0 ? $" - {detail}" : "")); }
            }

            // ---- Check 1: IsProduction=true ----
            var buildCfg = BuildConfigManager.Load();
            Check(buildCfg.IsProduction, "Check 1: BuildConfigManager.IsProduction == true");

            // ---- Check 2: No mock data found (LeaderboardManager, gated per Phase 6.1) ----
            var spawned = new System.Collections.Generic.List<GameObject>();
            T Spawn<T>() where T : MonoBehaviour
            {
                var go = new GameObject(typeof(T).Name);
                spawned.Add(go);
                var comp = go.AddComponent<T>();
                go.SendMessage("Awake", SendMessageOptions.DontRequireReceiver); // see Phase4Tools/Phase5Tools remarks - AddComponent's Awake() isn't reliably synchronous in this headless context
                return comp;
            }

            try
            {
                var save = Spawn<SaveService>();
                var leaderboard = Spawn<LeaderboardManager>();
                var top10 = leaderboard.GetTop10(LeaderboardType.AllTime);
                bool noMock = top10.All(p => !p.Id.StartsWith("mock_", StringComparison.Ordinal));
                Check(noMock, "Check 2: No mock data found", $"GetTop10 returned {top10.Count} row(s) in production mode");

                // ---- Check 5: legal pages load real text (not the missing-file fallback) ----
                var legal = Spawn<LegalPagesUI>();
                legal.Open();
                bool allThreeLoaded = true;
                string[] missingTabs = Array.Empty<string>();
                for (int i = 0; i < 3; i++)
                {
                    legal.Editor_ShowPage(i);
                    string body = legal.Editor_CurrentBodyText;
                    bool loaded = !string.IsNullOrEmpty(body) && !body.Contains("not found in Resources/LegalText");
                    if (!loaded) { allThreeLoaded = false; missingTabs = missingTabs.Append(((object)i).ToString()).ToArray(); }
                }
                Check(allThreeLoaded, "Check 5: Legal pages load real text from Resources/LegalText/*.txt",
                     allThreeLoaded ? "Terms, Privacy, Refund all loaded" : $"missing tab index(es): {string.Join(",", missingTabs)}");
            }
            finally
            {
                foreach (var go in spawned) UnityEngine.Object.DestroyImmediate(go);
            }

            // ---- Check 3: Firebase SDK present (real check via reflection - no SDK installed as of Phase 5/6) ----
            bool firebaseSdkPresent = AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => a.GetName().Name.Equals("Firebase.App", StringComparison.OrdinalIgnoreCase)
                       || a.GetName().Name.StartsWith("Firebase.", StringComparison.OrdinalIgnoreCase));
            Check(firebaseSdkPresent, "Check 3: Firebase SDK present",
                 firebaseSdkPresent ? "Firebase.* assembly found" : "no Firebase.* assembly loaded - see PRODUCTION_SETUP.md section 1 (mock facade still in use)");

            // ---- Check 4: AAB build success ----
            string aabFullPath = Path.GetFullPath(AabPath);
            bool aabExists = File.Exists(aabFullPath);
            double mb = aabExists ? new FileInfo(aabFullPath).Length / 1024.0 / 1024.0 : 0.0;
            Check(aabExists && mb < 150.0, "Check 4: AAB build present and under 150MB",
                 aabExists ? $"{AabPath} = {mb:0.0} MB" : $"{AabPath} not found - run 'Tangent Ludo Empire/Phase 6/Build Production AAB' first");

            Debug.Log($"Phase 6 Final Release Checklist: {pass} passed, {fail} failed.");
            if (fail > 0) EditorUtility.DisplayDialog("Phase 6 Final Release Checklist", $"{fail} check(s) FAILED - see Console.", "OK");
        }
    }
}
