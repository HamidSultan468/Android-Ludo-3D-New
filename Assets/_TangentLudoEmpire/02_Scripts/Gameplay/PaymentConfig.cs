using UnityEngine;

namespace TangentLudoEmpire.Payments
{
    /// <summary>
    /// Sandbox/merchant credentials for the real payment gateways, authored as an asset (same pattern as
    /// <see cref="TangentLudoEmpire.Core.SecurityConfig"/> - nothing baked into code). Must live in a
    /// <b>Resources</b> folder (<c>Assets/_TangentLudoEmpire/Resources/PaymentConfig.asset</c>) so
    /// <see cref="Load"/> can find it; auto-created with sandbox placeholder values by
    /// <c>Tangent Ludo Empire/Phase 3/Test JazzCash Deposit 100</c> (or Easypaisa) if missing.
    ///
    /// SECURITY: these are the values <see cref="JazzCashGateway"/> / <see cref="EasypaisaGateway"/>
    /// are forbidden from ever writing into a log line - see their class remarks. Treat this asset like
    /// a secret: sandbox values are fine to commit, real merchant credentials are not.
    /// </summary>
    [CreateAssetMenu(fileName = "PaymentConfig", menuName = "Tangent Ludo Empire/Payment Config", order = 0)]
    public class PaymentConfig : ScriptableObject
    {
        [Header("JazzCash (sandbox)")]
        public string JazzCash_MerchantID = "";
        public string JazzCash_Password = "";
        public string JazzCash_IntegritySalt = "";
        [Tooltip("pp_ReturnURL for the MWALLET post-data. Not called by the Phase 3.1 mock.")]
        public string JazzCash_ReturnURL = "https://sandbox.jazzcash.com.pk/return";

        [Header("Easypaisa (sandbox)")]
        public string Easypaisa_StoreID = "";
        public string Easypaisa_StorePassword = "";
        [Tooltip("postBackURL for the checkout post-data. Not called by the Phase 3.1 mock.")]
        public string Easypaisa_PostBackURL = "https://sandbox.easypaisa.com.pk/postback";

        [Header("Environment")]
        [Tooltip("Task 7 go-live switch. false (default) FORCES sandbox regardless of anything else - " +
                 "this is the single source of truth for which base URLs the gateways build requests " +
                 "against; there is deliberately no separate 'UseSandbox' flag that could disagree with it.")]
        public bool IsProductionMode = false;

        /// <summary>Kept for any existing log lines / external code expecting this name - now a computed
        /// mirror of <see cref="IsProductionMode"/> rather than its own independently-editable field, so
        /// the two can never contradict each other.</summary>
        public bool UseSandbox => !IsProductionMode;

        [Tooltip("Real JazzCash MWALLET checkout endpoint - VERIFY against your merchant agreement / " +
                 "JazzCash integration docs before go-live, this default is a best-effort mirror of the " +
                 "sandbox host shape, not a value fetched from JazzCash's own current documentation.")]
        public string JazzCash_LiveUrl = "https://payments.jazzcash.com.pk/CustomerPortal/transactionmanagement/merchantform/";

        [Tooltip("Real Easypaisa checkout endpoint - VERIFY against your merchant agreement / Easypaisa " +
                 "integration docs before go-live, same caveat as JazzCash_LiveUrl.")]
        public string Easypaisa_LiveUrl = "https://easypay.easypaisa.com.pk/easypay/Index.jsf";

        [Header("Cloud webhook + deep link (Phase 3.5)")]
        [Tooltip("Your backend's public HTTPS base URL - see BACKEND_WEBHOOK_SPEC.md. Gateways POST/redirect " +
                 "to {WebhookBaseURL}/api/payment/callback. Left at the placeholder default, gateways fall back " +
                 "to the local PaymentCallbackListener for Editor/Standalone dev-loop testing.")]
        public string WebhookBaseURL = "https://your-api.tangentludo.com";

        [Tooltip("Custom URL scheme this app registers (AndroidManifest.xml intent-filter + iOS Info.plist). " +
                 "DeepLinkManager listens for <scheme>://callback?orderId=...&status=... " +
                 "Keep this in sync with the scheme hardcoded in Assets/Plugins/Android/AndroidManifest.xml.")]
        public string DeepLinkScheme = "tle";

        /// <summary>True once <see cref="WebhookBaseURL"/> has been changed off its placeholder default -
        /// gateways use this to decide whether to point pp_ReturnURL/postBackURL at the real cloud
        /// webhook or fall back to the local <see cref="PaymentCallbackListener"/> for dev-loop testing.</summary>
        public bool HasRealWebhook =>
            !string.IsNullOrEmpty(WebhookBaseURL) && !WebhookBaseURL.Contains("your-api.tangentludo.com");

        private static PaymentConfig _cached;

        /// <summary>Loads (and caches) the config from Resources. Returns a safe empty-string instance if
        /// the asset is missing so the game never hard-fails - a gateway built from it will fail its own
        /// secure-hash / merchant-id checks rather than silently using garbage credentials.</summary>
        public static PaymentConfig Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<PaymentConfig>("PaymentConfig");
            if (_cached == null)
            {
                Debug.LogWarning("[PaymentConfig] No Resources/PaymentConfig asset - gateways will run with " +
                                 "empty credentials. Run a 'Tangent Ludo Empire/Phase 3/Test <Gateway> Deposit' " +
                                 "menu item once to generate sandbox placeholders.");
                _cached = CreateInstance<PaymentConfig>();
            }
            return _cached;
        }
    }
}
