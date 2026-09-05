using UnityEngine;

namespace LudoEmpire.Ludo
{
    /// <summary>
    /// Drives the Main Menu's two mode buttons - selecting either records the choice in
    /// <see cref="LudoGameModeSelection"/> and loads the board scene, where
    /// <see cref="LudoGameModeController"/> picks it up. Compiled into player builds -
    /// <see cref="LudoMainMenuSceneBuilder"/> wires this up automatically via <see cref="Configure"/>.
    /// </summary>
    public class LudoMainMenuController : MonoBehaviour
    {
        [Tooltip("Scene to load once a mode is chosen. Must be added to Build Settings.")]
        [SerializeField] private string boardSceneName = "SampleScene";

        public void Configure(string targetBoardSceneName)
        {
            if (!string.IsNullOrEmpty(targetBoardSceneName)) boardSceneName = targetBoardSceneName;
        }

        /// <summary>Wired to the "Player vs AI" button.</summary>
        public void PlayVsAI()
        {
            LudoGameModeSelection.Select(LudoGameMode.PlayerVsAI);
            LoadBoardScene();
        }

        /// <summary>Wired to the "Pass & Play" button.</summary>
        public void PassAndPlay()
        {
            LudoGameModeSelection.Select(LudoGameMode.PassAndPlay);
            LoadBoardScene();
        }

        private void LoadBoardScene()
        {
            if (string.IsNullOrEmpty(boardSceneName))
            {
                Debug.LogError("[LudoMainMenuController] No board scene name configured; cannot start the game.", this);
                return;
            }

            UnityEngine.SceneManagement.SceneManager.LoadScene(boardSceneName);
        }
    }
}
