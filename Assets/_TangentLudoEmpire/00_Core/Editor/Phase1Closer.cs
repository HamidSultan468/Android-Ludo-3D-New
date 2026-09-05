using UnityEditor;
using UnityEngine;

namespace TangentLudoEmpire.Core.EditorTools
{
    /// <summary>Convenience: runs the three scene/settings generators back-to-back. The MainMenu re-wire
    /// and the SimpleMenuLoader deletion stay as separate, deliberate steps (see <see cref="MainMenuWirer"/>)
    /// because they must be verified in Play mode first.</summary>
    public static class Phase1Closer
    {
        [MenuItem("Tangent Ludo Empire/Phase 1/CLOSE PHASE 1 (build scenes + fix build settings)")]
        public static void CloseAll()
        {
            DashboardSceneBuilder.BuildDashboard();
            BootSceneBuilder.BuildBoot();
            BuildSettingsFixer.FixBuildSettings();

            Debug.Log("Phase 1 Closure Complete " +
                      "(scenes generated: Boot.unity, Dashboard.unity | Build Settings: Boot=0, Dashboard=1, SampleScene=2). " +
                      "Next: 'Wire MainMenu (replace SimpleMenuLoader)', test in Play, then 'Delete SimpleMenuLoader.cs'.");
        }
    }
}
