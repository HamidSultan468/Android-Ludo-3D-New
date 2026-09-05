using System.Linq;
using LudoGame.AI;
using LudoGame.Chat;
using LudoGame.Game;
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
    /// One-click setup for the in-game Chat System: a floating chat button
    /// (with an unread badge and a turn-based pulse), a glassmorphism-style
    /// slide-in chat panel (quick-message grid + scrolling log + text
    /// input), and a reusable speech-bubble template that floats above
    /// whichever player sent a message. Creates ChatManager and
    /// ChatUIController and wires everything together - no manual dragging.
    ///
    /// How to use: run "1. Scene Bootstrapper" and "4. Basic UI Canvas
    /// Builder" first (so GameManager and a Canvas exist), then
    /// Window > Ludo Tools > Chat UI Builder. Safe to re-run - it reuses the
    /// existing Canvas rather than creating a second one.
    /// </summary>
    public static class ChatUIBuilder
    {
        [MenuItem("Window/Ludo Tools/Chat UI Builder")]
        private static void Build()
        {
            Canvas canvas = GetOrCreateCanvas();
            EnsureEventSystem();

            GameManager gameManager = Object.FindAnyObjectByType<GameManager>();
            if (gameManager == null)
            {
                EditorUtility.DisplayDialog("Chat UI Builder",
                    "No GameManager found in the scene. Run '1. Scene Bootstrapper' first, then re-run this tool.",
                    "OK");
                return;
            }

            ChatManager chatManager = BuildChatManager(gameManager);
            SpeechBubble bubbleTemplate = BuildSpeechBubbleTemplate(canvas.transform);
            var (chatButton, badge, badgeText, pulseTarget) = BuildChatButton(canvas.transform);
            var panel = BuildChatPanel(canvas.transform, chatManager);

            ChatUIController controller = canvas.gameObject.AddComponent<ChatUIController>();
            SetField(controller, "chatManager", chatManager);
            SetField(controller, "worldCamera", Camera.main);
            SetField(controller, "chatButton", chatButton);
            SetField(controller, "unreadBadge", badge);
            SetField(controller, "unreadBadgeText", badgeText);
            SetField(controller, "chatButtonPulseTarget", pulseTarget);
            SetField(controller, "panelRoot", panel.root);
            SetField(controller, "panelCanvasGroup", panel.canvasGroup);
            SetField(controller, "panelRect", panel.rect);
            SetField(controller, "inputField", panel.inputField);
            SetField(controller, "sendButton", panel.sendButton);
            SetField(controller, "messageLogContent", panel.logContent);
            SetField(controller, "messageLogScroll", panel.logScroll);
            SetField(controller, "speechBubbleTemplate", bubbleTemplate);
            SetField(controller, "bubbleParent", canvas.transform);
            SetField(controller, "gameManager", gameManager);

            AIPlayer[] aiPlayers = Object.FindObjectsByType<AIPlayer>();
            SetObjectArray(controller, "aiPlayers", aiPlayers);

            // The chat/send buttons and the input field's submit event are wired at runtime by
            // ChatUIController.OnEnable() itself (like AIPlayer subscribes to GameManager events) -
            // only the quick-message buttons need Editor-time persistent wiring, since
            // ChatUIController has no reference to each individual button/preset pairing.
            WireQuickMessageButtons(panel.quickMessageButtons, panel.quickMessageTexts, controller);

            Scene activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);

            EditorUtility.DisplayDialog("Chat UI Builder",
                "Chat System created: floating chat button (badge + turn pulse), glass chat panel " +
                "(quick messages + scrolling log + text input), and speech bubbles that float above " +
                "whoever sends a message.\n\n" +
                "Note: Unity's built-in font may show emoji as blank boxes - swap in an emoji-capable " +
                "font asset later if you want them to render as pictures instead of text.",
                "OK");
        }

        // ---------------- ChatManager ----------------

        private static ChatManager BuildChatManager(GameManager gameManager)
        {
            ChatManager existing = Object.FindAnyObjectByType<ChatManager>();
            if (existing != null)
            {
                SetField(existing, "gameManager", gameManager);
                return existing;
            }

            GameObject go = new GameObject("ChatManager");
            Undo.RegisterCreatedObjectUndo(go, "Chat UI Builder");
            ChatManager chatManager = go.AddComponent<ChatManager>();
            SetField(chatManager, "gameManager", gameManager);
            return chatManager;
        }

        // ---------------- Speech bubble template ----------------

        private static SpeechBubble BuildSpeechBubbleTemplate(Transform canvasTransform)
        {
            Image bubble = CreateGlassPanel(canvasTransform, "SpeechBubbleTemplate",
                new Color(1f, 1f, 1f, 0.85f), new Color(1f, 0.85f, 0.3f, 0.9f));

            RectTransform rect = bubble.rectTransform;
            rect.sizeDelta = new Vector2(340f, 110f);
            rect.pivot = new Vector2(0.5f, 0f);

            Text text = CreateText(bubble.transform, "Text", "", 26, TextAnchor.MiddleCenter);
            text.color = Color.black;
            InsetFullRect(text.rectTransform, 16f, 8f);

            CanvasGroup canvasGroup = bubble.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            SpeechBubble speechBubble = bubble.gameObject.AddComponent<SpeechBubble>();
            SetField(speechBubble, "messageText", text);
            SetField(speechBubble, "rectTransform", rect);

            bubble.gameObject.SetActive(false); // this is a template cloned via Instantiate(), never shown directly
            return speechBubble;
        }

        // ---------------- Chat button ----------------

        private static (Button button, GameObject badge, Text badgeText, RectTransform pulseTarget) BuildChatButton(Transform canvasTransform)
        {
            GameObject go = new GameObject("ChatButton", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Chat UI Builder");
            go.transform.SetParent(canvasTransform, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-30f, 30f);
            rect.sizeDelta = new Vector2(150f, 150f);

            Image background = go.AddComponent<Image>();
            background.sprite = RoundedSprite;
            background.type = Image.Type.Sliced;
            background.color = new Color(0.15f, 0.4f, 0.9f, 0.9f);

            Outline glow = go.AddComponent<Outline>();
            glow.effectColor = new Color(0.4f, 0.75f, 1f, 0.9f);
            glow.effectDistance = new Vector2(3f, 3f);

            Button button = go.AddComponent<Button>();
            button.targetGraphic = background;

            Text icon = CreateText(go.transform, "Icon", "\U0001F4AC", 60, TextAnchor.MiddleCenter); // 💬
            InsetFullRect(icon.rectTransform, 0f, 0f);

            GameObject badge = CreatePanel(go.transform, "UnreadBadge", new Color(0.9f, 0.15f, 0.15f, 1f));
            RectTransform badgeRect = badge.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(1f, 1f);
            badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(-10f, -10f);
            badgeRect.sizeDelta = new Vector2(48f, 48f);
            badge.GetComponent<Image>().sprite = RoundedSprite;
            badge.GetComponent<Image>().type = Image.Type.Sliced;

            Text badgeText = CreateText(badge.transform, "BadgeText", "0", 24, TextAnchor.MiddleCenter);
            InsetFullRect(badgeText.rectTransform, 2f, 2f);
            badge.SetActive(false);

            return (button, badge, badgeText, rect);
        }

        // ---------------- Chat panel ----------------

        private struct PanelResult
        {
            public GameObject root;
            public CanvasGroup canvasGroup;
            public RectTransform rect;
            public InputField inputField;
            public Button sendButton;
            public RectTransform logContent;
            public ScrollRect logScroll;
            public Button[] quickMessageButtons;
            public string[] quickMessageTexts;
        }

        private static PanelResult BuildChatPanel(Transform canvasTransform, ChatManager chatManager)
        {
            Image panelImage = CreateGlassPanel(canvasTransform, "ChatPanel",
                new Color(0.05f, 0.08f, 0.15f, 0.55f), new Color(0.4f, 0.75f, 1f, 0.8f));
            GameObject panel = panelImage.gameObject;

            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(1000f, 1300f);
            rect.anchoredPosition = new Vector2(0f, 210f);

            CanvasGroup canvasGroup = panel.AddComponent<CanvasGroup>();

            Text title = CreateText(panel.transform, "Title", "Chat", 40, TextAnchor.MiddleCenter);
            PositionBand(title.rectTransform, 0.90f, 1f, 10f);

            var (quickButtons, quickTexts) = BuildQuickMessageGrid(panel.transform, chatManager);

            var (logScroll, logContent) = CreateVerticalScrollView(panel.transform, "MessageLog");
            PositionBand(logScroll.GetComponent<RectTransform>(), 0.16f, 0.60f, 12f);

            var (inputField, sendButton) = BuildInputRow(panel.transform);

            panel.SetActive(false);

            return new PanelResult
            {
                root = panel,
                canvasGroup = canvasGroup,
                rect = rect,
                inputField = inputField,
                sendButton = sendButton,
                logContent = logContent,
                logScroll = logScroll,
                quickMessageButtons = quickButtons,
                quickMessageTexts = quickTexts,
            };
        }

        private static (Button[] buttons, string[] texts) BuildQuickMessageGrid(Transform panelTransform, ChatManager chatManager)
        {
            GameObject gridGO = new GameObject("QuickMessageGrid", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(gridGO, "Chat UI Builder");
            gridGO.transform.SetParent(panelTransform, false);
            PositionBand(gridGO.GetComponent<RectTransform>(), 0.62f, 0.89f, 12f);

            GridLayoutGroup grid = gridGO.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(232f, 100f);
            grid.spacing = new Vector2(8f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            string[] presets = chatManager != null ? chatManager.QuickMessages.ToArray() : new string[0];
            var buttons = new Button[presets.Length];

            for (int i = 0; i < presets.Length; i++)
            {
                Button button = CreateButton(gridGO.transform, "Quick_" + i, presets[i]);
                button.GetComponent<Image>().color = new Color(0.2f, 0.3f, 0.5f, 0.85f);
                buttons[i] = button;
            }

            return (buttons, presets);
        }

        private static (InputField inputField, Button sendButton) BuildInputRow(Transform panelTransform)
        {
            GameObject rowGO = new GameObject("InputRow", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(rowGO, "Chat UI Builder");
            rowGO.transform.SetParent(panelTransform, false);
            PositionBand(rowGO.GetComponent<RectTransform>(), 0f, 0.14f, 10f);

            InputField inputField = CreateInputField(rowGO.transform, "MessageInput", "Type a message...");
            RectTransform inputRect = inputField.GetComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0f, 0f);
            inputRect.anchorMax = new Vector2(1f, 1f);
            inputRect.offsetMin = new Vector2(0f, 0f);
            inputRect.offsetMax = new Vector2(-180f, 0f); // leaves room for the Send button on the right

            Button sendButton = CreateButton(rowGO.transform, "SendButton", "Send");
            RectTransform sendRect = sendButton.GetComponent<RectTransform>();
            sendRect.anchorMin = new Vector2(1f, 0f);
            sendRect.anchorMax = new Vector2(1f, 1f);
            sendRect.pivot = new Vector2(1f, 0.5f);
            sendRect.anchoredPosition = Vector2.zero;
            sendRect.sizeDelta = new Vector2(160f, 0f);

            return (inputField, sendButton);
        }

        // ---------------- Persistent button wiring ----------------

        /// <summary>
        /// Wires each quick-message button's OnClick to ChatUIController.SendQuickMessage(text)
        /// as a PERSISTENT listener (baked into the saved scene, unlike a plain runtime
        /// AddListener() call made from an Editor script, which is lost the moment the scene
        /// is saved and reopened).
        /// </summary>
        private static void WireQuickMessageButtons(Button[] buttons, string[] presets, ChatUIController controller)
        {
            for (int i = 0; i < buttons.Length && i < presets.Length; i++)
                UnityEventTools.AddStringPersistentListener(buttons[i].onClick, controller.SendQuickMessage, presets[i]);
        }
    }
}
