using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LudoEmpire.Ludo
{
    /// <summary>How aggressively/intelligently the bot evaluates its moves.</summary>
    public enum AIDifficulty
    {
        Easy,
        Medium,
        Hard
    }

    /// <summary>
    /// Computer-controlled opponent for single-player Ludo. Listens for its own turn on a
    /// <see cref="LudoBoardLogic"/>, rolls (optionally via a <see cref="LudoDiceRoller"/>), and picks
    /// which token to move using a difficulty-scaled heuristic (captures &gt; finishing &gt; safety &gt; progress).
    /// </summary>
    public class LudoAIBot : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField] private LudoBoardLogic board;
        [SerializeField] private PlayerColor aiColor = PlayerColor.Green;
        [SerializeField] private AIDifficulty difficulty = AIDifficulty.Medium;

        [Header("Optional")]
        [Tooltip("If assigned, the bot rolls this physically instead of picking a value directly.")]
        [SerializeField] private LudoDiceRoller diceRoller;
        [Tooltip("Small pause before the bot acts, purely for a believable 'thinking' pace.")]
        [SerializeField] private float minDecisionDelay = 0.4f;
        [SerializeField] private float maxDecisionDelay = 1.1f;

        [Header("Hard/Medium Heuristic Weights")]
        [SerializeField] private float captureWeight = 100f;
        [SerializeField] private float finishWeight = 90f;
        [SerializeField] private float exitBaseWeight = 40f;
        [SerializeField] private float blockingWeight = 20f;
        [SerializeField] private float dangerPenalty = 55f;
        [SerializeField] private float progressWeight = 1f;

        private bool _turnInProgress;

        public PlayerColor AIColor => aiColor;

        private void OnEnable()
        {
            if (board != null)
            {
                board.OnTurnChanged += HandleTurnChanged;
            }
            else
            {
                Debug.LogWarning($"[LudoAIBot] '{name}' has no LudoBoardLogic assigned; it will never act.", this);
            }
        }

        private void OnDisable()
        {
            if (board != null)
            {
                board.OnTurnChanged -= HandleTurnChanged;
            }
        }

        private void HandleTurnChanged(PlayerColor newPlayer)
        {
            if (newPlayer != aiColor || _turnInProgress)
            {
                return;
            }
            StartCoroutine(TakeTurnRoutine());
        }

        private IEnumerator TakeTurnRoutine()
        {
            _turnInProgress = true;
            yield return new WaitForSeconds(Random.Range(minDecisionDelay, maxDecisionDelay));

            if (board == null || board.IsGameOver || board.CurrentPlayer != aiColor)
            {
                _turnInProgress = false;
                yield break;
            }

            int diceValue;
            if (diceRoller != null)
            {
                bool rolled = false;
                int rolledValue = 1;
                void OnRolled(int v) { rolledValue = v; rolled = true; }
                diceRoller.OnDiceRollCompleted += OnRolled;
                diceRoller.Roll();
                yield return new WaitUntil(() => rolled);
                diceRoller.OnDiceRollCompleted -= OnRolled;
                diceValue = rolledValue;
            }
            else
            {
                diceValue = Random.Range(1, 7);
            }

            List<int> movable = ResolveMovableTokens(diceValue);

            while (movable != null && movable.Count > 0 && board.CurrentPlayer == aiColor && !board.IsGameOver)
            {
                yield return new WaitForSeconds(Random.Range(minDecisionDelay, maxDecisionDelay));

                int chosenTokenId = ChooseTokenToMove(movable, diceValue);
                if (chosenTokenId < 0)
                {
                    break;
                }

                if (!board.TryMoveToken(aiColor, chosenTokenId, diceValue, out MoveResult result) || !result.GrantsExtraTurn)
                {
                    break;
                }

                // Extra turn granted (rolled a 6, captured, or reached home) - roll again.
                yield return new WaitForSeconds(Random.Range(minDecisionDelay, maxDecisionDelay));

                if (diceRoller != null)
                {
                    bool rolled = false;
                    int rolledValue = 1;
                    void OnRolled(int v) { rolledValue = v; rolled = true; }
                    diceRoller.OnDiceRollCompleted += OnRolled;
                    diceRoller.Roll();
                    yield return new WaitUntil(() => rolled);
                    diceRoller.OnDiceRollCompleted -= OnRolled;
                    diceValue = rolledValue;
                }
                else
                {
                    diceValue = Random.Range(1, 7);
                }

                movable = ResolveMovableTokens(diceValue);
            }

            _turnInProgress = false;
        }

        /// <summary>
        /// Gets which tokens can move with this roll, without double-registering it. When
        /// <see cref="diceRoller"/> is assigned, it's the same shared physical dice
        /// <see cref="LudoBoardLogic"/> itself listens to - its own subscription already ran
        /// <see cref="LudoBoardLogic.RegisterDiceRoll"/> for this exact roll (consecutive-six tracking,
        /// auto-advancing the turn if nothing can move) before this coroutine even resumes, so calling
        /// it again here would double-count sixes and could stomp a turn that already auto-advanced.
        /// The pure <see cref="LudoBoardLogic.GetMovableTokens"/> query is used instead in that case.
        /// Only when no shared dice is wired (the plain-random fallback) does nothing else register the
        /// roll, so it's still done here.
        /// </summary>
        private List<int> ResolveMovableTokens(int diceValue)
        {
            return diceRoller != null
                ? board.GetMovableTokens(aiColor, diceValue)
                : board.RegisterDiceRoll(aiColor, diceValue);
        }

        /// <summary>
        /// Chooses which token id to move given the movable list and dice value. Pure decision
        /// logic (no coroutines/state), safe to call directly for testing.
        /// </summary>
        public int ChooseTokenToMove(List<int> movableTokenIds, int diceValue)
        {
            if (movableTokenIds == null || movableTokenIds.Count == 0)
            {
                return -1;
            }

            if (movableTokenIds.Count == 1)
            {
                return movableTokenIds[0];
            }

            if (difficulty == AIDifficulty.Easy)
            {
                return movableTokenIds[Random.Range(0, movableTokenIds.Count)];
            }

            int bestTokenId = movableTokenIds[0];
            float bestScore = float.NegativeInfinity;

            foreach (int tokenId in movableTokenIds)
            {
                float score = EvaluateMove(tokenId, diceValue);
                // Small jitter keeps Medium bots from being perfectly predictable.
                if (difficulty == AIDifficulty.Medium)
                {
                    score += Random.Range(-8f, 8f);
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTokenId = tokenId;
                }
            }

            return bestTokenId;
        }

        private float EvaluateMove(int tokenId, int diceValue)
        {
            if (board == null || !board.TrySimulateMove(aiColor, tokenId, diceValue, out MoveSimulation sim))
            {
                return float.NegativeInfinity;
            }

            float score = sim.ToPosition * progressWeight;

            if (sim.ExitsBase) score += exitBaseWeight;
            if (sim.ReachesHome) score += finishWeight;
            if (sim.CapturedCount > 0) score += captureWeight * sim.CapturedCount;

            if (sim.ToPosition <= LudoBoardLogic.CommonPathRelativeMax)
            {
                if (HasOwnTokenAt(sim.DestinationGlobalIndex, tokenId))
                {
                    score += blockingWeight;
                }

                if (!sim.DestinationIsSafe && difficulty == AIDifficulty.Hard && IsThreatenedByOpponent(sim.DestinationGlobalIndex))
                {
                    score -= dangerPenalty;
                }
            }

            return score;
        }

        private bool HasOwnTokenAt(int globalIndex, int movingTokenId)
        {
            if (board == null || globalIndex < 0) return false;

            foreach (var token in board.GetTokens(aiColor))
            {
                if (token.Id == movingTokenId || token.PathPosition < 0 || token.PathPosition > LudoBoardLogic.CommonPathRelativeMax)
                {
                    continue;
                }
                if (board.GetGlobalIndex(aiColor, token.PathPosition) == globalIndex)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Rough danger check: is any opponent token within one dice throw (1-6) of capturing this cell?</summary>
        private bool IsThreatenedByOpponent(int destinationGlobalIndex)
        {
            if (board == null || destinationGlobalIndex < 0)
            {
                return false;
            }

            foreach (var color in board.TurnOrder)
            {
                if (color == aiColor) continue;

                foreach (var token in board.GetTokens(color))
                {
                    if (token.PathPosition < 0 || token.PathPosition > LudoBoardLogic.CommonPathRelativeMax)
                    {
                        continue;
                    }

                    int opponentGlobal = board.GetGlobalIndex(color, token.PathPosition);
                    if (opponentGlobal < 0) continue;

                    int distance = (destinationGlobalIndex - opponentGlobal + LudoBoardLogic.CommonPathLength) % LudoBoardLogic.CommonPathLength;
                    if (distance >= 1 && distance <= 6)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
