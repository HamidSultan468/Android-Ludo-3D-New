using LudoGame.AI;
using LudoGame.Audio;
using LudoGame.Dice;
using LudoGame.Game;
using LudoGame.Player;
using LudoGame.Save;
using LudoGame.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LudoGame.EditorTools
{
    /// <summary>
    /// A safety-net pass: fixes the scene's Camera setup (unparented, tagged
    /// "MainCamera", and framed to look at the "LudoBoard" object so the Game
    /// view isn't blank) and finds every manager/UI script that already
    /// exists anywhere in the open scene, re-linking their cross references.
    /// Safe to run any time, as many times as you like - e.g. after moving
    /// things around by hand, or after building part of the scene manually
    /// and part with the other Ludo Tools.
    ///
    /// This does NOT create any new managers/UI - it only fixes the camera
    /// and re-links references between objects that already exist. Run
    /// "1. Scene Bootstrapper" first if the core managers don't exist yet.
    ///
    /// How to use: Window > Ludo Tools > 5. Wire All References.
    /// </summary>
    public static class SceneAutoWirer
    {
        private const string DefaultScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Window/Ludo Tools/5. Wire All References")]
        private static void WireAll()
        {
            RunWireAll();
        }

        /// <summary>
        /// Headless entry point for the command line - batch mode doesn't reliably have
        /// any particular scene "active" by default (it can open an empty scene), so this
        /// explicitly opens the main Ludo scene first instead of silently wiring nothing:
        ///   Unity.exe -batchmode -nographics -quit -projectPath "&lt;path&gt;"
        ///     -executeMethod LudoGame.EditorTools.SceneAutoWirer.WireAllInMainScene
        /// </summary>
        public static void WireAllInMainScene()
        {
            if (System.IO.File.Exists(DefaultScenePath))
                EditorSceneManager.OpenScene(DefaultScenePath);
            else
                Debug.LogWarning("SceneAutoWirer: '" + DefaultScenePath + "' not found - operating on whatever scene is currently active.");

            RunWireAll();
        }

        private static void RunWireAll()
        {
            Camera mainCamera = FixCameraSetup();

            DiceManager diceManager = Object.FindAnyObjectByType<DiceManager>();
            GameManager gameManager = Object.FindAnyObjectByType<GameManager>();
            AudioManager audioManager = Object.FindAnyObjectByType<AudioManager>();
            SaveManager saveManager = Object.FindAnyObjectByType<SaveManager>();
            TokenSelector tokenSelector = Object.FindAnyObjectByType<TokenSelector>();

            LudoEditorUtility.SetField(gameManager, "diceManager", diceManager);

            LudoEditorUtility.SetField(tokenSelector, "gameManager", gameManager);
            LudoEditorUtility.SetField(tokenSelector, "raycastCamera", mainCamera);

            LudoEditorUtility.SetField(audioManager, "diceManager", diceManager);
            LudoEditorUtility.SetField(audioManager, "gameManager", gameManager);
            LudoEditorUtility.SetField(audioManager, "tokenSelector", tokenSelector);

            foreach (DiceUI diceUI in Object.FindObjectsByType<DiceUI>())
            {
                LudoEditorUtility.SetField(diceUI, "diceManager", diceManager);
                LudoEditorUtility.SetField(diceUI, "gameManager", gameManager);
            }

            foreach (TurnIndicatorUI turnUI in Object.FindObjectsByType<TurnIndicatorUI>())
                LudoEditorUtility.SetField(turnUI, "gameManager", gameManager);

            foreach (GameOverUI gameOverUI in Object.FindObjectsByType<GameOverUI>())
                LudoEditorUtility.SetField(gameOverUI, "gameManager", gameManager);

            foreach (SettingsUI settingsUI in Object.FindObjectsByType<SettingsUI>())
            {
                LudoEditorUtility.SetField(settingsUI, "saveManager", saveManager);
                LudoEditorUtility.SetField(settingsUI, "audioManager", audioManager);
            }

            foreach (AIPlayer aiPlayer in Object.FindObjectsByType<AIPlayer>())
                LudoEditorUtility.SetField(aiPlayer, "gameManager", gameManager);

            Scene activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            bool saved = EditorSceneManager.SaveScene(activeScene);
            Debug.Log("SceneAutoWirer: scene '" + activeScene.name + "' saved: " + saved);

            EditorUtility.DisplayDialog("Wire All References",
                "Camera checked/fixed (root level, MainCamera tag, extra cameras disabled, framed on the board), " +
                "and re-linked every manager/UI script found in the open scene.\n\n" +
                "Scene saved automatically. Check the Console for details.",
                "OK");
        }

        /// <summary>
        /// Makes sure a camera in the scene is a root-level object tagged "MainCamera" -
        /// required for Camera.main (and this project's tap-to-move raycasting) to work
        /// reliably. If the camera is nested under another object (e.g. a board prefab),
        /// it's unparented to root (keeping its world position) and re-tagged.
        /// </summary>
        private static Camera FixCameraSetup()
        {
            Camera camera = Camera.main;
            if (camera == null) camera = Object.FindAnyObjectByType<Camera>();

            if (camera == null)
            {
                Debug.LogWarning("SceneAutoWirer: no Camera found anywhere in the scene - add one and re-run this tool.");
                return null;
            }

            if (camera.transform.parent != null)
            {
                Undo.SetTransformParent(camera.transform, null, "Wire All References - Unparent Camera");
                Debug.Log("SceneAutoWirer: unparented '" + camera.name + "' to the scene root.", camera);
            }

            if (!camera.CompareTag("MainCamera"))
            {
                Undo.RecordObject(camera.gameObject, "Wire All References - Tag Camera");
                camera.tag = "MainCamera";
                Debug.Log("SceneAutoWirer: tagged '" + camera.name + "' as MainCamera.", camera);
            }

            DisableExtraCameras(camera);
            FrameCameraOnBoard(camera);

            EditorUtility.SetDirty(camera.gameObject);
            EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);

            return camera;
        }

        /// <summary>
        /// More than one enabled Camera in the scene causes confusing/overlapping
        /// rendering - e.g. a leftover camera nested inside an old board prefab. Disables
        /// every camera except the one this tool picked as the main one (does not delete
        /// anything, so it's reversible with Undo or by re-enabling it by hand).
        /// </summary>
        private static void DisableExtraCameras(Camera keep)
        {
            Camera[] allCameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
            foreach (Camera cam in allCameras)
            {
                if (cam == keep || !cam.enabled) continue;

                Undo.RecordObject(cam, "Wire All References - Disable Extra Camera");
                cam.enabled = false;
                string parentName = cam.transform.parent != null ? cam.transform.parent.name : "scene root";
                Debug.LogWarning("SceneAutoWirer: disabled extra camera '" + cam.name + "' (under '" + parentName +
                    "') - only '" + keep.name + "' should be active. Delete the extra one if you don't need it.", cam);
            }
        }

        /// <summary>
        /// Points the camera at the board so the Game view isn't blank. Prefers
        /// "BoardPlaceholder" (created by "6. Create Placeholder Board", which is guaranteed
        /// to have real geometry) and only falls back to "LudoBoard" if that doesn't exist -
        /// "LudoBoard" may only contain waypoint markers with no visible mesh, which would
        /// frame the camera on the wrong (tiny/empty) bounds. Measures the board's actual
        /// rendered size (instead of a fixed magic position) so this works at any scale, then
        /// places the camera above and behind it, looking straight down at its center.
        /// </summary>
        private static void FrameCameraOnBoard(Camera camera)
        {
            GameObject board = GameObject.Find("BoardPlaceholder");
            if (board == null) board = GameObject.Find("LudoBoard");

            if (board == null)
            {
                Debug.LogWarning("SceneAutoWirer: no 'BoardPlaceholder' or 'LudoBoard' object found in the scene - " +
                    "couldn't auto-frame the camera. Position it manually so it looks at your board.", camera);
                return;
            }

            Bounds bounds = CalculateRendererBounds(board);
            Vector3 center = bounds.center;
            float boardSize = Mathf.Max(bounds.size.x, bounds.size.z, 1f);

            Undo.RecordObject(camera.transform, "Wire All References - Frame Camera");
            camera.transform.position = center + new Vector3(0f, boardSize * 1.1f, -boardSize * 0.75f);
            camera.transform.LookAt(center);

            Debug.Log("SceneAutoWirer: framed '" + camera.name + "' on '" + board.name + "' (center " + center + ", size " + boardSize + ").", camera);
        }

        /// <summary>Combined world-space bounds of every Renderer under the given object.</summary>
        private static Bounds CalculateRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one * 10f);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return bounds;
        }
    }
}
