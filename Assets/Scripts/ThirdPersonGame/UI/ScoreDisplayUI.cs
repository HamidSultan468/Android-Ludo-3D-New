using LudoGame.ThirdPersonGame;
using UnityEngine;
using UnityEngine.UI;

namespace LudoGame.ThirdPersonGame.UI
{
    /// <summary>
    /// Shows the player's current score. Purely a display layer - the Gameplay Logic
    /// phase's score-tracking script calls SetScore(...) whenever it changes.
    ///
    /// Setup (in the Unity Editor):
    /// 1. Put a UI Text under your Canvas for the score number.
    /// 2. Put this script anywhere (e.g. on that same Text object) and drag the Text
    ///    into "Score Text".
    /// 3. (Optional) Drag your ScoreManager into "Score Manager" so this updates itself
    ///    automatically - leave empty to call SetScore(...) manually instead.
    /// </summary>
    public class ScoreDisplayUI : MonoBehaviour
    {
        [Header("Auto-Hookup (optional - leave empty to call SetScore() manually instead)")]
        [SerializeField] private ScoreManager scoreManager;

        [SerializeField] private Text scoreText;
        [Tooltip("Text shown before the number, e.g. \"Score: \".")]
        [SerializeField] private string prefix = "Score: ";

        private void OnEnable()
        {
            if (scoreManager == null) return;
            scoreManager.OnScoreChanged += SetScore;
            SetScore(scoreManager.Score); // show the current value right away
        }

        private void OnDisable()
        {
            if (scoreManager == null) return;
            scoreManager.OnScoreChanged -= SetScore;
        }

        /// <summary>Call this whenever the score changes (done automatically if "Score Manager" is assigned).</summary>
        public void SetScore(int score)
        {
            if (scoreText != null) scoreText.text = prefix + score;
        }
    }
}
