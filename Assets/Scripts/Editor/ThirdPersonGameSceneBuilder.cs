using LudoGame.ThirdPersonGame;
using LudoGame.ThirdPersonGame.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static LudoGame.EditorTools.LudoEditorUtility;

namespace LudoGame.EditorTools
{
    /// <summary>
    /// One-click setup for the Third Person Game prototype: a placeholder
    /// terrain/roads/buildings environment with lighting, the player
    /// (Character Controller + Animator + PlayerMovement/PlayerAnimatorController/
    /// PlayerHealth all wired together), a following camera, audio, and the
    /// full HUD (health bar, score, pause menu, win/lose panel). Safe to run
    /// more than once - it reuses anything that already exists by name
    /// instead of duplicating it.
    ///
    /// Everything here is a placeholder stand-in (primitive shapes, an
    /// Animator Controller with no animation clips assigned yet) so there is
    /// something playable to test immediately - swap in real 3D models and
    /// animation clips whenever you have them; the wiring keeps working as
    /// long as you don't delete these objects' components.
    ///
    /// How to use:
    /// - Window > Ludo Tools > Third Person Game > 4. Build Everything (One-Click),
    ///   in an open scene of your choice (ideally a fresh empty one), or run
    ///   steps 1-3 separately for more control, or from the command line:
    ///   Unity.exe -batchmode -nographics -quit -projectPath "&lt;path&gt;"
    ///     -executeMethod LudoGame.EditorTools.ThirdPersonGameSceneBuilder.QaBuildAndSaveTestScene
    /// </summary>
    public static class ThirdPersonGameSceneBuilder
    {
        private const string UndoLabel = "Third Person Game Scene Builder";
        private const string GeneratedFolder = "Assets/Generated/ThirdPersonGame";
        private const string TestScenePath = "Assets/Scenes/ThirdPersonGame_Test.unity";

        [MenuItem("Window/Ludo Tools/Third Person Game/1. Build Environment")]
        private static void MenuBuildEnvironment() => BuildEnvironment();

        [MenuItem("Window/Ludo Tools/Third Person Game/2. Build Player + Camera + Managers")]
        private static void MenuBuildPlayer() => BuildPlayerAndCamera();

        [MenuItem("Window/Ludo Tools/Third Person Game/3. Build UI")]
        private static void MenuBuildUI()
        {
            PlayerHealth health = Object.FindAnyObjectByType<PlayerHealth>();
            ScoreManager score = Object.FindAnyObjectByType<ScoreManager>();
            GameManager gameManager = Object.FindAnyObjectByType<GameManager>();

            if (health == null || score == null || gameManager == null)
            {
                EditorUtility.DisplayDialog("Third Person Game - Build UI",
                    "Run step 2 (Build Player + Camera + Managers) first - the UI needs " +
                    "PlayerHealth, ScoreManager, and GameManager to already exist so it can wire itself to them.",
                    "OK");
                return;
            }

            BuildUI(health, score, gameManager);
            EditorUtility.DisplayDialog("Third Person Game - Build UI",
                "Built the HUD: health bar, score display, pause menu (Escape key or the pause button), " +
                "and a Win/Lose panel.",
                "OK");
        }

        [MenuItem("Window/Ludo Tools/Third Person Game/4. Build Everything (One-Click)")]
        private static void MenuBuildEverything()
        {
            BuildEverything();
            EditorUtility.DisplayDialog("Third Person Game Scene Builder",
                "Built the full prototype: terrain/roads/buildings environment, the player (movement, " +
                "animation states, health), a following camera, audio, and the HUD (health/score/pause/win-lose).\n\n" +
                "Placeholders to swap out later: primitive shapes for real 3D models, and the Animator " +
                "Controller's 4 states have no animation clips assigned yet - drag your real clips into " +
                "Idle/Walking/Praying/Defeated inside " + GeneratedFolder + "/PlayerAnimator.controller.\n\n" +
                "Save the scene (Ctrl+S) and press Play: WASD/arrow keys to move, Escape to pause.",
                "OK");
        }

