using UnityEngine;

namespace LudoGame.ThirdPersonGame
{
    /// <summary>
    /// A simple third-person camera that smoothly follows a target (the player) from a
    /// fixed offset behind and above it, and always looks at the target. The offset
    /// rotates with the target's facing direction, so the camera stays "behind" the
    /// player as it turns (matches PlayerMovement, which rotates the player to face
    /// wherever it's moving).
    ///
    /// Setup (in the Unity Editor):
    /// 1. Put this on the Main Camera.
    /// 2. Drag your player character into "Target".
    /// 3. Tweak "Offset" to change how far behind/above the camera sits - negative Z is
    ///    behind the player, positive Y is above.
    /// 4. Adjust the smoothing values if the camera feels too laggy or too snappy.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Follow Settings")]
        [Tooltip("Position offset from the target, in the target's own facing direction (behind and above).")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 4f, -6f);
        [Tooltip("How quickly the camera catches up to its desired position - smaller = snappier, larger = more lag/smoothness.")]
        [SerializeField] private float positionSmoothTime = 0.15f;
        [SerializeField] private float rotationSmoothSpeed = 8f;

        [Header("Look At")]
        [Tooltip("Extra height above the target's pivot to aim the camera at (e.g. the chest instead of the feet).")]
        [SerializeField] private float lookAtHeightOffset = 1.5f;

        private Vector3 currentVelocity; // used internally by SmoothDamp, ignore in the Inspector

        private void LateUpdate()
        {
            if (target == null) return;

            // LateUpdate (not Update) so the camera reacts AFTER the player has already
            // moved/rotated this frame - otherwise the camera would lag a frame behind.
            Vector3 desiredPosition = target.position + target.rotation * offset;
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, positionSmoothTime);

            Vector3 lookAtPoint = target.position + Vector3.up * lookAtHeightOffset;
            Quaternion desiredRotation = Quaternion.LookRotation(lookAtPoint - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSmoothSpeed * Time.deltaTime);
        }

        /// <summary>Switches which Transform the camera follows (e.g. when spawning a new player, or switching characters).</summary>
        public void SetTarget(Transform newTarget) => target = newTarget;
    }
}
