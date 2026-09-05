using System;
using System.Collections.Generic;
using LudoGame.Board;
using LudoGame.Game;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LudoGame.Player
{
    /// <summary>
    /// Lets the player tap/click a token on screen to move it. Listens to
    /// GameManager to know which tokens are currently allowed to move
    /// (after a dice roll), then forwards a valid tap to
    /// GameManager.TryMoveToken(). Uses Unity's new Input System, since this
    /// project has "Active Input Handling" set to the new system only.
    ///
    /// Setup (in the Unity Editor):
    /// 1. Every token prefab needs a Collider (e.g. Box Collider or Sphere
    ///    Collider) on it so it can be tapped/clicked - PlayerToken alone has
    ///    no collider.
    /// 2. Put this script on an empty GameObject (e.g. "InputManager") or on
    ///    the main Camera.
    /// 3. Drag your main Camera into "Raycast Camera" (leave empty to auto-use
    ///    Camera.main).
    /// 4. Drag your GameManager object into "Game Manager".
    /// 5. (Optional) Set "Token Layer Mask" to only the layer your tokens are
    ///    on, if you want to ignore clicks on the board/other objects faster.
    ///    Leave it as "Everything" if you don't want to bother with layers yet.
    /// </summary>
    public class TokenSelector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera raycastCamera;
        [SerializeField] private GameManager gameManager;

        [Header("Raycast Settings")]
        [Tooltip("Restrict tapping to a specific layer your tokens are on. Leave as Everything if unsure.")]
        [SerializeField] private LayerMask tokenLayerMask = ~0;
        [SerializeField] private float maxRayDistance = 100f;

        /// <summary>Raised when the player taps a token that is currently allowed to move.</summary>
        public event Action<PlayerToken> OnValidTokenTapped;

        /// <summary>Raised when the player taps a token that can't move right now (wrong color, or no legal move).</summary>
        public event Action<PlayerToken> OnInvalidTokenTapped;

        private readonly HashSet<PlayerToken> movableTokens = new HashSet<PlayerToken>();

        private void Awake()
        {
            if (raycastCamera == null) raycastCamera = Camera.main;

            // Camera.main relies on the camera being tagged "MainCamera" AND active - if
            // either isn't true, Camera.main silently returns null. Fall back to searching
            // every camera in the scene (including inactive ones - a plain
            // FindAnyObjectByType search skips inactive GameObjects by default, which
            // would miss a camera nested under a currently-disabled parent).
            if (raycastCamera == null) raycastCamera = FindAnySceneCamera();

            if (raycastCamera == null)
            {
                Debug.LogWarning("TokenSelector: no camera found anywhere in the scene (checked active and inactive objects). " +
                    "Assign 'Raycast Camera' manually in the Inspector, or tap-to-move will never work.", this);
            }
            else if (!raycastCamera.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("TokenSelector: using camera '" + raycastCamera.name + "', but it (or one of its parents) " +
                    "is inactive in the Hierarchy. Raycasting will still work, but an inactive camera usually means " +
                    "something else is set up wrong - consider activating it.", raycastCamera);
            }
        }

        /// <summary>
        /// Finds a Camera in the scene, including ones on inactive GameObjects.
        /// If there's more than one camera (e.g. a leftover preview camera nested
        /// in a board prefab), prefers one tagged "MainCamera", then any active
        /// one, before settling for whatever is found first.
        /// </summary>
        private static Camera FindAnySceneCamera()
        {
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
            if (cameras.Length == 0) return null;

            foreach (Camera cam in cameras)
                if (cam.CompareTag("MainCamera")) return cam;

            foreach (Camera cam in cameras)
                if (cam.gameObject.activeInHierarchy) return cam;

            return cameras[0];
        }

        private void OnEnable()
        {
            if (gameManager == null) return;
            gameManager.OnDiceResult += HandleDiceResult;
            gameManager.OnTurnStarted += HandleTurnStarted;
        }

        private void OnDisable()
        {
            if (gameManager == null) return;
            gameManager.OnDiceResult -= HandleDiceResult;
            gameManager.OnTurnStarted -= HandleTurnStarted;
        }

        private void Update()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;

            // Self-heal: a destroyed camera reference compares equal to null (Unity's
            // overloaded ==), so this also recovers if the camera we found in Awake()
            // was later replaced or destroyed.
            if (raycastCamera == null)
                raycastCamera = Camera.main != null ? Camera.main : FindAnySceneCamera();

            if (raycastCamera == null)
            {
                Debug.LogWarning("TokenSelector: no camera to raycast from - none exists anywhere in the scene.", this);
                return;
            }

            if (gameManager == null)
            {
                Debug.LogWarning("TokenSelector: Game Manager is not assigned - taps can't be forwarded.", this);
                return;
            }

            Vector2 screenPos = pointer.position.ReadValue();
            Ray ray = raycastCamera.ScreenPointToRay(screenPos);

            if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, tokenLayerMask)) return;

            PlayerToken token = hit.collider.GetComponentInParent<PlayerToken>();
            if (token == null) return;

            if (movableTokens.Contains(token))
            {
                OnValidTokenTapped?.Invoke(token);
                gameManager.TryMoveToken(token);
            }
            else
            {
                OnInvalidTokenTapped?.Invoke(token);
            }
        }

        private void HandleDiceResult(GridManager.PlayerColor color, int diceValue)
        {
            movableTokens.Clear();
            foreach (var token in gameManager.GetMovableTokens(diceValue))
                movableTokens.Add(token);
        }

        private void HandleTurnStarted(GridManager.PlayerColor color)
        {
            // A new turn just began (nobody has rolled yet), so nothing is tappable until HandleDiceResult fires again.
            movableTokens.Clear();
        }
    }
}
