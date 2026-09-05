using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TangentLudoEmpire.Wallet.UI;

namespace TangentLudoEmpire.Multiplayer
{
    /// <summary>Task A.3. Self-building room list (Text + Button, matching the Phase 3 wallet UI style) -
    /// a ScrollView of available rooms, a fee Dropdown, a Create button, refreshing every 5s.</summary>
    [DisallowMultipleComponent]
    public class RoomUI : MonoBehaviour
    {
        private static readonly int[] FeeOptions = { 10, 20, 50, 100, 500 };

        private GameObject _root;
        private Dropdown _feeDropdown;
        private RectTransform _listContent;
        private Text _statusLabel;
        private readonly List<GameObject> _rows = new List<GameObject>();

        private void Awake() { if (_root == null) BuildUI(); }
        private void OnEnable() { StartCoroutine(RefreshLoop()); }

        private IEnumerator RefreshLoop()
        {
            while (true)
            {
                Refresh();
                yield return new WaitForSecondsRealtime(5f);
            }
        }

        public void Refresh()
        {
            foreach (var row in _rows) Destroy(row);
            _rows.Clear();

            var rooms = RoomManager.Instance != null ? RoomManager.Instance.GetAvailableRooms() : new List<RoomData>();
            if (rooms.Count == 0)
            {
                _statusLabel.text = "No rooms open - create one.";
                return;
            }
            _statusLabel.text = $"{rooms.Count} room(s) open.";

            float y = 0;
            foreach (var room in rooms)
            {
                string roomId = room.RoomId;
                var row = WalletUiKit.Container(_listContent, "Row_" + roomId, new Vector2(600, 80), new Vector2(0, y),
                                                new Color(1f, 1f, 1f, 0.06f));
                WalletUiKit.Label(row.transform, $"Fee {room.EntryFee:0}  -  Waiting {room.PlayerIds.Count}/{room.MaxPlayers}",
                                  26, new Vector2(400, 70), new Vector2(-90, 0), TextAnchor.MiddleLeft);
                WalletUiKit.Btn(row.transform, "Join", new Vector2(140, 60), new Vector2(220, 0), () => OnJoinClicked(roomId));
                _rows.Add(row);
                y -= 90;
            }
        }

        public void OnCreateClicked()
        {
            if (RoomManager.Instance == null) return;
            decimal fee = FeeOptions[Mathf.Clamp(_feeDropdown.value, 0, FeeOptions.Length - 1)];
            string roomId = RoomManager.Instance.CreateAndJoinRoom(fee, 4, out bool joined, out string error);
            _statusLabel.text = joined ? $"Created and joined room {roomId} (fee {fee:0})." : $"Created {roomId}, but couldn't join: {error}";
            Refresh();
        }

        private void OnJoinClicked(string roomId)
        {
            if (RoomManager.Instance == null) return;
            bool ok = RoomManager.Instance.JoinRoom(roomId, out string error);
            _statusLabel.text = ok ? $"Joined {roomId}." : $"Join failed: {error}";
            Refresh();
        }

        private void BuildUI()
        {
            var canvas = WalletUiKit.EnsureCanvas("WalletCanvas");
            _root = WalletUiKit.Container(canvas.transform, "RoomUI", new Vector2(780, 900), Vector2.zero, WalletUiKit.Panel);
            WalletUiKit.Label(_root.transform, "ONLINE ROOMS", 40, new Vector2(720, 60), new Vector2(0, 400));

            _feeDropdown = WalletUiKit.DropdownField(_root.transform, new[] { "10", "20", "50", "100", "500" },
                                                     new Vector2(300, 70), new Vector2(-190, 330));
            WalletUiKit.Btn(_root.transform, "Create Room", new Vector2(260, 70), new Vector2(180, 330), OnCreateClicked);

            _statusLabel = WalletUiKit.Label(_root.transform, "", 24, new Vector2(720, 40), new Vector2(0, 270));

            var viewport = WalletUiKit.Container(_root.transform, "Viewport", new Vector2(700, 560), new Vector2(0, -30),
                                                 new Color(1f, 1f, 1f, 0.03f));
            viewport.AddComponent<RectMask2D>();
            var scroll = _root.AddComponent<ScrollRect>();
            scroll.viewport = WalletUiKit.Rect(viewport);
            scroll.horizontal = false;

            var content = WalletUiKit.Container(viewport.transform, "Content", new Vector2(680, 2000), Vector2.zero);
            _listContent = WalletUiKit.Rect(content);
            _listContent.pivot = new Vector2(0.5f, 1f);
            scroll.content = _listContent;

            _root.SetActive(false);
        }

        public void Open()  { if (_root != null) { _root.SetActive(true); Refresh(); } }
        public void Close() { if (_root != null) _root.SetActive(false); }
    }
}
