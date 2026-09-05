using UnityEngine;
using TangentLudoEmpire.Bridge;
using TangentLudoEmpire.Services;

namespace TangentLudoEmpire.Multiplayer
{
    /// <summary>
    /// Task A.4. Does NOT touch LudoEmpire.Ludo directly - like <see cref="WalletGameBridge"/>, it's
    /// called by RoomManager/RoomUI and (via <see cref="TangentLudoEmpire.Bridge.LudoWalletHooks"/>-style
    /// wiring, left for a human to add) by gameplay's own victory/start hooks.
    ///
    /// SPEC CORRECTION: Task A.4 said "When Room Full: for each player call ReportGameEntry(fee)" - but
    /// Task A.1 already charges every player their entry fee the moment they individually join
    /// (WalletGameBridge.TryEnterGame). Charging again here would double-charge every player in the room.
    /// <see cref="OnRoomFull"/> only locks the room and audits who's in the match; no wallet call happens.
    /// </summary>
    public static class RoomGameBridge
    {
        /// <summary>Called once, by RoomManager.JoinRoom, the instant a room's last seat fills. Locks the
        /// room to Started (no more joins) - does NOT re-charge anyone, see class remarks.</summary>
        public static void OnRoomFull(string roomId)
        {
            var room = MockRoomBackend.GetRoom(roomId);
            if (room == null) return;

            room.Status = RoomStatus.Started;
            AuditLog.Log("ROOM_GAME_START", roomId, string.Join(",", room.PlayerIds));
            Debug.Log($"[RoomGameBridge] Room {roomId} full ({room.PlayerIds.Count}/{room.MaxPlayers}) - match starting.");
        }

        /// <summary>Called when the match tied to a room ends. Credits the winner via the same
        /// WalletGameBridge.ReportWin path any other match win uses.</summary>
        public static void OnGameEnd(string roomId, string winnerId, decimal winnerAmount)
        {
            AuditLog.Log("ROOM_GAME_END", roomId, $"winner={winnerId} amount={winnerAmount:0.00}");

            if (RoomManager.Instance != null && winnerId == RoomManager.Instance.LocalPlayerId)
                WalletGameBridge.ReportWin(winnerAmount);
            // else: the winner is a different (mock) player - nothing to credit locally, a real backend
            // would settle every player's own wallet server-side.

            MockRoomBackend.CloseRoom(roomId);
        }
    }
}
