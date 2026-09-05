using System.Collections.Generic;
using UnityEngine;

namespace LudoEmpire.Ludo
{
    /// <summary>
    /// Frames the 3D Ludo board for mobile:
    ///
    ///  * On game start it measures the real world-space bounds of the board geometry and fits the
    ///    whole board on screen with a small padding, for the device's actual aspect ratio (16:9,
    ///    18:9, 19.5:9, tablet, ...). Perspective and orthographic cameras are both supported.
    ///  * It holds that full-board shot for <see cref="openingOverviewSeconds"/> (default 5 s).
    ///  * During play it keeps the whole board in view but gently pans to follow whoever's turn it is
    ///    and moves forward with their token as it advances (<see cref="keepWholeBoardInView"/>).
    ///  * It only pulls all the way back to the neutral overview after <see cref="idleTimeoutSeconds"/>
    ///    of no dice/move activity (default 30 s) or when the game ends.
    ///  * The player can override framing at any time: pinch to zoom, or the on-screen +/- buttons
    ///    call <see cref="ZoomIn"/> / <see cref="ZoomOut"/> / <see cref="ResetZoom"/>.
    ///
    /// Purely reactive to <see cref="LudoBoardLogic"/>/<see cref="LudoDiceRoller"/> events; no per-frame
    /// game-state polling, no per-frame allocations (bounds measured only on Start / RecenterCamera).
    /// Attach to the scene Camera - <see cref="LudoBoardSceneBuilder"/> wires this via <see cref="Configure"/>.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class LudoCameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LudoBoardLogic board;
        [SerializeField] private LudoDiceRoller diceRoller;
        [Tooltip("Root of the board geometry to frame. Empty -> looks for \"LudoBoard_Root\", then all MeshRenderers.")]
        [SerializeField] private Transform boardRoot;

        [Header("Board Fit")]
        [SerializeField] private bool autoFrameBoardOnStart = true;
        [Tooltip("Extra margin around the board, as a fraction of board size (0.08 = 8% each side).")]
        [Range(0f, 0.4f)]
        [SerializeField] private float overviewPadding = 0.08f;
        [Tooltip("Downward tilt of the shot, in degrees (90 = straight top-down).")]
        [Range(20f, 90f)]
        [SerializeField] private float viewPitch = 58f;
        [SerializeField] private float viewYaw = 0f;

        [Header("Pacing")]
        [Tooltip("Seconds to hold the full-board overview at game start before follow-cam begins.")]
        [SerializeField] private float openingOverviewSeconds = 5f;
        [Tooltip("Seconds of no dice/move activity before the camera returns to the neutral overview.")]
        [SerializeField] private float idleTimeoutSeconds = 30f;

        [Header("Follow Cam (keeps the board in view while tracking the active player)")]
        [SerializeField] private bool keepWholeBoardInView = true;
        [Tooltip("0 = stay centred on the board, 1 = centre fully on the active player's tokens.")]
        [Range(0f, 1f)]
        [SerializeField] private float playerFollowBias = 0.4f;
        [Tooltip("0 = stay centred on the board, 1 = centre fully on the current move (from->to).")]
        [Range(0f, 1f)]
        [SerializeField] private float moveFollowBias = 0.55f;
        [Tooltip("Distance multiplier vs the full-board fit while following a player (0.8 = 20% tighter).")]
        [Range(0.4f, 1f)]
        [SerializeField] private float followZoom = 0.8f;
        [Tooltip("Distance multiplier vs the full-board fit while previewing/animating a move.")]
        [Range(0.3f, 1f)]
        [SerializeField] private float moveZoom = 0.7f;

        [Header("Manual Zoom (pinch / +- buttons)")]
        [SerializeField] private float minZoom = 0.6f;
        [SerializeField] private float maxZoom = 2.5f;
        [Tooltip("Multiplier applied per ZoomIn()/ZoomOut() button press.")]
        [SerializeField] private float zoomStep = 1.2f;
        [SerializeField] private float pinchSensitivity = 0.01f;
        [Tooltip("Reset manual zoom back to 1x whenever the camera changes framing mode.")]
        [SerializeField] private bool resetZoomOnModeChange = false;

        [Header("Motion")]
        [SerializeField] private float positionSmoothTime = 0.55f;
        [SerializeField] private float rotationLerpSpeed = 4f;
        [SerializeField] private float fovLerpSpeed = 4f;

        [Header("Fallback framing (used only before Start / if bounds can't be measured)")]
        [SerializeField] private Vector3 fallbackPosition = new Vector3(0f, 26f, -16f);
        [SerializeField] private Vector3 fallbackEulerAngles = new Vector3(58f, 0f, 0f);
        [SerializeField] private float fieldOfView = 55f;

