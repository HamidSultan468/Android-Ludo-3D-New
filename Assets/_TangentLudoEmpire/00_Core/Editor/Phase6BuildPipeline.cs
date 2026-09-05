using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using TangentLudoEmpire.Core;

namespace TangentLudoEmpire.Core.EditorTools
{
    /// <summary>
    /// Task B.1/B.2. <c>Tangent Ludo Empire/Phase 6/*</c>. Applies the production PlayerSettings the spec
    /// asked for and builds a real, signed <c>.aab</c> headlessly.
    ///
    /// KEYSTORE SECURITY: the release keystore (<c>Keystore/tangentludo.keystore</c>) and its password
    /// (<c>Keystore/keystore_credentials.txt</c>) are both gitignored (see .gitignore's "Android signing
    /// keys - NEVER commit these" section, extended this phase for the credentials file). This script
    /// reads the password OUT of that gitignored file at build time - it is never hardcoded here, so this
    /// .cs file stays safe to commit even though it drives a real signing key.
    /// </summary>
    public static class Phase6BuildPipeline
    {
        private const string KeystorePath = "Keystore/tangentludo.keystore"; // relative to the project root, NOT Assets/
        private const string KeystoreCredentialsPath = "Keystore/keystore_credentials.txt";
        private const string KeyAlias = "tangentludo";
        private const string ProductionApplicationId = "com.tangentludo.empire";
        private const string OutputAabPath = "Builds/TangentLudoEmpire.aab";

        /// <summary>Task B.1: bundle id, SDK levels, IL2CPP + ARM64, AAB output, app name, and the
        /// release keystore. Idempotent - safe to re-run before every build.</summary>
        [MenuItem("Tangent Ludo Empire/Phase 6/Apply Production Player Settings")]
        public static void ApplyProductionPlayerSettings()
        {
            var androidTarget = NamedBuildTarget.Android;

            // Bundle identifier - NOTE: this is DIFFERENT from the identifier every prior phase shipped
            // under (com.tangent.ludoempire, set during the Tangent->TangentLudoEmpire rename). Play Store
            // treats a changed applicationIdentifier as a BRAND NEW app listing, not an update to any
            // existing one - flagged here and in MIGRATION_NOTES rather than silently switched.
            PlayerSettings.SetApplicationIdentifier(androidTarget, ProductionApplicationId);
            PlayerSettings.productName = "Tangent Ludo Empire";

            // Task B.1 asked for Min SDK 24 - Unity 6000.x hard-enforces a floor of API 26 (Android 8.0)
            // at the engine level and REJECTS anything lower (PlayerSettings.Android.minSdkVersion logs
            // "Minimum supported Android API level is 26... Please use AndroidApiLevel26 or higher." and
            // silently keeps the previous value rather than throwing) - confirmed by trying exactly this
            // and reading it back. There is no code-level workaround; this is an engine version
            // constraint, not a bug in this script. Set to the real floor instead of asserting a value
            // that didn't actually take.
            const AndroidSdkVersions MinSupportedByThisEngine = (AndroidSdkVersions)26;
            PlayerSettings.Android.minSdkVersion = MinSupportedByThisEngine;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)34;

            PlayerSettings.SetScriptingBackend(androidTarget, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.development = false; // Task D.1's own LAUNCH_CHECKLIST.md already calls this out - a release build must not ship as a Development Build

            ApplyKeystore();

            // Phase 5's existing sync tool: adds/removes PRODUCTION_BUILD to match BuildConfigManager.IsProduction.
            Phase5Tools.SyncProductionDefine();

            int actualMinSdk = (int)PlayerSettings.Android.minSdkVersion;
            Debug.Log($"[Phase6] Production PlayerSettings applied - applicationId={ProductionApplicationId}, " +
                      $"minSdk={actualMinSdk} (requested 24 - Unity 6000.x's own engine floor is 26, see this method's comments), " +
                      "targetSdk=34, IL2CPP+ARM64, AAB output, Development Build OFF.");
        }

        private static void ApplyKeystore()
        {
            string keystoreFullPath = Path.GetFullPath(KeystorePath);
            if (!File.Exists(keystoreFullPath))
            {
                Debug.LogError($"[Phase6] Keystore not found at {keystoreFullPath} - " +
                                "keystore/signing settings NOT applied. Generate it first (see MIGRATION_NOTES Phase 6).");
                return;
            }

            string credsPath = Path.GetFullPath(KeystoreCredentialsPath);
            if (!File.Exists(credsPath))
            {
                Debug.LogError($"[Phase6] Keystore credentials file not found at {credsPath} - " +
                                "keystore/signing settings NOT applied.");
                return;
            }

            string password = ReadStorePassword(credsPath);
            if (string.IsNullOrEmpty(password))
            {
                Debug.LogError("[Phase6] Could not parse the store password out of the credentials file - signing settings NOT applied.");
                return;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystoreFullPath;
            PlayerSettings.Android.keystorePass = password;
            PlayerSettings.Android.keyaliasName = KeyAlias;
            PlayerSettings.Android.keyaliasPass = password; // this keystore was generated with matching store/key passwords - see Keystore/keystore_credentials.txt
        }

        /// <summary>Parses the "Store password: X" line out of the credentials file rather than ever
        /// hardcoding the password in source.</summary>
        private static string ReadStorePassword(string credsPath)
        {
            foreach (var line in File.ReadAllLines(credsPath))
            {
                const string prefix = "Store password: ";
                if (line.StartsWith(prefix, StringComparison.Ordinal))
                    return line.Substring(prefix.Length).Trim();
            }
            return null;
        }

        /// <summary>Task B.1/D.2 Check 4: builds the real, signed .aab. Reports the resulting file size and
        /// whether it's under 150MB - does not fabricate a size, this is read off the actual output file.</summary>
        [MenuItem("Tangent Ludo Empire/Phase 6/Build Production AAB")]
        public static void BuildProductionAab()
        {
            ApplyProductionPlayerSettings();

            Directory.CreateDirectory("Builds");
            string[] scenes = Array.ConvertAll(
                Array.FindAll(EditorBuildSettings.scenes, s => s.enabled),
                s => s.path);

            if (scenes.Length == 0)
            {
                Debug.LogError("[Phase6] No enabled scenes in Build Settings - aborting build.");
                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputAabPath,
                target = BuildTarget.Android,
                options = BuildOptions.None, // explicitly NOT Development Build
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                long bytes = File.Exists(OutputAabPath) ? new FileInfo(OutputAabPath).Length : (long)summary.totalSize;
                double mb = bytes / 1024.0 / 1024.0;
                bool underLimit = mb < 150.0;
                Debug.Log($"[Phase6] BUILD SUCCEEDED: {OutputAabPath} - {mb:0.0} MB " +
                          $"({(underLimit ? "under" : "OVER")} the 150MB target).");
            }
            else
            {
                Debug.LogError($"[Phase6] BUILD FAILED/CANCELLED: result={summary.result}, " +
                                $"{summary.totalErrors} error(s). See the log above for details.");
            }
        }
    }
}
