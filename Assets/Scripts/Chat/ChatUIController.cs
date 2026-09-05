using System.Collections;
using LudoGame.AI;
using LudoGame.Board;
using LudoGame.Game;
using UnityEngine;
using UnityEngine.UI;

namespace LudoGame.Chat
{
    /// <summary>
    /// Drives the floating chat button (with its unread badge and "waiting
    /// for your turn" pulse), the glass chat panel's open/close animation,
    /// the quick-message grid, the text input, and the scrolling message
    /// log. This is purely a display layer - all message data lives in
    /// ChatManager, and each floating bubble is its own SpeechBubble.
    ///
    /// Setup: built automatically by ChatUIBuilder (Window > Ludo Tools >
    /// Chat UI Builder). To wire by hand, fill in every field below and hook
    /// each quick-message button's OnClick to SendQuickMessage(string).
    /// </summary>
    public class ChatUIController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ChatManager chatManager;
        [SerializeField] private Camera worldCamera;

        [Header("Chat Button")]
        [SerializeField] private Button chatButton;
        [SerializeField] private GameObject unreadBadge;
        [SerializeField] private Text unreadBadgeText;
        [Tooltip("The button's own RectTransform (or a child) that gets scaled up/down for the pulse animation.")]
        [SerializeField] private RectTransform chatButtonPulseTarget;

        [Header("Panel")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup panelCanvasGroup;
        [SerializeField] private RectTransform panelRect;
        [Tooltip("How far below its open position the panel starts, for the slide-in.")]
        [SerializeField] private float slideDistance = 60f;
        [SerializeField] private float slideDuration = 0.25f;
        [Tooltip("Auto-collapse the panel after this many seconds of no activity while it's open. 0 disables auto-close.")]
        [SerializeField] private float autoCloseDelay = 5f;

        [Header("Input")]
        [SerializeField] private InputField inputField;
        [SerializeField] private Button sendButton;

        [Header("Message Log")]
        [SerializeField] private RectTransform messageLogContent;
        [SerializeField] private ScrollRect messageLogScroll;

        [Header("Speech Bubble")]
        [SerializeField] private SpeechBubble speechBubbleTemplate;
        [SerializeField] private Transform bubbleParent;
        [SerializeField] private float bubbleDuration = 3f;

        [Header("Pulse (highlights the button while it's not your turn / you're waiting)")]
        [SerializeField] private float pulseSpeed = 3f;
        [SerializeField] private float pulseScale = 0.08f;

        [Header("Turn-Based Pulse (optional)")]
        [Tooltip("Drag your GameManager here to auto-pulse the chat button while an AI is playing (a good idle moment to chat). Leave empty and call SetWaitingState(...) yourself for custom logic instead (e.g. a networked \"not my turn\" signal).")]
        [SerializeField] private GameManager gameManager;
        [Tooltip("AI-controlled players - the button pulses while it's one of their turns.")]
        [SerializeField] private AIPlayer[] aiPlayers;

        private bool isOpen;
        private bool isWaiting;
        private Vector2 panelOpenPosition;
        private Vector3 chatButtonBaseScale = Vector3.one;
        private Coroutine panelAnimationCoroutine;
        private Coroutine autoCloseCoroutine;

        private void Awake()
        {
            if (chatButtonPulseTarget != null) chatButtonBaseScale = chatButtonPulseTarget.localScale;
            if (panelRect != null) panelOpenPosition = panelRect.anchoredPosition;
            if (worldCamera == null) worldCamera = Camera.main;

            SetPanelOpen(false, instant: true);
        }

        private void OnEnable()
        {
            if (chatManager != null)
            {
                chatManager.OnMessageSent += HandleMessageSent;
                chatManager.OnUnreadCountChanged += HandleUnreadCountChanged;
            }

            if (chatButton != null) chatButton.onClick.AddListener(ToggleOpen);
            if (sendButton != null) sendButton.onClick.AddListener(SendTypedMessage);
            if (inputField != null) inputField.onSubmit.AddListener(OnInputSubmit);

            if (gameManager != null) gameManager.OnTurnStarted += HandleTurnStarted;
        }

        private void OnDisable()
        {
            if (chatManager != null)
            {
                chatManager.OnMessageSent -= HandleMessageSent;
                chatManager.OnUnreadCountChanged -= HandleUnreadCountChanged;
            }

            if (chatButton != null) chatButton.onClick.RemoveListener(ToggleOpen);
            if (sendButton != null) sendButton.onClick.RemoveListener(SendTypedMessage);
            if (inputField != null) inputField.onSubmit.RemoveListener(OnInputSubmit);

            if (gameManager != null) gameManager.OnTurnStarted -= HandleTurnStarted;
        }

        /// <summary>Pulses the chat button while the new turn belongs to one of the assigned AI players (nobody needs to actively play right now).</summary>
        private void HandleTurnStarted(GridManager.PlayerColor color)
        {
            bool waiting = false;
            if (aiPlayers != null)
            {
                foreach (AIPlayer ai in aiPlayers)
                {
                    if (ai != null && ai.Color == color)
                    {
                        waiting = true;
                        break;
                    }
                }
            }

            SetWaitingState(waiting);
        }

        private void Update()
        {
            if (chatButtonPulseTarget == null) return;

            if (isWaiting)
            {
                float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseScale;
                chatButtonPulseTarget.localScale = chatButtonBaseScale * pulse;
            }
            else if (chatButtonPulseTarget.localScale != chatButtonBaseScale)
            {
                chatButtonPulseTarget.localScale = chatButtonBaseScale;
            }
        }

        /// <summary>
        /// Switches the chat button between calm (State 1 - it's your turn, actively playing)
        /// and pulsing/glowing (State 2 - waiting/spectating, a good moment to chat). Wire this
        /// to whatever "is it my turn" signal your setup uses (e.g. an AIPlayer's turn, or a
        /// networked "not my turn" state) - GameManager.OnTurnStarted is the usual hook point.
        /// </summary>
        public void SetWaitingState(bool waiting)
        {
            isWaiting = waiting;
            if (!waiting && chatButtonPulseTarget != null)
                chatButtonPulseTarget.localScale = chatButtonBaseScale;
        }

        public void ToggleOpen() => SetPanelOpen(!isOpen);

        private void OnInputSubmit(string _) => SendTypedMessage();

        /// <summary>Hook this to each quick-message button's OnClick, with that button's preset text as the argument.</summary>
        public void SendQuickMessage(string text)
        {
            chatManager?.SendCurrentPlayerMessage(text);
            SetPanelOpen(false); // Auto-close after sending
        }

        private void SendTypedMessage()
        {
            if (inputField == null || string.IsNullOrWhiteSpace(inputField.text)) return;

            chatManager?.SendCurrentPlayerMessage(inputField.text);
            inputField.text = string.Empty;
            SetPanelOpen(false); // Auto-close after sending
        }

        private void SetPanelOpen(bool open, bool instant = false)
        {
            isOpen = open;
            if (open) chatManager?.MarkAllRead();

            if (panelAnimationCoroutine != null) StopCoroutine(panelAnimationCoroutine);
            if (autoCloseCoroutine != null) StopCoroutine(autoCloseCoroutine);

            if (instant)
            {
                ApplyPanelState(open, open ? 1f : 0f, open ? panelOpenPosition : panelOpenPosition + Vector2.down * slideDistance);
                if (panelRoot != null) panelRoot.SetActive(open);
                return;
            }

            panelAnimationCoroutine = StartCoroutine(AnimatePanel(open));

            if (open) StartAutoCloseTimer();
        }

        private IEnumerator AnimatePanel(bool open)
        {
            if (panelRoot != null) panelRoot.SetActive(true);

            float fromAlpha = panelCanvasGroup != null ? panelCanvasGroup.alpha : (open ? 0f : 1f);
            float toAlpha = open ? 1f : 0f;

            Vector2 closedPosition = panelOpenPosition + Vector2.down * slideDistance;
            Vector2 fromPos = open ? closedPosition : panelOpenPosition;
            Vector2 toPos = open ? panelOpenPosition : closedPosition;

            float t = 0f;
            while (t < slideDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / slideDuration);
                ApplyPanelState(open, Mathf.Lerp(fromAlpha, toAlpha, p), Vector2.Lerp(fromPos, toPos, p));
                yield return null;
            }

            ApplyPanelState(open, toAlpha, toPos);
            if (panelRoot != null) panelRoot.SetActive(open);
        }

