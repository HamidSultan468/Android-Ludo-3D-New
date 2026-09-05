using System;
using System.Collections.Generic;

namespace TangentLudoEmpire.Payments
{
    /// <summary>
    /// Abstraction over a cash-in / cash-out provider (JazzCash, Easypaisa, a card PSP, ...).
    /// Phase 3.1 ships sandbox-mock <see cref="JazzCashGateway"/> / <see cref="EasypaisaGateway"/>
    /// (real post-data shape + real secure-hash math, timed coin-flip instead of an HTTP call) plus the
    /// original <see cref="MockPaymentGateway"/>.
    ///
    /// Contract:
    ///  * amounts are <see cref="decimal"/> in the account currency, always &gt; 0;
    ///  * <paramref name="phone"/>-style params are the payer's mobile account number (required by
    ///    both JazzCash MWALLET and Easypaisa MA - a card-only provider would ignore it);
    ///  * the callback is invoked exactly once, on the Unity main thread;
    ///  * <c>callback(true, gatewayRef)</c> on success - <c>gatewayRef</c> is the provider's settlement
    ///    id and MUST be server-verified (<see cref="TangentLudoEmpire.Services.AntiCheatManager.ValidatePayment"/>)
    ///    before any funds are credited;
    ///  * <c>callback(false, errorCode)</c> on failure - a short machine-readable code.
    ///
    /// Phase 3.2: <c>Deposit</c>'s callback now fires only once money has actually moved - the gateway
    /// itself owns the pending-deposit lifecycle (<see cref="TangentLudoEmpire.Wallet.MoneyWallet.BeginPendingDeposit"/>
    /// / <c>ConfirmPendingDeposit</c> / <c>FailPendingDeposit</c>) so a caller (UI) never needs to (and
    /// must never) credit the wallet itself - see MoneyWallet's remarks on why that would double-credit.
    ///
    /// The gateway NEVER touches the wallet's crypto/config and NEVER logs a secret (integrity salt /
    /// store password) - only <see cref="GenerateSecureHash"/>'s output (a hash), never its key material.
    /// </summary>
    public interface IPaymentGateway
    {
        /// <summary>Short display/audit name, e.g. "JazzCash", "Easypaisa", "Mock".</summary>
        string GatewayName { get; }

        void Deposit(decimal amount, string phone, Action<bool, string> callback);
        void Withdraw(decimal amount, string account, Action<bool, string> callback);

        /// <summary>Provider-specific request-signing hash (JazzCash: HMAC-SHA256 keyed by the
        /// integrity salt; Easypaisa: SHA-256 of storeId+orderId+amount+password). Never includes the
        /// key material itself in the return value's caller-visible context (only the derived hash).</summary>
        string GenerateSecureHash(Dictionary<string, string> data);

        /// <summary>Re-derives the hash from an INBOUND callback's own fields and compares it to the
        /// hash field the callback itself carries. False (and a logged <c>CALLBACK_TAMPERED</c>) on any
        /// mismatch or missing hash - <see cref="TangentLudoEmpire.Payments.PaymentCallbackListener"/>
        /// treats that as a failed payment, never a success, regardless of the response code.</summary>
        bool ValidateCallbackHash(Dictionary<string, string> data);
    }
}
