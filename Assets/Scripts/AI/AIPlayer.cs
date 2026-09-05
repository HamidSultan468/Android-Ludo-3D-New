using System.Collections;
using System.Collections.Generic;
using LudoGame.Board;
using LudoGame.Game;
using LudoGame.Player;
using UnityEngine;

namespace LudoGame.AI
{
    /// <summary>
    /// A computer-controlled player. On its own turn it automatically rolls
    /// the dice through GameManager, then picks one of its legally movable
    /// tokens and moves it - so a human never has to play this color.
    ///
    /// Setup (in the Unity Editor):
    /// 1. Put this script on an empty GameObject (e.g. "AI_Yellow").
    /// 2. Set "Color" to the color this AI should control (e.g. Yellow).
    ///    IMPORTANT: do NOT also let a human control this same color through
    ///    TokenSelector/UI - pick one or the other per color.
    /// 3. Drag your GameManager into "Game Manager".
    /// 4. The small delays just make the AI feel less instant/robotic; tweak
    ///    them to taste.
    /// </summary>
    public class AIPlayer : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("Which color this AI controls. Must match one of GameManager's Players entries.")]
        [SerializeField] private GridManager.PlayerColor color;

        [Header("References")]
        [SerializeField] private GameManager gameManager;

        /// <summary>Which color this AI controls - e.g. for other systems (like the chat button's turn pulse) to check "is this color AI-controlled?".</summary>
        public GridManager.PlayerColor Color => color;

        [Header("Thinking Delays (seconds)")]
        [Tooltip("How long the AI waits before rolling the dice on its turn.")]
        [SerializeField] private float delayBeforeRoll = 0.6f;
        [Tooltip("How long the AI waits before picking a token to move.")]
        [SerializeField] private float delayBeforeMove = 0.5f;

        private void OnEnable()
        {
            if (gameManager == null) return;
            gameManager.OnTurnStarted += HandleTurnStarted;
            gameManager.OnDiceResult += HandleDiceResult;
        }

        private void OnDisable()
        {
            if (gameManager == null) return;
            gameManager.OnTurnStarted -= HandleTurnStarted;
            gameManager.OnDiceResult -= HandleDiceResult;
        }

        private void HandleTurnStarted(GridManager.PlayerColor turnColor)
        {
            if (turnColor != color) return;
            StartCoroutine(RollAfterDelay());
        }

        private void HandleDiceResult(GridManager.PlayerColor turnColor, int diceValue)
        {
            if (turnColor != color) return;
            StartCoroutine(MoveAfterDelay(diceValue));
        }

        private IEnumerator RollAfterDelay()
        {
            yield return new WaitForSeconds(delayBeforeRoll);
            gameManager.RollDice();
        }

        private IEnumerator MoveAfterDelay(int diceValue)
        {
            yield return new WaitForSeconds(delayBeforeMove);

            // If only one token could move, GameManager already moved it automatically
            // the instant the dice result fired - this call is then simply ignored.
            List<PlayerToken> movable = gameManager.GetMovableTokens(diceValue);
            if (movable.Count == 0) yield break;

            PlayerToken chosen = ChooseTokenToMove(movable);
            gameManager.TryMoveToken(chosen);
        }

        /// <summary>
        /// Simple strategy: push tokens that are further along (home stretch,
        /// then on the shared path) before bringing a new token out of the yard.
        /// </summary>
        private PlayerToken ChooseTokenToMove(List<PlayerToken> movable)
        {
            PlayerToken best = movable[0];
            int bestPriority = GetPriority(best.State);

            for (int i = 1; i < movable.Count; i++)
            {
                int priority = GetPriority(movable[i].State);
                if (priority > bestPriority)
                {
                    bestPriority = priority;
                    best = movable[i];
                }
            }

            return best;
        }

        private int GetPriority(PlayerToken.TokenState state)
        {
            switch (state)
            {
                case PlayerToken.TokenState.InHomeStretch: return 3;
                case PlayerToken.TokenState.OnBoard: return 2;
                case PlayerToken.TokenState.InYard: return 1;
                default: return 0;
            }
        }
    }
}
