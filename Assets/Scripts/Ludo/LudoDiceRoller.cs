using System;
using System.Collections;
using UnityEngine;

namespace LudoEmpire.Ludo
{
    /// <summary>
    /// Drives a physics-based 3D dice roll using a Rigidbody and resolves the
    /// resulting face-up value (1-6) once the dice comes to rest.
    /// Attach to a die GameObject that has a Collider + Rigidbody.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class LudoDiceRoller : MonoBehaviour
    {
        [Header("Launch Settings")]
        [SerializeField] private float minForce = 3f;
        [SerializeField] private float maxForce = 6f;
        [SerializeField] private float minTorque = 4f;
        [SerializeField] private float maxTorque = 9f;
        [SerializeField] private Vector3 launchDirectionBias = new Vector3(0f, 1f, 0f);

        [Header("Settle Detection")]
        [Tooltip("Linear velocity magnitude below which the dice is considered stopped.")]
        [SerializeField] private float linearVelocitySleepThreshold = 0.05f;
        [Tooltip("Angular velocity magnitude below which the dice is considered stopped.")]
        [SerializeField] private float angularVelocitySleepThreshold = 0.05f;
        [Tooltip("Seconds the dice must remain below the sleep thresholds before we read its face.")]
        [SerializeField] private float requiredStillDuration = 0.35f;
        [Tooltip("Hard timeout so a roll can never hang forever (e.g. dice wedged against a wall).")]
        [SerializeField] private float maxRollDuration = 6f;

        [Header("Face Mapping")]
        [Tooltip("Local-space outward normal for die faces 1-6, in order. Index 0 = value 1, etc. " +
                 "Defaults to a standard cube with opposite faces summing to 7.")]
        [SerializeField]
        private Vector3[] faceNormals =
        {
            Vector3.up,        // 1
            Vector3.right,     // 2
            Vector3.forward,   // 3
            Vector3.back,      // 4
            Vector3.left,      // 5
            Vector3.down       // 6
        };

        [Header("Optional")]
        [Tooltip("If assigned, the dice resets to this transform's position/rotation before every roll.")]
        [SerializeField] private Transform spawnPoint;

        [Header("Player Turn Management")]
        [Tooltip("Legacy standalone mode. When OFF (the normal setup), this die only resolves a value and " +
                 "raises OnDiceRollCompleted - a LudoBoardLogic listener owns all turn/movement resolution. " +
                 "Turn ON only if you want this die to move 'activePlayer' directly and cycle 'turnOrder' " +
                 "itself, with no LudoBoardLogic present (otherwise every roll would resolve twice).")]
        [SerializeField] private bool driveActivePlayerDirectly = false;
        [Tooltip("The PlayerController whose token moves when the dice settles. Left empty, a roll still " +
                 "resolves a value but no token moves. Only used when 'Drive Active Player Directly' is on.")]
        [SerializeField] private PlayerController activePlayer;
        [Tooltip("Optional turn sequence. When a roll is not a 6, 'activePlayer' automatically advances to " +
                 "the next entry in this list (wrapping around). Leave empty to manage turns externally.")]
        [SerializeField] private PlayerController[] turnOrder;

        /// <summary>Raised the moment a physical roll begins.</summary>
        public event Action OnDiceRollStarted;

        /// <summary>Raised once the dice has settled, with the resolved face value (1-6).</summary>
        public event Action<int> OnDiceRollCompleted;

        /// <summary>Raised whenever the turn passes to a new active player.</summary>
        public event Action<PlayerController> OnActivePlayerChanged;

        public bool IsRolling { get; private set; }
        public int LastRolledValue { get; private set; } = 1;

        /// <summary>The player whose token will move on the next resolved roll.</summary>
        public PlayerController ActivePlayer => activePlayer;

        /// <summary>Assigns whose turn it is. Pass null to disable movement until a player is assigned again.</summary>
        public void SetActivePlayer(PlayerController player)
        {
            activePlayer = player;
            OnActivePlayerChanged?.Invoke(activePlayer);
        }

        private Rigidbody _rigidbody;
        private Coroutine _rollRoutine;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody == null)
            {
                Debug.LogError($"[LudoDiceRoller] No Rigidbody found on '{name}'. This component requires one.", this);
                return;
            }

