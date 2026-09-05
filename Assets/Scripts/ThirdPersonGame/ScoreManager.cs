using System;
using UnityEngine;

namespace LudoGame.ThirdPersonGame
{
    /// <summary>
    /// Tracks the player's score. Pure data/logic - ScoreDisplayUI listens to
    /// OnScoreChanged to update the number on screen.
    ///
    /// Setup: put this on an empty "ScoreManager" GameObject. Call AddScore(amount)
    /// from wherever the player earns points (picking up an item, felling a tree, etc.).
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public int Score { get; private set; }

        /// <summary>Raised whenever the score changes, with the new total.</summary>
        public event Action<int> OnScoreChanged;

        public void AddScore(int amount)
        {
            if (amount == 0) return;
            Score = Mathf.Max(0, Score + amount);
            OnScoreChanged?.Invoke(Score);
        }

        /// <summary>Resets to 0 - call this to start a fresh attempt.</summary>
        public void ResetScore()
        {
            Score = 0;
            OnScoreChanged?.Invoke(Score);
        }
    }
}
