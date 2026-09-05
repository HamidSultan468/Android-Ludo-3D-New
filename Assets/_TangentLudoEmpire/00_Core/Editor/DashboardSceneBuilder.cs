using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TangentLudoEmpire.Core;

namespace TangentLudoEmpire.Core.EditorTools
{
    /// <summary>
    /// Generates <c>Assets/_TangentLudoEmpire/Scenes/Dashboard.unity</c> from scratch: a 1080x1920 portrait
    /// Canvas on a #0E0E1A background, a top bar (profile chip + Local + PKT <see cref="ClockWidget"/>),
    /// a 3-slide <see cref="CarouselWidget"/>, a 3x3 button grid driven by a
    /// <see cref="DashboardButtonData"/> asset (created if missing), and a bottom legal strip.
    /// Rerunnable - deletes the old scene file first. No runtime code.
    /// </summary>
    public static class DashboardSceneBuilder
    {
        internal const string SceneDir = "Assets/_TangentLudoEmpire/Scenes";
        internal const string DataDir  = "Assets/_TangentLudoEmpire/Data";
        internal static readonly Color BgColor  = new Color(0.055f, 0.055f, 0.10f); // #0E0E1A
        internal static readonly Color BtnColor = new Color(0.20f, 0.60f, 0.90f);   // #3399E5

        [MenuItem("Tangent Ludo Empire/Phase 1/Build Dashboard Scene")]
        public static void BuildDashboard()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Directory.CreateDirectory(SceneDir);
            string path = $"{SceneDir}/{AppConstants.SCENE_DASHBOARD}.unity";
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGO = new GameObject("Main Camera", typeof(Camera));
            camGO.tag = "MainCamera";
            var cam = camGO.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BgColor;
            cam.orthographic = true;

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            // full-screen dark background image (so it reads dark even over the camera)
            var bg = Panel(canvasGO.transform, "BG", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, BgColor);
            bg.SetAsFirstSibling();

            // ---- Top bar ----
            var topBar = Panel(canvasGO.transform, "TopBar", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -70f), new Vector2(0f, 140f), new Color(1f, 1f, 1f, 0.03f));
            var profile = Panel(topBar, "ProfileIcon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(100f, 0f), new Vector2(104f, 104f), BtnColor);
            Label(profile, "P", 46, Color.white);
            Label(MakeChild(topBar, "Coins", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(340f, 0f), new Vector2(240f, 84f)), "0", 40, Color.white).name = "CoinsLabel";

            var localClock = Label(MakeChild(topBar, "LocalClock", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-230f, 26f), new Vector2(380f, 52f)), "--:--", 34, Color.white);
            localClock.gameObject.AddComponent<ClockWidget>().Configure("Local", false, 0f);
            var pktClock = Label(MakeChild(topBar, "PKTClock", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-230f, -26f), new Vector2(380f, 52f)), "--:--", 34, new Color(0.6f, 0.85f, 1f));
            pktClock.gameObject.AddComponent<ClockWidget>().Configure("PKT", true, 5f);

            // ---- Carousel ----
            var carousel = Label(MakeChild(canvasGO.transform, "Carousel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -280f), new Vector2(920f, 130f)), "Top Player 1", 46, Color.white);
            carousel.gameObject.AddComponent<CanvasGroup>();
            carousel.gameObject.AddComponent<CarouselWidget>();

            // ---- Grid parent ----
            var gridGO = new GameObject("ButtonGrid", typeof(RectTransform));
            gridGO.transform.SetParent(canvasGO.transform, false);
            var gridRT = gridGO.GetComponent<RectTransform>();
            gridRT.anchorMin = gridRT.anchorMax = gridRT.pivot = new Vector2(0.5f, 0.5f);
            gridRT.anchoredPosition = new Vector2(0f, -40f);
            gridRT.sizeDelta = new Vector2(972f, 972f);

            // ---- Bottom bar ----
            Label(MakeChild(canvasGO.transform, "BottomBar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 55f), new Vector2(1000f, 64f)), "Terms  |  Privacy  |  Law and Regulations", 28,
                new Color(1f, 1f, 1f, 0.6f));

            // ---- Data + controller ----
            var data = LoadOrCreateButtonData();
            var coinsLabel = GameObject.Find("CoinsLabel")?.GetComponent<Text>();
            canvasGO.AddComponent<DashboardController>().Configure(data, gridRT, coinsLabel);

            EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.Refresh();
            Debug.Log($"[DashboardSceneBuilder] Built {path}");
        }

        // -------- shared build helpers --------

        internal static DashboardButtonData LoadOrCreateButtonData()
        {
            Directory.CreateDirectory(DataDir);
            string assetPath = $"{DataDir}/DashboardButtons.asset";
            var data = AssetDatabase.LoadAssetAtPath<DashboardButtonData>(assetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<DashboardButtonData>();
                data.buttons = DashboardButtonData.DefaultSet();
                AssetDatabase.CreateAsset(data, assetPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[DashboardSceneBuilder] Created {assetPath}");
            }
            return data;
        }

        internal static Transform Panel(Transform parent, string name, Vector2 aMin, Vector2 aMax,
            Vector2 anchoredPos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return go.transform;
        }

        internal static Transform MakeChild(Transform parent, string name, Vector2 aMin, Vector2 aMax,
            Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;
            return go.transform;
        }

        internal static Text Label(Transform parent, string text, int size, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
            var t = go.GetComponent<Text>();
            t.text = text; t.fontSize = size; t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }
    }
}
