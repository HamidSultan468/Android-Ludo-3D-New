using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LudoEmpire.Ludo
{
    /// <summary>
    /// Editor-only tool that procedurally builds a lightweight Main Menu scene: a title plus
    /// "Player vs AI" / "Pass &amp; Play" buttons wired to a <see cref="LudoMainMenuController"/>. Saves
    /// the scene to <see cref="ScenePath"/> and registers both it and the board scene in Build Settings,
    /// so runtime <c>SceneManager.LoadScene</c> calls (from here, and from
    /// <see cref="LudoVictoryScreenController"/>'s Main Menu button) actually work in a build. The new
    /// scene is opened additively alongside whatever you already have open, so your current work is never
    /// touched or replaced - only closed again (after being saved to disk) once the build finishes.
    /// Lives under an "Editor" folder like the project's other editor-only tools, so it's automatically
    /// excluded from player builds without needing an explicit <c>#if UNITY_EDITOR</c> guard.
    /// </summary>
    public static class LudoMainMenuSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";
        private const string BoardScenePath = "Assets/Scenes/SampleScene.unity";
        private const string BoardSceneName = "SampleScene";

        [MenuItem("Ludo Tools/Build Main Menu Scene")]
        public static void BuildMainMenuScene()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Build Main Menu Scene?",
                $"This creates (or overwrites) '{ScenePath}' with a procedurally generated main menu " +
                "(title + Player vs AI / Pass & Play buttons), opened additively alongside whatever you " +
                "already have open - your current scene isn't touched. It also registers both this scene " +
                $"and '{BoardScenePath}' in Build Settings so scene loads work at runtime.",
                "Build", "Cancel");
            if (!proceed) return;

            Scene menuScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            BuildContent(menuScene);

            bool saved = EditorSceneManager.SaveScene(menuScene, ScenePath);
            if (!saved)
            {
                Debug.LogError($"[LudoMainMenuSceneBuilder] Failed to save the scene to '{ScenePath}'.");
                return;
            }

            AddSceneToBuildSettings(ScenePath);
            AddSceneToBuildSettings(BoardScenePath);

            EditorSceneManager.CloseScene(menuScene, true);

            Debug.Log($"[LudoMainMenuSceneBuilder] Main Menu scene built and saved to '{ScenePath}', and " +
                      "both it and the board scene are registered in Build Settings.");
        }

        private static void BuildContent(Scene targetScene)
        {
            GameObject cameraGO = new GameObject("Main Camera");
            cameraGO.tag = "MainCamera";
            Camera cam = cameraGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.05f);
            SceneManager.MoveGameObjectToScene(cameraGO, targetScene);

            GameObject canvasGO = new GameObject("Canvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);

            canvasGO.AddComponent<GraphicRaycaster>();
            SceneManager.MoveGameObjectToScene(canvasGO, targetScene);

            GameObject eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<EventSystem>();
            eventSystemGO.AddComponent<StandaloneInputModule>();
            SceneManager.MoveGameObjectToScene(eventSystemGO, targetScene);

            Text title = CreateLabel(canvasGO.transform, "TitleText", new Vector2(0f, 500f), new Vector2(900f, 220f), "LUDO EMPIRE", 88);
            title.color = new Color(0.4f, 0.85f, 1f);

            Text subtitle = CreateLabel(canvasGO.transform, "SubtitleText", new Vector2(0f, 350f), new Vector2(700f, 80f), "Choose a Mode", 36);
            subtitle.color = new Color(0.85f, 0.85f, 0.9f);

            Button vsAiButton = CreateButton(canvasGO.transform, "PlayVsAIButton", new Vector2(0f, 100f), new Vector2(520f, 140f), "Player vs AI");
            Button passPlayButton = CreateButton(canvasGO.transform, "PassAndPlayButton", new Vector2(0f, -90f), new Vector2(520f, 140f), "Pass & Play");

            LudoMainMenuController menuController = canvasGO.AddComponent<LudoMainMenuController>();
            menuController.Configure(BoardSceneName);

            UnityEventTools.AddPersistentListener(vsAiButton.onClick, menuController.PlayVsAI);
            UnityEventTools.AddPersistentListener(passPlayButton.onClick, menuController.PassAndPlay);

            EnsureMusicManager(targetScene);
        }

        /// <summary>Creates a LudoMusicManager here too, so background music is already playing on the
        /// very first screen. Since this scene and the board scene are separate files, each bakes in its
        /// own copy - harmless, because LudoMusicManager.Awake()'s own singleton guard destroys whichever
        /// instance loads second at actual runtime (the survivor's DontDestroyOnLoad carries it across the
        /// Main Menu -&gt; board scene switch), so exactly one ever ends up persisting during play.</summary>
        private static void EnsureMusicManager(Scene targetScene)
        {
            GameObject musicGO = new GameObject("MusicManager");
            musicGO.AddComponent<AudioSource>();
            musicGO.AddComponent<LudoMusicManager>();
            SceneManager.MoveGameObjectToScene(musicGO, targetScene);
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath)) return;

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == scenePath))
            {
                return; // already registered
            }

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static Text CreateLabel(Transform parent, string name, Vector2 anchoredPos, Vector2 size, string text, int fontSize)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            Text label = go.AddComponent<Text>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return label;
        }

        private static Button CreateButton(Transform parent, string name, Vector2 anchoredPos, Vector2 size, string label)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            Image image = go.AddComponent<Image>();
            image.color = new Color(0.12f, 0.45f, 0.85f);

            Button button = go.AddComponent<Button>();

            Text buttonText = CreateLabel(go.transform, "Label", Vector2.zero, size, label, 42);
            RectTransform textRt = buttonText.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            textRt.anchoredPosition = Vector2.zero;

            return button;
        }
    }
}
