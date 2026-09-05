using System;
using System.Collections;
using System.Collections.Generic;
using LudoGame.Board;
using LudoGame.Dice;
using LudoGame.Player;
using UnityEngine;

namespace LudoGame.Game
{
    /// <summary>
    /// Ties GridManager, PlayerToken and DiceManager together into one turn-based
    /// game: whose turn it is, which tokens are allowed to move, capturing
    /// opponent tokens, and detecting when a player has won.
    ///
    /// Setup (in the Unity Editor):
    /// 1. Put this script on an empty GameObject named "GameManager".
    /// 2. Drag your DiceManager object into the "Dice Manager" field.
    /// 3. Under "Players", add one entry per color (Red, Green, Yellow, Blue),
    ///    set its Color, and drag that color's 4 token GameObjects (the ones
    ///    with PlayerToken on them) into its Tokens list.
    /// 4. Hook a UI "Roll" button's OnClick to RollDice().
    /// 5. When a player has more than one token able to move, this script waits
    ///    for you to call TryMoveToken(token) (e.g. when the player taps a
    ///    token on screen). If only one token can move, it moves automatically.
    /// 6. "Auto Select Delay" is a safety net: if nobody taps a token within
    ///    that many seconds (e.g. tap input isn't wired up yet), the first
    ///    legal token is picked automatically so the game never gets stuck.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Serializable]
        public class PlayerData
        {
            public GridManager.PlayerColor color;
            public List<PlayerToken> tokens = new List<PlayerToken>();
        }

        [Header("References")]
        [SerializeField] private DiceManager diceManager;

        [Header("Players (one entry per color, 4 tokens each)")]
        [SerializeField] private List<PlayerData> players = new List<PlayerData>();

        [Header("Fallback (in case tap/click input isn't working)")]
        [Tooltip("If more than one token can move and nobody taps one within this many seconds, " +
                 "the first legal token is picked automatically so the game never gets stuck. " +
                 "Set to 0 to disable and wait forever for a real tap.")]
        [SerializeField] private float autoSelectDelay = 5f;

        /// <summary>Index into the Players list of whoever's turn it currently is.</summary>
        public int CurrentPlayerIndex { get; private set; }

        public GridManager.PlayerColor CurrentPlayerColor => players[CurrentPlayerIndex].color;

        /// <summary>Raised whenever a new turn begins (including when the same player goes again after a 6).</summary>
        public event Action<GridManager.PlayerColor> OnTurnStarted;

        /// <summary>Raised right after a dice roll resolves, before any token has moved.</summary>
        public event Action<GridManager.PlayerColor, int> OnDiceResult;

        /// <summary>Raised when a player's 4th token reaches Finished.</summary>
        public event Action<GridManager.PlayerColor> OnPlayerWon;

        /// <summary>Raised when a moving token lands on and captures an opponent token.</summary>
        public event Action<PlayerToken, PlayerToken> OnTokenCaptured;

        private int pendingDiceValue = -1;
        private bool waitingForTokenChoice;
        private Coroutine autoSelectCoroutine;

        private void OnEnable()
        {
            if (diceManager == null) return;
            diceManager.OnDiceRolled += HandleDiceRolled;
            diceManager.OnTurnCancelledBySixes += HandleTurnCancelledBySixes;
        }

        private void OnDisable()
        {
            if (diceManager == null) return;
            diceManager.OnDiceRolled -= HandleDiceRolled;
            diceManager.OnTurnCancelledBySixes -= HandleTurnCancelledBySixes;
        }

        private void Start()
        {
            foreach (var player in players)
                foreach (var token in player.tokens)
                    token.OnMoveFinished += HandleTokenMoveFinished;

            OnTurnStarted?.Invoke(CurrentPlayerColor);
        }

        /// <summary>Call this from a UI "Roll" button. Ignored while a token move is still pending.</summary>
        public void RollDice()
        {
            if (waitingForTokenChoice) return;
            diceManager.Roll();
        }

        /// <summary>Tokens belonging to the current player that are legally allowed to use the given dice value.</summary>
        public List<PlayerToken> GetMovableTokens(int diceValue)
        {
            var result = new List<PlayerToken>();
            foreach (var token in players[CurrentPlayerIndex].tokens)
                if (token.CanMove(diceValue))
                    result.Add(token);
            return result;
        }

        /// <summary>Call this when the player picks a token to move (e.g. taps it on screen).</summary>
        public void TryMoveToken(PlayerToken token)
        {
            if (!waitingForTokenChoice) return;
            if (token.Color != CurrentPlayerColor) return;
            if (!token.CanMove(pendingDiceValue)) return;

            CancelAutoSelect();
            waitingForTokenChoice = false;
            token.MoveByDice(pendingDiceValue);
        }

        private void HandleDiceRolled(int value)
        {
            pendingDiceValue = value;

            var movable = GetMovableTokens(value);
            if (movable.Count == 0)
            {
                Debug.Log("GameManager: " + CurrentPlayerColor + " rolled " + value + " but has no legal move - ending turn (goAgain=" + (value == 6) + ").", this);
                EndTurn(goAgain: value == 6);
                return;
            }

            waitingForTokenChoice = true;
            OnDiceResult?.Invoke(CurrentPlayerColor, value);

            if (movable.Count == 1)
            {
                TryMoveToken(movable[0]);
            }
            else
            {
                // This is expected, not a bug: with more than one legal move, we wait for
                // TryMoveToken() to be called by the UI/TokenSelector when the player taps one.
                Debug.Log("GameManager: " + CurrentPlayerColor + " rolled " + value + " - " + movable.Count +
                    " tokens can move. Waiting for the player to tap one (this is normal, not a freeze).", this);

                CancelAutoSelect();
                if (autoSelectDelay > 0f)
                    autoSelectCoroutine = StartCoroutine(AutoSelectAfterDelay(movable));
            }
        }

