using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TangentLudoEmpire.Wallet.UI;

namespace TangentLudoEmpire.Social
{
    /// <summary>Task C.2. SPEC CORRECTION: asked for "Canvas with 3 Tabs" - same Canvas-is-not-a-base-
    /// class correction as TournamentUI. Self-building MonoBehaviour with 3 tab buttons
    /// (Daily/Weekly/AllTime) driving one list.</summary>
    [DisallowMultipleComponent]
    public class LeaderboardUI : MonoBehaviour
    {
        private GameObject _root;
        private Text _listLabel;
        private LeaderboardType _activeTab = LeaderboardType.AllTime;

        private void Awake() { if (_root == null) BuildUI(); }
        private void OnEnable() => Refresh();

        public void SelectTab(int type)
        {
            _activeTab = (LeaderboardType)type;
            Refresh();
        }

        public void Refresh()
        {
            if (LeaderboardManager.Instance == null) { _listLabel.text = "(leaderboard unavailable)"; return; }

            var top = LeaderboardManager.Instance.GetTop10(_activeTab);
            var sb = new StringBuilder();
            sb.AppendLine($"[{_activeTab}]");
            sb.AppendLine("  #  Name            Winnings   Wins");
            for (int i = 0; i < top.Count; i++)
            {
                var p = top[i];
                sb.AppendLine($"  {i + 1,2} {p.Name,-14} {p.TotalWinnings,10:0.00} {p.Wins,6}");
            }
            _listLabel.text = sb.ToString();
        }

        private void BuildUI()
        {
            var canvas = WalletUiKit.EnsureCanvas("WalletCanvas");
            _root = WalletUiKit.Container(canvas.transform, "LeaderboardUI", new Vector2(760, 900), Vector2.zero, WalletUiKit.Panel);
            WalletUiKit.Label(_root.transform, "LEADERBOARD", 40, new Vector2(700, 60), new Vector2(0, 400));

            WalletUiKit.Btn(_root.transform, "Daily",   new Vector2(220, 70), new Vector2(-250, 320), () => SelectTab(0));
            WalletUiKit.Btn(_root.transform, "Weekly",  new Vector2(220, 70), new Vector2(0, 320),    () => SelectTab(1));
            WalletUiKit.Btn(_root.transform, "AllTime", new Vector2(220, 70), new Vector2(250, 320),  () => SelectTab(2));

            var box = WalletUiKit.Container(_root.transform, "ListBox", new Vector2(700, 560), new Vector2(0, -20),
                                            new Color(1f, 1f, 1f, 0.04f));
            _listLabel = WalletUiKit.Label(box.transform, "", 22, new Vector2(660, 540), Vector2.zero, TextAnchor.UpperLeft);

            _root.SetActive(false);
        }

        public void Open()  { if (_root != null) { _root.SetActive(true); Refresh(); } }
        public void Close() { if (_root != null) _root.SetActive(false); }
    }
}
