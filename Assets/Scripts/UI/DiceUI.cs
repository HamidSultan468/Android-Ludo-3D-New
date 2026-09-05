using LudoGame.Board;
using LudoGame.Dice;
using LudoGame.Game;
using UnityEngine;
using UnityEngine.UI;

namespace LudoGame.UI
{
    /// <summary>
    /// Connects the on-screen Roll button and dice-value display to
    /// DiceManager (and, if assigned, GameManager). Purely a UI layer -
    /// all dice/turn rules stay inside DiceManager and GameManager.
    ///
    /// Setup (in the Unity Editor):
    /// 1. Put this script on your dice UI panel (a Canvas child).
    /// 2. Drag your DiceManager into "Dice Manager".
    /// 3. (Optional but recommended) Drag your GameManager into "Game Manager"
    ///    so the Roll button automatically disables itself until a token has
    ///    finished moving. Leave empty only for quick standalone testing.
    /// 4. Drag a UI Button into "Roll Button".
    /// 5. Show the result either (or both) ways:
    ///    - Drag a UI Text into "Value Text" to show the number (e.g. "4").
    ///    - Drag a UI Image into "Face Image" and fill "Dice Face Sprites"
    ///      with 6 sprites in order (index 0 = face for value 1, ... index 5 = value 6).
    /// </summary>
    public class DiceUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DiceManager diceManager;
        [SerializeField] private GameManager gameManager;

        [Header("UI Elements")]
        [SerializeField] private Button rollButton;
        [SerializeField] private Text valueText;
        [SerializeField] private Image faceImage;
        [Tooltip("6 sprites in order: index 0 = face for value 1, index 5 = face for value 6.")]
        [SerializeField] private Sprite[] diceFaceSprites = new Sprite[6];

        private void OnEnable()
        {
            if (diceManager != null)
            {
                diceManager.OnRollStarted += HandleRollStarted;
                diceManager.OnDiceRolled += HandleDiceRolled;
                diceManager.OnTurnCancelledBySixes += HandleTurnCancelledBySixes;
            }

            if (gameManager != null)
                gameManager.OnTurnStarted += HandleTurnStarted;

            if (rollButton != null)
                rollButton.onClick.AddListener(HandleRollButtonClicked);
        }

        private void OnDisable()
        {
            if (diceManager != null)
            {
                diceManager.OnRollStarted -= HandleRollStarted;
                diceManager.OnDiceRolled -= HandleDiceRolled;
                diceManager.OnTurnCancelledBySixes -= HandleTurnCancelledBySixes;
            }

            if (gameManager != null)
                gameManager.OnTurnStarted -= HandleTurnStarted;

            if (rollButton != null)
                rollButton.onClick.RemoveListener(HandleRollButtonClicked);
        }

        private void HandleRollButtonClicked()
        {
            if (gameManager != null)
                gameManager.RollDice();
            else if (diceManager != null)
                diceManager.Roll();
        }

        private void HandleRollStarted()
        {
            SetButtonInteractable(false);
        }

        private void HandleDiceRolled(int value)
        {
            ShowValue(value);

            // With no GameManager, re-enable immediately so the dice can be tested on its own.
            // With a GameManager, keep the button disabled - it re-enables via OnTurnStarted
            // once the player has finished moving a token.
            if (gameManager == null)
                SetButtonInteractable(true);
        }

        private void HandleTurnCancelledBySixes()
        {
            ShowValue(6);

            if (gameManager == null)
                SetButtonInteractable(true);
        }

        private void HandleTurnStarted(GridManager.PlayerColor color)
        {
            SetButtonInteractable(true);
        }

        private void ShowValue(int value)
        {
            if (valueText != null)
                valueText.text = value.ToString();

            if (faceImage != null && diceFaceSprites != null && value >= 1 && value <= diceFaceSprites.Length)
                faceImage.sprite = diceFaceSprites[value - 1];
        }

        private void SetButtonInteractable(bool interactable)
        {
            if (rollButton != null)
                rollButton.interactable = interactable;
        }
    }
}
