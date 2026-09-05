using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TangentLudoEmpire.Tournament
{
    public enum TournamentStatus { Registering, Active, Completed }

    /// <summary>Live tournament state - like RoomData, not persisted (a running tournament is a live
    /// session concept; a completed one's payout is durable because it went through MoneyWallet, which
    /// IS persisted).</summary>
    [Serializable]
    public class TournamentData
    {
        public string Id;
        public int MaxPlayers = 8;
        public List<string> Players = new List<string>();
        public TournamentStatus Status;
        public long CreatedAtUtcMs;
        public string WinnerId;
        public string RunnerUpId;

        [SerializeField] private string entryFeeRaw = "0";
        public decimal EntryFee
        {
            get => decimal.TryParse(entryFeeRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;
            set => entryFeeRaw = decimal.Round(value, 2).ToString(CultureInfo.InvariantCulture);
        }

        [SerializeField] private string prizePoolRaw = "0";
        public decimal PrizePool
        {
            get => decimal.TryParse(prizePoolRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;
            set => prizePoolRaw = decimal.Round(value, 2).ToString(CultureInfo.InvariantCulture);
        }

        public bool IsFull => Players.Count >= MaxPlayers;

        public static TournamentData New(decimal entryFee, int maxPlayers)
        {
            return new TournamentData
            {
                Id = "TRN_" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant(),
                MaxPlayers = maxPlayers,
                EntryFee = entryFee,
                PrizePool = 0m,
                Status = TournamentStatus.Registering,
                CreatedAtUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
        }
    }
}
