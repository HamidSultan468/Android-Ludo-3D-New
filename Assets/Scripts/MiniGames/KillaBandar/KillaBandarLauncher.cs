using UnityEngine;
using UnityEngine.SceneManagement;

namespace LudoGame.MiniGames.KillaBandar
{
    /// <summary>
    /// Handles the "Play Killa Bandar" launch workflow described in the
    /// design doc: transitions out of the main Ludo board into the Killa
    /// Bandar arena scene, and back again via "Return to Ludo".
    ///
    /// Setup (in the Unity Editor):
    /// 1. Put this on any object in your Main Menu and/or Pause Menu scene
    ///    (there's no Pause Menu script in this project yet - add this
    ///    wherever you build one).
    /// 2. Set "Killa Bandar Scene Name" and "Ludo Scene Name" to your exact
    ///    scene names (both must be added to Build Settings > Scenes In Build).
    /// 3. Hook a "Play Killa Bandar" button's OnClick to LaunchKillaBandar(),
    ///    and an "Exit/Return" button inside the Killa Bandar scene to
    ///    ReturnToLudo().
    /// </summary>
    public class KillaBandarLauncher : MonoBehaviour
    {
        [SerializeField] private string killaBandarSceneName = "KillaBandarScene";
        [SerializeField] private string ludoSceneName = "SampleScene";

        /// <summary>Loads the Killa Bandar arena. Hook this to the "Play Killa Bandar" button.</summary>
        public void LaunchKillaBandar()
        {
            if (string.IsNullOrEmpty(killaBandarSceneName)) return;
            SceneManager.LoadScene(killaBandarSceneName);
        }

        /// <summary>Returns to the main Ludo board. Hook this to the in-arena "Exit/Return" button.</summary>
        public void ReturnToLudo()
        {
            if (string.IsNullOrEmpty(ludoSceneName)) return;
            SceneManager.LoadScene(ludoSceneName);
        }
    }
}
