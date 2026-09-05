using System;
using UnityEngine;

namespace LudoGame.ThirdPersonGame
{
    public enum GameResult { None, Win, Lose }

    /// <summary>
    /// Decides when the game ends: Lose when the player's health hits 0 (via
    /// PlayerHealth.OnDefeated), Win when the score reaches a target - or call Win()
    /// directly from a "goal reached" trigger-zone script for a location-based win
    /// instead of/alongside the score target.
    ///
    /// Setup (in the Unity Editor):
    /// 1. Put this on an empty "GameManager" GameObject.
    /// 2. Drag in your PlayerHealth and ScoreManager.
    /// 3. Set "Win Score Target" (0 disables the score-based win, if you only want a
    ///    location/objective-based win instead).
    /// 4. Hook a GameResultUI (see UI folder) to OnGameEnded to show a Win/Lose panel.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private ScoreManager scoreManager;

        [Header("Win Condition")]
        [Tooltip("Reaching this score ends the game with a Win. Set to 0 to disable and rely only on calling Win() manually (e.g. from a goal trigger).")]
        [SerializeField] private int winScoreTarget = 100;

        public GameResult Result { get; private set; } = GameResult.None;

        /// <summary>Raised once, the moment the game ends (Win or Lose).</summary>
        public event Action<GameResult> OnGameEnded;

        private void OnEnable()
        {
            if (playerHealth != null) playerHealth.OnDefeated += HandlePlayerDefeated;
            if (scoreManager != null) scoreManager.OnScoreChanged += HandleScoreChanged;
        }

        private void OnDisable()
        {
            if (playerHealth != null) playerHealth.OnDefeated -= HandlePlayerDefeated;
            if (scoreManager != null) scoreManager.OnScoreChanged -= HandleScoreChanged;
        }

        private void HandlePlayerDefeated() => EndGame(GameResult.Lose);

        private void HandleScoreChanged(int score)
        {
            if (winScoreTarget > 0 && score >= winScoreTarget)
                EndGame(GameResult.Win);
        }

        /// <summary>Call this from a goal/objective script (e.g. reaching a location) for a non-score win.</summary>
        public void Win() => EndGame(GameResult.Win);

        public void Lose() => EndGame(GameResult.Lose);

        private void EndGame(GameResult result)
        {
            if (Result != GameResult.None) return; // already ended - ignore any further calls
            Result = result;
            OnGameEnded?.Invoke(result);
        }

        /// <summary>Resets health/score and clears the result - hook to a Restart button for a fresh attempt.</summary>
        public void RestartGame()
        {
            Result = GameResult.None;
            scoreManager?.ResetScore();
            playerHealth?.ResetHealth();
        }
    }
}