        /// <summary>
        /// Safety net for when tap/click input isn't reaching TokenSelector (e.g. missing
        /// Collider, no camera assigned). Without this, the game would wait forever for a
        /// tap that never arrives, which looks exactly like a freeze.
        /// </summary>
        private IEnumerator AutoSelectAfterDelay(List<PlayerToken> movable)
        {
            yield return new WaitForSeconds(autoSelectDelay);

            if (!waitingForTokenChoice) yield break; // the player already picked one manually

            PlayerToken chosen = ChooseFallbackToken(movable);
            Debug.LogWarning("GameManager: no token tap was received within " + autoSelectDelay +
                "s, auto-selecting '" + chosen.name + "' so the game doesn't get stuck. " +
                "If this keeps happening, check TokenSelector's Raycast Camera and make sure your " +
                "tokens have a Collider (see TokenSelector.cs setup notes).", this);

            TryMoveToken(chosen);
        }

        /// <summary>Prefers tokens already on the board/home stretch over ones still waiting in the yard.</summary>
        private PlayerToken ChooseFallbackToken(List<PlayerToken> movable)
        {
            PlayerToken best = movable[0];
            int bestPriority = GetTokenPriority(best.State);

            for (int i = 1; i < movable.Count; i++)
            {
                int priority = GetTokenPriority(movable[i].State);
                if (priority > bestPriority)
                {
                    bestPriority = priority;
                    best = movable[i];
                }
            }

            return best;
        }

        private int GetTokenPriority(PlayerToken.TokenState state)
        {
            switch (state)
            {
                case PlayerToken.TokenState.InHomeStretch: return 3;
                case PlayerToken.TokenState.OnBoard: return 2;
                case PlayerToken.TokenState.InYard: return 1;
                default: return 0;
            }
        }

        private void CancelAutoSelect()
        {
            if (autoSelectCoroutine == null) return;
            StopCoroutine(autoSelectCoroutine);
            autoSelectCoroutine = null;
        }

        private void HandleTurnCancelledBySixes()
        {
            EndTurn(goAgain: false);
        }

        private void HandleTokenMoveFinished(PlayerToken mover)
        {
            bool captured = CheckCapture(mover);

            if (HasPlayerWon(mover.Color))
            {
                OnPlayerWon?.Invoke(mover.Color);
                return;
            }

            if (captured)
                Debug.Log("GameManager: " + CurrentPlayerColor + " captured a token - bonus roll granted.", this);

            // A roll of 6 or a capture both earn another roll (classic Ludo rule).
            EndTurn(goAgain: pendingDiceValue == 6 || captured);
        }

        /// <summary>Sends any opponent token on the mover's landing cell back to its yard. Returns true if a capture happened.</summary>
        private bool CheckCapture(PlayerToken mover)
        {
            int moverIndex = mover.CurrentMainPathIndex();
            if (moverIndex < 0) return false; // token is in its home stretch/finished, no captures there
            if (GridManager.Instance.IsSafeCell(moverIndex)) return false; // Safe Square protection

            bool captured = false;

            foreach (var player in players)
            {
                if (player.color == mover.Color) continue;

                foreach (var token in player.tokens)
                {
                    if (token.CurrentMainPathIndex() == moverIndex)
                    {
                        token.SendBackToYard();
                        OnTokenCaptured?.Invoke(mover, token);
                        captured = true;
                    }
                }
            }

            return captured;
        }

        /// <summary>
        /// A representative token Transform for the given color, for UI purposes such as
        /// anchoring a chat speech bubble above "that player". Prefers a token currently
        /// visible on the board/home stretch; falls back to a yard token so there's always
        /// something to point at even before that color has moved anything out.
        /// </summary>
        public Transform GetAnchorTransform(GridManager.PlayerColor color)
        {
            PlayerData player = players.Find(p => p.color == color);
            if (player == null || player.tokens.Count == 0) return null;

            foreach (var token in player.tokens)
                if (token.State == PlayerToken.TokenState.OnBoard || token.State == PlayerToken.TokenState.InHomeStretch)
                    return token.transform;

            return player.tokens[0].transform;
        }

        private bool HasPlayerWon(GridManager.PlayerColor color)
        {
            PlayerData player = players.Find(p => p.color == color);
            if (player == null) return false;

            foreach (var token in player.tokens)
                if (token.State != PlayerToken.TokenState.Finished)
                    return false;

            return true;
        }

        private void EndTurn(bool goAgain)
        {
            CancelAutoSelect();
            waitingForTokenChoice = false;
            pendingDiceValue = -1;

            if (!goAgain)
                CurrentPlayerIndex = (CurrentPlayerIndex + 1) % players.Count;

            OnTurnStarted?.Invoke(CurrentPlayerColor);
        }
    }
}
