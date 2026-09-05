using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TangentLudoEmpire.Multiplayer
{
    public enum RoomStatus { Waiting, Full, Started, Expired }

    /// <summary>
    /// Live matchmaking-lobby state. Deliberately NOT persisted (no SaveEncrypted file) - unlike the
    /// wallet/KYC/leaderboard data, a room only matters while players are actively waiting in it; losing
    /// it on an app restart is correct behaviour (the entry fee was already reserved via
    /// <see cref="TangentLudoEmpire.Bridge.WalletGameBridge.TryEnterGame"/>, and would be refunded by
    /// <see cref="MockRoomBackend"/>'s 120s expiry rather than silently vanish).
    /// </summary>
    [Serializable]
    public class RoomData
    {
        public string RoomId;
        public int MaxPlayers = 4;
        public List<string> PlayerIds = new List<string>();
        public RoomStatus Status;
        public long CreatedAtUtcMs;

        [SerializeField] private string entryFeeRaw = "0";
        public decimal EntryFee
        {
            get => decimal.TryParse(entryFeeRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;
            set => entryFeeRaw = decimal.Round(value, 2).ToString(CultureInfo.InvariantCulture);
        }

        public bool IsFull => PlayerIds.Count >= MaxPlayers;
        public DateTime CreatedAtUtc => DateTimeOffset.FromUnixTimeMilliseconds(CreatedAtUtcMs).UtcDateTime;

        /// <summary>Starts with NO players, host included - a bug found by Phase4Tools' headless test:
        /// auto-seating the host here meant RoomManager.JoinRoom's "already in room" idempotency check
        /// (correctly there to stop a second real join from double-charging) fired for the host's very
        /// FIRST join, skipping WalletGameBridge.TryEnterGame entirely - the host was never actually
        /// charged. The host now pays the same way everyone else does, via a real JoinRoom call after
        /// CreateRoom (RoomManager.CreateRoom does not auto-join).</summary>
        public static RoomData New(decimal entryFee, int maxPlayers)
        {
            return new RoomData
            {
                RoomId = "ROOM_" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant(),
                MaxPlayers = maxPlayers,
                EntryFee = entryFee,
                Status = RoomStatus.Waiting,
                CreatedAtUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
        }
    }
}
