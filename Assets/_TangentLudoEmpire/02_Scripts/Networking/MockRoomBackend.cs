using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace TangentLudoEmpire.Multiplayer
{
    /// <summary>
    /// Task A.2. In-memory mock matchmaking backend - no network, no persistence (see RoomData's remarks).
    ///
    /// TODO (Firebase Realtime DB / Photon Rooms): a real backend makes room create/join/list genuinely
    /// async server calls, at which point <see cref="RoomManager"/>'s public API (deliberately synchronous
    /// here, matching Task A.1's literal method signatures) would need to become <c>Task&lt;T&gt;</c>-based
    /// too - every list mutation below is instant/synchronous specifically so the Edit-mode headless test
    /// doesn't need to await anything or risk `-quit` firing before a real delay's continuation runs.
    /// <see cref="SimulateNetworkDelayAsync"/> is a fire-and-forget, state-mutating-nothing stand-in for
    /// "Task.Delay(0.5s)" (Task A.2's literal ask) - it only logs, on a genuine async continuation, to
    /// prove the concept without making anything else in the pipeline depend on its timing.
    /// </summary>
    public static class MockRoomBackend
    {
        public const float RoomTimeoutSeconds = 120f;

        private static readonly List<RoomData> _rooms = new List<RoomData>();

        /// <summary>Fired when <see cref="PruneExpiredRooms"/> removes a room that never filled - the
        /// subscriber (RoomManager) is responsible for refunding every player still in it.</summary>
        public static event Action<RoomData> OnRoomExpired;

        /// <summary><paramref name="hostPlayerId"/> is unused here (the host does not auto-join - see
        /// RoomData.New's remarks) but kept in the signature since RoomManager.CreateRoom's call site
        /// already reads naturally with it.</summary>
        public static RoomData CreateRoom(decimal entryFee, int maxPlayers, string hostPlayerId)
        {
            var room = RoomData.New(entryFee, maxPlayers);
            _rooms.Add(room);
            SimulateNetworkDelayAsync($"CreateRoom {room.RoomId}");
            return room;
        }

        public static (bool ok, RoomData room, string error) TryJoin(string roomId, string playerId)
        {
            var room = _rooms.FirstOrDefault(r => r.RoomId == roomId);
            if (room == null) return (false, null, "ROOM_NOT_FOUND");
            if (room.Status != RoomStatus.Waiting) return (false, null, "ROOM_NOT_JOINABLE");
            if (room.PlayerIds.Contains(playerId)) return (true, room, null); // already in it - idempotent
            if (room.IsFull) return (false, null, "ROOM_FULL");

            room.PlayerIds.Add(playerId);
            if (room.IsFull) room.Status = RoomStatus.Full;
            SimulateNetworkDelayAsync($"JoinRoom {roomId}");
            return (true, room, null);
        }

        public static void RemovePlayer(string roomId, string playerId)
        {
            var room = _rooms.FirstOrDefault(r => r.RoomId == roomId);
            if (room == null) return;
            room.PlayerIds.Remove(playerId);
            if (room.PlayerIds.Count == 0) _rooms.Remove(room);
        }

        /// <summary>The match tied to this room is over (win/loss recorded) - the room has no further
        /// purpose, remove it entirely rather than trying to peel players out one at a time.</summary>
        public static void CloseRoom(string roomId) => _rooms.RemoveAll(r => r.RoomId == roomId);

        public static RoomData GetRoom(string roomId) => _rooms.FirstOrDefault(r => r.RoomId == roomId);

        public static List<RoomData> GetAvailableRooms()
        {
            PruneExpiredRooms();
            return _rooms.Where(r => r.Status == RoomStatus.Waiting).ToList();
        }

        /// <summary>Removes any Waiting room older than <see cref="RoomTimeoutSeconds"/>, firing
        /// <see cref="OnRoomExpired"/> for each so callers can refund. Called from RoomManager's Update
        /// in Play mode, or directly by the headless test after backdating a room's timestamp.</summary>
        public static void PruneExpiredRooms()
        {
            var now = DateTimeOffset.UtcNow;
            var expired = _rooms.Where(r => r.Status == RoomStatus.Waiting &&
                                            (now - r.CreatedAtUtc).TotalSeconds > RoomTimeoutSeconds).ToList();
            foreach (var room in expired)
            {
                room.Status = RoomStatus.Expired;
                _rooms.Remove(room);
                OnRoomExpired?.Invoke(room);
            }
        }

        private static async void SimulateNetworkDelayAsync(string context)
        {
            await Task.Delay(500);
            Debug.Log($"[MockRoomBackend] (simulated network) {context} settled after 0.5s.");
        }

#if UNITY_EDITOR
        /// <summary>EDITOR ONLY - compiled out of every player build. Backdates a room's creation time so
        /// PruneExpiredRooms treats it as expired without a real 120s wait. Used by Phase4Tools' test.</summary>
        public static void Editor_ForceExpire(string roomId)
        {
            var room = _rooms.FirstOrDefault(r => r.RoomId == roomId);
            if (room != null) room.CreatedAtUtcMs = DateTimeOffset.UtcNow.AddSeconds(-(RoomTimeoutSeconds + 5)).ToUnixTimeMilliseconds();
        }

        public static void Editor_ClearAll() => _rooms.Clear();
#endif
    }
}
