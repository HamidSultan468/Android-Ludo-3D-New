using System;
using System.Globalization;
using UnityEngine;

namespace TangentLudoEmpire.Wallet
{
    /// <summary>Lifecycle of a withdraw request (Task 2). Distinct from <see cref="TxStatus"/> - a
    /// request tracks admin/payout workflow state, a <see cref="Transaction"/> tracks a single balance
    /// movement (the deduction happens once, at request time, regardless of how the request later
    /// resolves - see <see cref="MoneyWallet.RequestWithdraw"/>).</summary>
    public enum WithdrawStatus { Pending, Approved, Rejected, Paid }

    /// <summary>What a caller of <see cref="MoneyWallet.RequestWithdraw"/> actually needs to know: was
    /// the request rejected outright (KYC/anti-fraud/balance), queued for admin approval, or paid out
    /// immediately (auto-payout, amount under the threshold).</summary>
    public enum WithdrawResult { Rejected, Pending, Paid }

    /// <summary>
    /// One withdraw request. The balance is deducted (as a normal <see cref="TxType.Withdraw"/>
    /// <see cref="Transaction"/>) the moment a request is created, whatever its eventual outcome - a
    /// <see cref="WithdrawStatus.Rejected"/> request refunds it (<see cref="TxType.Refund"/>); a
    /// <see cref="WithdrawStatus.Paid"/> one does not (the money already left).
    /// </summary>
    [Serializable]
    public class WithdrawRequest
    {
        public string RequestId;
        public WithdrawStatus Status;
        public long CreatedAt;      // Unix ms UTC
        public long ProcessedAt;    // Unix ms UTC - when it left Pending; 0 while still Pending
        public string Provider;     // "JazzCash" | "Easypaisa" - which IPayoutGateway to use
        public string Account;      // payout destination (phone/account number)
        public string PayoutRef;    // the payout gateway's reference, once Paid
        public string Note;         // reject reason / admin note

        [SerializeField] private string amountRaw = "0";
        public decimal Amount
        {
            get => decimal.TryParse(amountRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;
            set => amountRaw = decimal.Round(value, 2).ToString(CultureInfo.InvariantCulture);
        }

        public static WithdrawRequest New(decimal amount, string provider, string account)
        {
            return new WithdrawRequest
            {
                RequestId = "WD_" + Guid.NewGuid().ToString("N").Substring(0, 16),
                Amount = amount,
                Status = WithdrawStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ProcessedAt = 0,
                Provider = provider ?? "",
                Account = account ?? "",
                PayoutRef = "",
                Note = "",
            };
        }

        public DateTime CreatedAtUtc => DateTimeOffset.FromUnixTimeMilliseconds(CreatedAt).UtcDateTime;
    }
}
