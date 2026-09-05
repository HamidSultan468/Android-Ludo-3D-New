using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LudoGame.ThirdPersonGame.UI
{
    /// <summary>
    /// The game's starting screen: a Play button that loads the gameplay scene, and a
    /// Quit button to close the app.
    ///
    /// Setup (in the Unity Editor):
    /// 1. Create a Main Menu scene (or reuse the Ludo game's one - see LudoGame.UI.MainMenuUI
    ///    for the same pattern already used there) and put this on a Canvas child.
    /// 2. Add both this menu scene and your gameplay scene to
    ///    File > Build Settings > Scenes In Build.
    /// 3. Set "Gameplay Scene Name" to the exact gameplay scene name.
    /// 4. Drag a UI Button into "Play Button" and another into "Quit Button".
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Scene To Load")]
        [SerializeField] private string gameplaySceneName = "ThirdPersonGame";

        [Header("UI Elements")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button quitButton;

        private void OnEnable()
        {
            if (playButton != null) playButton.onClick.AddListener(PlayGame);
            if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
        }

        private void OnDisable()
        {
            if (playButton != null) playButton.onClick.RemoveListener(PlayGame);
            if (quitButton != null) quitButton.onClick.RemoveListener(QuitGame);
        }

        public void PlayGame()
        {
            if (string.IsNullOrEmpty(gameplaySceneName)) return;
            SceneManager.LoadScene(gameplaySceneName);
        }

        /// <summary>Quits the application. Unity ignores this while testing inside the Editor.</summary>
        public void QuitGame()
        {
            Application.Quit();
        }
    }
}
