using LudoGame.Board;

namespace LudoGame.Chat
{
    /// <summary>One chat message: who sent it and what it says.</summary>
    public readonly struct ChatMessage
    {
        public readonly GridManager.PlayerColor Sender;
        public readonly string Text;

        public ChatMessage(GridManager.PlayerColor sender, string text)
        {
            Sender = sender;
            Text = text;
        }
    }
}
