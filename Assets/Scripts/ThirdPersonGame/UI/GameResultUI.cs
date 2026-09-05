using LudoGame.ThirdPersonGame;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LudoGame.ThirdPersonGame.UI
{
    /// <summary>
    /// Shows a "You Win!" or "You Lose!" panel when GameManager.OnGameEnded fires, with
    /// buttons to restart or return to the main menu.
    ///
    /// Setup (in the Unity Editor):
    /// 1. Build a result panel under your Canvas (background + text + buttons) and put
    ///    this script on it (or an empty object referencing it).
    /// 2. Drag your GameManager into "Game Manager".
    /// 3. Drag the panel into "Result Panel" - hidden automatically at the start.
    /// 4. (Optional) Drag a UI Text into "Result Text" for the "You Win!"/"You Lose!" message.
    /// 5. (Optional) Drag Restart/Main Menu buttons - Restart calls GameManager.RestartGame()
    ///    and hides this panel; Main Menu loads "Main Menu Scene Name".
    /// </summary>
    public class GameResultUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameManager gameManager;

        [Header("UI Elements (optional)")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Text resultText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;

        [Header("Messages")]
        [SerializeField] private string winMessage = "You Win!";
        [SerializeField] private string loseMessage = "You Lose!";

        [Header("Main Menu Scene (optional)")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private void Awake()
        {
            if (resultPanel != null) resultPanel.SetActive(false);
        }

        private void OnEnable()
        {
            if (gameManager != null) gameManager.OnGameEnded += HandleGameEnded;
            if (restartButton != null) restartButton.onClick.AddListener(Restart);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(GoToMainMenu);
        }

        private void OnDisable()
        {
            if (gameManager != null) gameManager.OnGameEnded -= HandleGameEnded;
            if (restartButton != null) restartButton.onClick.RemoveListener(Restart);
            if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(GoToMainMenu);
        }

        private void HandleGameEnded(GameResult result)
        {
            if (resultText != null)
                resultText.text = result == GameResult.Win ? winMessage : loseMessage;

            if (resultPanel != null) resultPanel.SetActive(true);
        }

        public void Restart()
        {
            if (resultPanel != null) resultPanel.SetActive(false);
            gameManager?.RestartGame();
        }

        public void GoToMainMenu()
        {
            if (string.IsNullOrEmpty(mainMenuSceneName)) return;
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
