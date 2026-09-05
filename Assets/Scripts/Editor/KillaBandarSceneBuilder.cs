using LudoGame.Board;
using LudoGame.MiniGames.KillaBandar;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static LudoGame.EditorTools.LudoEditorUtility;

namespace LudoGame.EditorTools
{
    /// <summary>
    /// One-click setup for a playable Killa Bandar arena: ground, Killa
    /// stake, Milestone Target, 4 player avatars (colored like the main
    /// Ludo game's 4 players), rope, shoe pile, and a full HUD - all wired
    /// to KillaBandarGameManager. Also has a headless QA entry point that
    /// builds and saves a dedicated test scene from the command line, the
    /// same pattern as the Empire tools.
    ///
    /// How to use:
    /// - Window > Ludo Tools > Killa Bandar > 1. Build Arena Scene (in an
    ///   open scene of your choice - ideally a fresh empty one), or
    /// - Window > Ludo Tools > Killa Bandar > 2. QA: Build + Save Test Scene
    ///   for a one-click version that saves straight to
    ///   Assets/Scenes/KillaBandarMilestone_Test.unity, or from the command line:
    ///   Unity.exe -batchmode -nographics -quit -projectPath "&lt;path&gt;"
    ///     -executeMethod LudoGame.EditorTools.KillaBandarSceneBuilder.QaBuildAndSaveTestScene
    /// </summary>
    public static class KillaBandarSceneBuilder
    {
        private const string TestScenePath = "Assets/Scenes/KillaBandarMilestone_Test.unity";

        private static readonly GridManager.PlayerColor[] PlayerOrder =
        {
            GridManager.PlayerColor.Red, GridManager.PlayerColor.Green,
            GridManager.PlayerColor.Yellow, GridManager.PlayerColor.Blue,
        };

        private static readonly Color[] PlayerColors = { Color.red, Color.green, Color.yellow, new Color(0.2f, 0.4f, 1f) };

        [MenuItem("Window/Ludo Tools/Killa Bandar/1. Build Arena Scene")]
        private static void BuildInCurrentScene() => Build();

