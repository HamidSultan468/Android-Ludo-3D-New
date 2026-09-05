using LudoGame.Board;
using LudoGame.Game;
using UnityEngine;
using UnityEngine.UI;

namespace LudoGame.UI
{
    /// <summary>
    /// Shows on screen whose turn it currently is, by listening to
    /// GameManager.OnTurnStarted. Purely a display layer - GameManager
    /// still decides the actual turn order.
    ///
    /// Setup (in the Unity Editor):
    /// 1. Put this script on your turn-indicator UI element (a Canvas child).
    /// 2. Drag your GameManager into "Game Manager".
    /// 3. (Optional) Drag a UI Text into "Turn Text" to show a message like
    ///    "Red's Turn".
    /// 4. (Optional) Drag a UI Image into "Turn Color Swatch" to tint a small
    ///    dot/panel with the current player's color.
    /// 5. (Optional) If you have a highlight/glow GameObject around each
    ///    player's token tray, drag each one into its matching Red/Green/
    ///    Yellow/Blue Highlight field - only the current player's highlight
    ///    will be turned on.
    /// </summary>
    public class TurnIndicatorUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameManager gameManager;

        [Header("UI Elements (optional)")]
        [SerializeField] private Text turnText;
        [SerializeField] private Image turnColorSwatch;

        [Header("Player Colors")]
        [SerializeField] private Color redColor = Color.red;
        [SerializeField] private Color greenColor = Color.green;
        [SerializeField] private Color yellowColor = Color.yellow;
        [SerializeField] private Color blueColor = Color.blue;

        [Header("Per-Color Turn Highlights (optional)")]
        [Tooltip("An object (e.g. a glow border) that turns on only around the current player's area.")]
        [SerializeField] private GameObject redHighlight;
        [SerializeField] private GameObject greenHighlight;
        [SerializeField] private GameObject yellowHighlight;
        [SerializeField] private GameObject blueHighlight;

        private void OnEnable()
        {
            if (gameManager == null) return;

            gameManager.OnTurnStarted += HandleTurnStarted;
            HandleTurnStarted(gameManager.CurrentPlayerColor); // show the current turn right away
        }

        private void OnDisable()
        {
            if (gameManager == null) return;
            gameManager.OnTurnStarted -= HandleTurnStarted;
        }

        private void HandleTurnStarted(GridManager.PlayerColor color)
        {
            if (turnText != null)
                turnText.text = color + "'s Turn";

            if (turnColorSwatch != null)
                turnColorSwatch.color = GetColor(color);

            SetActiveHighlight(color);
        }

        private Color GetColor(GridManager.PlayerColor color)
        {
            switch (color)
            {
                case GridManager.PlayerColor.Red: return redColor;
                case GridManager.PlayerColor.Green: return greenColor;
                case GridManager.PlayerColor.Yellow: return yellowColor;
                case GridManager.PlayerColor.Blue: return blueColor;
                default: return Color.white;
            }
        }

        private void SetActiveHighlight(GridManager.PlayerColor color)
        {
            if (redHighlight != null) redHighlight.SetActive(color == GridManager.PlayerColor.Red);
            if (greenHighlight != null) greenHighlight.SetActive(color == GridManager.PlayerColor.Green);
            if (yellowHighlight != null) yellowHighlight.SetActive(color == GridManager.PlayerColor.Yellow);
            if (blueHighlight != null) blueHighlight.SetActive(color == GridManager.PlayerColor.Blue);
        }
    }
}
