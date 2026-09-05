using System;
using System.Globalization;
using UnityEngine;

namespace TangentLudoEmpire.Wallet
{
    /// <summary>What a ledger entry represents. <see cref="Refund"/> added Phase 3.3 - a rejected
    /// withdraw request credits the reserved amount back; without its own type it would have to be
    /// mis-tagged as Deposit/Bonus, which would also mis-tag it in every ledger-sum integrity check.</summary>
    public enum TxType { Deposit, Withdraw, GameWin, GameLoss, Bonus, Refund }

    /// <summary>Lifecycle of a single transaction.</summary>
    public enum TxStatus { Pending, Success, Failed }

    /// <summary>
    /// One immutable-ish ledger row. MONEY IS ALWAYS <see cref="decimal"/> in the public API.
    ///
    /// Unity's <c>JsonUtility</c> (what <see cref="TangentLudoEmpire.Core.SaveService"/> serialises with)
    /// cannot round-trip <c>decimal</c>, so the amount is stored as an invariant-culture string in
    /// <see cref="amountRaw"/> and surfaced through the <see cref="Amount"/> property. Never read/write
    /// <see cref="amountRaw"/> directly outside this class.
    /// </summary>
    [Serializable]
    public class Transaction
    {
        public string TxId;
        public TxType Type;
        public TxStatus Status;
        public long Timestamp;      // Unix milliseconds, UTC
        public string GatewayRef;

        [SerializeField] private string amountRaw = "0";

        /// <summary>Signed amount in the account currency. Deposits / wins are positive,
        /// withdrawals / losses are stored positive too - the <see cref="Type"/> carries the direction.</summary>
        public decimal Amount
        {
            get => decimal.TryParse(amountRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;
            set => amountRaw = value.ToString(CultureInfo.InvariantCulture);
        }

        public Transaction() { }

        public static Transaction New(TxType type, decimal amount, TxStatus status, string gatewayRef = "")
        {
            return new Transaction
            {
                TxId = "TX_" + Guid.NewGuid().ToString("N").Substring(0, 16),
                Type = type,
                Amount = decimal.Round(Math.Abs(amount), 2),
                Status = status,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                GatewayRef = gatewayRef ?? ""
            };
        }

        public DateTime TimestampUtc => DateTimeOffset.FromUnixTimeMilliseconds(Timestamp).UtcDateTime;

        public override string ToString() =>
            $"{TimestampUtc:yyyy-MM-dd HH:mm} | {Type,-9} | {Amount,12:0.00} | {Status,-7} | {GatewayRef}";
    }
}