        [MenuItem("Window/Ludo Tools/Third Person Game/QA: Build + Save Test Scene")]
        public static void QaBuildAndSaveTestScene()
        {
            Debug.Log("[QA] Starting Third Person Game automated setup...");

            EnsureFolder("Assets/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Debug.Log("[QA] Created a fresh scene for testing.");

            BuildEverything();
            Debug.Log("[QA] Build complete.");

            bool saved = EditorSceneManager.SaveScene(scene, TestScenePath);
            Debug.Log("[QA] Scene saved to " + TestScenePath + " - success: " + saved);
            Debug.Log("[QA] Third Person Game automated setup COMPLETE. Open " + TestScenePath + " and press Play to test.");
        }

        private static void BuildEverything()
        {
            BuildEnvironment();
            var (_, health, score, gameManager) = BuildPlayerAndCamera();
            BuildUI(health, score, gameManager);
        }

        // ==================== Step 1: Environment ====================

        private static void BuildEnvironment()
        {
            BuildTerrain();
            BuildRoads();
            BuildBuildings();
            SetupLighting();
        }

        private static Transform BuildTerrain()
        {
            GameObject existing = GameObject.Find("ThirdPersonGameTerrain");
            if (existing != null) return existing.transform;

            EnsureFolder(GeneratedFolder);
            string dataPath = GeneratedFolder + "/TerrainData.asset";

            TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
            if (terrainData == null)
            {
                terrainData = new TerrainData();
                terrainData.heightmapResolution = 129;
                terrainData.size = new Vector3(200f, 30f, 200f);
                AssetDatabase.CreateAsset(terrainData, dataPath);
            }

            // Terrain.CreateTerrainGameObject also adds a Terrain Collider automatically -
            // this is what stops the player from falling through the ground (Phase 5).
            GameObject terrainGO = Terrain.CreateTerrainGameObject(terrainData);
            Undo.RegisterCreatedObjectUndo(terrainGO, UndoLabel);
            terrainGO.name = "ThirdPersonGameTerrain";
            terrainGO.transform.position = new Vector3(-100f, 0f, -100f); // centers the terrain on the world origin

            GameObjectUtility.SetStaticEditorFlags(terrainGO,
                StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.NavigationStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

            return terrainGO.transform;
        }

        private static void BuildRoads()
        {
            BuildRoadSegment("Road_NorthSouth", new Vector3(8f, 1f, 100f));
            BuildRoadSegment("Road_EastWest", new Vector3(100f, 1f, 8f));
        }

        private static void BuildRoadSegment(string name, Vector3 size)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null) return;

            GameObject road = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Undo.RegisterCreatedObjectUndo(road, UndoLabel);
            road.name = name;
            road.transform.position = new Vector3(0f, 0.05f, 0f); // slightly above the terrain to avoid z-fighting
            road.transform.localScale = new Vector3(size.x / 10f, 1f, size.z / 10f); // Unity's default Plane is 10x10 units
            TintPrimitive(road, new Color(0.25f, 0.25f, 0.28f)); // asphalt grey

            GameObjectUtility.SetStaticEditorFlags(road, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
        }

        private static void BuildBuildings()
        {
            BuildBuilding("Building_1", new Vector2(20f, 20f), new Vector3(8f, 10f, 8f));
            BuildBuilding("Building_2", new Vector2(-20f, 20f), new Vector3(10f, 12f, 10f));
            BuildBuilding("Building_3", new Vector2(20f, -20f), new Vector3(6f, 8f, 6f));
            BuildBuilding("Building_4", new Vector2(-20f, -20f), new Vector3(9f, 14f, 9f));
            BuildBuilding("Building_5", new Vector2(0f, 35f), new Vector3(12f, 10f, 8f));
            BuildBuilding("Building_6", new Vector2(35f, 0f), new Vector3(8f, 10f, 12f));
        }

        private static void BuildBuilding(string name, Vector2 groundPosition, Vector3 size)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null) return;

            // CreatePrimitive(Cube) already includes a Box Collider - satisfies Phase 5
            // (no extra collider setup needed for these placeholders).
            GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(building, UndoLabel);
            building.name = name;
            building.transform.position = new Vector3(groundPosition.x, size.y / 2f, groundPosition.y); // half-height so it sits ON the ground, not buried in it
            building.transform.localScale = size;
            TintPrimitive(building, new Color(0.55f, 0.5f, 0.45f)); // concrete-ish

            GameObjectUtility.SetStaticEditorFlags(building,
                StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }

