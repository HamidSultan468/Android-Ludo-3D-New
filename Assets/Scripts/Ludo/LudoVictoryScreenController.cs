using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LudoEmpire.Ludo
{
    /// <summary>
    /// Runtime companion dropped in by the scene builder: shows a full-screen Victory panel the instant
    /// a player gets all 4 tokens home (<see cref="LudoBoardLogic.OnPlayerFinished"/>), naming the winner
    /// and offering Replay/Main Menu buttons. The panel itself blocks further board interaction (it's a
    /// full-screen raycast-blocking overlay), so gameplay effectively stops there even though
    /// <see cref="LudoBoardLogic"/> keeps tracking placements for the remaining players underneath it.
    /// Compiled into player builds - safe to ignore or remove if you drive your own end-of-match UI.
    /// </summary>
    public class LudoVictoryScreenController : MonoBehaviour
    {
        // Mirrors LudoBoardSceneBuilder.PlayerColors' Neon Glow palette, same as LudoHudBinder.TurnTextColors.
        private static readonly Dictionary<PlayerColor, Color> WinnerTextColors = new Dictionary<PlayerColor, Color>
        {
            { PlayerColor.Red, new Color(1f, 0.35f, 0.85f) },
            { PlayerColor.Green, new Color(0.45f, 0.95f, 0.55f) },
            { PlayerColor.Yellow, new Color(1f, 0.9f, 0.4f) },
            { PlayerColor.Blue, new Color(0.4f, 0.85f, 1f) },
        };

        [SerializeField] private LudoBoardLogic board;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text winnerText;
        [Tooltip("Scene to load when Main Menu is clicked. Must be added to Build Settings.")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        public void Configure(LudoBoardLogic targetBoard, GameObject panel, Text winnerLabel, string targetMainMenuSceneName)
        {
            board = targetBoard;
            panelRoot = panel;
            winnerText = winnerLabel;
            if (!string.IsNullOrEmpty(targetMainMenuSceneName)) mainMenuSceneName = targetMainMenuSceneName;

            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (board != null) board.OnPlayerFinished += HandlePlayerFinished;
        }

        private void OnDisable()
        {
            if (board != null) board.OnPlayerFinished -= HandlePlayerFinished;
        }

        private void HandlePlayerFinished(PlayerColor winner)
        {
            if (panelRoot == null) return;
            if (panelRoot.activeSelf) return; // already showing for an earlier winner - leave it as-is

            if (winnerText != null)
            {
                winnerText.text = $"{winner} Wins!";
                winnerText.color = WinnerTextColors.TryGetValue(winner, out Color tint) ? tint : Color.white;
            }

            panelRoot.SetActive(true);
        }

        /// <summary>Wired to the panel's Replay button - reloads the current scene for a fresh match.</summary>
        public void OnReplayClicked()
        {
            UnityEngine.SceneManagement.Scene active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(active.buildIndex);
        }

        /// <summary>Wired to the panel's Main Menu button - loads <see cref="mainMenuSceneName"/> if it's
        /// configured and registered in Build Settings; logs a warning and does nothing otherwise, rather
        /// than letting a bad scene name throw mid-game.</summary>
        public void OnMainMenuClicked()
        {
            if (string.IsNullOrEmpty(mainMenuSceneName))
            {
                Debug.LogWarning("[LudoVictoryScreenController] No main menu scene name configured; ignoring Main Menu click.", this);
                return;
            }

            UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
