using System;
using UnityEngine;

namespace LudoGame.ThirdPersonGame
{
    /// <summary>
    /// Tracks the player's health, and switches on the Defeated animation (via
    /// PlayerAnimatorController) the moment it reaches 0.
    ///
    /// Setup (in the Unity Editor):
    /// 1. Put this on the same GameObject as PlayerAnimatorController.
    /// 2. Call TakeDamage(amount) from whatever can hurt the player (an enemy, a hazard).
    /// 3. Drag this into HealthBarUI's source, or call HealthBarUI.SetHealth(...) from
    ///    OnHealthChanged yourself if you prefer manual wiring.
    /// </summary>
    [RequireComponent(typeof(PlayerAnimatorController))]
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 100;

        public int MaxHealth => maxHealth;
        public int CurrentHealth { get; private set; }
        public bool IsDefeated { get; private set; }

        /// <summary>Raised whenever health changes, with (current, max) - HealthBarUI listens to this.</summary>
        public event Action<int, int> OnHealthChanged;

        /// <summary>Raised once, the moment health reaches 0.</summary>
        public event Action OnDefeated;

        private PlayerAnimatorController animatorController;

        private void Awake()
        {
            animatorController = GetComponent<PlayerAnimatorController>();
            CurrentHealth = maxHealth;
        }

        private void Start()
        {
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth); // let the UI show the starting value
        }

        public void TakeDamage(int amount)
        {
            if (IsDefeated || amount <= 0) return;
            SetHealth(CurrentHealth - amount);
        }

        public void Heal(int amount)
        {
            if (IsDefeated || amount <= 0) return;
            SetHealth(CurrentHealth + amount);
        }

        /// <summary>Restores full health and clears Defeated - call this to start a fresh attempt.</summary>
        public void ResetHealth()
        {
            IsDefeated = false;
            animatorController.SetDefeated(false);
            SetHealth(maxHealth);
        }

        private void SetHealth(int value)
        {
            CurrentHealth = Mathf.Clamp(value, 0, maxHealth);
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

            if (CurrentHealth <= 0 && !IsDefeated)
            {
                IsDefeated = true;
                animatorController.SetDefeated(true);
                OnDefeated?.Invoke();
            }
        }
    }
}