        private static void SetupLighting()
        {
            Light sun = FindDirectionalLight();
            if (sun == null)
            {
                GameObject lightGO = new GameObject("Directional Light", typeof(Light));
                Undo.RegisterCreatedObjectUndo(lightGO, UndoLabel);
                sun = lightGO.GetComponent<Light>();
                sun.type = LightType.Directional;
            }

            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
        }

        private static Light FindDirectionalLight()
        {
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) return light;
            return null;
        }

        // ==================== Step 2: Player, Camera, Managers ====================

        private static (GameObject player, PlayerHealth health, ScoreManager score, GameManager gameManager) BuildPlayerAndCamera()
        {
            GameObject player = BuildPlayer();
            PlayerMovement movement = player.GetComponent<PlayerMovement>();
            PlayerHealth health = player.GetComponent<PlayerHealth>();

            BuildCamera(player);

            AudioManager audio = GetOrCreate<AudioManager>("AudioManager");
            SetField(audio, "playerMovement", movement);

            ScoreManager score = GetOrCreate<ScoreManager>("ScoreManager");

            GameManager gameManager = GetOrCreate<GameManager>("GameManager");
            SetField(gameManager, "playerHealth", health);
            SetField(gameManager, "scoreManager", score);

            return (player, health, score, gameManager);
        }

        private static GameObject BuildPlayer()
        {
            GameObject existing = GameObject.Find("Player");
            if (existing != null) return existing;

            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Undo.RegisterCreatedObjectUndo(player, UndoLabel);
            player.name = "Player";
            player.transform.position = new Vector3(0f, 1f, -5f);
            TintPrimitive(player, new Color(0.2f, 0.5f, 0.9f));

            // A Character Controller replaces the primitive's default Capsule Collider - having
            // both would double up on collision handling. Its default shape (height 2, radius
            // 0.5, centered on the pivot) already matches this primitive's mesh exactly.
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            player.AddComponent<CharacterController>();

            Animator animator = player.AddComponent<Animator>();
            animator.runtimeAnimatorController = BuildAnimatorController();

            player.AddComponent<PlayerMovement>();
            PlayerAnimatorController animController = player.AddComponent<PlayerAnimatorController>();
            SetField(animController, "playerMovement", player.GetComponent<PlayerMovement>());

            player.AddComponent<PlayerHealth>();

            return player;
        }

        /// <summary>
        /// Builds the Animator Controller's states/parameters/transitions exactly as described
        /// in the manual Phase 3 setup steps. No animation clips are assigned to the 4 states
        /// (there are no clips in the project yet) - drag real Motion clips into
        /// Idle/Walking/Praying/Defeated later; the parameters/transitions already work.
        /// </summary>
        private static AnimatorController BuildAnimatorController()
        {
            EnsureFolder(GeneratedFolder);
            string path = GeneratedFolder + "/PlayerAnimator.controller";

            AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null) return existing;

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Pray", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Defeated", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState idle = stateMachine.AddState("Idle");
            AnimatorState walking = stateMachine.AddState("Walking");
            AnimatorState praying = stateMachine.AddState("Praying");
            AnimatorState defeated = stateMachine.AddState("Defeated");
            stateMachine.defaultState = idle;

            AnimatorStateTransition idleToWalking = idle.AddTransition(walking);
            idleToWalking.hasExitTime = false;
            idleToWalking.duration = 0.1f;
            idleToWalking.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");

            AnimatorStateTransition walkingToIdle = walking.AddTransition(idle);
            walkingToIdle.hasExitTime = false;
            walkingToIdle.duration = 0.1f;
            walkingToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");

            AnimatorStateTransition anyToPraying = stateMachine.AddAnyStateTransition(praying);
            anyToPraying.hasExitTime = false;
            anyToPraying.duration = 0.05f;
            anyToPraying.AddCondition(AnimatorConditionMode.If, 0f, "Pray");

            AnimatorStateTransition prayingToIdle = praying.AddTransition(idle);
            prayingToIdle.hasExitTime = true;
            prayingToIdle.exitTime = 1f;
            prayingToIdle.duration = 0.15f;

