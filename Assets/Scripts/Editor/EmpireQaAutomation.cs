using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LudoGame.EditorTools
{
    /// <summary>
    /// Headless QA entry point: seeds Empire starter data and builds the
    /// Milestone 1 scene in a dedicated, saved test scene - no Editor GUI
    /// interaction required. Intended to be run from the command line via:
    ///
    ///   Unity.exe -batchmode -nographics -quit -projectPath "&lt;path&gt;"
    ///     -executeMethod LudoGame.EditorTools.EmpireQaAutomation.SetupMilestone1Scene
    ///     -logFile "&lt;path&gt;"
    ///
    /// Also available from the Editor menu for a one-click version of the
    /// same flow: Window > Ludo Tools > Empire > 3. QA: Seed + Build + Save.
    /// </summary>
    public static class EmpireQaAutomation
    {
        private const string TestScenePath = "Assets/Scenes/EmpireMilestone1_Test.unity";

        [MenuItem("Window/Ludo Tools/Empire/3. QA: Seed + Build + Save")]
        public static void SetupMilestone1Scene()
        {
            Debug.Log("[QA] Starting Empire Milestone 1 automated setup...");

            EnsureFolder("Assets/Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Debug.Log("[QA] Created a fresh scene for testing.");

            EmpireDataSeeder.Seed();
            Debug.Log("[QA] Seed Starter Data complete.");

            EmpireSceneBuilder.Build();
            Debug.Log("[QA] Build Milestone 1 Scene complete.");

            bool saved = EditorSceneManager.SaveScene(scene, TestScenePath);
            Debug.Log("[QA] Scene saved to " + TestScenePath + " - success: " + saved);

            Debug.Log("[QA] Empire Milestone 1 automated setup COMPLETE. Open " + TestScenePath + " and press Play to test.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folderName = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