            if (faceNormals == null || faceNormals.Length != 6)
            {
                Debug.LogWarning($"[LudoDiceRoller] faceNormals on '{name}' must contain exactly 6 entries. " +
                                  "Resetting to default cube mapping.", this);
                faceNormals = new[] { Vector3.up, Vector3.right, Vector3.forward, Vector3.back, Vector3.left, Vector3.down };
            }
        }

        /// <summary>
        /// Launches the dice with random force/torque and resolves the outcome via physics.
        /// Safe to call repeatedly; ignored while a roll is already in progress.
        /// </summary>
        public void Roll()
        {
            if (IsRolling)
            {
                return;
            }

            if (_rigidbody == null)
            {
                Debug.LogError($"[LudoDiceRoller] Cannot roll '{name}': missing Rigidbody.", this);
                return;
            }

            if (_rollRoutine != null)
            {
                StopCoroutine(_rollRoutine);
            }

            _rollRoutine = StartCoroutine(RollRoutine());
        }

        /// <summary>
        /// Bypasses physics entirely and reports a specific value immediately.
        /// Useful for AI "instant" turns, automated tests, or a "skip animation" setting.
        /// </summary>
        public void RollWithForcedValue(int forcedValue)
        {
            forcedValue = Mathf.Clamp(forcedValue, 1, 6);

            if (IsRolling && _rollRoutine != null)
            {
                StopCoroutine(_rollRoutine);
                IsRolling = false;
            }

            OnDiceRollStarted?.Invoke();
            CompleteRoll(forcedValue);
        }

        private IEnumerator RollRoutine()
        {
            IsRolling = true;
            OnDiceRollStarted?.Invoke();

            ResetToSpawn();
            ApplyRandomImpulse();

            float stillTime = 0f;
            float elapsed = 0f;

            var wait = new WaitForFixedUpdate();
            while (elapsed < maxRollDuration)
            {
                yield return wait;
                elapsed += Time.fixedDeltaTime;

                if (_rigidbody == null)
                {
                    break;
                }

                bool isStill = _rigidbody.linearVelocity.magnitude <= linearVelocitySleepThreshold &&
                               _rigidbody.angularVelocity.magnitude <= angularVelocitySleepThreshold;

                stillTime = isStill ? stillTime + Time.fixedDeltaTime : 0f;

                if (stillTime >= requiredStillDuration)
                {
                    break;
                }
            }

            int value = ResolveFaceValue();
            IsRolling = false;
            _rollRoutine = null;

            CompleteRoll(value);
        }

        /// <summary>Shared tail-end for both a physical roll and <see cref="RollWithForcedValue"/>: records the
        /// value, notifies listeners, then triggers the active player's movement (see <see cref="TryMoveActivePlayer"/>).</summary>
        private void CompleteRoll(int value)
        {
            LastRolledValue = value;
            OnDiceRollCompleted?.Invoke(value);
            TryMoveActivePlayer(value);
        }

        /// <summary>
        /// Moves <see cref="activePlayer"/> by the resolved dice value, guarded so a roll never triggers
        /// movement with no player assigned or while that player's token is already mid-move. Once the
        /// move finishes, the turn automatically passes to the next player in <see cref="turnOrder"/>
        /// unless the roll was a 6 (classic Ludo "roll a 6, go again" rule).
        /// </summary>
        private void TryMoveActivePlayer(int diceValue)
        {
            if (!driveActivePlayerDirectly)
            {
                // Normal setup: a LudoBoardLogic listening to OnDiceRollCompleted owns turn/movement
                // resolution. Doing anything here as well would move a token (and advance the turn) twice.
                return;
            }

            if (activePlayer == null)
            {
                Debug.LogWarning($"[LudoDiceRoller] Rolled a {diceValue} but no 'activePlayer' is assigned; skipping movement.", this);
                return;
            }

            if (activePlayer.IsMoving)
            {
                Debug.LogWarning($"[LudoDiceRoller] Rolled a {diceValue} but '{activePlayer.name}' is already moving; skipping movement.", this);
                return;
            }

            activePlayer.MoveSteps(diceValue, () => HandleActivePlayerMoveFinished(diceValue));
        }

        private void HandleActivePlayerMoveFinished(int diceValue)
        {
            if (diceValue != 6)
            {
                AdvanceToNextPlayer();
            }
            // A 6 grants an extra turn: activePlayer stays the same and simply rolls again.
        }

        /// <summary>Passes the turn to whichever entry follows <see cref="activePlayer"/> in <see cref="turnOrder"/>,
        /// wrapping back to the start. No-op if <see cref="turnOrder"/> is empty (turns are managed externally).</summary>
        private void AdvanceToNextPlayer()
        {
            if (turnOrder == null || turnOrder.Length == 0)
            {
                return;
            }

            int currentIndex = Array.IndexOf(turnOrder, activePlayer);
            int nextIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % turnOrder.Length;
            SetActivePlayer(turnOrder[nextIndex]);
        }

        private void ResetToSpawn()
        {
            if (spawnPoint == null || _rigidbody == null)
            {
                return;
            }

            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.position = spawnPoint.position;
            _rigidbody.rotation = spawnPoint.rotation;
        }

        private void ApplyRandomImpulse()
        {
            if (_rigidbody == null)
            {
                return;
            }

            Vector3 randomDir = UnityEngine.Random.insideUnitSphere;
            if (launchDirectionBias.sqrMagnitude > 0.0001f)
            {
                randomDir = (randomDir + launchDirectionBias).normalized;
            }

            float force = UnityEngine.Random.Range(minForce, maxForce);
            Vector3 torque = new Vector3(
                UnityEngine.Random.Range(minTorque, maxTorque) * RandomSign(),
                UnityEngine.Random.Range(minTorque, maxTorque) * RandomSign(),
                UnityEngine.Random.Range(minTorque, maxTorque) * RandomSign());

            _rigidbody.AddForce(randomDir * force, ForceMode.Impulse);
            _rigidbody.AddTorque(torque, ForceMode.Impulse);
        }

        private static float RandomSign()
        {
            return UnityEngine.Random.value < 0.5f ? -1f : 1f;
        }

        /// <summary>
        /// Determines which face is pointing up by comparing each face normal
        /// (transformed into world space) against world-up, and picking the best match.
        /// Falls back to a uniform random value if the dice state is invalid.
        /// </summary>
        private int ResolveFaceValue()
        {
            if (transform == null || faceNormals == null || faceNormals.Length != 6)
            {
                return UnityEngine.Random.Range(1, 7);
            }

            int bestIndex = 0;
            float bestDot = float.NegativeInfinity;

            for (int i = 0; i < faceNormals.Length; i++)
            {
                Vector3 worldNormal = transform.TransformDirection(faceNormals[i].normalized);
                float dot = Vector3.Dot(worldNormal, Vector3.up);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestIndex = i;
                }
            }

            return bestIndex + 1;
        }
    }
}
