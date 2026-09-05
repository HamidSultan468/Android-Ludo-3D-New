using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LudoGame.UI
{
    /// <summary>
    /// The game's starting screen: a Play button that loads the gameplay
    /// scene, and a Quit button to close the app.
    ///
    /// Setup (in the Unity Editor):
    /// 1. Create a new scene for your main menu (e.g. "MainMenu"), and put
    ///    this script on a Canvas child there.
    /// 2. Add BOTH your main menu scene and your gameplay scene
    ///    (e.g. "SampleScene") to File > Build Settings > Scenes In Build -
    ///    SceneManager.LoadScene only works for scenes listed there.
    /// 3. Set "Gameplay Scene Name" to the EXACT name of your gameplay scene.
    /// 4. Drag a UI Button into "Play Button" and another into "Quit Button".
    /// 5. Make your main menu scene the FIRST one in Build Settings, so the
    ///    game opens on it.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Scene To Load")]
        [Tooltip("Exact name of your gameplay scene, as it appears in Build Settings.")]
        [SerializeField] private string gameplaySceneName = "SampleScene";

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

        /// <summary>Loads the gameplay scene. Hook this to the Play button.</summary>
        public void PlayGame()
        {
            if (string.IsNullOrEmpty(gameplaySceneName)) return;
            SceneManager.LoadScene(gameplaySceneName);
        }

        /// <summary>Quits the application. Hook this to the Quit button. (Unity ignores this while testing inside the Editor.)</summary>
        public void QuitGame()
        {
            Application.Quit();
        }
    }
}
