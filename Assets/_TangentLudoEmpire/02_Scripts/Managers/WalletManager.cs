using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using LudoEmpire.Ludo; // CurrencyManager lives here - we only *call* it, never modify it.

namespace TangentLudoEmpire.Core
{
    /// <summary>
    /// Thin façade over the existing <see cref="CurrencyManager"/>. Phase 1 rules:
    ///   * How coins are EARNED is unchanged - <see cref="CurrencyManager"/> still owns AddCoins/TrySpendCoins.
    ///   * How coins are SAVED moves here: every <see cref="CurrencyManager.OnCoinsChanged"/> is mirrored
    ///     into the JSON profile via <see cref="SaveService"/>, and on first contact the profile's balance
    ///     is pushed back into <see cref="CurrencyManager"/> (its <c>SetBalance</c> = "cloud-save restore")
    ///     so the JSON file is the source of truth going forward.
    ///
    /// <see cref="CurrencyManager"/> is created by the board scene, not the menu, so this binds lazily on
    /// every scene load.
    /// </summary>
    [DisallowMultipleComponent]
    public class WalletManager : MonoBehaviour
    {
        public static WalletManager Instance { get; private set; }

        private bool _bound;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject); // Editor/test code (headless tools) would otherwise hit InvalidOperationException here and abort the rest of Awake()
            SceneManager.sceneLoaded += HandleSceneLoaded;
            TryBind();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            var cm = CurrencyManager.Instance;
            if (cm != null) cm.OnCoinsChanged -= HandleCoinsChanged;
        }

        private void HandleSceneLoaded(Scene s, LoadSceneMode m) => TryBind();

        /// <summary>Hook CurrencyManager once it exists: push the saved balance in, then mirror changes out.</summary>
        private void TryBind()
        {
            if (_bound) return;
            var cm = CurrencyManager.Instance;
            if (cm == null) return;

            long saved = SaveService.Instance != null ? SaveService.Instance.Profile.coins : cm.Coins;
            if (saved != cm.Coins)
            {
                Debug.Log($"[WalletManager] Restoring saved balance {saved} into CurrencyManager (was {cm.Coins}).");
                cm.SetBalance(saved); // public "cloud-save restore" API - no modification to CurrencyManager
            }

            cm.OnCoinsChanged += HandleCoinsChanged;
            _bound = true;
            Debug.Log("[WalletManager] Bound to CurrencyManager.");
        }

        private void HandleCoinsChanged(long newBalance)
        {
            SaveService.Instance?.SetCoins(newBalance); // persist via JSON profile
        }

        // ---- public façade (delegates to CurrencyManager; safe no-ops if it isn't loaded yet) ----

        public long GetCoins() => CurrencyManager.Instance != null
            ? CurrencyManager.Instance.Coins
            : (SaveService.Instance != null ? SaveService.Instance.Profile.coins : 0);

        public void AddCoins(long amount, string reason)
        {
            Debug.Log($"[WalletManager] AddCoins({amount}, \"{reason}\")");
            if (amount <= 0) return;
            if (CurrencyManager.Instance != null) CurrencyManager.Instance.AddCoins(amount, reason);
            else Debug.LogWarning("[WalletManager] AddCoins before CurrencyManager exists - ignored.");
        }

        public bool SpendCoins(long amount, string reason)
        {
            Debug.Log($"[WalletManager] SpendCoins({amount}, \"{reason}\")");
            if (amount <= 0) return false;
            if (CurrencyManager.Instance != null) return CurrencyManager.Instance.TrySpendCoins(amount, reason);
            Debug.LogWarning("[WalletManager] SpendCoins before CurrencyManager exists - ignored.");
            return false;
        }
    }
}
