using System;
using System.Collections.Generic;
using LudoGame.Board;
using UnityEngine;

namespace LudoGame.MiniGames.KillaBandar
{
    public enum KillaBandarState
    {
        /// <summary>Normal play: attackers try to steal shoes, protector defends/tags.</summary>
        Defending,
        /// <summary>All shoes stolen: attackers may strike/throw at the protector.</summary>
        AttackerStrike,
        /// <summary>Protector is sprinting for the Milestone Target.</summary>
        MilestoneSprint,
    }

    /// <summary>
    /// Orchestrates one continuous Killa Bandar session: tracks who's
    /// Protector, watches for shoe steals and tags, and handles the
    /// Milestone Sprint escape. No fixed round timer - it runs until a
    /// role swap happens or the player exits back to the Ludo board.
    ///
    /// Setup (in the Unity Editor):
    /// 1. Put this on an empty "KillaBandarGameManager" GameObject.
    /// 2. Drag in the ShoePile, RopeConstraint, the Killa stake and
    ///    Milestone Target Transforms, and every KillaBandarPlayer in the scene.
    /// 3. Call AssignRoles(startingProtector) once to begin a session.
    /// 4. Call BeginMilestoneSprint() (e.g. from a "Sprint" UI button) when
    ///    the Protector attempts the Milestone escape.
    /// </summary>
    public class KillaBandarGameManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ShoePile shoePile;
        [SerializeField] private RopeConstraint ropeConstraint;
        [SerializeField] private Transform killaStake;
        [SerializeField] private Transform milestoneTarget;
        [SerializeField] private List<KillaBandarPlayer> players = new List<KillaBandarPlayer>();

        [Header("Starting Protector (optional - auto-begins the session on Start)")]
        [SerializeField] private KillaBandarPlayer startingProtector;

        [Header("Rules")]
        [Tooltip("How close the Protector must get to an Attacker to tag them.")]
        [SerializeField] private float tagRadius = 1f;
        [Tooltip("How close the Protector must get to the Milestone Target to complete a sprint.")]
        [SerializeField] private float milestoneRadius = 1f;

        public KillaBandarState State { get; private set; } = KillaBandarState.Defending;
        public KillaBandarPlayer Protector { get; private set; }

        /// <summary>Raised whenever the state machine changes state.</summary>
        public event Action<KillaBandarState> OnStateChanged;

        /// <summary>Raised with the new Protector's color whenever a role swap happens.</summary>
        public event Action<GridManager.PlayerColor> OnRoleSwapped;

        /// <summary>Raised whenever the shoe count changes.</summary>
        public event Action<int> OnShoesChanged;

        /// <summary>Raised when a Milestone Sprint succeeds: shoes reset, same Protector, round continues.</summary>
        public event Action OnMilestoneSprintSucceeded;

        private void OnEnable()
        {
            if (shoePile != null)
            {
                shoePile.OnShoesChanged += HandleShoesChanged;
                shoePile.OnAllShoesStolen += HandleAllShoesStolen;
            }
        }

        private void OnDisable()
        {
            if (shoePile != null)
            {
                shoePile.OnShoesChanged -= HandleShoesChanged;
                shoePile.OnAllShoesStolen -= HandleAllShoesStolen;
            }
        }

        /// <summary>Starts (or restarts) a session with the given player as Protector. Clears all tags.</summary>
        public void AssignRoles(KillaBandarPlayer protector)
        {
            Protector = protector;
            foreach (KillaBandarPlayer player in players)
                player.SetRole(player == protector ? KillaBandarRole.Protector : KillaBandarRole.Attacker);

            if (ropeConstraint != null) ropeConstraint.SetProtector(protector.transform);

            SetState(KillaBandarState.Defending);
        }

        private void Start()
        {
            if (Protector == null && startingProtector != null)
                AssignRoles(startingProtector);
        }

        private void Update()
        {
            if (Protector == null) return;

            switch (State)
            {
                case KillaBandarState.Defending:
                case KillaBandarState.AttackerStrike:
                    TryStealsAndTags();
                    break;
                case KillaBandarState.MilestoneSprint:
                    TryCatchDuringSprint();
                    CheckMilestoneReached();
                    break;
            }
        }

        private void TryStealsAndTags()
        {
            foreach (KillaBandarPlayer player in players)
            {
                if (player == Protector || player.IsTagged) continue;

                // A tag resolves the whole exchange for this frame - stop checking further players.
                if (Vector3.Distance(player.transform.position, Protector.transform.position) <= tagRadius)
                {
                    HandleTag(player);
                    return;
                }

                if (State == KillaBandarState.Defending && shoePile != null && shoePile.IsInStealRange(player.transform.position))
                    shoePile.TrySteal();
            }
        }

        private void HandleTag(KillaBandarPlayer taggedAttacker)
        {
            taggedAttacker.MarkTagged();

            // Condition A: tagging an attacker while shoes remain -> immediate role swap.
            // If shoes are already at 0 (Attacker Strike), tagging doesn't save the Protector -
            // per the design, only a successful Milestone Sprint can save them at that point.
            if (shoePile == null || shoePile.ShoesRemaining > 0)
            {
                OnRoleSwapped?.Invoke(taggedAttacker.Color);
                AssignRoles(taggedAttacker);
            }
        }

        private void HandleAllShoesStolen()
        {
            SetState(KillaBandarState.AttackerStrike);
        }

        /// <summary>Call this (e.g. from a "Sprint" UI button) when the Protector attempts the Milestone escape.</summary>
        public void BeginMilestoneSprint()
        {
            if (State == KillaBandarState.MilestoneSprint) return;
            SetState(KillaBandarState.MilestoneSprint);
        }

        private void TryCatchDuringSprint()
        {
            foreach (KillaBandarPlayer player in players)
            {
                if (player == Protector || player.IsTagged) continue;
                if (Vector3.Distance(player.transform.position, Protector.transform.position) > tagRadius) continue;

                // Getting caught during the sprint always swaps roles - shoe count no longer matters,
                // since the sprint is the Protector's one lifeline once shoes are critical/empty.
                player.MarkTagged();
                OnRoleSwapped?.Invoke(player.Color);
                AssignRoles(player);
                return;
            }
        }

        private void CheckMilestoneReached()
        {
            if (milestoneTarget == null || Protector == null) return;
            if (Vector3.Distance(Protector.transform.position, milestoneTarget.position) > milestoneRadius) return;

            // Success Effect: no role swap. Shoes reset, round restarts with the same Protector.
            shoePile?.ResetShoes();
            OnMilestoneSprintSucceeded?.Invoke();
            AssignRoles(Protector); // clears tags and returns to Defending
        }

        private void HandleShoesChanged(int amount) => OnShoesChanged?.Invoke(amount);

        private void SetState(KillaBandarState state)
        {
            State = state;
            OnStateChanged?.Invoke(state);
        }
    }
}
