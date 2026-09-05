using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using TangentLudoEmpire.Core;
using TangentLudoEmpire.Services;
using TangentLudoEmpire.Wallet;

namespace TangentLudoEmpire.Payments
{
    /// <summary>
    /// Easypaisa Mobile Account (MA) checkout via <see cref="WebViewManager"/> - Phase 3.2 sandbox.
    /// See <see cref="JazzCashGateway"/>'s class remarks for the shared limitations (localhost callback
    /// is Editor/dev-loop only; a live POST-vs-GET nuance may need a local auto-submit relay page).
    ///
    /// SECURITY: <see cref="PaymentConfig.Easypaisa_StorePassword"/> feeds the hash, it is never logged.
    /// </summary>
    public class EasypaisaGateway : IPaymentGateway
    {
        public string GatewayName => "Easypaisa";

        private const string SandboxBaseUrl = "https://merchant.easypaisa.com.pk/easypay/Index.jsf";

        private readonly PaymentConfig _cfg;

        public EasypaisaGateway(PaymentConfig cfg) => _cfg = cfg ?? PaymentConfig.Load();

        public void Deposit(decimal amount, string phone, Action<bool, string> callback)
        {
            if (amount <= 0m) { callback?.Invoke(false, "INVALID_AMOUNT"); return; }
            if (string.IsNullOrWhiteSpace(phone)) { callback?.Invoke(false, "INVALID_PHONE"); return; }

            string orderId = MakeOrderId(); // TLE_{UserID}_{Ticks} - Task 5.3 anti-replay format
            string amountStr = decimal.Round(amount, 2).ToString(CultureInfo.InvariantCulture);

            var post = new Dictionary<string, string>
            {
                ["storeId"] = _cfg.Easypaisa_StoreID ?? "",
                ["amount"] = amountStr,
                ["orderId"] = orderId,
                // Phase 3.5: prefer the cloud webhook once PaymentConfig.WebhookBaseURL is a real value;
                // fall back to the local dev-loop listener until then (see PaymentConfig.HasRealWebhook).
                ["postBackURL"] = _cfg.HasRealWebhook
                    ? _cfg.WebhookBaseURL.TrimEnd('/') + "/api/payment/callback"
                    : PaymentCallbackListener.CallbackUrl,
                // Mobile: our backend redirects the browser here (appending ?orderId=&status=) after it
                // verifies the gateway's webhook - see BACKEND_WEBHOOK_SPEC.md. Android routes it to
                // DeepLinkManager via the intent-filter in Assets/Plugins/Android/AndroidManifest.xml.
                ["postBackURL_Backup"] = $"{_cfg.DeepLinkScheme}://callback",
            };
            string hash = GenerateSecureHash(post);
            post["easypaisaCallbackHash"] = hash; // carried along for parity with JazzCash's pp_SecureHash

            // Task 7 go-live switch: IsProductionMode=false FORCES sandbox regardless of anything else.
            string baseUrl = _cfg.IsProductionMode ? _cfg.Easypaisa_LiveUrl : SandboxBaseUrl;
            string url = BuildUrl(baseUrl, post);

            Debug.Log($"[EasypaisaGateway] Deposit {amount:0.00} PKR order={orderId} " +
                      $"store={_cfg.Easypaisa_StoreID} sandbox={_cfg.UseSandbox} hash={Mask(hash)}");

            var wallet = MoneyWallet.Instance;
            if (wallet == null) { callback?.Invoke(false, "WALLET_UNAVAILABLE"); return; }
            wallet.BeginPendingDeposit(orderId, amount, "DEPOSIT_" + GatewayName);
            AuditLog.Log("WEBVIEW_OPEN", GatewayName, orderId);

            WebViewManager.Instance.OpenPaymentUrl(url, orderId, (ok, txnOrErr) =>
            {
                if (!ok)
                {
                    wallet.FailPendingDeposit(orderId, txnOrErr);
                    callback?.Invoke(false, txnOrErr);
                    return;
                }

                wallet.ConfirmPendingDeposit(orderId, amount, credited =>
                    callback?.Invoke(credited, credited ? txnOrErr : "CONFIRM_DENIED"));
            });
        }

        public void Withdraw(decimal amount, string account, Action<bool, string> callback)
        {
            // Easypaisa's merchant checkout (MA) API is deposit-only; payouts need a separate
            // disbursement product - out of scope for Phase 3.
            callback?.Invoke(false, "WITHDRAW_NOT_SUPPORTED");
        }

        /// <summary>hash = SHA256(storeId + orderId + amount + password), per the Easypaisa sandbox spec
        /// (Task 4.2). storeId/orderId/amount come from <paramref name="data"/>; the password comes only
        /// from config so it can never end up in a caller-supplied dictionary (and therefore never in a
        /// log of that dictionary). Same method signs the outbound request and verifies the callback.</summary>
        public string GenerateSecureHash(Dictionary<string, string> data)
        {
            data ??= new Dictionary<string, string>();
            string storeId = data.TryGetValue("storeId", out var s) ? s : "";
            string orderId = data.TryGetValue("orderId", out var o) ? o : "";
            string amount = data.TryGetValue("amount", out var a) ? a : "";
            string raw = storeId + orderId + amount + (_cfg.Easypaisa_StorePassword ?? "");

            using var sha = SHA256.Create();
            byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            var sb = new StringBuilder(h.Length * 2);
            foreach (byte b in h) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>Task 5.1/5.2: re-derive the hash from the callback's own storeId/orderId/amount and
        /// compare it to the hash the callback carries. False (+ a logged CALLBACK_TAMPERED) on any
        /// mismatch or missing hash.</summary>
        public bool ValidateCallbackHash(Dictionary<string, string> data)
        {
            if (data == null || !data.TryGetValue("easypaisaCallbackHash", out var provided) || string.IsNullOrEmpty(provided))
            {
                Debug.LogError("[EasypaisaGateway] CALLBACK_TAMPERED - missing callback hash.");
                return false;
            }

            string recomputed = GenerateSecureHash(data);
            bool ok = string.Equals(provided, recomputed, StringComparison.OrdinalIgnoreCase);
            if (!ok)
            {
                string order = data.TryGetValue("orderId", out var o) ? o : "?";
                Debug.LogError($"[EasypaisaGateway] CALLBACK_TAMPERED - hash mismatch for order {order}.");
            }
            return ok;
        }

        // ---------------------------------------------------------------- helpers ----

        /// <summary>TLE_{UserID}_{Ticks} (Task 5.3 anti-replay format). Public (not just used internally
        /// by <see cref="Deposit"/>) so Phase3Tools' headless test can check the format directly.</summary>
        public static string MakeOrderId()
        {
            string userId = BackendService.Instance != null ? BackendService.Instance.UserId : "local";
            return $"TLE_{Sanitize(userId)}_{DateTime.UtcNow.Ticks}"; // Task 5.3
        }

        private static string Sanitize(string s) => string.IsNullOrEmpty(s) ? "local" : s.Replace('_', '-');

        private static string BuildUrl(string baseUrl, Dictionary<string, string> data)
        {
            var sb = new StringBuilder(baseUrl).Append('?');
            bool first = true;
            foreach (var kv in data)
            {
                if (!first) sb.Append('&');
                sb.Append(Uri.EscapeDataString(kv.Key)).Append('=').Append(Uri.EscapeDataString(kv.Value ?? ""));
                first = false;
            }
            return sb.ToString();
        }

        private static string Mask(string s) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length <= 12 ? "…" : s.Substring(0, 6) + "…" + s.Substring(s.Length - 4));
    }
}
