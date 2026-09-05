using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TangentLudoEmpire.Core;
using TangentLudoEmpire.Wallet;
using TangentLudoEmpire.Wallet.UI;

namespace TangentLudoEmpire.Bridge
{
    /// <summary>
    /// Task 2: the player-facing feedback layer between the wallet and the match. Spawned by
    /// GameServices right after MoneyWallet, so the normal bootstrap path binds immediately; also retries
    /// on every scene load in case a scene-placed instance Awakes before MoneyWallet does (same pattern
    /// as Core.WalletManager's TryBind).
    /// </summary>
    [DisallowMultipleComponent]
    public class GameWalletConnector : MonoBehaviour
    {
        public static GameWalletConnector Instance { get; private set; }

        private bool _bound;
        private GameObject _lowBalanceRoot;
        private Text _lowBalanceLabel;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject); // Editor/test code (headless tools) would otherwise hit InvalidOperationException here and abort the rest of Awake()
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            TryBind();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Unbind();
        }

        private void HandleSceneLoaded(Scene s, LoadSceneMode m) => TryBind();

        private void TryBind()
        {
            if (_bound) return;
            if (MoneyWallet.Instance == null) return;

            MoneyWallet.Instance.OnBalanceChanged += HandleBalanceChanged;
            WalletGameBridge.OnGameEntryRequested += HandleEntryRequested;
            WalletGameBridge.OnGameWinReported += HandleWinReported;
            _bound = true;
        }

        private void Unbind()
        {
            if (!_bound) return;
            if (MoneyWallet.Instance != null) MoneyWallet.Instance.OnBalanceChanged -= HandleBalanceChanged;
            WalletGameBridge.OnGameEntryRequested -= HandleEntryRequested;
            WalletGameBridge.OnGameWinReported -= HandleWinReported;
            _bound = false;
        }

        // ---- MoneyWallet.OnBalanceChanged (Task 2 OnEnable instruction): a generic hook point. WalletUI
        // already refreshes its own label off the same event, so there's nothing else owned here to
        // redraw - kept as an explicit subscription so anything added later has one obvious place to hook. ----
        private void HandleBalanceChanged(decimal newBalance) { }

        // Fires BEFORE MoneyWallet.DeductFunds runs (see WalletGameBridge.TryEnterGame), so the balance
        // read here is still the PRE-attempt balance - exactly the moment to decide "can they afford it".
        private void HandleEntryRequested(decimal fee)
        {
            decimal balance = MoneyWallet.Instance != null ? MoneyWallet.Instance.GetBalance() : 0m;
            if (balance < fee) ShowLowBalancePopup(fee - balance);
        }

        private void HandleWinReported(decimal amount) => ShowWinToast(amount);

        // ---------------------------------------------------------------- Task 2 functions ----

        /// <summary>Self-building "you're short by X" popup with a Deposit call-to-action into WalletUI.</summary>
        public void ShowLowBalancePopup(decimal needed)
        {
            if (_lowBalanceRoot == null) BuildLowBalancePopup();
            if (_lowBalanceLabel != null)
                _lowBalanceLabel.text = $"Insufficient balance.\nYou need {needed:0.00} more to join.";
            _lowBalanceRoot.SetActive(true);
        }

        /// <summary>Reuses the Phase 1 ToastManager - a win doesn't need its own popup, one line is enough.</summary>
        public void ShowWinToast(decimal amount) => ToastManager.Show($"You won {amount:0.00}!");

        private void CloseLowBalancePopup() { if (_lowBalanceRoot != null) _lowBalanceRoot.SetActive(false); }

        private void OpenDepositFromPopup()
        {
            CloseLowBalancePopup();
            var walletUi = FindAnyObjectByType<WalletUI>();
            if (walletUi == null) walletUi = new GameObject("WalletUI").AddComponent<WalletUI>();
            walletUi.OpenDeposit();
        }

        private void BuildLowBalancePopup()
        {
            var canvas = WalletUiKit.EnsureCanvas("WalletCanvas");
            _lowBalanceRoot = WalletUiKit.Container(canvas.transform, "LowBalancePopup", new Vector2(700, 420), Vector2.zero, WalletUiKit.Panel);
            WalletUiKit.Label(_lowBalanceRoot.transform, "LOW BALANCE", 38, new Vector2(640, 60), new Vector2(0, 140));
            _lowBalanceLabel = WalletUiKit.Label(_lowBalanceRoot.transform, "", 28, new Vector2(640, 100), new Vector2(0, 30));
            WalletUiKit.Btn(_lowBalanceRoot.transform, "Deposit", new Vector2(300, 90), new Vector2(-160, -100), OpenDepositFromPopup);
            WalletUiKit.Btn(_lowBalanceRoot.transform, "Cancel", new Vector2(300, 90), new Vector2(160, -100), CloseLowBalancePopup,
                            new Color(0.3f, 0.35f, 0.42f, 1f));
            _lowBalanceRoot.SetActive(false);
        }
    }
}
