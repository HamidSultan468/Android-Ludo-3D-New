using System;
using UnityEngine;

namespace LudoGame.MiniGames.KillaBandar
{
    /// <summary>The pile of shoes guarded at the Killa (central stake).</summary>
    public class ShoePile : MonoBehaviour
    {
        [SerializeField] private int startingShoeCount = 6;
        [Tooltip("How close an Attacker must be to the Killa to attempt a steal.")]
        [SerializeField] private float stealRadius = 1.5f;
        [SerializeField] private Transform killaStake;

        public int ShoesRemaining { get; private set; }

        /// <summary>Raised whenever the shoe count changes, with the new total.</summary>
        public event Action<int> OnShoesChanged;

        /// <summary>Raised once, the moment the pile hits zero (triggers Attacker Strike state).</summary>
        public event Action OnAllShoesStolen;

        private void Awake()
        {
            ShoesRemaining = startingShoeCount;
        }

        /// <summary>True if the given position is close enough to the Killa to attempt a steal.</summary>
        public bool IsInStealRange(Vector3 position)
        {
            if (killaStake == null) return false;
            return Vector3.Distance(position, killaStake.position) <= stealRadius;
        }

        /// <summary>Tries to steal one shoe. Returns true if a shoe was taken.</summary>
        public bool TrySteal()
        {
            if (ShoesRemaining <= 0) return false;

            ShoesRemaining--;
            OnShoesChanged?.Invoke(ShoesRemaining);

            if (ShoesRemaining == 0)
                OnAllShoesStolen?.Invoke();

            return true;
        }

        /// <summary>Resets the pile to its starting count (e.g. after a successful Milestone Sprint).</summary>
        public void ResetShoes()
        {
            ShoesRemaining = startingShoeCount;
            OnShoesChanged?.Invoke(ShoesRemaining);
        }
    }
}
