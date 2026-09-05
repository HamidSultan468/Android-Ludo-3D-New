using System;
using UnityEngine;
using TangentLudoEmpire.Services;
using TangentLudoEmpire.Wallet;

namespace TangentLudoEmpire.Payments
{
    /// <summary>
    /// Phase 3.5: the mobile-real path that <see cref="PaymentCallbackListener"/> (Editor/Standalone
    /// localhost only - see its class remarks) can't cover. The backend, after verifying a gateway's
    /// webhook (see <c>BACKEND_WEBHOOK_SPEC.md</c>), bounces the user's browser to
    /// <c>&lt;DeepLinkScheme&gt;://callback?orderId=...&amp;status=...</c>, which Android hands to this
    /// app via the intent-filter in <c>Assets/Plugins/Android/AndroidManifest.xml</c>.
    ///
    /// SECURITY - "never trust the client" still applies to a deep link: another app could in principle
    /// register the same custom scheme and fire a forged <c>tle://callback?orderId=X&amp;status=000</c>
    /// straight at this app to try to claim a fake deposit. That can't actually credit anything on its
    /// own - <see cref="MoneyWallet.ConfirmPendingDeposit(string,Action{bool})"/> still runs the SAME
    /// anti-cheat payment gate every other credit path uses
    /// (<see cref="TangentLudoEmpire.Services.AntiCheatManager.ValidatePayment"/>), which in production
    /// asks the backend to confirm that specific order was actually paid before a single unit moves. The
    /// deep link is a NOTIFICATION to go check, never proof by itself.
    /// </summary>
    [DisallowMultipleComponent]
    public class DeepLinkManager : MonoBehaviour
    {
        public static DeepLinkManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject); // Editor/test code (headless tools) would otherwise hit InvalidOperationException here and abort the rest of Awake()

            Application.deepLinkActivated += OnDeepLink;
        }

        private void Start()
        {
            // Cold start: the app may have been LAUNCHED by the deep link, in which case
            // deepLinkActivated can fire before this component's Awake finished subscribing (or not
            // fire at all on some platform/Unity-version combinations) - Application.absoluteURL still
            // carries the launch URL in that case. Standard belt-and-suspenders pattern for Unity deep
            // linking; a no-op on a normal (non-deep-link) launch, where absoluteURL is empty.
            if (!string.IsNullOrEmpty(Application.absoluteURL))
                OnDeepLink(Application.absoluteURL);
        }

        private void OnDestroy()
        {
            if (Instance == this) Application.deepLinkActivated -= OnDeepLink;
        }

        /// <summary>Public so <c>Phase3Tools</c>' "Test DeepLink" menu item can drive it directly with a
        /// synthetic URL, without needing a real OS-level Intent (which can't be simulated headlessly).</summary>
        public void OnDeepLink(string url)
        {
            if (string.IsNullOrEmpty(url)) return;

            Uri uri;
            try { uri = new Uri(url); }
            catch (Exception e) { Debug.LogWarning($"[DeepLinkManager] Not a valid URL: {url} ({e.Message})"); return; }

            var cfg = PaymentConfig.Load();
            bool schemeMatches = string.Equals(uri.Scheme, cfg.DeepLinkScheme, StringComparison.OrdinalIgnoreCase);
            bool hostMatches = string.Equals(uri.Host, "callback", StringComparison.OrdinalIgnoreCase);
            if (!schemeMatches || !hostMatches)
            {
                Debug.Log($"[DeepLinkManager] Ignoring unrelated deep link: {url}");
                return;
            }

            var data = PaymentCallbackListener.ParseQuery(uri.Query);
            string orderId = data.TryGetValue("orderId", out var o) ? o : "";
            string status = data.TryGetValue("status", out var s) ? s : "";
            bool success = status == "000"; // backend normalises both gateways to this one sentinel - see BACKEND_WEBHOOK_SPEC.md

            if (string.IsNullOrEmpty(orderId))
            {
                Debug.LogWarning($"[DeepLinkManager] Deep link missing orderId: {url}");
                return;
            }

            Debug.Log("DEEP LINK RECEIVED. Crediting...");
            AuditLog.Log("DEEPLINK_RECEIVED", orderId, $"status={status}");

            var webView = WebViewManager.Instance;
            if (webView != null && webView.HasPendingFlow(orderId))
            {
                // App stayed alive through the whole browser round trip - resolve through the same path
                // PaymentCallbackListener uses, so the gateway's own Deposit() callback (and therefore
                // DepositPopup's UI) fires exactly once, correctly.
                string txnId = data.TryGetValue("txnId", out var t) ? t : orderId;
                webView.ResolvePayment(orderId, success, txnId, 0m);
                return;
            }

            // Cold start (or the app simply outlived the WebViewManager's in-memory flow) - nothing is
            // waiting on a UI callback, so confirm straight against the wallet's durable Pending row.
            var wallet = MoneyWallet.Instance;
            if (wallet == null)
            {
                Debug.LogWarning("[DeepLinkManager] MoneyWallet not available yet - cannot resolve this deep link.");
                return;
            }

            if (success) wallet.ConfirmPendingDeposit(orderId, credited =>
                Debug.Log(credited ? $"[DeepLinkManager] {orderId} confirmed and credited."
                                    : $"[DeepLinkManager] {orderId} confirm denied (see AuditLog)."));
            else wallet.FailPendingDeposit(orderId, "DEEPLINK_FAILED");
        }
    }
}
