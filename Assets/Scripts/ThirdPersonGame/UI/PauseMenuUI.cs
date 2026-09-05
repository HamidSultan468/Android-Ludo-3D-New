using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LudoGame.ThirdPersonGame.UI
{
    /// <summary>
    /// Pauses the game (freezes movement/physics via Time.timeScale) and shows a panel
    /// with Resume/Main Menu/Quit buttons. Toggle with the Escape key or a UI Pause
    /// button.
    ///
    /// Setup (in the Unity Editor):
    /// 1. Build a Pause panel under your Canvas (it will be hidden automatically at the
    ///    start) and put this script on it (or an empty object referencing it).
    /// 2. Drag the panel itself into "Pause Panel".
    /// 3. Drag your Resume/Main Menu/Quit buttons into their fields (all optional).
    /// 4. Set "Main Menu Scene Name" to your menu scene's exact name.
    /// 5. (Optional) Drag an on-screen "Pause" button into "Open Pause Button" too, for
    ///    touch devices with no Escape key.
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button openPauseButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button quitButton;

        [Header("Main Menu Scene")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        public bool IsPaused { get; private set; }

        private void Awake()
        {
            if (pausePanel != null) pausePanel.SetActive(false);
        }

        private void OnEnable()
        {
            if (openPauseButton != null) openPauseButton.onClick.AddListener(Pause);
            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(GoToMainMenu);
            if (quitButton != null) quitButton.onClick.AddListener(Application.Quit);
        }

        private void OnDisable()
        {
            if (openPauseButton != null) openPauseButton.onClick.RemoveListener(Pause);
            if (resumeButton != null) resumeButton.onClick.RemoveListener(Resume);
            if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(GoToMainMenu);
            if (quitButton != null) quitButton.onClick.RemoveListener(Application.Quit);

            // Safety net: if this UI is disabled/destroyed (e.g. scene change) while paused,
            // make sure the game doesn't stay frozen forever on the next scene.
            if (IsPaused) Time.timeScale = 1f;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                TogglePause();
        }

        public void TogglePause()
        {
            if (IsPaused) Resume();
            else Pause();
        }

        public void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;
            Time.timeScale = 0f;
            if (pausePanel != null) pausePanel.SetActive(true);
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = 1f;
            if (pausePanel != null) pausePanel.SetActive(false);
        }

        public void GoToMainMenu()
        {
            Time.timeScale = 1f; // always restore normal speed before leaving, or the next scene loads frozen
            if (string.IsNullOrEmpty(mainMenuSceneName)) return;
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
