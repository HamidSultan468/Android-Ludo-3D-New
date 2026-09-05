using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using TangentLudoEmpire.Wallet;

namespace TangentLudoEmpire.Payments
{
    /// <summary>
    /// Fake gateway for development / QA with no real-world post-data shape or WebView step - use this
    /// when you just need "a gateway that works", not JazzCash/Easypaisa parity. No network, simulated
    /// latency, a fixed success rate.
    ///  * Deposit : ~2s, 90% success -> callback(true, "MOCK_TX_&lt;n&gt;"), credits MoneyWallet directly
    ///    (no WebView/pending-deposit step - there's nothing to lose money to if the app closes mid-mock).
    ///  * Withdraw: ~3s, 80% success -> callback(true, "MOCK_WD_&lt;n&gt;")
    ///  * failure -> callback(false, "&lt;errorCode&gt;")
    /// </summary>
    public class MockPaymentGateway : IPaymentGateway
    {
        public string GatewayName => "Mock";

        private const float DepositDelaySeconds  = 2f;
        private const float WithdrawDelaySeconds = 3f;
        private const float DepositSuccessRate   = 0.90f;
        private const float WithdrawSuccessRate  = 0.80f;

        private static int _counter = 100;

        public void Deposit(decimal amount, string phone, Action<bool, string> callback)
        {
            if (amount <= 0m) { callback?.Invoke(false, "INVALID_AMOUNT"); return; }
            PaymentRunner.Instance.Run(PaymentRunner.SimulateResult(
                DepositDelaySeconds, DepositSuccessRate, "MOCK_TX_" + NextId(), "PAYMENT_DECLINED", (ok, refOrErr) =>
                {
                    if (!ok) { callback?.Invoke(false, refOrErr); return; }

                    var wallet = MoneyWallet.Instance;
                    if (wallet == null) { callback?.Invoke(false, "WALLET_UNAVAILABLE"); return; }

                    // Deposit's callback contract (Phase 3.2): fires only once money has actually moved.
                    wallet.AddFunds(amount, "DEPOSIT_" + GatewayName, refOrErr, credited =>
                        callback?.Invoke(credited, credited ? refOrErr : "CONFIRM_DENIED"));
                }));
        }

        public void Withdraw(decimal amount, string account, Action<bool, string> callback)
        {
            if (amount <= 0m) { callback?.Invoke(false, "INVALID_AMOUNT"); return; }
            if (string.IsNullOrWhiteSpace(account)) { callback?.Invoke(false, "INVALID_ACCOUNT"); return; }
            PaymentRunner.Instance.Run(PaymentRunner.SimulateResult(
                WithdrawDelaySeconds, WithdrawSuccessRate, "MOCK_WD_" + NextId(), "INSUFFICIENT_BALANCE", callback));
        }

        /// <summary>Not security-critical (there's no real merchant secret behind the mock) - a
        /// deterministic SHA-256 over the sorted key=value pairs, same hex style as the real gateways.</summary>
        public string GenerateSecureHash(Dictionary<string, string> data)
        {
            data ??= new Dictionary<string, string>();
            string joined = string.Join("&", data.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                                                  .Select(kv => $"{kv.Key}={kv.Value}"));
            using var sha = SHA256.Create();
            byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(joined));
            var sb = new StringBuilder(h.Length * 2);
            foreach (byte b in h) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>No real callback ever reaches the mock (it never opens a WebView) - always true.</summary>
        public bool ValidateCallbackHash(Dictionary<string, string> data) => true;

        private static int NextId() => System.Threading.Interlocked.Increment(ref _counter);
    }
}
