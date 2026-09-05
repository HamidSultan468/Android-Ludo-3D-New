using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TangentLudoEmpire.Wallet.UI;

namespace TangentLudoEmpire.Social
{
    /// <summary>Task C.5. Popup list of recent games, same self-building style as
    /// Phase3/UI/TransactionHistoryUI.</summary>
    [DisallowMultipleComponent]
    public class GameHistoryUI : MonoBehaviour
    {
        private const int ShowLast = 20;

        private GameObject _root;
        private Text _listLabel;

        private void Awake() { if (_root == null) BuildUI(); Close(); }

        public void Refresh()
        {
            if (GameHistoryManager.Instance == null) { _listLabel.text = "(history unavailable)"; return; }

            var history = GameHistoryManager.Instance.GetHistory();
            if (history.Count == 0) { _listLabel.text = "(no games played yet)"; return; }

            var sb = new StringBuilder();
            sb.AppendLine("  WHEN (UTC)      ENTRY      WIN     RESULT");
            int start = Mathf.Max(0, history.Count - ShowLast);
            for (int i = history.Count - 1; i >= start; i--)
            {
                var r = history[i];
                sb.AppendLine($"  {r.Time:MM-dd HH:mm}  {r.Entry,8:0.00} {r.Win,8:0.00}  {(r.IsWin ? "WIN" : "LOSS")}");
            }
            _listLabel.text = sb.ToString();
        }

        private void BuildUI()
        {
            var canvas = WalletUiKit.EnsureCanvas("WalletCanvas");
            _root = WalletUiKit.Container(canvas.transform, "GameHistoryUI", new Vector2(760, 900), Vector2.zero, WalletUiKit.Panel);
            WalletUiKit.Label(_root.transform, "GAME HISTORY", 38, new Vector2(700, 60), new Vector2(0, 400));

            var box = WalletUiKit.Container(_root.transform, "ListBox", new Vector2(700, 680), new Vector2(0, -10),
                                            new Color(1f, 1f, 1f, 0.04f));
            _listLabel = WalletUiKit.Label(box.transform, "", 22, new Vector2(660, 660), Vector2.zero, TextAnchor.UpperLeft);

            WalletUiKit.Btn(_root.transform, "Close", new Vector2(260, 80), new Vector2(0, -400), Close,
                            new Color(0.3f, 0.35f, 0.42f, 1f));
        }

        public void Open()  { if (_root != null) { _root.SetActive(true); Refresh(); } }
        public void Close() { if (_root != null) _root.SetActive(false); }
    }
}
