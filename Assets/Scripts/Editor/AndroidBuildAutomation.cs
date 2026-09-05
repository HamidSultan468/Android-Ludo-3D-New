using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LudoGame.EditorTools
{
    /// <summary>
    /// Headless Android build entry point - builds a debug .apk from the
    /// command line, no GUI clicks needed. Requires Android Build Support
    /// (SDK + NDK + OpenJDK) to be installed via Unity Hub first - run
    /// Window > Ludo Tools > Build > Check Android Build Tools to verify.
    ///
    /// Uses Unity's automatic debug keystore (not a real release signing
    /// key) - fine for testing on your own device, NOT for Play Store
    /// submission (see ANDROID_BUILD_GUIDE.md for the real keystore steps).
    ///
    /// Command line:
    ///   Unity.exe -batchmode -nographics -quit -projectPath "&lt;path&gt;"
    ///     -executeMethod LudoGame.EditorTools.AndroidBuildAutomation.BuildDebugApk
    ///     -logFile "&lt;path&gt;"
    /// </summary>
    public static class AndroidBuildAutomation
    {
        private const string OutputPath = "Builds/Android/AndroidLudo3D.apk";

        [MenuItem("Window/Ludo Tools/Build/Check Android Build Tools")]
        public static void CheckAndroidBuildTools()
        {
            string jdkPath = UnityEditor.Android.AndroidExternalToolsSettings.jdkRootPath;
            string sdkPath = UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath;
            string ndkPath = UnityEditor.Android.AndroidExternalToolsSettings.ndkRootPath;

            string message =
                "JDK: " + (string.IsNullOrEmpty(jdkPath) ? "NOT FOUND" : jdkPath) + "\n" +
                "SDK: " + (string.IsNullOrEmpty(sdkPath) ? "NOT FOUND" : sdkPath) + "\n" +
                "NDK: " + (string.IsNullOrEmpty(ndkPath) ? "NOT FOUND" : ndkPath);

            Debug.Log("[BUILD] Android tool check:\n" + message);
            EditorUtility.DisplayDialog("Android Build Tools", message, "OK");
        }

        [MenuItem("Window/Ludo Tools/Build/Build Debug APK")]
        public static void BuildDebugApk()
        {
            Debug.Log("[BUILD] Starting Android debug build...");

            if (string.IsNullOrEmpty(UnityEditor.Android.AndroidExternalToolsSettings.jdkRootPath))
            {
                Debug.LogError("[BUILD] ABORTED: no JDK configured. Install OpenJDK via Unity Hub > " +
                    "Installs > (gear icon) > Add Modules > Android Build Support > OpenJDK, then try again.");
                return;
            }

            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[BUILD] ABORTED: no scenes in Build Settings. Add at least one scene " +
                    "(File > Build Settings > Add Open Scenes) and try again.");
                return;
            }

            Debug.Log("[BUILD] Scenes: " + string.Join(", ", scenes));

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(OutputPath) ?? "Builds/Android");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development,
            };

            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
            UnityEditor.Build.Reporting.BuildSummary summary = report.summary;

            Debug.Log("[BUILD] Result: " + summary.result +
                " | Errors: " + summary.totalErrors +
                " | Warnings: " + summary.totalWarnings +
                " | Size: " + summary.totalSize + " bytes" +
                " | Time: " + summary.totalTime);

            if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
                Debug.Log("[BUILD] SUCCESS: " + OutputPath);
            else
                Debug.LogError("[BUILD] FAILED - see the errors above/in the full log.");
        }
    }
}
