using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using TangentLudoEmpire.Core;
using TangentLudoEmpire.Services;
using TangentLudoEmpire.Wallet;

namespace TangentLudoEmpire.Payments
{
    /// <summary>
    /// JazzCash Mobile Wallet (MWALLET) checkout via <see cref="WebViewManager"/> - Phase 3.2 sandbox.
    ///
    /// Deposit now sends the user to JazzCash's own sandbox CustomerPortal page (real pp_* post-data +
    /// a real HMAC-SHA256 <c>pp_SecureHash</c>) and only calls back once
    /// <see cref="PaymentCallbackListener"/> reports the gateway's redirect - no more fake wait/coin-flip.
    ///
    /// LIVE-INTEGRATION TODO: <see cref="TangentLudoEmpire.Payments.PaymentCallbackListener"/>'s class
    /// remarks cover the localhost-callback limitation; the other TODO here is that JazzCash's real
    /// CustomerPortal form is typically POSTed from a webpage, not opened as a GET query string - if the
    /// live sandbox rejects the URL below, the fix is a tiny local HTML page (served the same way the
    /// callback is, over TCP) that auto-submits a POST form, opened via <c>Application.OpenURL</c> instead
    /// of the gateway URL directly. Not built here since it can't be verified without a live sandbox call.
    ///
    /// SECURITY: <see cref="PaymentConfig.JazzCash_IntegritySalt"/> is the HMAC key - it is used, never
    /// logged. Every log line here carries only non-secret fields and a masked hash.
    /// </summary>
    public class JazzCashGateway : IPaymentGateway
    {
        public string GatewayName => "JazzCash";

        private const string SandboxBaseUrl = "https://sandbox.jazzcash.com.pk/CustomerPortal/transactionmanagement/merchantform/";

        private readonly PaymentConfig _cfg;

        public JazzCashGateway(PaymentConfig cfg) => _cfg = cfg ?? PaymentConfig.Load();

        public void Deposit(decimal amount, string phone, Action<bool, string> callback)
        {
            if (amount <= 0m) { callback?.Invoke(false, "INVALID_AMOUNT"); return; }
            if (string.IsNullOrWhiteSpace(phone)) { callback?.Invoke(false, "INVALID_PHONE"); return; }

            string orderId = MakeOrderId(); // TLE_{UserID}_{Ticks} - Task 5.3 anti-replay format
            long paisa = (long)(decimal.Round(amount, 2) * 100m); // pp_Amount is in paisa

            var post = new Dictionary<string, string>
            {
                ["pp_Amount"] = paisa.ToString(CultureInfo.InvariantCulture),
                ["pp_BillReference"] = orderId,
                ["pp_MerchantTxnRef"] = orderId, // Phase 3.5 Task 3 - carried alongside pp_BillReference for the backend's reconciliation
                ["pp_MerchantID"] = _cfg.JazzCash_MerchantID ?? "",
                // Phase 3.5: prefer the cloud webhook once PaymentConfig.WebhookBaseURL is a real value;
                // fall back to the local dev-loop listener until then (see PaymentConfig.HasRealWebhook).
                ["pp_ReturnURL"] = _cfg.HasRealWebhook
                    ? _cfg.WebhookBaseURL.TrimEnd('/') + "/api/payment/callback"
                    : PaymentCallbackListener.CallbackUrl,
                // Mobile: our backend redirects the browser here (appending ?orderId=&status=) after it
                // verifies the gateway's webhook - see BACKEND_WEBHOOK_SPEC.md. Android routes it to
                // DeepLinkManager via the intent-filter in Assets/Plugins/Android/AndroidManifest.xml.
                ["pp_ReturnURL_Backup"] = $"{_cfg.DeepLinkScheme}://callback",
            };
            post["pp_SecureHash"] = GenerateSecureHash(post);

            // Task 7 go-live switch: IsProductionMode=false FORCES sandbox regardless of anything else.
            string baseUrl = _cfg.IsProductionMode ? _cfg.JazzCash_LiveUrl : SandboxBaseUrl;
            string url = BuildUrl(baseUrl, post);

            Debug.Log($"[JazzCashGateway] Deposit {amount:0.00} PKR ({paisa} paisa) order={orderId} " +
                      $"merchant={_cfg.JazzCash_MerchantID} sandbox={_cfg.UseSandbox} hash={Mask(post["pp_SecureHash"])}");

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

                // The redirect's own hash already passed PaymentCallbackListener's ValidateCallbackHash
                // check before OnPaymentComplete ever fired, so this is the anti-cheat server gate only.
                wallet.ConfirmPendingDeposit(orderId, amount, credited =>
                    callback?.Invoke(credited, credited ? txnOrErr : "CONFIRM_DENIED"));
            });
        }

        public void Withdraw(decimal amount, string account, Action<bool, string> callback)
        {
            // JazzCash's merchant checkout API (MWALLET/card) is deposit-only. Payouts need the
            // separate Disbursement/IBFT API - out of scope for Phase 3.
            callback?.Invoke(false, "WITHDRAW_NOT_SUPPORTED");
        }

        /// <summary>pp_SecureHash: HMAC-SHA256 over the pp_* values (sorted by key, '&amp;'-joined,
        /// pp_SecureHash itself excluded), keyed by <see cref="PaymentConfig.JazzCash_IntegritySalt"/> -
        /// matches JazzCash's documented hash construction. Used to both SIGN the outbound request and
        /// VERIFY the inbound callback (same math, either direction).</summary>
        public string GenerateSecureHash(Dictionary<string, string> data)
        {
            if (data == null) data = new Dictionary<string, string>();
            string joined = string.Join("&", data
                .Where(kv => !string.Equals(kv.Key, "pp_SecureHash", StringComparison.Ordinal))
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => kv.Value ?? ""));
            return ComputeHMAC(joined, _cfg.JazzCash_IntegritySalt ?? "");
        }

        /// <summary>Task 5.1/5.2: re-derive the hash from the callback's own pp_* fields and compare it
        /// to the pp_SecureHash the callback carries. False (+ a logged CALLBACK_TAMPERED) on any
        /// mismatch or missing hash - the caller must treat that as a failed payment.</summary>
        public bool ValidateCallbackHash(Dictionary<string, string> data)
        {
            if (data == null || !data.TryGetValue("pp_SecureHash", out var provided) || string.IsNullOrEmpty(provided))
            {
                Debug.LogError("[JazzCashGateway] CALLBACK_TAMPERED - missing pp_SecureHash.");
                return false;
            }

            string recomputed = GenerateSecureHash(data);
            bool ok = string.Equals(provided, recomputed, StringComparison.OrdinalIgnoreCase);
            if (!ok)
            {
                string order = data.TryGetValue("pp_BillReference", out var r) ? r : "?";
                Debug.LogError($"[JazzCashGateway] CALLBACK_TAMPERED - secure-hash mismatch for order {order}.");
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

        private static string ComputeHMAC(string data, string key)
        {
            using var h = new HMACSHA256(Encoding.UTF8.GetBytes(key ?? ""));
            byte[] mac = h.ComputeHash(Encoding.UTF8.GetBytes(data ?? ""));
            var sb = new StringBuilder(mac.Length * 2);
            foreach (byte b in mac) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>Never print a full hash to a log - first 6 + last 4 hex chars is enough to eyeball
        /// "did this change" without handing out the value.</summary>
        private static string Mask(string s) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length <= 12 ? "…" : s.Substring(0, 6) + "…" + s.Substring(s.Length - 4));
    }
}
