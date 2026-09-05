using UnityEngine;
using UnityEngine.InputSystem;

namespace LudoGame.Empire
{
    /// <summary>
    /// Lets the player tap/click a ResourceTree to chop it. Uses the new
    /// Input System, the same tap-to-select pattern as TokenSelector in the
    /// main Ludo game.
    ///
    /// Setup: put this on an empty GameObject (or the main Camera). Leave
    /// "Raycast Camera" empty to auto-use Camera.main.
    /// </summary>
    public class TreeChopInput : MonoBehaviour
    {
        [SerializeField] private Camera raycastCamera;
        [SerializeField] private float maxRayDistance = 100f;

        /// <summary>Raised with the running total of trees felled this session.</summary>
        public event System.Action<int> OnTreesFelledChanged;

        public int TreesFelled { get; private set; }

        private void Awake()
        {
            if (raycastCamera == null) raycastCamera = Camera.main;

            // Same fix as TokenSelector.cs: Camera.main (and a plain FindAnyObjectByType search)
            // both silently skip inactive GameObjects, which would miss a camera nested under a
            // currently-disabled parent and leave tree-chopping permanently unable to raycast.
            if (raycastCamera == null) raycastCamera = FindAnySceneCamera();
        }

        /// <summary>
        /// Finds a Camera in the scene, including ones on inactive GameObjects. If there's more
        /// than one, prefers one tagged "MainCamera", then any active one, before settling for
        /// whatever is found first.
        /// </summary>
        private static Camera FindAnySceneCamera()
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (cameras.Length == 0) return null;

            foreach (Camera cam in cameras)
                if (cam.CompareTag("MainCamera")) return cam;

            foreach (Camera cam in cameras)
                if (cam.gameObject.activeInHierarchy) return cam;

            return cameras[0];
        }

        private void Update()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;

            // Self-heal: recovers if the camera found in Awake() was later replaced/destroyed,
            // or wasn't found yet at that point (e.g. it starts inactive and activates later).
            if (raycastCamera == null)
                raycastCamera = Camera.main != null ? Camera.main : FindAnySceneCamera();

            if (raycastCamera == null) return;

            Ray ray = raycastCamera.ScreenPointToRay(pointer.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance)) return;

            ResourceTree tree = hit.collider.GetComponentInParent<ResourceTree>();
            if (tree == null) return;

            if (tree.Chop())
            {
                TreesFelled++;
                OnTreesFelledChanged?.Invoke(TreesFelled);
            }
        }
    }
}
