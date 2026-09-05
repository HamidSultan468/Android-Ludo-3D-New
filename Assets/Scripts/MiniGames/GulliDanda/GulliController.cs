using System;
using UnityEngine;

namespace LudoGame.MiniGames.GulliDanda
{
    /// <summary>
    /// Physics behavior for the Gulli (the small wooden peg): the initial
    /// "flip" off the ground, the mid-air "strike" that sends it flying, and
    /// tracking how far it travels until it lands or gets caught.
    ///
    /// Setup (in the Unity Editor):
    /// 1. Put this on the Gulli's 3D model, alongside a Rigidbody and a
    ///    Collider (e.g. Capsule Collider).
    /// 2. Set "Ground Y" to the world Y position of the playing field surface.
    /// 3. GulliDandaGameManager calls Flip(), then Strike(direction, power).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class GulliController : MonoBehaviour
    {
        [Header("Flip (tap to pop the Gulli into the air)")]
        [SerializeField] private float flipUpSpeed = 4f;
        [SerializeField] private float flipForwardSpeed = 1f;

        [Header("Strike (swipe + power meter hit)")]
        [Tooltip("Speed added at power = 1.0 (a Perfect hit).")]
        [SerializeField] private float maxStrikeSpeed = 18f;
        [Tooltip("How steeply the Gulli launches upward on a strike, in degrees.")]
        [SerializeField] private float strikeLaunchAngle = 35f;

        [Header("Ground Detection")]
        [SerializeField] private float groundY;
        [SerializeField] private float settleVelocityThreshold = 0.15f;

        [Header("Home Position")]
        [Tooltip("Where the Gulli resets to before every Flip() (the Kotha). Leave empty to use its starting scene position.")]
        [SerializeField] private Transform spawnPoint;

        /// <summary>True from the moment it's struck until it lands or is caught.</summary>
        public bool IsInPlay { get; private set; }
        public bool HasLanded { get; private set; }
        public bool WasCaught { get; private set; }

        /// <summary>World position where the strike happened - distance is measured from here.</summary>
        public Vector3 StrikeOrigin { get; private set; }

        public Rigidbody Body { get; private set; }

        /// <summary>Raised once the Gulli comes to rest on the ground, with the horizontal distance travelled (meters).</summary>
        public event Action<float> OnLanded;

        /// <summary>Raised if the fielder catches the Gulli out of the air.</summary>
        public event Action OnCaught;

        private Vector3 homePosition;

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            // Fall back to wherever it starts in the scene if no explicit spawn point is set,
            // so every turn begins from the same spot instead of drifting from where the last one ended.
            homePosition = spawnPoint != null ? spawnPoint.position : transform.position;
        }

        /// <summary>Resets the Gulli to its Kotha spawn point and pops it up off the ground so it can be struck mid-air.</summary>
        public void Flip()
        {
            HasLanded = false;
            WasCaught = false;
            IsInPlay = false;
            Body.isKinematic = false;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            transform.position = spawnPoint != null ? spawnPoint.position : homePosition;
            Body.linearVelocity = new Vector3(0f, flipUpSpeed, flipForwardSpeed);
        }

        /// <summary>
        /// Hits the airborne Gulli. "direction" is the horizontal (XZ) swipe direction,
        /// "power" is 0-1 from the timing meter (1 = Perfect).
        /// </summary>
        public void Strike(Vector3 direction, float power)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;
            direction.Normalize();

            power = Mathf.Clamp01(power);
            float speed = maxStrikeSpeed * power;

            float radians = strikeLaunchAngle * Mathf.Deg2Rad;
            Vector3 velocity = direction * (speed * Mathf.Cos(radians)) + Vector3.up * (speed * Mathf.Sin(radians));

            Body.linearVelocity = velocity;
            StrikeOrigin = transform.position;
            IsInPlay = true;
        }

        /// <summary>Call this when the fielder successfully catches the Gulli mid-air.</summary>
        public void Catch()
        {
            if (!IsInPlay || HasLanded) return;

            WasCaught = true;
            IsInPlay = false;
            Body.linearVelocity = Vector3.zero;
            Body.isKinematic = true;
            OnCaught?.Invoke();
        }

        private void FixedUpdate()
        {
            if (!IsInPlay || HasLanded) return;

            bool onGround = transform.position.y <= groundY + 0.05f;
            bool settled = Body.linearVelocity.magnitude <= settleVelocityThreshold;

            if (onGround && settled)
            {
                HasLanded = true;
                IsInPlay = false;

                Vector3 flatStrike = new Vector3(StrikeOrigin.x, 0f, StrikeOrigin.z);
                Vector3 flatLanding = new Vector3(transform.position.x, 0f, transform.position.z);
                float distance = Vector3.Distance(flatStrike, flatLanding);

                OnLanded?.Invoke(distance);
            }
        }
    }
}