        private Camera _camera;

        // Base framing (before manual zoom) - one code path feeds ApplyFraming().
        private Vector3 _framedCenter;
        private Vector3 _framedViewDir = Vector3.forward;
        private float _framedDistance = 30f;
        private float _framedOrthoHalf = 10f;

        // Resolved targets the Update() loop eases toward.
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private float _targetFov;
        private float _targetOrthoSize;
        private Vector3 _positionVelocity;

        private float _idleTimer;
        private bool _focused;                 // false = neutral overview, true = follow/move cam
        private float _suppressFocusUntil;     // opening board-hold
        private float _manualZoom = 1f;
        private float _lastAspect = -1f;

        // Cached full-board fit (recomputed on Start / RecenterCamera / aspect change).
        private Vector3 _boardCenter;
        private float _boardFitDistance = 30f;
        private float _boardFitOrthoHalf = 10f;
        private float _prevPinchDistance = -1f;

        private readonly Vector3[] _corners = new Vector3[8];

        /// <summary>True while the camera is following the action rather than showing the neutral overview.</summary>
        public bool IsFocused => _focused;
        /// <summary>Current manual zoom multiplier (1 = the auto framing, &gt;1 = zoomed in).</summary>
        public float ManualZoom => _manualZoom;

        private void Awake()
        {
            _camera = GetComponent<Camera>();

            _framedCenter = fallbackPosition + Quaternion.Euler(fallbackEulerAngles) * Vector3.forward * 20f;
            _framedViewDir = Quaternion.Euler(fallbackEulerAngles) * Vector3.forward;
            _framedDistance = 20f;
            _framedOrthoHalf = _camera != null ? _camera.orthographicSize : 5f;
            _targetFov = fieldOfView;
            ApplyFraming();
            transform.SetPositionAndRotation(_targetPosition, _targetRotation);
            if (_camera != null && !_camera.orthographic) _camera.fieldOfView = _targetFov;
        }

        private void Start()
        {
            if (autoFrameBoardOnStart) RecenterCamera();
            else ShowOverview(instant: true);

            _suppressFocusUntil = Time.time + Mathf.Max(0f, openingOverviewSeconds);
        }

        private void OnEnable()
        {
            if (board != null)
            {
                board.OnTurnChanged += HandleTurnChanged;
                board.OnTokenMoved += HandleTokenMoved;
                board.OnGameOver += HandleGameOver;
                HandleTurnChanged(board.CurrentPlayer);
            }

            if (diceRoller != null)
            {
                diceRoller.OnDiceRollStarted += HandleActivity;
                diceRoller.OnDiceRollCompleted += HandleDiceRollCompleted;
            }
        }

        private void OnDisable()
        {
            if (board != null)
            {
                board.OnTurnChanged -= HandleTurnChanged;
                board.OnTokenMoved -= HandleTokenMoved;
                board.OnGameOver -= HandleGameOver;
            }

            if (diceRoller != null)
            {
                diceRoller.OnDiceRollStarted -= HandleActivity;
                diceRoller.OnDiceRollCompleted -= HandleDiceRollCompleted;
            }
        }

        /// <summary>Wires the board/dice this camera reacts to.</summary>
        public void Configure(LudoBoardLogic targetBoard, LudoDiceRoller targetDice)
        {
            board = targetBoard;
            diceRoller = targetDice;
        }

        /// <summary>Overload that also supplies the board-geometry root used for overview framing.</summary>
        public void Configure(LudoBoardLogic targetBoard, LudoDiceRoller targetDice, Transform boardGeometryRoot)
        {
            board = targetBoard;
            diceRoller = targetDice;
            if (boardGeometryRoot != null) boardRoot = boardGeometryRoot;
        }

        // ---- public camera controls (buttons / pinch) --------------------------------------------

        /// <summary>Hook to a "+" UI button. Zooms the camera in one step (clamped).</summary>
        public void ZoomIn() => SetManualZoom(_manualZoom * zoomStep);

        /// <summary>Hook to a "-" UI button. Zooms the camera out one step (clamped).</summary>
        public void ZoomOut() => SetManualZoom(_manualZoom / zoomStep);

        /// <summary>Hook to a "reset view" UI button. Returns to the automatic framing.</summary>
        public void ResetZoom() => SetManualZoom(1f);

        private void SetManualZoom(float value)
        {
            _manualZoom = Mathf.Clamp(value, minZoom, maxZoom);
            ApplyFraming();
        }

