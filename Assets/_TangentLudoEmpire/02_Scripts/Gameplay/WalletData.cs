using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TangentLudoEmpire.Wallet
{
    /// <summary>
    /// The persisted real-money wallet. Stored AES-256 encrypted + SHA-256 verified by
    /// <see cref="TangentLudoEmpire.Core.SaveService"/> at
    /// <c>Application.persistentDataPath/tle_wallet_v1.enc</c>.
    ///
    /// As with <see cref="Transaction"/>, the <c>decimal</c> balance is stored as an invariant string
    /// (<see cref="balanceRaw"/>) because <c>JsonUtility</c> cannot serialise <c>decimal</c>. Use the
    /// <see cref="Balance"/> property.
    ///
    /// This is the CLIENT copy. The server is authoritative - <see cref="TangentLudoEmpire.Services.AntiCheatManager.ValidateWallet"/>
    /// reconciles it and can lock the wallet on drift.
    /// </summary>
    [Serializable]
    public class WalletData
    {
        public string PlayerId = "";
        public List<Transaction> History = new List<Transaction>();

        /// <summary>Phase 3.3 (Task 2). Not capped like History - withdraw requests are few and each one
        /// represents money that's either still reserved or needs an audit trail, so none are dropped.</summary>
        public List<WithdrawRequest> WithdrawRequests = new List<WithdrawRequest>();

        [SerializeField] private string balanceRaw = "0";

        public decimal Balance
        {
            get => decimal.TryParse(balanceRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;
            set => balanceRaw = decimal.Round(value, 2).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Keeps <see cref="History"/> bounded (newest kept).</summary>
        public const int MaxHistory = 100;

        public void AddTransaction(Transaction tx)
        {
            if (tx == null) return;
            History ??= new List<Transaction>();
            History.Add(tx);
            int overflow = History.Count - MaxHistory;
            if (overflow > 0) History.RemoveRange(0, overflow);
        }

        public static WalletData CreateNew(string playerId) => new WalletData
        {
            PlayerId = string.IsNullOrEmpty(playerId) ? "local" : playerId,
            History = new List<Transaction>(),
            Balance = 0m
        };

        /// <summary>Stable string the integrity check hashes: balance + every tx id/amount/status.
        /// Any tampered field changes it.</summary>
        public string IntegrityString()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(PlayerId).Append('|').Append(balanceRaw).Append('|').Append(History?.Count ?? 0);
            if (History != null)
                foreach (var t in History)
                    sb.Append('#').Append(t.TxId).Append(':').Append(t.Amount.ToString(CultureInfo.InvariantCulture))
                      .Append(':').Append((int)t.Type).Append(':').Append((int)t.Status);
            sb.Append('|').Append(WithdrawRequests?.Count ?? 0);
            if (WithdrawRequests != null)
                foreach (var w in WithdrawRequests)
                    sb.Append('~').Append(w.RequestId).Append(':').Append(w.Amount.ToString(CultureInfo.InvariantCulture))
                      .Append(':').Append((int)w.Status);
            return sb.ToString();
        }
    }
}
