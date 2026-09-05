using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LudoEmpire.Ludo
{
    /// <summary>The four classic Ludo player colors, also used as the fixed turn order index.</summary>
    public enum PlayerColor
    {
        Red = 0,
        Green = 1,
        Yellow = 2,
        Blue = 3
    }

    /// <summary>
    /// Runtime state for a single goti (playing piece).
    /// PathPosition semantics: -1 = in base/yard, 0-50 = on the 52-cell shared path
    /// (51 relative steps starting at the player's own start square), 51-56 = private
    /// home stretch (6 cells), 57 = finished/home.
    /// </summary>
    [Serializable]
    public class LudoToken
    {
        public int Id;
        public PlayerColor Color;
        public int PathPosition = -1;

        /// <summary>Optional 3D representation moved around the board as the token progresses. May be null.</summary>
        public Transform Visual;

        /// <summary>Optional character controller found on <see cref="Visual"/> (or one of its children). When
        /// present, the board drives this directly - via its own waypoint-stepping <c>MoveSteps</c>/<c>WarpToPosition</c>
        /// API - instead of tweening <see cref="Visual"/>'s Transform, so real 3D characters walk/animate through
        /// their own Animator rather than sliding around like the plain placeholder tokens.</summary>
        public PlayerController Character;

        public bool IsInBase => PathPosition < 0;
        public bool IsFinished => PathPosition >= LudoBoardLogic.FinishPosition;
    }

    /// <summary>Six waypoints leading a single color's token from the shared path into the center.</summary>
    [Serializable]
    public class HomeStretchWaypoints
    {
        public Transform[] waypoints = new Transform[LudoBoardLogic.HomeStretchLength];
    }

    /// <summary>Parking spot markers inside a color's yard/base (one per token).</summary>
    [Serializable]
    public class YardWaypoints
    {
        public Transform[] waypoints = new Transform[LudoBoardLogic.TokensPerPlayer];
    }

    /// <summary>Outcome of a resolved <see cref="LudoBoardLogic.TryMoveToken"/> call.</summary>
    public class MoveResult
    {
        public bool Success;
        public bool ExitedBase;
        public bool ReachedHome;
        public bool GrantsExtraTurn;
        public readonly List<(PlayerColor color, int tokenId)> CapturedTokens = new List<(PlayerColor, int)>();
    }

    /// <summary>Non-mutating preview of what a move would do. Used by AI and move-validity UI.</summary>
    public struct MoveSimulation
    {
        public int FromPosition;
        public int ToPosition;
        public bool ExitsBase;
        public bool ReachesHome;
        public int CapturedCount;
        public int DestinationGlobalIndex;
        public bool DestinationIsSafe;
    }

    /// <summary>
    /// Core Ludo rules engine: the 52-tile shared path, per-color home stretches, safe zones,
    /// capturing, and 4-player turn management. Purely logical (works with or without any
    /// visual waypoints assigned) so it can be driven by UI, AI, or a network layer alike.
    /// </summary>
    public class LudoBoardLogic : MonoBehaviour
    {
        public const int TokensPerPlayer = 4;
        public const int CommonPathLength = 52;
        public const int HomeStretchLength = 6;
        // 51 shared-path relative positions per color (0 = own start square .. 50 = last cell before the private home stretch).
        public const int CommonPathRelativeMax = CommonPathLength - 2; // 50
        // Home stretch occupies relative positions 51-56 (6 cells); 57 is the finished/home position itself.
        public const int FinishPosition = CommonPathRelativeMax + HomeStretchLength + 1; // 57

        private static readonly Dictionary<PlayerColor, int> StartOffsets = new Dictionary<PlayerColor, int>
        {
            { PlayerColor.Red, 0 },
            { PlayerColor.Green, 13 },
            { PlayerColor.Yellow, 26 },
            { PlayerColor.Blue, 39 }
        };

        [Header("Players")]
        [SerializeField] private PlayerColor[] turnOrder = { PlayerColor.Red, PlayerColor.Green, PlayerColor.Yellow, PlayerColor.Blue };
        [Tooltip("Standard Ludo grants another roll for a 6 or a capture. Some rule sets also grant one for " +
                 "getting a token all the way home - leave OFF for strict traditional play.")]
        [SerializeField] private bool extraTurnOnReachingHome = false;
        [Tooltip("Pick a random player to roll first each new game (standard Ludo). The rotation order is " +
                 "unchanged - it still cycles Red -> Green -> Yellow -> Blue from whoever starts.")]
        [SerializeField] private bool randomizeStartingPlayer = false;

        [Header("Optional Visuals (assign for animated movement)")]
        [Tooltip("52 waypoints, in path order, index 0 = Red's start square.")]
        [SerializeField] private Transform[] commonPathWaypoints = new Transform[CommonPathLength];
        [Tooltip("Indexed by PlayerColor (Red, Green, Yellow, Blue).")]
        [SerializeField] private HomeStretchWaypoints[] homeStretches = new HomeStretchWaypoints[4];
        [Tooltip("Indexed by PlayerColor (Red, Green, Yellow, Blue).")]
        [SerializeField] private YardWaypoints[] yardSlots = new YardWaypoints[4];
        [SerializeField] private float tokenMoveSpeed = 6f;

        [Header("Optional Dice Integration")]
        [Tooltip("If assigned, this board auto-registers whichever value the die reports for the current player.")]
        [SerializeField] private LudoDiceRoller diceRoller;

        public event Action<PlayerColor> OnTurnChanged;
        public event Action<PlayerColor, int> OnExtraTurnGranted;
        public event Action<PlayerColor, int> OnTurnSkipped;
        public event Action<PlayerColor, int, List<int>> OnMovableTokensAvailable;
        public event Action<PlayerColor, int, int, int> OnTokenMoved; // color, tokenId, fromPos, toPos
        public event Action<PlayerColor, int, PlayerColor, int> OnTokenCaptured; // victimColor, victimId, byColor, byId
        public event Action<PlayerColor, int> OnTokenReachedHome;
        public event Action<PlayerColor> OnPlayerFinished;
        public event Action<List<PlayerColor>> OnGameOver;

        private Dictionary<PlayerColor, LudoToken[]> _tokensByColor;
        private int _currentPlayerIndex;
        private int _consecutiveSixes;
        private List<PlayerColor> _finishOrder = new List<PlayerColor>();
        private bool _gameOver;
        private readonly Dictionary<PlayerColor, Transform[]> _colorRunCache = new Dictionary<PlayerColor, Transform[]>();

        public PlayerColor CurrentPlayer => (turnOrder != null && turnOrder.Length > 0) ? turnOrder[_currentPlayerIndex] : PlayerColor.Red;
        public bool IsGameOver => _gameOver;
        public IReadOnlyList<PlayerColor> TurnOrder => turnOrder;
        public IReadOnlyList<PlayerColor> FinishOrder => _finishOrder;

        private void Awake()
        {
            InitializeGame(turnOrder);
        }

        private void OnEnable()
        {
            SubscribeDiceRoller();
        }

        private void OnDisable()
        {
            UnsubscribeDiceRoller();
        }

        /// <summary>Resets all tokens to base and starts a fresh game with the given (or default 4-color) turn order.</summary>
        public void InitializeGame(PlayerColor[] players = null)
        {
            turnOrder = (players != null && players.Length > 0) ? players : new[] { PlayerColor.Red, PlayerColor.Green, PlayerColor.Yellow, PlayerColor.Blue };

            _tokensByColor = new Dictionary<PlayerColor, LudoToken[]>();
            foreach (var color in turnOrder)
            {
                var tokens = new LudoToken[TokensPerPlayer];
                for (int i = 0; i < TokensPerPlayer; i++)
                {
                    tokens[i] = new LudoToken { Id = i, Color = color, PathPosition = -1 };
                }
                _tokensByColor[color] = tokens;
            }

            _currentPlayerIndex = (randomizeStartingPlayer && turnOrder.Length > 0)
                ? UnityEngine.Random.Range(0, turnOrder.Length)
                : 0;
            _consecutiveSixes = 0;
            _finishOrder = new List<PlayerColor>();
            _gameOver = false;

            OnTurnChanged?.Invoke(CurrentPlayer);
        }

        /// <summary>Wires (or rewires) an optional physical dice so its results auto-feed the current player's turn.</summary>
        public void SetDiceRoller(LudoDiceRoller roller)
        {
            if (diceRoller == roller) return;
            UnsubscribeDiceRoller();
            diceRoller = roller;
            SubscribeDiceRoller();
        }

        /// <summary>Assigns board waypoint transforms, typically called once by the scene builder tool.</summary>
        public void ConfigureWaypoints(Transform[] common, HomeStretchWaypoints[] home, YardWaypoints[] yards)
        {
            if (common != null) commonPathWaypoints = common;
            if (home != null) homeStretches = home;
            if (yards != null) yardSlots = yards;
            _colorRunCache.Clear(); // waypoints changed - any cached per-color run is now stale
        }

        /// <summary>
        /// Attaches a visual object to a logical token so future moves animate it. If <paramref name="visual"/>
        /// (or one of its children) carries a <see cref="PlayerController"/>, the board hands that character its
        /// full path of waypoints for this color and warps it to the token's current position (its yard slot, since
        /// every token starts in base) so subsequent moves drive the real character instead of tweening the raw Transform.
        /// </summary>
        public void SetTokenVisual(PlayerColor color, int tokenId, Transform visual)
        {
            LudoToken token = FindToken(color, tokenId);
            if (token == null)
            {
                Debug.LogWarning($"[LudoBoardLogic] SetTokenVisual: token {tokenId} not found for {color}.", this);
                return;
            }

            token.Visual = visual;
            token.Character = visual != null ? visual.GetComponentInChildren<PlayerController>() : null;

            if (token.Character != null)
            {
                token.Character.waypoints = GetColorRun(color);

                Vector3 startPosition = GetYardSlotPosition(color, tokenId) ?? visual.position;
                token.Character.WarpToPosition(startPosition, PathIndexFor(token.PathPosition));
            }
        }

        /// <summary>
        /// Builds (and caches) the full ordered run of waypoints for one color: shared-path cells 0-50 at
        /// their color-relative offset, followed by that color's 6 private home-stretch cells - 57 entries
        /// total, index-aligned 1:1 with <see cref="LudoToken.PathPosition"/> (0-56). Handed to a token's
        /// <see cref="PlayerController"/> so its own step-by-step movement walks the exact path this board computes.
        /// </summary>
        private Transform[] GetColorRun(PlayerColor color)
        {
            if (_colorRunCache.TryGetValue(color, out Transform[] cached))
            {
                return cached;
            }

            var run = new Transform[CommonPathRelativeMax + 1 + HomeStretchLength]; // 51 + 6 = 57

            for (int i = 0; i <= CommonPathRelativeMax; i++)
            {
                int g = GetGlobalIndex(color, i);
                run[i] = (commonPathWaypoints != null && g >= 0 && g < commonPathWaypoints.Length) ? commonPathWaypoints[g] : null;
            }

            int colorIndex = (int)color;
            if (homeStretches != null && colorIndex < homeStretches.Length && homeStretches[colorIndex]?.waypoints != null)
            {
                Transform[] stretch = homeStretches[colorIndex].waypoints;
                for (int i = 0; i < stretch.Length && (CommonPathRelativeMax + 1 + i) < run.Length; i++)
                {
                    run[CommonPathRelativeMax + 1 + i] = stretch[i];
                }
            }

            _colorRunCache[color] = run;
            return run;
        }

        /// <summary>Clamps a logical PathPosition to a valid index into a color's run array (see <see cref="GetColorRun"/>),
        /// which only goes up to the last home-stretch cell (56) - there is no separate waypoint for "finished" (57).</summary>
        private int PathIndexFor(int pathPosition)
        {
            return Mathf.Clamp(pathPosition, -1, CommonPathRelativeMax + HomeStretchLength);
        }

        public LudoToken[] GetTokens(PlayerColor color)
        {
            return _tokensByColor != null && _tokensByColor.TryGetValue(color, out var tokens) ? tokens : Array.Empty<LudoToken>();
        }

        /// <summary>
        /// Call once a dice value is known for the current player. Computes and returns which of their
        /// tokens can legally move; if none can move (or a 3rd consecutive six is rolled) the turn is
        /// auto-passed and an empty list is returned.
        /// </summary>
        public List<int> RegisterDiceRoll(PlayerColor color, int diceValue)
        {
            if (_gameOver)
            {
                Debug.LogWarning("[LudoBoardLogic] RegisterDiceRoll ignored: game is over.", this);
                return new List<int>();
            }

            if (color != CurrentPlayer)
            {
                Debug.LogWarning($"[LudoBoardLogic] {color} rolled out of turn (current player is {CurrentPlayer}).", this);
                return new List<int>();
            }

            diceValue = Mathf.Clamp(diceValue, 1, 6);

            if (diceValue == 6)
            {
                _consecutiveSixes++;
                if (_consecutiveSixes >= 3)
                {
                    _consecutiveSixes = 0;
                    OnTurnSkipped?.Invoke(color, diceValue);
                    AdvanceTurn();
                    return new List<int>();
                }
            }

            List<int> movable = GetMovableTokens(color, diceValue);
            OnMovableTokensAvailable?.Invoke(color, diceValue, movable);

            if (movable.Count == 0)
            {
                OnTurnSkipped?.Invoke(color, diceValue);
                AdvanceTurn();
            }

            return movable;
        }

        /// <summary>Returns the ids of this color's tokens that have at least one legal move for the given dice value.</summary>
        public List<int> GetMovableTokens(PlayerColor color, int diceValue)
        {
            var result = new List<int>();
            if (_tokensByColor == null || !_tokensByColor.TryGetValue(color, out var tokens) || tokens == null)
            {
                return result;
            }

            foreach (var token in tokens)
            {
                if (!token.IsFinished && CanMoveToken(token, diceValue))
                {
                    result.Add(token.Id);
                }
            }
            return result;
        }

        /// <summary>Attempts to move a token, resolving captures, safe zones, and extra-turn/turn-advance rules.</summary>
        public bool TryMoveToken(PlayerColor color, int tokenId, int diceValue, out MoveResult result)
        {
            result = new MoveResult();

            if (_gameOver)
            {
                Debug.LogWarning("[LudoBoardLogic] TryMoveToken ignored: game is over.", this);
                return false;
            }

            if (color != CurrentPlayer)
            {
                Debug.LogWarning($"[LudoBoardLogic] {color} attempted to move out of turn (current player is {CurrentPlayer}).", this);
                return false;
            }

            LudoToken token = FindToken(color, tokenId);
            if (token == null)
            {
                Debug.LogWarning($"[LudoBoardLogic] TryMoveToken: token {tokenId} not found for {color}.", this);
                return false;
            }

            diceValue = Mathf.Clamp(diceValue, 1, 6);
            if (!CanMoveToken(token, diceValue))
            {
                Debug.LogWarning($"[LudoBoardLogic] Illegal move: {color} token {tokenId} cannot move with dice {diceValue}.", this);
                return false;
            }

            int fromPos = token.PathPosition;
            int toPos = token.IsInBase ? 0 : fromPos + diceValue;

            token.PathPosition = toPos;
            result.Success = true;
            result.ExitedBase = fromPos < 0;
            result.ReachedHome = toPos == FinishPosition;

            if (toPos <= CommonPathRelativeMax)
            {
                int globalIndex = GetGlobalIndex(color, toPos);
                if (!IsSafeGlobalIndex(globalIndex))
                {
                    var occupants = GetTokensAtGlobalIndex(globalIndex, color);
                    foreach (var occupant in occupants)
                    {
                        occupant.token.PathPosition = -1;
                        result.CapturedTokens.Add((occupant.color, occupant.token.Id));
                        OnTokenCaptured?.Invoke(occupant.color, occupant.token.Id, color, tokenId);

                        // Warp first, *then* pulse the Death animation - WarpToPosition cancels any
                        // in-flight coroutine on the character, which would otherwise kill the pulse's
                        // own reset-after-delay before it ever ran.
                        AnimateTokenToBase(occupant.token);
                        occupant.token.Character?.PlayDeath();
                    }
                }
            }

            OnTokenMoved?.Invoke(color, tokenId, fromPos, toPos);
            AnimateTokenMovement(token, fromPos, toPos);

            if (result.CapturedTokens.Count > 0)
            {
                token.Character?.PlayAttack();
            }

            if (result.ReachedHome)
            {
                OnTokenReachedHome?.Invoke(color, tokenId);
                token.Character?.PlayVictory();

                if (_tokensByColor.TryGetValue(color, out var allTokens) && Array.TrueForAll(allTokens, t => t.IsFinished))
                {
                    if (!_finishOrder.Contains(color))
                    {
                        _finishOrder.Add(color);
                    }
                    OnPlayerFinished?.Invoke(color);
                    CheckForGameOver();
                }
            }

            result.GrantsExtraTurn = !_gameOver && (diceValue == 6 || result.CapturedTokens.Count > 0
                                                    || (extraTurnOnReachingHome && result.ReachedHome));

            if (!_gameOver)
            {
                if (result.GrantsExtraTurn)
                {
                    OnExtraTurnGranted?.Invoke(color, diceValue);
                }
                else
                {
                    AdvanceTurn();
                }
            }

            return true;
        }

        /// <summary>Non-mutating preview of a potential move; used by AI evaluation without touching real state.</summary>
        public bool TrySimulateMove(PlayerColor color, int tokenId, int diceValue, out MoveSimulation simulation)
        {
            simulation = default;

            LudoToken token = FindToken(color, tokenId);
            if (token == null || token.IsFinished)
            {
                return false;
            }

            diceValue = Mathf.Clamp(diceValue, 1, 6);
            if (!CanMoveToken(token, diceValue))
            {
                return false;
            }

            int fromPos = token.PathPosition;
            int toPos = token.IsInBase ? 0 : fromPos + diceValue;

            int capturedCount = 0;
            int destGlobal = -1;
            bool destSafe = true;

            if (toPos <= CommonPathRelativeMax)
            {
                destGlobal = GetGlobalIndex(color, toPos);
                destSafe = IsSafeGlobalIndex(destGlobal);
                if (!destSafe)
                {
                    capturedCount = GetTokensAtGlobalIndex(destGlobal, color).Count;
                }
            }

            simulation = new MoveSimulation
            {
                FromPosition = fromPos,
                ToPosition = toPos,
                ExitsBase = fromPos < 0,
                ReachesHome = toPos == FinishPosition,
                CapturedCount = capturedCount,
                DestinationGlobalIndex = destGlobal,
                DestinationIsSafe = destSafe
            };
            return true;
        }

        /// <summary>Global (0-51) index of a relative path position for a given color. Returns -1 if not on the shared path.</summary>
        public int GetGlobalIndex(PlayerColor color, int relativePosition)
        {
            if (relativePosition < 0 || relativePosition > CommonPathRelativeMax || !StartOffsets.TryGetValue(color, out int offset))
            {
                return -1;
            }
            return (offset + relativePosition) % CommonPathLength;
        }

        /// <summary>True if the given shared-path cell is a start square or star square (no captures happen there).</summary>
        public bool IsSafeGlobalIndex(int globalIndex)
        {
            if (globalIndex < 0) return false;
            foreach (int offset in StartOffsets.Values)
            {
                if (globalIndex == offset || globalIndex == (offset + 8) % CommonPathLength)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>World position for a relative path position, if the corresponding waypoint has been assigned.</summary>
        public Vector3? GetWorldPosition(PlayerColor color, int relativePosition)
        {
            if (relativePosition < 0)
            {
                return null;
            }

            if (relativePosition <= CommonPathRelativeMax)
            {
                int g = GetGlobalIndex(color, relativePosition);
                if (commonPathWaypoints != null && g >= 0 && g < commonPathWaypoints.Length && commonPathWaypoints[g] != null)
                {
                    return commonPathWaypoints[g].position;
                }
                return null;
            }

            int stretchIndex = relativePosition - (CommonPathRelativeMax + 1);
            int colorIndex = (int)color;
            if (homeStretches != null && colorIndex < homeStretches.Length && homeStretches[colorIndex]?.waypoints != null &&
                stretchIndex >= 0 && stretchIndex < homeStretches[colorIndex].waypoints.Length)
            {
                Transform wp = homeStretches[colorIndex].waypoints[stretchIndex];
                return wp != null ? wp.position : (Vector3?)null;
            }
            return null;
        }

        private Vector3? GetYardSlotPosition(PlayerColor color, int tokenId)
        {
            int colorIndex = (int)color;
            if (yardSlots == null || colorIndex >= yardSlots.Length || yardSlots[colorIndex]?.waypoints == null || yardSlots[colorIndex].waypoints.Length == 0)
            {
                return null;
            }
            int slot = Mathf.Clamp(tokenId, 0, yardSlots[colorIndex].waypoints.Length - 1);
            Transform t = yardSlots[colorIndex].waypoints[slot];
            return t != null ? t.position : (Vector3?)null;
        }

        private bool CanMoveToken(LudoToken token, int diceValue)
        {
            if (token == null || token.IsFinished)
            {
                return false;
            }

            if (token.IsInBase)
            {
                if (diceValue != 6) return false;
                int destGlobal = GetGlobalIndex(token.Color, 0);
                return !IsBlockedByOpponentBlockade(token.Color, destGlobal);
            }

            int newPos = token.PathPosition + diceValue;
            if (newPos > FinishPosition)
            {
                return false; // must land on Home with an exact count
            }

            if (newPos <= CommonPathRelativeMax)
            {
                int destGlobal = GetGlobalIndex(token.Color, newPos);
                return !IsBlockedByOpponentBlockade(token.Color, destGlobal);
            }

            return true; // private home stretch, always free to enter
        }

        private bool IsBlockedByOpponentBlockade(PlayerColor movingColor, int globalIndex)
        {
            return GetTokensAtGlobalIndex(globalIndex, movingColor).Count >= 2;
        }

        private List<(PlayerColor color, LudoToken token)> GetTokensAtGlobalIndex(int globalIndex, PlayerColor? excludeColor = null)
        {
            var found = new List<(PlayerColor, LudoToken)>();
            if (_tokensByColor == null)
            {
                return found;
            }

            foreach (var kvp in _tokensByColor)
            {
                if (excludeColor.HasValue && kvp.Key == excludeColor.Value) continue;
                foreach (var t in kvp.Value)
                {
                    if (t.PathPosition >= 0 && t.PathPosition <= CommonPathRelativeMax && GetGlobalIndex(kvp.Key, t.PathPosition) == globalIndex)
                    {
                        found.Add((kvp.Key, t));
                    }
                }
            }
            return found;
        }

        private LudoToken FindToken(PlayerColor color, int tokenId)
        {
            if (_tokensByColor == null || !_tokensByColor.TryGetValue(color, out var tokens) || tokens == null)
            {
                return null;
            }
            return Array.Find(tokens, t => t.Id == tokenId);
        }

        private void AdvanceTurn()
        {
            _consecutiveSixes = 0;
            if (turnOrder == null || turnOrder.Length == 0)
            {
                return;
            }

            int attempts = 0;
            do
            {
                _currentPlayerIndex = (_currentPlayerIndex + 1) % turnOrder.Length;
                attempts++;
            } while (_finishOrder.Contains(turnOrder[_currentPlayerIndex]) && attempts <= turnOrder.Length);

            OnTurnChanged?.Invoke(CurrentPlayer);
        }

        private void CheckForGameOver()
        {
            if (_gameOver || turnOrder == null || turnOrder.Length < 2)
            {
                return;
            }

            if (_finishOrder.Count >= turnOrder.Length - 1)
            {
                foreach (var p in turnOrder)
                {
                    if (!_finishOrder.Contains(p)) _finishOrder.Add(p);
                }
                _gameOver = true;
                OnGameOver?.Invoke(new List<PlayerColor>(_finishOrder));
            }
        }

        private void SubscribeDiceRoller()
        {
            if (diceRoller != null)
            {
                diceRoller.OnDiceRollCompleted += HandleDiceRollCompleted;
            }
        }

        private void UnsubscribeDiceRoller()
        {
            if (diceRoller != null)
            {
                diceRoller.OnDiceRollCompleted -= HandleDiceRollCompleted;
            }
        }

        private void HandleDiceRollCompleted(int value)
        {
            RegisterDiceRoll(CurrentPlayer, value);
        }

        private void AnimateTokenMovement(LudoToken token, int fromPos, int toPos)
        {
            if (token?.Visual == null || !isActiveAndEnabled)
            {
                return;
            }

            if (token.Character != null)
            {
                MoveCharacter(token, fromPos, toPos);
                return;
            }

            StartCoroutine(MoveVisualAlongPath(token, fromPos, toPos));
        }

        private void AnimateTokenToBase(LudoToken token)
        {
            if (token?.Visual == null || !isActiveAndEnabled)
            {
                return;
            }
            Vector3? basePos = GetYardSlotPosition(token.Color, token.Id);
            if (!basePos.HasValue)
            {
                return;
            }

            if (token.Character != null)
            {
                // A capture snaps the token straight back to its yard - matches classic Ludo's instant
                // "sent home" feel and keeps the character's internal path index perfectly in sync with -1.
                token.Character.WarpToPosition(basePos.Value, -1);
                return;
            }

            StartCoroutine(MoveVisualToPoint(token.Visual, basePos.Value));
        }

        /// <summary>Drives a token's real 3D character through <see cref="PlayerController.MoveSteps"/>, one
        /// waypoint-step per relative-position delta (exiting base is always a single hop onto position 0,
        /// regardless of the 6 that unlocked it). If the character is still finishing a previous hop, this
        /// move isn't dropped - it snaps straight to the new logical position/index instead, so the visual
        /// can never fall out of sync with <see cref="LudoToken.PathPosition"/> under back-to-back moves
        /// (e.g. extra turns from rolling a six, or a fast AI turn).</summary>
        private void MoveCharacter(LudoToken token, int fromPos, int toPos)
        {
            PlayerController character = token.Character;
            int steps = fromPos < 0 ? 1 : toPos - fromPos;

            if (character.IsMoving)
            {
                Vector3 snapPosition = GetWorldPosition(token.Color, toPos) ?? character.transform.position;
                character.WarpToPosition(snapPosition, PathIndexFor(toPos));
                return;
            }

            character.MoveSteps(steps);
        }

        private IEnumerator MoveVisualAlongPath(LudoToken token, int fromPos, int toPos)
        {
            int cursor = fromPos;

            if (fromPos < 0)
            {
                Vector3? entry = GetWorldPosition(token.Color, 0);
                if (entry.HasValue) yield return MoveVisualToPoint(token.Visual, entry.Value);
                cursor = 0;
            }

            int step = toPos >= cursor ? 1 : -1;
            while (cursor != toPos)
            {
                cursor += step;
                Vector3? point = GetWorldPosition(token.Color, cursor);
                if (point.HasValue)
                {
                    yield return MoveVisualToPoint(token.Visual, point.Value);
                }
            }
        }

        private IEnumerator MoveVisualToPoint(Transform visual, Vector3 target)
        {
            if (visual == null) yield break;

            while (visual != null && Vector3.Distance(visual.position, target) > 0.01f)
            {
                visual.position = Vector3.MoveTowards(visual.position, target, tokenMoveSpeed * Time.deltaTime);
                yield return null;
            }
            if (visual != null) visual.position = target;
        }
    }
}
