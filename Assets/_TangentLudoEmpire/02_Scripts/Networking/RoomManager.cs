using System;
using System.Collections.Generic;
using UnityEngine;
using TangentLudoEmpire.Bridge;
using TangentLudoEmpire.Services;
using TangentLudoEmpire.Wallet;

namespace TangentLudoEmpire.Multiplayer
{
    /// <summary>
    /// Task A.1. Online room / matchmaking-lobby manager. Entry fee is charged the instant a player
    /// JOINS (via <see cref="WalletGameBridge.TryEnterGame"/>), not when the room fills - see
    /// "Charge-at-join, refund-on-expiry" in MIGRATION_NOTES for why, and why
    /// <see cref="RoomGameBridge.OnRoomFull"/> deliberately does NOT charge again despite the original
    /// Task A.4 wording suggesting a second per-player charge there.
    /// </summary>
    [DisallowMultipleComponent]
    public class RoomManager : MonoBehaviour
    {
        public static RoomManager Instance { get; private set; }

        /// <summary>Fired the moment a room's last seat fills. RoomManager itself always handles this
        /// (calls RoomGameBridge.OnRoomFull) - the event exists too so UI/other systems can react.</summary>
        public static event Action<string> OnRoomFull;

        public string LocalPlayerId => BackendService.Instance != null ? BackendService.Instance.UserId : "local";

        private string _currentRoomId;
        public string CurrentRoomId => _currentRoomId;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject); // Editor/test code (headless tools) would otherwise hit InvalidOperationException here and abort the rest of Awake()
            MockRoomBackend.OnRoomExpired += HandleRoomExpired;
        }

        private void OnDestroy()
        {
            if (Instance == this) MockRoomBackend.OnRoomExpired -= HandleRoomExpired;
        }

        private void Update()
        {
            // Play-mode-only real ticking; Edit-mode tests call MockRoomBackend.PruneExpiredRooms directly
            // after backdating a room via Editor_ForceExpire.
            MockRoomBackend.PruneExpiredRooms();
        }

        /// <summary>Literal Task A.1 signature. Creates an empty room - does NOT seat or charge the host;
        /// see RoomData.New's remarks for why (a real bug this session's headless test caught: auto-
        /// seating the host here made their first JoinRoom call skip the charge). Use
        /// <see cref="CreateAndJoinRoom"/> for the common "host creates and sits down" UI flow.</summary>
        public string CreateRoom(decimal fee, int maxPlayers = 4)
        {
            var room = MockRoomBackend.CreateRoom(fee, maxPlayers, LocalPlayerId);
            AuditLog.Log("ROOM_CREATED", room.RoomId, $"fee={fee:0.00} max={maxPlayers}");
            return room.RoomId;
        }

        /// <summary>Create then immediately JoinRoom as the host - charges the host's entry fee exactly
        /// like any other join. Returns false (room still exists, just unhosted) if the host can't afford it.</summary>
        public string CreateAndJoinRoom(decimal fee, int maxPlayers, out bool joined, out string error)
        {
            string roomId = CreateRoom(fee, maxPlayers);
            joined = JoinRoom(roomId, out error);
            return roomId;
        }

        /// <summary>Literal Task A.1 signature (bool JoinRoom(string roomId)) - use the out-error overload
        /// below to see WHY it failed ("Low Balance" etc.) for UI feedback.</summary>
        public bool JoinRoom(string roomId) => JoinRoom(roomId, out _);

        /// <summary>Task A.1: on join, FIRST call WalletGameBridge.TryEnterGame(fee). If that fails,
        /// returns false and <paramref name="error"/> is "Low Balance" (or whatever TryEnterGame's denial
        /// reason was) - the room seat is never taken without the fee actually clearing.</summary>
        public bool JoinRoom(string roomId, out string error)
        {
            error = null;
            var room = MockRoomBackend.GetRoom(roomId);
            if (room == null) { error = "Room not found"; return false; }
            if (room.PlayerIds.Contains(LocalPlayerId)) { _currentRoomId = roomId; return true; } // already in

            if (!WalletGameBridge.TryEnterGame(room.EntryFee))
            {
                error = "Low Balance";
                AuditLog.Log("ROOM_JOIN_DENIED", roomId, error);
                return false;
            }

            var (ok, joinedRoom, joinErr) = MockRoomBackend.TryJoin(roomId, LocalPlayerId);
            if (!ok)
            {
                // Seat was lost (race/full) after the fee already cleared - refund immediately, never
                // strand a charge with no seat behind it.
                MoneyWallet.Instance?.AddFunds(room.EntryFee, "ROOM_JOIN_FAILED_REFUND");
                error = joinErr ?? "Could not join room";
                AuditLog.Log("ROOM_JOIN_FAILED", roomId, error);
                return false;
            }

            _currentRoomId = roomId;
            AuditLog.Log("ROOM_JOINED", roomId, $"{LocalPlayerId} ({joinedRoom.PlayerIds.Count}/{joinedRoom.MaxPlayers})");

            if (joinedRoom.IsFull)
            {
                OnRoomFull?.Invoke(roomId);
                RoomGameBridge.OnRoomFull(roomId);
            }
            return true;
        }

        public void LeaveRoom()
        {
            if (string.IsNullOrEmpty(_currentRoomId)) return;
            var room = MockRoomBackend.GetRoom(_currentRoomId);
            if (room != null && room.Status == RoomStatus.Waiting)
            {
                // Only refund a Waiting-room leave - once Full/Started, the match is committed (matches
                // how a withdraw request's reservation is only refunded on reject, never after payout).
                MoneyWallet.Instance?.AddFunds(room.EntryFee, "ROOM_LEAVE_REFUND");
                AuditLog.Log("ROOM_LEFT_REFUNDED", _currentRoomId, LocalPlayerId);
            }
            MockRoomBackend.RemovePlayer(_currentRoomId, LocalPlayerId);
            _currentRoomId = null;
        }

        public List<RoomData> GetAvailableRooms() => MockRoomBackend.GetAvailableRooms();

        private void HandleRoomExpired(RoomData room)
        {
            AuditLog.Log("ROOM_EXPIRED", room.RoomId, $"{room.PlayerIds.Count} player(s) refunded");
            foreach (var playerId in room.PlayerIds)
            {
                // Mock/local-only: we can only actually credit the LOCAL wallet. A real backend would
                // refund every player's own account server-side.
                if (playerId == LocalPlayerId)
                    MoneyWallet.Instance?.AddFunds(room.EntryFee, "ROOM_EXPIRED_REFUND");
            }
            if (_currentRoomId == room.RoomId) _currentRoomId = null;
        }
    }
}
