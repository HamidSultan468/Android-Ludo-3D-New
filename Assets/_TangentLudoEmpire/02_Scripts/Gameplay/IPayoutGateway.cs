using System;

namespace TangentLudoEmpire.Payments
{
    /// <summary>
    /// Task 3: the cash-OUT counterpart to <see cref="IPaymentGateway"/> (cash-in). Deliberately
    /// separate interfaces - a deposit gateway sends the user to a checkout page and waits for a
    /// browser-driven callback; a payout gateway is a server-to-server disbursement call with no user
    /// interaction, called only after <see cref="TangentLudoEmpire.Wallet.MoneyWallet.RequestWithdraw"/>
    /// (or admin approval) has already reserved the funds.
    /// </summary>
    public interface IPayoutGateway
    {
        /// <summary>Short display/audit name, e.g. "JazzCash", "Easypaisa".</summary>
        string GatewayName { get; }

        /// <summary><c>callback(true, payoutRef)</c> on success, <c>callback(false, errorCode)</c> on
        /// failure - invoked exactly once, on the main thread. Never call this before the funds have
        /// already been deducted from the wallet (see MoneyWallet.RequestWithdraw/ApproveWithdraw) - a
        /// payout gateway has no concept of "insufficient balance", that check belongs to the wallet.</summary>
        void Payout(decimal amount, string account, Action<bool, string> callback);
    }
}
