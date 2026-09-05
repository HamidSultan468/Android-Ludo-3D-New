using UnityEngine;

namespace LudoGame.ThirdPersonGame
{
    /// <summary>
    /// Drives the player's Animator based on movement (from PlayerMovement) and game
    /// actions (praying, defeat). Keeps all animation-parameter names in one place, so
    /// nothing else in the project needs to know the exact Animator parameter strings.
    ///
    /// Setup (in the Unity Editor):
    /// 1. Put this on the same GameObject as PlayerMovement and the Animator component.
    /// 2. Make sure the Animator's Controller has these parameters (see the Animator
    ///    setup steps): a Bool "IsMoving", a Trigger "Pray", and a Bool "Defeated".
    /// 3. Call TriggerPray() whenever the player performs the prayer action (e.g. from
    ///    an interaction script or a UI button).
    /// 4. Call SetDefeated(true) from your Gameplay Logic (see Phase 8) when the player
    ///    loses, and SetDefeated(false) when a new match/level starts.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimatorController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Leave empty to auto-find PlayerMovement on this same GameObject.")]
        [SerializeField] private PlayerMovement playerMovement;

        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int PrayHash = Animator.StringToHash("Pray");
        private static readonly int DefeatedHash = Animator.StringToHash("Defeated");

        private Animator animator;

        public bool IsDefeated { get; private set; }

        private void Awake()
        {
            animator = GetComponent<Animator>();
            if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
        }

        private void Update()
        {
            // Once defeated, movement no longer drives the Idle/Walking blend - the
            // Defeated animation (via "Any State -> Defeated") takes over regardless.
            if (IsDefeated) return;

            bool isMoving = playerMovement != null && playerMovement.IsMoving;
            animator.SetBool(IsMovingHash, isMoving);
        }

        /// <summary>Plays the Praying animation once. Hook this to whatever triggers prayer (input, an interaction zone, etc.).</summary>
        public void TriggerPray()
        {
            if (IsDefeated) return;
            animator.SetTrigger(PrayHash);
            AudioManager.Instance?.PlayPraySound();
        }

        /// <summary>Switches the Defeated animation on/off. Call with true when the player loses, false to reset for a new attempt.</summary>
        public void SetDefeated(bool defeated)
        {
            IsDefeated = defeated;
            animator.SetBool(DefeatedHash, defeated);

            if (defeated)
            {
                animator.SetBool(IsMovingHash, false); // freeze the movement blend while defeated
                AudioManager.Instance?.PlayDefeatedSound();
            }
        }
    }
}
