using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using TangentLudoEmpire.Core;

namespace TangentLudoEmpire.Core.EditorTools
{
    /// <summary>Sets the Scenes In Build to: 0 = Boot, 1 = Dashboard, 2 = SampleScene (enabled). Every
    /// other existing scene is kept in the list but disabled, so nothing is silently dropped.</summary>
    public static class BuildSettingsFixer
    {
        [MenuItem("Tangent Ludo Empire/Phase 1/Fix Build Settings Order")]
        public static void FixBuildSettings()
        {
            string boot   = $"{DashboardSceneBuilder.SceneDir}/{AppConstants.SCENE_BOOT}.unity";
            string dash    = $"{DashboardSceneBuilder.SceneDir}/{AppConstants.SCENE_DASHBOARD}.unity";
            string sample  = "Assets/Scenes/SampleScene.unity";

            var ordered = new List<EditorBuildSettingsScene>();
            var seen = new HashSet<string>();

            void AddEnabled(string p)
            {
                if (!File.Exists(p)) { Debug.LogError($"[BuildSettingsFixer] Missing scene, run its builder first: {p}"); return; }
                ordered.Add(new EditorBuildSettingsScene(p, true));
                seen.Add(p);
            }

            AddEnabled(boot);     // 0
            AddEnabled(dash);     // 1
            AddEnabled(sample);   // 2

            // keep everything else, disabled, in its current relative order
            foreach (var s in EditorBuildSettings.scenes)
                if (!seen.Contains(s.path))
                    ordered.Add(new EditorBuildSettingsScene(s.path, false));

            EditorBuildSettings.scenes = ordered.ToArray();

            var sb = new System.Text.StringBuilder("[BuildSettingsFixer] Scenes In Build:\n");
            for (int i = 0; i < ordered.Count; i++)
                sb.AppendLine($"  {i}: {(ordered[i].enabled ? "[x]" : "[ ]")} {ordered[i].path}");
            Debug.Log(sb.ToString());
        }
    }
}
