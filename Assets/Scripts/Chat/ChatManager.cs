using System;
using System.Collections.Generic;
using LudoGame.Board;
using LudoGame.Game;
using UnityEngine;

namespace LudoGame.Chat
{
    /// <summary>
    /// Central chat state: message history, the unread-badge counter, and the
    /// preset quick-messages/emojis players can send with one tap. This is
    /// pure data/logic - ChatUIController draws the panel/button and
    /// SpeechBubble draws the floating bubbles; neither of them stores state
    /// of their own.
    ///
    /// Setup (in the Unity Editor):
    /// 1. Put this on an empty "ChatManager" GameObject (the Chat UI Builder
    ///    tool does this for you).
    /// 2. Drag in your GameManager - used to know whose turn it is (so a
    ///    typed/quick message is attributed to the right color) and to find
    ///    a token to anchor speech bubbles above.
    /// 3. Edit "Quick Messages" in the Inspector to change the preset list.
    /// </summary>
    public class ChatManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameManager gameManager;

        [Header("Quick Messages")]
        [Tooltip("One-tap preset messages/emojis shown in the chat panel's quick-message grid.")]
        [SerializeField]
        private List<string> quickMessages = new List<string>
        {
            "Good Luck! 🍀",
            "Well Played! 👏",
            "Hurry Up! ⏳",
            "Ouch! 😖",
            "😂",
            "😮",
            "🎲",
            "🔥",
        };

        [Header("History")]
        [Tooltip("How many messages to keep - older ones drop off so the log doesn't grow forever.")]
        [SerializeField] private int maxHistory = 100;

        public IReadOnlyList<string> QuickMessages => quickMessages;
        public IReadOnlyList<ChatMessage> History => history;
        public int UnreadCount { get; private set; }

        private readonly List<ChatMessage> history = new List<ChatMessage>();

        /// <summary>Raised whenever a new message is sent, with the full message.</summary>
        public event Action<ChatMessage> OnMessageSent;

        /// <summary>Raised whenever UnreadCount changes (e.g. to refresh a badge).</summary>
        public event Action<int> OnUnreadCountChanged;

        /// <summary>Sends "text" attributed to whoever the current player is. Does nothing if text is blank.</summary>
        public void SendCurrentPlayerMessage(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            GridManager.PlayerColor sender = gameManager != null ? gameManager.CurrentPlayerColor : GridManager.PlayerColor.Red;
            Send(sender, text.Trim());
        }

        public void Send(GridManager.PlayerColor sender, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var message = new ChatMessage(sender, text);
            history.Add(message);
            if (history.Count > maxHistory) history.RemoveAt(0);

            UnreadCount++;
            OnUnreadCountChanged?.Invoke(UnreadCount);
            OnMessageSent?.Invoke(message);
        }

        /// <summary>Call this when the player opens the chat panel - clears the unread badge back to 0.</summary>
        public void MarkAllRead()
        {
            if (UnreadCount == 0) return;
            UnreadCount = 0;
            OnUnreadCountChanged?.Invoke(0);
        }

        /// <summary>A Transform to float a speech bubble above for the given color (a token on the board, if that color has one out).</summary>
        public Transform GetBubbleAnchor(GridManager.PlayerColor color)
        {
            return gameManager != null ? gameManager.GetAnchorTransform(color) : null;
        }
    }
}
