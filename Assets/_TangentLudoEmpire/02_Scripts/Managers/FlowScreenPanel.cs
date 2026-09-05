using UnityEngine;

namespace LudoEmpire.Ludo
{
    /// <summary>
    /// Marks a UI panel as one of the flow's <see cref="FlowScreen"/> destinations. Put this on the
    /// panel root (MainMenu, Dashboard, ModeSelect, PlayerSelect, Settings ...). It registers with
    /// <see cref="FlowManager"/> so navigation can show/hide it by id - buttons only ever pass a
    /// <see cref="FlowScreen"/> value, never a GameObject reference.
    ///
    /// Show/hide runs an optional CanvasGroup fade + slide so panel transitions are animated without a
    /// per-frame Update on idle panels (the tween coroutine only runs during the transition).
    /// </summary>
    [DisallowMultipleComponent]
    public class FlowScreenPanel : MonoBehaviour
    {
        [Tooltip("Which flow screen this panel is. 'None' -> the object name is used as the id.")]
        [SerializeField] private FlowScreen screen = FlowScreen.None;

        [Header("Transition (optional)")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform slideTarget;
        [Tooltip("Local-space offset the panel slides in FROM when shown (and out TO when hidden).")]
        [SerializeField] private Vector2 slideOffset = new Vector2(0f, -60f);
        [SerializeField] private float transitionSeconds = 0.22f;
        [Tooltip("Show this panel immediately on Start (typically only the MainMenu panel).")]
        [SerializeField] private bool shownByDefault = false;

        /// <summary>Registration id: the explicit <see cref="screen"/> name, else this object's name.</summary>
        public string PanelId => screen != FlowScreen.None ? screen.ToString() : name;
        public FlowScreen Screen => screen;
        public bool IsShown { get; private set; }

        private Vector2 _shownAnchoredPos;
        private Coroutine _tween;
        private bool _capturedPose;

        private void Reset()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            slideTarget = transform as RectTransform;
        }

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (slideTarget == null) slideTarget = transform as RectTransform;
            CapturePose();
        }

        private void OnEnable()
        {
            FlowManager.Instance.RegisterPanel(this);
            if (!_capturedPose) CapturePose();
        }

        private void OnDisable()
        {
            // FlowManager may already be torn down on app quit.
            if (FlowManager.Instance != null) FlowManager.Instance.UnregisterPanel(this);
        }

        private void Start()
        {
            SetShown(shownByDefault, instant: true);
        }

        private void CapturePose()
        {
            if (slideTarget != null) _shownAnchoredPos = slideTarget.anchoredPosition;
            _capturedPose = true;
        }

        /// <summary>Show or hide this panel, animating unless <paramref name="instant"/>.</summary>
        public void SetShown(bool show, bool instant = false)
        {
            IsShown = show;

            if (_tween != null) { StopCoroutine(_tween); _tween = null; }

            if (instant || transitionSeconds <= 0f || !isActiveAndEnabled)
            {
                ApplyImmediate(show);
                return;
            }

            gameObject.SetActive(true);
            _tween = StartCoroutine(TweenRoutine(show));
        }

        private void ApplyImmediate(bool show)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = show ? 1f : 0f;
                canvasGroup.interactable = show;
                canvasGroup.blocksRaycasts = show;
            }
            if (slideTarget != null)
                slideTarget.anchoredPosition = show ? _shownAnchoredPos : _shownAnchoredPos + slideOffset;

            gameObject.SetActive(show);
        }

        private System.Collections.IEnumerator TweenRoutine(bool show)
        {
            float from = canvasGroup != null ? canvasGroup.alpha : (show ? 0f : 1f);
            float to = show ? 1f : 0f;
            Vector2 posFrom = slideTarget != null ? slideTarget.anchoredPosition : Vector2.zero;
            Vector2 posTo = show ? _shownAnchoredPos : _shownAnchoredPos + slideOffset;

            if (canvasGroup != null && show) { canvasGroup.interactable = false; canvasGroup.blocksRaycasts = true; }

            float t = 0f;
            while (t < transitionSeconds)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / transitionSeconds));
                if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(from, to, p);
                if (slideTarget != null) slideTarget.anchoredPosition = Vector2.Lerp(posFrom, posTo, p);
                yield return null;
            }

            _tween = null;
            ApplyImmediate(show);
        }
    }
}
