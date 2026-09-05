using UnityEngine;

namespace LudoGame.MiniGames.KillaBandar
{
    /// <summary>
    /// Keeps the Protector tethered within a max distance of the Killa
    /// stake - a simple distance-clamp "rope" rather than a full physics
    /// joint, which is reliable and cheap on mobile. Draws the rope with a
    /// LineRenderer so it's visible.
    ///
    /// Setup: put this on an empty "Rope" object (or the Protector itself).
    /// Assign the Killa stake and the current Protector's Transform -
    /// KillaBandarGameManager re-assigns "Protector Transform" on every
    /// role swap via SetProtector().
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class RopeConstraint : MonoBehaviour
    {
        [SerializeField] private Transform killaStake;
        [SerializeField] private Transform protectorTransform;
        [SerializeField] private float ropeLength = 4f;

        public float RopeLength => ropeLength;

        /// <summary>How extended the rope currently is: 0 = at the stake, 1 = at max length.</summary>
        public float Extension { get; private set; }

        private LineRenderer line;

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.positionCount = 2;
        }

        /// <summary>Call this whenever the Protector changes (e.g. on a role swap).</summary>
        public void SetProtector(Transform newProtector)
        {
            protectorTransform = newProtector;
        }

        private void LateUpdate()
        {
            if (killaStake == null || protectorTransform == null) return;

            Vector3 toProtector = protectorTransform.position - killaStake.position;
            toProtector.y = 0f;

            float distance = toProtector.magnitude;
            if (distance > ropeLength && distance > 0.0001f)
            {
                Vector3 clamped = killaStake.position + toProtector.normalized * ropeLength;
                clamped.y = protectorTransform.position.y;
                protectorTransform.position = clamped;
                distance = ropeLength;
            }

            Extension = ropeLength > 0f ? distance / ropeLength : 0f;

            line.SetPosition(0, killaStake.position);
            line.SetPosition(1, protectorTransform.position);
        }
    }
}
