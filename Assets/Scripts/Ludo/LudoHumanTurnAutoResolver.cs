using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LudoEmpire.Ludo
{
    /// <summary>
    /// Drives a human-controlled colour's move once the dice has been rolled: the player taps one of
    /// their movable tokens on the board and it moves. Kept named "AutoResolver" for scene compatibility,
    /// but it is now a real tap-to-choose selector - it only falls back to auto-picking the first legal
    /// token if no tap arrives within <see cref="autoPickTimeoutSeconds"/> (so a turn can never hang if
    /// tap input isn't reaching the board, e.g. missing camera/collider).
    ///
    /// While waiting it gently pulses the scale of each tappable token so the player can see which of
    /// their pieces this roll can actually move.
    ///
    /// The roll itself stays on the Roll button (<see cref="LudoRollButtonController"/>). AI colours are
    /// played by <see cref="LudoAIBot"/> and are ignored here. In Pass &amp; Play,
    /// <see cref="LudoGameModeController"/> calls <see cref="SetAllHumanMode"/> so every colour becomes
    /// tap-controlled. Compiled into player builds.
    /// </summary>
    public class LudoHumanTurnAutoResolver : MonoBehaviour
    {
        [SerializeField] private LudoBoardLogic board;
        [SerializeField] private PlayerColor humanColor = PlayerColor.Red;

        [Header("Tap-to-choose")]
        [Tooltip("Camera used to raycast taps against the board tokens. Leave empty to use Camera.main " +
                 "(with an any-camera fallback).")]
        [SerializeField] private Camera raycastCamera;
        [Tooltip("Which layers the token colliders live on. Default = Everything.")]
        [SerializeField] private LayerMask tokenLayerMask = ~0;
        [Tooltip("If no token is tapped within this many seconds, the first legal token is moved " +
                 "automatically so the game never stalls. Set to 0 to always wait for a real tap.")]
        [SerializeField] private float autoPickTimeoutSeconds = 6f;
        [Tooltip("Pulse the scale of tappable tokens while waiting for the player to choose one.")]
        [SerializeField] private bool highlightMovableTokens = true;
        [Tooltip("Peak extra scale of the highlight pulse (0.14 = up to +14%).")]
        [SerializeField] private float highlightPulseAmount = 0.14f;
        [SerializeField] private float highlightPulseSpeed = 5f;

        private bool _allColorsHuman;
        private Coroutine _pending;
        private readonly List<Transform> _pulsed = new List<Transform>();
        private readonly List<Vector3> _pulsedScales = new List<Vector3>();

        public void Configure(LudoBoardLogic targetBoard, PlayerColor humanPlayerColor)
        {
            board = targetBoard;
            humanColor = humanPlayerColor;
        }

        /// <summary>Switches from "only <see cref="humanColor"/> is tap-controlled" to "every colour is
        /// tap-controlled" - used by <see cref="LudoGameModeController"/> for Pass &amp; Play.</summary>
        public void SetAllHumanMode(bool allHuman)
        {
            _allColorsHuman = allHuman;
        }

        private void OnEnable()
        {
            if (board != null)
            {
                board.OnMovableTokensAvailable += HandleMovableTokensAvailable;
                board.OnTurnChanged += HandleTurnChanged;
            }
        }

        private void OnDisable()
        {
            if (board != null)
            {
                board.OnMovableTokensAvailable -= HandleMovableTokensAvailable;
                board.OnTurnChanged -= HandleTurnChanged;
            }
            CancelPending();
        }

        private void HandleTurnChanged(PlayerColor _)
        {
            // A fresh turn (or an auto-skip) supersedes any half-finished token choice.
            CancelPending();
        }

        private void HandleMovableTokensAvailable(PlayerColor color, int diceValue, List<int> movable)
        {
            if (board == null || movable == null || movable.Count == 0) return;
            if (!(_allColorsHuman || color == humanColor)) return; // AI plays its own move independently

            var choices = new List<int>(movable);
            EnsureTapColliders(color, choices);

            CancelPending();
            _pending = StartCoroutine(AwaitTokenChoice(color, diceValue, choices));
        }

        private IEnumerator AwaitTokenChoice(PlayerColor color, int diceValue, List<int> movable)
        {
            // OnMovableTokensAvailable fires from inside LudoBoardLogic's own call stack; defer a frame
            // so TryMoveToken never re-enters it mid-call.
            yield return null;

            BeginHighlight(color, movable);

            float elapsed = 0f;
            while (true)
            {
                if (board == null || board.IsGameOver || board.CurrentPlayer != color)
                {
                    _pending = null;
                    RestoreHighlight();
                    yield break;
                }

                PulseHighlight();

                int tappedId = ReadTappedTokenId(color, movable);
                if (tappedId >= 0)
                {
                    _pending = null;
                    RestoreHighlight();
                    board.TryMoveToken(color, tappedId, diceValue, out _);
                    yield break;
                }

                elapsed += Time.deltaTime;
                if (autoPickTimeoutSeconds > 0f && elapsed >= autoPickTimeoutSeconds)
                {
                    _pending = null;
                    RestoreHighlight();
                    board.TryMoveToken(color, movable[0], diceValue, out _);
                    yield break;
                }

                yield return null;
            }
        }

        // ---- highlight pulse -------------------------------------------------

        private void BeginHighlight(PlayerColor color, List<int> movable)
        {
            RestoreHighlight();
            if (!highlightMovableTokens) return;

            LudoToken[] tokens = board.GetTokens(color);
            foreach (int id in movable)
            {
                foreach (var tk in tokens)
                {
                    if (tk.Id != id || tk.Visual == null) continue;
                    _pulsed.Add(tk.Visual);
                    _pulsedScales.Add(tk.Visual.localScale);
                    break;
                }
            }
        }

        private void PulseHighlight()
        {
            if (_pulsed.Count == 0) return;
            float k = 1f + highlightPulseAmount * Mathf.Abs(Mathf.Sin(Time.time * highlightPulseSpeed));
            for (int i = 0; i < _pulsed.Count; i++)
            {
                if (_pulsed[i] != null) _pulsed[i].localScale = _pulsedScales[i] * k;
            }
        }

        private void RestoreHighlight()
        {
            for (int i = 0; i < _pulsed.Count; i++)
            {
                if (_pulsed[i] != null) _pulsed[i].localScale = _pulsedScales[i];
            }
            _pulsed.Clear();
            _pulsedScales.Clear();
        }

        // ---- tap raycast --------------------------------------------------

        /// <summary>Returns the id of a movable token the player tapped this frame, or -1.</summary>
        private int ReadTappedTokenId(PlayerColor color, List<int> movable)
        {
            if (!TryGetTapPosition(out Vector2 screenPos)) return -1;

            Camera cam = ResolveCamera();
            if (cam == null) return -1;

            if (!Physics.Raycast(cam.ScreenPointToRay(screenPos), out RaycastHit hit, 500f, tokenLayerMask))
            {
                return -1;
            }

            Transform hitT = hit.collider.transform;
            LudoToken[] tokens = board.GetTokens(color);
            foreach (int id in movable)
            {
                foreach (var tk in tokens)
                {
                    if (tk.Id != id || tk.Visual == null) continue;
                    if (hitT == tk.Visual || hitT.IsChildOf(tk.Visual) || tk.Visual.IsChildOf(hitT))
                    {
                        return id;
                    }
                }
            }
            return -1;
        }

        private static bool TryGetTapPosition(out Vector2 pos)
        {
            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began)
                {
                    pos = t.position;
                    return true;
                }
            }
            if (Input.GetMouseButtonDown(0))
            {
                pos = Input.mousePosition;
                return true;
            }
            pos = default;
            return false;
        }

        private Camera ResolveCamera()
        {
            if (raycastCamera != null) return raycastCamera;
            raycastCamera = Camera.main;
            if (raycastCamera == null)
            {
                var cams = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                if (cams.Length > 0) raycastCamera = cams[0];
            }
            return raycastCamera;
        }

        /// <summary>Makes sure every movable token has a collider so a tap can hit it - freshly built
        /// scenes get one from the scene builder, but older scenes may not.</summary>
        private void EnsureTapColliders(PlayerColor color, List<int> movable)
        {
            LudoToken[] tokens = board.GetTokens(color);
            foreach (int id in movable)
            {
                foreach (var tk in tokens)
                {
                    if (tk.Id != id || tk.Visual == null) continue;
                    if (tk.Visual.GetComponentInChildren<Collider>() != null) break;

                    var box = tk.Visual.gameObject.AddComponent<BoxCollider>();
                    Renderer r = tk.Visual.GetComponentInChildren<Renderer>();
                    if (r != null)
                    {
                        box.center = tk.Visual.InverseTransformPoint(r.bounds.center);
                        Vector3 s = tk.Visual.InverseTransformVector(r.bounds.size);
                        box.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)) * 1.3f;
                    }
                    else
                    {
                        box.size = Vector3.one;
                    }
                    break;
                }
            }
        }

        private void CancelPending()
        {
            if (_pending != null)
            {
                StopCoroutine(_pending);
                _pending = null;
            }
            RestoreHighlight();
        }
    }
}
