using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TangentLudoEmpire.Core
{
    /// <summary>
    /// Minimal bottom-of-screen "toast" popup. Self-builds its own overlay Canvas on first use, so any
    /// script can call <c>ToastManager.Show("...")</c> with no scene setup. Used by the Dashboard's
    /// "Coming Soon" buttons in Phase 1.
    /// </summary>
    [DisallowMultipleComponent]
    public class ToastManager : MonoBehaviour
    {
        private static ToastManager _instance;

        public static ToastManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<ToastManager>();
                    if (_instance == null)
                        _instance = new GameObject("ToastManager").AddComponent<ToastManager>();
                }
                return _instance;
            }
        }

        [SerializeField] private float defaultSeconds = 1.6f;
        [SerializeField] private float fadeSeconds = 0.2f;

        private CanvasGroup _group;
        private Text _label;
        private Coroutine _routine;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject); // Editor/test code (headless tools) would otherwise hit InvalidOperationException here and abort the rest of Awake()
            BuildUI();
        }

        /// <summary>Static convenience so callers don't touch the singleton directly.</summary>
        public static void Show(string message, float seconds = -1f) => Instance.ShowInternal(message, seconds);

        private void ShowInternal(string message, float seconds)
        {
            if (_group == null) BuildUI();
            _label.text = message;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(ShowRoutine(seconds > 0f ? seconds : defaultSeconds));
        }

        private IEnumerator ShowRoutine(float hold)
        {
            yield return Fade(0f, 1f);
            yield return new WaitForSecondsRealtime(hold);
            yield return Fade(1f, 0f);
            _routine = null;
        }

        private IEnumerator Fade(float from, float to)
        {
            float t = 0f;
            while (t < fadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(from, to, t / fadeSeconds);
                yield return null;
            }
            _group.alpha = to;
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("ToastCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);

            var panelGO = new GameObject("ToastPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            panelGO.transform.SetParent(canvasGO.transform, false);
            var rt = panelGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 220f);
            rt.sizeDelta = new Vector2(760f, 120f);
            panelGO.GetComponent<Image>().color = new Color(0.055f, 0.055f, 0.10f, 0.95f); // #0E0E1A-ish
            _group = panelGO.GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;

            var textGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(panelGO.transform, false);
            var trt = textGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.sizeDelta = Vector2.zero;
            _label = textGO.GetComponent<Text>();
            _label.alignment = TextAnchor.MiddleCenter;
            _label.fontSize = 40;
            _label.color = Color.white;
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.horizontalOverflow = HorizontalWrapMode.Wrap;
        }
    }
}