        /// <summary>
        /// Re-measures the board and recomputes the full-board fit for the current aspect ratio. Call
        /// after the board is rebuilt/resized or the screen orientation changes. Applied immediately if
        /// the camera is showing the overview.
        /// </summary>
        public void RecenterCamera()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            _lastAspect = _camera != null ? _camera.aspect : 1f;

            Bounds bounds = ComputeBoardBounds();
            _boardCenter = bounds.center;
            ComputeBoardFit(bounds, out _boardFitDistance, out _boardFitOrthoHalf);

            if (!_focused) ShowOverview(instant: true);
        }

        private void Update()
        {
            if (_camera == null) return;

            if (!_focused && Mathf.Abs(_camera.aspect - _lastAspect) > 0.01f)
            {
                RecenterCamera();
            }

            HandlePinchZoom();

            if (_focused)
            {
                _idleTimer += Time.deltaTime;
                if (_idleTimer >= idleTimeoutSeconds) ShowOverview();
            }

            transform.position = Vector3.SmoothDamp(transform.position, _targetPosition, ref _positionVelocity, positionSmoothTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, rotationLerpSpeed * Time.deltaTime);

            if (_camera.orthographic)
                _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, _targetOrthoSize, fovLerpSpeed * Time.deltaTime);
            else
                _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, _targetFov, fovLerpSpeed * Time.deltaTime);
        }

        private void HandlePinchZoom()
        {
            if (Input.touchCount != 2)
            {
                _prevPinchDistance = -1f;
                return;
            }

            float dist = (Input.GetTouch(0).position - Input.GetTouch(1).position).magnitude;
            if (_prevPinchDistance > 0f)
            {
                float delta = (dist - _prevPinchDistance) * pinchSensitivity;
                if (Mathf.Abs(delta) > 0.0001f) SetManualZoom(_manualZoom * (1f + delta));
            }
            _prevPinchDistance = dist;
        }

        // ---- framing pipeline ------------------------------------------------------------------

        /// <summary>Stores the pre-zoom framing; <see cref="ApplyFraming"/> turns it into camera targets.</summary>
        private void SetFraming(Vector3 center, Vector3 viewDir, float distance, float orthoHalf)
        {
            _framedCenter = center;
            _framedViewDir = viewDir.sqrMagnitude > 1e-5f ? viewDir.normalized : Vector3.forward;
            _framedDistance = Mathf.Max(0.1f, distance);
            _framedOrthoHalf = Mathf.Max(0.1f, orthoHalf);
            ApplyFraming();
        }

        /// <summary>Applies the stored framing plus the manual zoom multiplier to the camera targets.</summary>
        private void ApplyFraming()
        {
            float z = Mathf.Max(0.01f, _manualZoom);
            _targetRotation = Quaternion.LookRotation(_framedViewDir, Vector3.up);
            _targetPosition = _framedCenter - _framedViewDir * (_framedDistance / z);
            _targetFov = fieldOfView;
            _targetOrthoSize = _framedOrthoHalf / z;

            if (_camera != null && !_camera.orthographic)
            {
                float needFar = (_framedDistance / z) + _framedDistance + 20f;
                if (_camera.farClipPlane < needFar) _camera.farClipPlane = needFar;
            }
        }

        private Vector3 ViewDir() => Quaternion.Euler(viewPitch, viewYaw, 0f) * Vector3.forward;

        // ---- board bounds + fit --------------------------------------------------------------

        private Bounds ComputeBoardBounds()
        {
            Transform root = boardRoot;
            if (root == null)
            {
                GameObject found = GameObject.Find("LudoBoard_Root");
                if (found != null) { root = found.transform; boardRoot = root; }
            }

            MeshRenderer[] renderers = root != null
                ? root.GetComponentsInChildren<MeshRenderer>()
                : FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            Bounds bounds = default;
            bool has = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer r = renderers[i];
                if (r == null || !r.enabled) continue;
                if (!has) { bounds = r.bounds; has = true; }
                else bounds.Encapsulate(r.bounds);
            }

            if (!has)
                return new Bounds(fallbackPosition + ViewDir() * 20f, Vector3.one * 20f);

            return bounds;
        }

        /// <summary>Distance (perspective) / half-size (orthographic) so every corner of
        /// <paramref name="bounds"/> sits inside the viewport with <see cref="overviewPadding"/> margin
        /// at the camera's current aspect ratio.</summary>
        private void ComputeBoardFit(Bounds bounds, out float distance, out float orthoHalf)
        {
            Vector3 dir = ViewDir();
            Vector3 up = Quaternion.Euler(viewPitch, viewYaw, 0f) * Vector3.up;
            Vector3 right = Quaternion.Euler(viewPitch, viewYaw, 0f) * Vector3.right;

            Vector3 e = bounds.extents;
            _corners[0] = new Vector3(+e.x, +e.y, +e.z); _corners[1] = new Vector3(+e.x, +e.y, -e.z);
            _corners[2] = new Vector3(+e.x, -e.y, +e.z); _corners[3] = new Vector3(+e.x, -e.y, -e.z);
            _corners[4] = new Vector3(-e.x, +e.y, +e.z); _corners[5] = new Vector3(-e.x, +e.y, -e.z);
            _corners[6] = new Vector3(-e.x, -e.y, +e.z); _corners[7] = new Vector3(-e.x, -e.y, -e.z);

            float aspect = _camera != null && _camera.aspect > 0.01f ? _camera.aspect : 1.7778f;
            float tanV = Mathf.Tan(Mathf.Max(1f, fieldOfView) * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * aspect;

            float d = 0f, half = 0f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = _corners[i];
                float f = Vector3.Dot(c, dir);
                float u = Mathf.Abs(Vector3.Dot(c, up));
                float rgt = Mathf.Abs(Vector3.Dot(c, right));
                d = Mathf.Max(d, u / tanV - f, rgt / tanH - f);
                half = Mathf.Max(half, u, rgt / aspect);
            }

            distance = Mathf.Max(d * (1f + overviewPadding), (_camera != null ? _camera.nearClipPlane : 0.3f) + 1f);
            orthoHalf = half * (1f + overviewPadding);
        }

        // ---- event handlers -----------------------------------------------------------------

        private void HandleTurnChanged(PlayerColor color) => FocusOnPlayer(color);

        private void HandleTokenMoved(PlayerColor color, int tokenId, int fromPos, int toPos)
        {
            HandleActivity();

            LudoToken token = FindToken(color, tokenId);
            Vector3? fromPoint = token != null ? ResolveTokenWorldPosition(token) : null;
            Vector3? toPoint = board != null ? board.GetWorldPosition(color, ClampToWaypointRange(toPos)) : null;

            if (fromPoint.HasValue && toPoint.HasValue) FrameMoveSpan(fromPoint.Value, toPoint.Value);
            else FocusOnPlayer(color);
        }

        private void HandleDiceRollCompleted(int diceValue)
        {
            HandleActivity();
            if (board != null) FocusOnRoll(board.CurrentPlayer, diceValue);
        }

        private void HandleGameOver(List<PlayerColor> finishOrder) => ShowOverview();

        private void HandleActivity() => _idleTimer = 0f;

        // ---- framing modes ----------------------------------------------------------------

        /// <summary>Frames a preview of the active token's tile and its destination together, biased so
        /// the whole board stays readable (<see cref="moveFollowBias"/> / <see cref="moveZoom"/>).</summary>
        public void FocusOnRoll(PlayerColor color, int diceValue)
        {
            if (Time.time < _suppressFocusUntil) return;
            if (board == null) { ShowOverview(); return; }

            LudoToken referenceToken = FindReferenceMovableToken(color, diceValue);
            if (referenceToken == null) { FocusOnPlayer(color); return; }

            Vector3? fromPoint = ResolveTokenWorldPosition(referenceToken);
            int destPos = referenceToken.IsInBase ? 0 : ClampToWaypointRange(referenceToken.PathPosition + diceValue);
            Vector3? toPoint = board.GetWorldPosition(color, destPos);

            if (!fromPoint.HasValue || !toPoint.HasValue) { FocusOnPlayer(color); return; }
            FrameMoveSpan(fromPoint.Value, toPoint.Value);
        }

        /// <summary>Frames the current move. With <see cref="keepWholeBoardInView"/> the camera stays on
        /// the board-fit distance and just pans toward the move; otherwise it dollies right in.</summary>
        private void FrameMoveSpan(Vector3 fromPoint, Vector3 toPoint)
        {
            if (Time.time < _suppressFocusUntil) return;
            EnterFocus();

            Vector3 moveMid = (fromPoint + toPoint) * 0.5f;

            if (keepWholeBoardInView)
            {
                Vector3 center = Vector3.Lerp(_boardCenter, moveMid, moveFollowBias);
                SetFraming(center, ViewDir(), _boardFitDistance * moveZoom, _boardFitOrthoHalf * moveZoom);
            }
            else
            {
                float span = Mathf.Max(Vector3.Distance(fromPoint, toPoint) * 2f, 6f);
                float tanV = Mathf.Tan(Mathf.Max(1f, fieldOfView) * 0.5f * Mathf.Deg2Rad);
                float dist = Mathf.Clamp((span * 0.5f) / tanV, 4f, _boardFitDistance);
                SetFraming(moveMid, ViewDir(), dist, span * 0.5f);
            }
        }

        /// <summary>Pans toward the given player's tokens while keeping the board in frame.</summary>
        public void FocusOnPlayer(PlayerColor color)
        {
            if (Time.time < _suppressFocusUntil) return;
            if (board == null) { ShowOverview(); return; }

            Vector3? focusPoint = GetAveragePlayerPosition(color) ?? GetYardFocusPoint(color);
            if (!focusPoint.HasValue) { ShowOverview(); return; }

            EnterFocus();

            if (keepWholeBoardInView)
            {
                Vector3 center = Vector3.Lerp(_boardCenter, focusPoint.Value, playerFollowBias);
                SetFraming(center, ViewDir(), _boardFitDistance * followZoom, _boardFitOrthoHalf * followZoom);
            }
            else
            {
                SetFraming(focusPoint.Value, ViewDir(), _boardFitDistance * 0.45f, _boardFitOrthoHalf * 0.45f);
            }
        }

        /// <summary>Neutral full-board shot. <paramref name="instant"/> snaps this frame.</summary>
        public void ShowOverview(bool instant = false)
        {
            bool wasFocused = _focused;
            _focused = false;
            _idleTimer = 0f;
            if (resetZoomOnModeChange && wasFocused) _manualZoom = 1f;

            SetFraming(_boardCenter, ViewDir(), _boardFitDistance, _boardFitOrthoHalf);

            if (instant)
            {
                transform.SetPositionAndRotation(_targetPosition, _targetRotation);
                if (_camera != null)
                {
                    if (_camera.orthographic) _camera.orthographicSize = _targetOrthoSize;
                    else _camera.fieldOfView = _targetFov;
                }
                _positionVelocity = Vector3.zero;
            }
        }

        private void EnterFocus()
        {
            if (!_focused && resetZoomOnModeChange) _manualZoom = 1f;
            _focused = true;
            _idleTimer = 0f;
        }

        // ---- helpers ---------------------------------------------------------------------

        private LudoToken FindReferenceMovableToken(PlayerColor color, int diceValue)
        {
            if (board == null) return null;
            List<int> movableIds = board.GetMovableTokens(color, diceValue);
            if (movableIds == null || movableIds.Count == 0) return null;

            LudoToken best = null;
            foreach (int id in movableIds)
            {
                LudoToken candidate = FindToken(color, id);
                if (candidate == null) continue;
                bool better = best == null ||
                    (best.IsInBase && !candidate.IsInBase) ||
                    (candidate.IsInBase == best.IsInBase && candidate.PathPosition > best.PathPosition);
                if (better) best = candidate;
            }
            return best;
        }

        private LudoToken FindToken(PlayerColor color, int tokenId)
        {
            if (board == null) return null;
            LudoToken[] tokens = board.GetTokens(color);
            if (tokens == null) return null;
            foreach (LudoToken token in tokens)
                if (token != null && token.Id == tokenId) return token;
            return null;
        }

        private static int ClampToWaypointRange(int pathPosition)
        {
            return Mathf.Min(pathPosition, LudoBoardLogic.CommonPathRelativeMax + LudoBoardLogic.HomeStretchLength);
        }

        private Vector3? GetAveragePlayerPosition(PlayerColor color)
        {
            LudoToken[] tokens = board.GetTokens(color);
            if (tokens == null || tokens.Length == 0) return null;

            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (LudoToken token in tokens)
            {
                if (token == null || token.IsInBase || token.IsFinished) continue;
                Vector3? pos = ResolveTokenWorldPosition(token);
                if (pos.HasValue) { sum += pos.Value; count++; }
            }
            return count > 0 ? sum / count : (Vector3?)null;
        }

        private Vector3? ResolveTokenWorldPosition(LudoToken token)
        {
            if (token.Character != null) return token.Character.transform.position;
            if (token.Visual != null) return token.Visual.position;
            return board.GetWorldPosition(token.Color, token.PathPosition);
        }

        private Vector3? GetYardFocusPoint(PlayerColor color)
        {
            LudoToken[] tokens = board.GetTokens(color);
            if (tokens == null || tokens.Length == 0) return null;

            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (LudoToken token in tokens)
            {
                if (token == null) continue;
                Transform anchor = token.Character != null ? token.Character.transform : token.Visual;
                if (anchor == null) continue;
                sum += anchor.position;
                count++;
            }
            return count > 0 ? sum / count : (Vector3?)null;
        }
    }
}
