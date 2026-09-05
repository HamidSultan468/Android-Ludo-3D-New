using System.Collections;
using UnityEngine;
using LudoEmpire.Ludo; // FlowManager + FlowScreen live here (existing flow layer).

namespace TangentLudoEmpire.Core
{
    /// <summary>
    /// Root bootstrap for the Tangent ecosystem. First thing alive in the app: it spawns the other
    /// manager singletons if they don't already exist, then (on the Boot scene only) holds for a beat
    /// and routes to the Dashboard.
    ///
    /// Self-installs via <see cref="RuntimeInitializeOnLoadMethod"/> so the managers exist even when a
    /// gameplay scene is opened directly in the Editor - but it will not auto-navigate unless it is the
    /// Boot scene's own configured instance (<see cref="autoGoToDashboardOnBoot"/>).
    /// </summary>
    [DefaultExecutionOrder(-300)]
    [DisallowMultipleComponent]
    public class GameServices : MonoBehaviour
    {
        public static GameServices Instance { get; private set; }

        [Header("Boot")]
        [Tooltip("ON for the GameServices object placed in Boot.unity. OFF for the auto-spawned fallback.")]
        [SerializeField] private bool autoGoToDashboardOnBoot = false;
        [SerializeField] private float bootHoldSeconds = AppConstants.BOOT_HOLD_SECONDS;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<GameServices>() != null) return; // a scene-placed one will Awake itself
            new GameObject("GameServices (auto)").AddComponent<GameServices>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject); // Editor/test code (headless tools) would otherwise hit InvalidOperationException here and abort the rest of Awake()

            EnsureManagers();

            if (autoGoToDashboardOnBoot)
                StartCoroutine(BootRoutine());
        }

        /// <summary>Creates the sibling managers if they aren't in the scene already. Order matters:
        /// they Awake in call order (AddComponent is synchronous), and later ones depend on earlier ones.
        /// Each manager puts itself under DontDestroyOnLoad in its own Awake, so they stay root objects.</summary>
        private void EnsureManagers()
        {
            // Phase 2 security core first - SaveService reads through SecurityManager.
            Ensure<TangentLudoEmpire.Services.BackendService>(); // no deps
            Ensure<TangentLudoEmpire.Services.AuditLog>();       // no deps; SecurityManager logs into it
            Ensure<SecurityManager>();                 // uses AuditLog
            Ensure<TangentLudoEmpire.Services.AntiCheatManager>();

            // Phase 1 services.
            Ensure<SaveService>();
            Ensure<WalletManager>();

            // Phase 3 real-money wallet - after SaveService (it loads tle_wallet_v1.enc through it),
            // before the Dashboard so a balance is ready to show. GameWalletConnector right after
            // MoneyWallet so its OnEnable subscription binds on the very first tick (Phase 3.4).
            Ensure<TangentLudoEmpire.Wallet.MoneyWallet>();
            Ensure<TangentLudoEmpire.Bridge.GameWalletConnector>();

            // Phase 3.2/3.5 payment plumbing. BUGFIX (found while building Phase 3.5): these were never
            // added here, so WebViewManager.Instance / PaymentCallbackListener.Instance were permanently
            // null unless a scene happened to have one placed - every WebView deposit would have thrown a
            // NullReferenceException the moment JazzCashGateway/EasypaisaGateway.Deposit tried to open one.
            Ensure<TangentLudoEmpire.Payments.WebViewManager>();
            Ensure<TangentLudoEmpire.Payments.PaymentCallbackListener>();
            Ensure<TangentLudoEmpire.Payments.DeepLinkManager>();

            // Phase 3.3: KYC gate + rate/amount limiting for MoneyWallet.RequestWithdraw.
            Ensure<TangentLudoEmpire.Kyc.KycManager>();
            Ensure<TangentLudoEmpire.Services.AntiFraudManager>();

            // Phase 4: online rooms, tournaments, leaderboard/referral/history.
            Ensure<TangentLudoEmpire.Multiplayer.RoomManager>();
            Ensure<TangentLudoEmpire.Tournament.TournamentManager>();
            Ensure<TangentLudoEmpire.Social.GameHistoryManager>();  // before LeaderboardManager - it reads windowed history
            Ensure<TangentLudoEmpire.Social.LeaderboardManager>();
            Ensure<TangentLudoEmpire.Social.ReferralManager>();

            Ensure<SoundManager>();
            Ensure<FlowManager>(); // also self-bootstraps, but make the dependency explicit here

            // Phase 5.1/5.2: gameplay anti-cheat + mock Firebase. AntiCheatManager's own Awake subscribes
            // WalletSecurityBridge to WalletGameBridge.OnSuspiciousActivity; AnalyticsBridge/CrashlyticsManager
            // are static and need an explicit Ensure-style call instead of a MonoBehaviour Awake.
            Ensure<TangentLudoEmpire.Security.GameplayAntiCheatManager>();
            Ensure<TangentLudoEmpire.Analytics.FirebaseManager>();
            TangentLudoEmpire.Analytics.AnalyticsBridge.EnsureSubscribed();
            TangentLudoEmpire.Analytics.CrashlyticsManager.EnsureHooked();
            TangentLudoEmpire.Polish.SettingsManager.PushToSoundManager(); // apply saved volume prefs now that SoundManager exists

            Debug.Log("[GameServices] Managers ready: BackendService, SecurityManager, AuditLog, AntiCheatManager, " +
                      "SaveService, WalletManager, MoneyWallet, GameWalletConnector, WebViewManager, " +
                      "PaymentCallbackListener, DeepLinkManager, KycManager, AntiFraudManager, RoomManager, " +
                      "TournamentManager, GameHistoryManager, LeaderboardManager, ReferralManager, SoundManager, FlowManager, " +
                      "GameplayAntiCheatManager, FirebaseManager, AnalyticsBridge, CrashlyticsManager.");
        }

        private static void Ensure<T>() where T : MonoBehaviour
        {
            if (FindAnyObjectByType<T>() != null) return;
            new GameObject(typeof(T).Name).AddComponent<T>();
        }

        private IEnumerator BootRoutine()
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, bootHoldSeconds));
            Debug.Log("[GameServices] Boot hold done -> Dashboard.");
            FlowManager.Instance.GoTo(FlowScreen.Dashboard);
        }
    }
}
