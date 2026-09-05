using System;
using System.Collections;
using UnityEngine;

namespace LudoGame.MiniGames.GulliDanda
{
    /// <summary>
    /// The fielder ("Bhaju"): predicts where the struck Gulli will land using
    /// basic projectile motion, tries to catch it if it can reach the flight
    /// path in time, otherwise runs to the landing spot and throws it back at
    /// the Kotha/Danda target.
    ///
    /// Setup (in the Unity Editor):
    /// 1. Put this on the fielder's 3D model.
    /// 2. Drag in the Gulli's GulliController and the Kotha/Danda target Transform.
    /// 3. Call BeginFielding() right after GulliController.Strike() is called
    ///    (GulliDandaGameManager already does this for you).
    ///
    /// Movement here is a simple straight-line Lerp so this works with no extra
    /// setup - swap the body of MoveTowards() for NavMeshAgent calls if you want
    /// the fielder to path around obstacles instead.
    /// </summary>
    public class BhajuAI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GulliController gulli;
        [SerializeField] private Transform kothaTarget;

        [Header("Fielder Ability")]
        [Tooltip("How fast the fielder can run to a target position (m/s).")]
        [SerializeField] private float moveSpeed = 6f;
        [Tooltip("How close the fielder needs to be to the Gulli to attempt a catch.")]
        [SerializeField] private float catchRadius = 1.5f;
        [Tooltip("Base chance (0-1) of hitting the Kotha with the return throw at close range.")]
        [SerializeField] private float baseThrowAccuracy = 0.85f;
        [Tooltip("How much throw accuracy drops per meter of throw distance.")]
        [SerializeField] private float accuracyFalloffPerMeter = 0.02f;

        /// <summary>Raised if the fielder catches the Gulli mid-air (batter is out, no score).</summary>
        public event Action OnCaughtGulli;

        /// <summary>Raised when the return throw hits the Kotha (batter is out, but keeps the distance already earned).</summary>
        public event Action OnHitKotha;

        /// <summary>Raised when the return throw misses (batter is safe for another turn).</summary>
        public event Action OnThrowMissed;

        public void BeginFielding()
        {
            StopAllCoroutines();
            StartCoroutine(FieldingRoutine());
        }

        private IEnumerator FieldingRoutine()
        {
            float groundY = transform.position.y;

            Vector3 landingPoint = PredictLandingPoint(gulli.Body, groundY);
            float timeToLand = PredictTimeToLand(gulli.Body, groundY);

            // Can the fielder reach the Gulli's flight path before it lands? Needs a
            // safety margin (0.9x) so it arrives a little early rather than just missing it.
            float timeToReach = Vector3.Distance(transform.position, landingPoint) / Mathf.Max(moveSpeed, 0.01f);
            bool canAttemptCatch = timeToReach <= timeToLand * 0.9f;

            if (canAttemptCatch)
            {
                yield return MoveTowards(landingPoint, timeToLand);

                if (gulli.IsInPlay && Vector3.Distance(transform.position, gulli.transform.position) <= catchRadius)
                {
                    gulli.Catch();
                    OnCaughtGulli?.Invoke();
                    yield break;
                }
            }
            else
            {
                yield return MoveTowards(landingPoint, timeToLand + 0.5f);
            }

            // Missed the catch (or never attempted one) - wait for it to land, then throw back.
            yield return new WaitUntil(() => gulli.HasLanded || gulli.WasCaught);
            if (gulli.WasCaught) yield break;

            ThrowAtKotha();
        }

        private IEnumerator MoveTowards(Vector3 target, float withinSeconds)
        {
            float elapsed = 0f;
            Vector3 start = transform.position;
            withinSeconds = Mathf.Max(withinSeconds, 0.01f);

            while (elapsed < withinSeconds && Vector3.Distance(transform.position, target) > 0.05f)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(start, target, elapsed / withinSeconds);
                yield return null;
            }
        }

        private void ThrowAtKotha()
        {
            float distance = Vector3.Distance(transform.position, kothaTarget.position);
            float accuracy = Mathf.Clamp01(baseThrowAccuracy - accuracyFalloffPerMeter * distance);

            bool hit = UnityEngine.Random.value <= accuracy;
            if (hit) OnHitKotha?.Invoke();
            else OnThrowMissed?.Invoke();
        }

        /// <summary>Projectile-motion time (seconds) until the Gulli reaches groundY.</summary>
        private static float PredictTimeToLand(Rigidbody rb, float groundY)
        {
            float g = Mathf.Abs(Physics.gravity.y);
            float y0 = rb.position.y - groundY;
            float vy = rb.linearVelocity.y;

            // Solve 0 = y0 + vy*t - 0.5*g*t^2 for the positive root.
            float discriminant = vy * vy + 2f * g * y0;
            if (discriminant < 0f) return 0f;

            return Mathf.Max((vy + Mathf.Sqrt(discriminant)) / g, 0f);
        }

        /// <summary>Projectile-motion landing position (assumes constant horizontal velocity, no air drag).</summary>
        private static Vector3 PredictLandingPoint(Rigidbody rb, float groundY)
        {
            float t = PredictTimeToLand(rb, groundY);
            Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            Vector3 landing = rb.position + horizontalVelocity * t;
            landing.y = groundY;
            return landing;
        }
    }
}