            AnimatorStateTransition anyToDefeated = stateMachine.AddAnyStateTransition(defeated);
            anyToDefeated.hasExitTime = false;
            anyToDefeated.duration = 0.05f;
            anyToDefeated.AddCondition(AnimatorConditionMode.If, 0f, "Defeated");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return controller;
        }

        private static void BuildCamera(GameObject player)
        {
            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindAnyObjectByType<Camera>();

            if (cam == null)
            {
                GameObject camGO = new GameObject("Main Camera", typeof(Camera));
                Undo.RegisterCreatedObjectUndo(camGO, UndoLabel);
                camGO.tag = "MainCamera";
                cam = camGO.GetComponent<Camera>();
            }

            CameraFollow follow = cam.GetComponent<CameraFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
            SetField(follow, "target", player.transform);
        }

        // ==================== Step 3: UI ====================

        private static void BuildUI(PlayerHealth health, ScoreManager score, GameManager gameManager)
        {
            Canvas canvas = GetOrCreateCanvas();
            EnsureEventSystem();

            BuildHealthBar(canvas.transform, health);
            BuildScoreDisplay(canvas.transform, score);

            var (pausePanel, resumeButton, pauseMainMenuButton, quitButton) = BuildPausePanel(canvas.transform);
            Button openPauseButton = BuildOpenPauseButton(canvas.transform);
            var (resultPanel, resultText, restartButton, resultMainMenuButton) = BuildResultPanel(canvas.transform);

            // PauseMenuUI and GameResultUI both need to live on an ALWAYS-ACTIVE object, separate
            // from the panels they show/hide: if placed directly on a panel that starts hidden
            // (SetActive(false)), their own OnEnable (where they subscribe to the Escape key /
            // GameManager.OnGameEnded) would never run until something else showed the panel
            // first - and nothing would, since showing the panel is exactly what those two are for.
            GameObject controllers = GameObject.Find("UIControllers");
            if (controllers == null)
            {
                controllers = new GameObject("UIControllers");
                Undo.RegisterCreatedObjectUndo(controllers, UndoLabel);
            }

            PauseMenuUI pauseMenu = controllers.GetComponent<PauseMenuUI>();
            if (pauseMenu == null) pauseMenu = controllers.AddComponent<PauseMenuUI>();
            SetField(pauseMenu, "pausePanel", pausePanel);
            SetField(pauseMenu, "openPauseButton", openPauseButton);
            SetField(pauseMenu, "resumeButton", resumeButton);
            SetField(pauseMenu, "mainMenuButton", pauseMainMenuButton);
            SetField(pauseMenu, "quitButton", quitButton);

            GameResultUI resultUI = controllers.GetComponent<GameResultUI>();
            if (resultUI == null) resultUI = controllers.AddComponent<GameResultUI>();
            SetField(resultUI, "gameManager", gameManager);
            SetField(resultUI, "resultPanel", resultPanel);
            SetField(resultUI, "resultText", resultText);
            SetField(resultUI, "restartButton", restartButton);
            SetField(resultUI, "mainMenuButton", resultMainMenuButton);
        }

        private static void BuildHealthBar(Transform canvasTransform, PlayerHealth health)
        {
            GameObject existing = GameObject.Find("HealthBar");
            if (existing != null) return;

            GameObject container = new GameObject("HealthBar", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(container, UndoLabel);
            container.transform.SetParent(canvasTransform, false);

            RectTransform containerRect = container.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0f, 1f);
            containerRect.anchorMax = new Vector2(0f, 1f);
            containerRect.pivot = new Vector2(0f, 1f);
            containerRect.anchoredPosition = new Vector2(30f, -30f);
            containerRect.sizeDelta = new Vector2(320f, 36f);

            Image background = container.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.4f);

            GameObject fillGO = new GameObject("Fill", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(fillGO, UndoLabel);
            fillGO.transform.SetParent(container.transform, false);
            InsetFullRect(fillGO.GetComponent<RectTransform>(), 4f, 4f);

            Image fillImage = fillGO.AddComponent<Image>();
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = 1f;
            fillImage.color = Color.green;

            HealthBarUI healthBarUI = container.AddComponent<HealthBarUI>();
            SetField(healthBarUI, "playerHealth", health);
            SetField(healthBarUI, "fillImage", fillImage);
        }

        private static void BuildScoreDisplay(Transform canvasTransform, ScoreManager score)
        {
            GameObject existing = GameObject.Find("ScoreText");
            if (existing != null) return;

            Text text = CreateText(canvasTransform, "ScoreText", "Score: 0", 32, TextAnchor.UpperRight);
            RectTransform rt = text.rectTransform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-30f, -30f);
            rt.sizeDelta = new Vector2(300f, 50f);

            ScoreDisplayUI scoreUI = text.gameObject.AddComponent<ScoreDisplayUI>();
            SetField(scoreUI, "scoreManager", score);
            SetField(scoreUI, "scoreText", text);
        }

        private static (GameObject panel, Button resume, Button mainMenu, Button quit) BuildPausePanel(Transform canvasTransform)
        {
            GameObject existing = GameObject.Find("PausePanel");
            if (existing != null)
                return (existing, existing.transform.Find("ResumeButton")?.GetComponent<Button>(),
                    existing.transform.Find("MainMenuButton")?.GetComponent<Button>(),
                    existing.transform.Find("QuitButton")?.GetComponent<Button>());

            GameObject panel = CreatePanel(canvasTransform, "PausePanel", new Color(0f, 0f, 0f, 0.8f));
            panel.SetActive(false);

            Text title = CreateText(panel.transform, "Title", "Paused", 48, TextAnchor.MiddleCenter);
            PositionRow(title.rectTransform, 0.65f, 500f, 80f);

            Button resumeButton = CreateButton(panel.transform, "ResumeButton", "Resume");
            PositionRow(resumeButton.GetComponent<RectTransform>(), 0.5f, 240f, 70f);

            Button mainMenuButton = CreateButton(panel.transform, "MainMenuButton", "Main Menu");
            PositionRow(mainMenuButton.GetComponent<RectTransform>(), 0.38f, 240f, 70f);

            Button quitButton = CreateButton(panel.transform, "QuitButton", "Quit");
            PositionRow(quitButton.GetComponent<RectTransform>(), 0.26f, 240f, 70f);

            return (panel, resumeButton, mainMenuButton, quitButton);
        }

        private static (GameObject panel, Text resultText, Button restart, Button mainMenu) BuildResultPanel(Transform canvasTransform)
        {
            GameObject existing = GameObject.Find("ResultPanel");
            if (existing != null)
                return (existing, existing.transform.Find("ResultText")?.GetComponent<Text>(),
                    existing.transform.Find("RestartButton")?.GetComponent<Button>(),
                    existing.transform.Find("MainMenuButton")?.GetComponent<Button>());

            GameObject panel = CreatePanel(canvasTransform, "ResultPanel", new Color(0f, 0f, 0f, 0.85f));
            panel.SetActive(false);

            Text resultText = CreateText(panel.transform, "ResultText", "You Win!", 56, TextAnchor.MiddleCenter);
            PositionRow(resultText.rectTransform, 0.6f, 700f, 100f);

            Button restartButton = CreateButton(panel.transform, "RestartButton", "Restart");
            PositionRow(restartButton.GetComponent<RectTransform>(), 0.45f, 240f, 70f);

            Button mainMenuButton = CreateButton(panel.transform, "MainMenuButton", "Main Menu");
            PositionRow(mainMenuButton.GetComponent<RectTransform>(), 0.32f, 240f, 70f);

            return (panel, resultText, restartButton, mainMenuButton);
        }

        private static Button BuildOpenPauseButton(Transform canvasTransform)
        {
            GameObject existing = GameObject.Find("OpenPauseButton");
            if (existing != null) return existing.GetComponent<Button>();

            Button button = CreateButton(canvasTransform, "OpenPauseButton", "II");
            RectTransform rt = button.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-20f, -20f);
            rt.sizeDelta = new Vector2(70f, 70f);

            return button;
        }

        // ==================== Shared Helpers ====================

        private static T GetOrCreate<T>(string name) where T : Component
        {
            T existing = Object.FindAnyObjectByType<T>();
            if (existing != null) return existing;

            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
            return go.AddComponent<T>();
        }

        private static void TintPrimitive(GameObject go, Color color)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            Material mat = renderer.sharedMaterial != null ? new Material(renderer.sharedMaterial) : new Material(Shader.Find("Standard"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color")) mat.color = color;
            renderer.sharedMaterial = mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent); // recursively create parent folders too (e.g. "Assets/Generated" before "Assets/Generated/ThirdPersonGame")

            string folderName = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