        private void ApplyPanelState(bool interactable, float alpha, Vector2 position)
        {
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = alpha;
                panelCanvasGroup.interactable = interactable;
                panelCanvasGroup.blocksRaycasts = interactable;
            }

            if (panelRect != null) panelRect.anchoredPosition = position;
        }

        private void StartAutoCloseTimer()
        {
            if (autoCloseDelay <= 0f) return;
            if (autoCloseCoroutine != null) StopCoroutine(autoCloseCoroutine);
            autoCloseCoroutine = StartCoroutine(AutoCloseAfterDelay());
        }

        private IEnumerator AutoCloseAfterDelay()
        {
            yield return new WaitForSeconds(autoCloseDelay);
            SetPanelOpen(false);
        }

        private void HandleMessageSent(ChatMessage message)
        {
            AppendToLog(message);
            SpawnBubble(message);

            // Any new activity resets the "5 seconds of inactivity" auto-close clock while open.
            if (isOpen) StartAutoCloseTimer();
        }

        private void AppendToLog(ChatMessage message)
        {
            if (messageLogContent == null) return;

            GameObject entry = new GameObject("Message", typeof(RectTransform));
            entry.transform.SetParent(messageLogContent, false);

            LayoutElement layout = entry.AddComponent<LayoutElement>();
            layout.preferredHeight = 34f;
            layout.flexibleWidth = 1f;

            Text text = entry.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.text = message.Sender + ": " + message.Text;

            StartCoroutine(ScrollToBottomNextFrame());
        }

        private IEnumerator ScrollToBottomNextFrame()
        {
            yield return null; // wait a frame so the layout group finishes repositioning the new entry
            if (messageLogScroll != null) messageLogScroll.verticalNormalizedPosition = 0f;
        }

        private void SpawnBubble(ChatMessage message)
        {
            if (speechBubbleTemplate == null || bubbleParent == null || chatManager == null) return;

            Transform anchor = chatManager.GetBubbleAnchor(message.Sender);
            if (anchor == null) return;

            SpeechBubble bubble = Instantiate(speechBubbleTemplate, bubbleParent);
            bubble.Show(message.Text, anchor, worldCamera, bubbleDuration);
        }

        private void HandleUnreadCountChanged(int count)
        {
            if (unreadBadge != null) unreadBadge.SetActive(count > 0);
            if (unreadBadgeText != null) unreadBadgeText.text = count > 9 ? "9+" : count.ToString();
        }
    }
}