        [MenuItem("Window/Ludo Tools/Killa Bandar/2. QA: Build + Save Test Scene")]
        public static void QaBuildAndSaveTestScene()
        {
            Debug.Log("[QA] Starting Killa Bandar automated setup...");

            EnsureFolder("Assets/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Debug.Log("[QA] Created a fresh scene for testing.");

            Build();
            Debug.Log("[QA] Build Arena Scene complete.");

            bool saved = EditorSceneManager.SaveScene(scene, TestScenePath);
            Debug.Log("[QA] Scene saved to " + TestScenePath + " - success: " + saved);
            Debug.Log("[QA] Killa Bandar automated setup COMPLETE. Open " + TestScenePath + " and press Play to test.");
        }

        private static void Build()
        {
            Transform stake = BuildStakeAndGround();
            Transform milestone = BuildMilestoneTarget();
            KillaBandarPlayer[] players = BuildPlayers(stake);

            ShoePile shoePile = BuildShoePile(stake);
            RopeConstraint rope = BuildRope(stake, players[0].transform);

            GameObject managerGO = new GameObject("KillaBandarGameManager");
            Undo.RegisterCreatedObjectUndo(managerGO, "Killa Bandar Scene Builder");
            KillaBandarGameManager manager = managerGO.AddComponent<KillaBandarGameManager>();
            SetField(manager, "shoePile", shoePile);
            SetField(manager, "ropeConstraint", rope);
            SetField(manager, "killaStake", stake);
            SetField(manager, "milestoneTarget", milestone);
            SetObjectArray(manager, "players", players);
            SetField(manager, "startingProtector", players[0]); // Red starts as Killa Bandar

            AddAttackerAi(players, manager, stake);
            BuildUi(manager);

            EditorUtility.DisplayDialog("Killa Bandar Scene Builder",
                "Built the arena: ground, Killa stake, Milestone Target, 4 players (Red starts as " +
                "Protector), rope, shoe pile, and HUD.\n\nSave the scene (Ctrl+S) and press Play to test.",
                "OK");
        }

        // ---------------- World ----------------

        private static Transform BuildStakeAndGround()
        {
            if (GameObject.Find("KillaArenaGround") == null)
            {
                GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                Undo.RegisterCreatedObjectUndo(ground, "Killa Bandar Scene Builder");
                ground.name = "KillaArenaGround";
                ground.transform.localScale = Vector3.one * 2.5f;
                TintPrimitive(ground, new Color(0.55f, 0.45f, 0.3f));
            }

            GameObject stakeGO = GameObject.Find("KillaStake");
            if (stakeGO == null)
            {
                stakeGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Undo.RegisterCreatedObjectUndo(stakeGO, "Killa Bandar Scene Builder");
                stakeGO.name = "KillaStake";
                stakeGO.transform.position = new Vector3(0f, 0.5f, 0f);
                stakeGO.transform.localScale = new Vector3(0.3f, 0.5f, 0.3f);
                TintPrimitive(stakeGO, new Color(0.4f, 0.25f, 0.1f));
            }

            return stakeGO.transform;
        }

        private static Transform BuildMilestoneTarget()
        {
            GameObject go = GameObject.Find("MilestoneTarget");
            if (go != null) return go.transform;

            go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Undo.RegisterCreatedObjectUndo(go, "Killa Bandar Scene Builder");
            go.name = "MilestoneTarget";
            go.transform.position = new Vector3(9f, 0.05f, 0f);
            go.transform.localScale = new Vector3(1.2f, 0.05f, 1.2f);
            TintPrimitive(go, new Color(1f, 0.85f, 0.2f));

            return go.transform;
        }

        private static KillaBandarPlayer[] BuildPlayers(Transform stake)
        {
            Transform root = GameObject.Find("Players")?.transform;
            if (root != null)
                return root.GetComponentsInChildren<KillaBandarPlayer>();

            root = new GameObject("Players").transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Killa Bandar Scene Builder");

            var players = new KillaBandarPlayer[PlayerOrder.Length];
            for (int i = 0; i < PlayerOrder.Length; i++)
            {
                float angle = i / (float)PlayerOrder.Length * Mathf.PI * 2f;
                Vector3 pos = stake.position + new Vector3(Mathf.Cos(angle) * 3f, 0.9f, Mathf.Sin(angle) * 3f);

                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Undo.RegisterCreatedObjectUndo(go, "Killa Bandar Scene Builder");
                go.name = PlayerOrder[i] + "_Player";
                go.transform.SetParent(root, false);
                go.transform.position = pos;
                go.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
                TintPrimitive(go, PlayerColors[i]);

                KillaBandarPlayer player = go.AddComponent<KillaBandarPlayer>();
                SetEnumField(player, "color", PlayerOrder[i]);
                players[i] = player;
            }

            return players;
        }

        private static ShoePile BuildShoePile(Transform stake)
        {
            ShoePile existing = Object.FindAnyObjectByType<ShoePile>();
            if (existing != null) return existing;

            GameObject go = new GameObject("ShoePile");
            Undo.RegisterCreatedObjectUndo(go, "Killa Bandar Scene Builder");
            ShoePile pile = go.AddComponent<ShoePile>();
            SetField(pile, "killaStake", stake);
            return pile;
        }

        private static RopeConstraint BuildRope(Transform stake, Transform protector)
        {
            RopeConstraint existing = Object.FindAnyObjectByType<RopeConstraint>();
            if (existing != null) return existing;

            GameObject go = new GameObject("Rope", typeof(LineRenderer));
            Undo.RegisterCreatedObjectUndo(go, "Killa Bandar Scene Builder");

            LineRenderer line = go.GetComponent<LineRenderer>();
            line.startWidth = 0.05f;
            line.endWidth = 0.05f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = line.endColor = new Color(0.6f, 0.5f, 0.3f);

            RopeConstraint rope = go.AddComponent<RopeConstraint>();
            SetField(rope, "killaStake", stake);
            SetField(rope, "protectorTransform", protector);
            return rope;
        }

        /// <summary>
        /// Adds attacker AI to every player - it self-disables whenever that
        /// player becomes the Protector (see KillaBandarAttackerAI), so this
        /// works uniformly across role swaps instead of needing to be
        /// re-wired each time.
        /// </summary>
        private static void AddAttackerAi(KillaBandarPlayer[] players, KillaBandarGameManager manager, Transform stake)
        {
            foreach (KillaBandarPlayer player in players)
            {
                KillaBandarAttackerAI ai = player.gameObject.AddComponent<KillaBandarAttackerAI>();
                SetField(ai, "player", player);
                SetField(ai, "gameManager", manager);
                SetField(ai, "killaStake", stake);
            }
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

        // ---------------- UI ----------------

        private static void BuildUi(KillaBandarGameManager manager)
        {
            Canvas canvas = GetOrCreateCanvas();
            EnsureEventSystem();

            Text shoesText = CreateText(canvas.transform, "ShoesText", "Shoes: 6", 30, TextAnchor.UpperLeft);
            AnchorTopLeft(shoesText.rectTransform, 0f);

            Text stateText = CreateText(canvas.transform, "StateText", "Defending", 30, TextAnchor.UpperLeft);
            AnchorTopLeft(stateText.rectTransform, 40f);

            Text protectorText = CreateText(canvas.transform, "ProtectorText", "Red is Killa Bandar", 30, TextAnchor.UpperLeft);
            AnchorTopLeft(protectorText.rectTransform, 80f);

            Button sprintButton = CreateButton(canvas.transform, "SprintButton", "Sprint to Milestone!");
            RectTransform sprintRect = sprintButton.GetComponent<RectTransform>();
            sprintRect.anchorMin = new Vector2(0.5f, 0f);
            sprintRect.anchorMax = new Vector2(0.5f, 0f);
            sprintRect.pivot = new Vector2(0.5f, 0f);
            sprintRect.anchoredPosition = new Vector2(0f, 30f);
            sprintRect.sizeDelta = new Vector2(360f, 70f);
            sprintButton.gameObject.SetActive(false);

            Button returnButton = CreateButton(canvas.transform, "ReturnToLudoButton", "Return to Ludo");
            RectTransform returnRect = returnButton.GetComponent<RectTransform>();
            returnRect.anchorMin = new Vector2(1f, 1f);
            returnRect.anchorMax = new Vector2(1f, 1f);
            returnRect.pivot = new Vector2(1f, 1f);
            returnRect.anchoredPosition = new Vector2(-20f, -20f);
            returnRect.sizeDelta = new Vector2(240f, 60f);

            GameObject launcherGO = new GameObject("KillaBandarLauncher");
            Undo.RegisterCreatedObjectUndo(launcherGO, "Killa Bandar Scene Builder");
            KillaBandarLauncher launcher = launcherGO.AddComponent<KillaBandarLauncher>();
            // AddListener() here would only be a runtime-only callback, lost the moment this
            // scene is saved and reopened. AddPersistentListener bakes it into the serialized
            // scene (same as wiring by hand in the Inspector) so it survives a restart.
            UnityEventTools.AddPersistentListener(returnButton.onClick, launcher.ReturnToLudo);

            KillaBandarUI ui = canvas.gameObject.AddComponent<KillaBandarUI>();
            SetField(ui, "gameManager", manager);
            SetField(ui, "shoesText", shoesText);
            SetField(ui, "stateText", stateText);
            SetField(ui, "protectorText", protectorText);
            SetField(ui, "sprintButton", sprintButton);

            BuildJoystick(canvas.transform, manager);
        }

        /// <summary>Builds an on-screen joystick (bottom-left) that always drives whoever is currently the Protector.</summary>
        private static void BuildJoystick(Transform canvasTransform, KillaBandarGameManager manager)
        {
            Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            GameObject bg = new GameObject("JoystickBackground", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(bg, "Killa Bandar Scene Builder");
            bg.transform.SetParent(canvasTransform, false);

            Image bgImage = bg.AddComponent<Image>();
            bgImage.sprite = knob;
            bgImage.color = new Color(1f, 1f, 1f, 0.25f);

            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0f);
            bgRect.anchorMax = new Vector2(0f, 0f);
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.anchoredPosition = new Vector2(160f, 160f);
            bgRect.sizeDelta = new Vector2(220f, 220f);

            GameObject handle = new GameObject("JoystickHandle", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(handle, "Killa Bandar Scene Builder");
            handle.transform.SetParent(bg.transform, false);

            Image handleImage = handle.AddComponent<Image>();
            handleImage.sprite = knob;
            handleImage.color = new Color(1f, 1f, 1f, 0.6f);

            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.anchoredPosition = Vector2.zero;
            handleRect.sizeDelta = new Vector2(90f, 90f);

            VirtualJoystick joystick = bg.AddComponent<VirtualJoystick>();
            SetField(joystick, "background", bgRect);
            SetField(joystick, "handle", handleRect);

            GameObject controllerGO = new GameObject("KillaBandarInputController");
            Undo.RegisterCreatedObjectUndo(controllerGO, "Killa Bandar Scene Builder");
            KillaBandarInputController controller = controllerGO.AddComponent<KillaBandarInputController>();
            SetField(controller, "gameManager", manager);
            SetField(controller, "joystick", joystick);
        }

        private static void AnchorTopLeft(RectTransform rt, float yOffset)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(30f, -30f - yOffset);
            rt.sizeDelta = new Vector2(420f, 36f);
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
