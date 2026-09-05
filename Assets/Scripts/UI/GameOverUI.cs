using LudoGame.Board;
using LudoGame.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LudoGame.UI
{
    /// <summary>
    /// Shows a "X Wins!" screen when GameManager.OnPlayerWon fires, and
    /// offers buttons to restart the match or go back to the main menu.
    ///
    /// Setup (in the Unity Editor):
    /// 1. Build a win-screen panel under your Canvas (background + text +
    ///    buttons), and put this script on it (or on an empty object that
    ///    references it).
    /// 2. Drag your GameManager into "Game Manager".
    /// 3. Drag the panel itself into "Game Over Panel" - it will be hidden
    ///    automatically at the start and only shown when someone wins.
    /// 4. (Optional) Drag a UI Text into "Winner Text" to show a message like
    ///    "Red Wins!".
    /// 5. (Optional) Drag a UI Button into "Restart Button" to reload the
    ///    match, and another into "Main Menu Button" (with "Main Menu Scene
    ///    Name" set to your menu scene's name) to leave the match.
    /// </summary>
    public class GameOverUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameManager gameManager;

        [Header("UI Elements (optional)")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private Text winnerText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;

        [Header("Player Colors (for winner text tint)")]
        [SerializeField] private Color redColor = Color.red;
        [SerializeField] private Color greenColor = Color.green;
        [SerializeField] private Color yellowColor = Color.yellow;
        [SerializeField] private Color blueColor = Color.blue;

        [Header("Main Menu Scene (optional)")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private void Awake()
        {
            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);
        }

        private void OnEnable()
        {
            if (gameManager != null)
                gameManager.OnPlayerWon += HandlePlayerWon;

            if (restartButton != null)
                restartButton.onClick.AddListener(RestartGame);

            if (mainMenuButton != null)
                mainMenuButton.onClick.AddListener(GoToMainMenu);
        }

        private void OnDisable()
        {
            if (gameManager != null)
                gameManager.OnPlayerWon -= HandlePlayerWon;

            if (restartButton != null)
                restartButton.onClick.RemoveListener(RestartGame);

            if (mainMenuButton != null)
                mainMenuButton.onClick.RemoveListener(GoToMainMenu);
        }

        private void HandlePlayerWon(GridManager.PlayerColor color)
        {
            if (winnerText != null)
            {
                winnerText.text = color + " Wins!";
                winnerText.color = GetColor(color);
            }

            if (gameOverPanel != null)
                gameOverPanel.SetActive(true);
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

        /// <summary>Reloads the current scene to start a fresh game. Hook this to the Restart button.</summary>
        public void RestartGame()
        {
            Scene current = SceneManager.GetActiveScene();
            SceneManager.LoadScene(current.buildIndex);
        }

        /// <summary>Loads the main menu scene, if one is set. Hook this to the Main Menu button.</summary>
        public void GoToMainMenu()
        {
            if (string.IsNullOrEmpty(mainMenuSceneName)) return;
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
