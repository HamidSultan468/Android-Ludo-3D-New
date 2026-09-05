using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace TangentLudoEmpire.Wallet.UI
{
    /// <summary>
    /// Scrollable list of the last 20 transactions (newest first). One monospace <see cref="Text"/> block
    /// inside a <see cref="ScrollRect"/> - no per-row prefabs, deliberately plain.
    /// </summary>
    [DisallowMultipleComponent]
    public class TransactionHistoryUI : MonoBehaviour
    {
        private const int ShowLast = 20;

        [SerializeField] private Text listLabel;
        private GameObject _root;

        private void Awake()
        {
            if (listLabel == null) BuildUI();
            Close();
        }

        public void Open()  { if (_root != null) _root.SetActive(true); Refresh(); }
        public void Close() { if (_root != null) _root.SetActive(false); }

        public void Refresh()
        {
            if (listLabel == null) return;
            var wallet = MoneyWallet.Instance;
            if (wallet == null) { listLabel.text = "(no wallet)"; return; }

            var history = wallet.GetHistory();
            if (history.Count == 0) { listLabel.text = "(no transactions yet)"; return; }

            var sb = new StringBuilder();
            sb.AppendLine("  WHEN (UTC)      TYPE        AMOUNT   STATUS");
            sb.AppendLine("  ------------------------------------------------");
            int start = Mathf.Max(0, history.Count - ShowLast);
            for (int i = history.Count - 1; i >= start; i--)
            {
                var t = history[i];
                sb.AppendLine($"  {t.TimestampUtc:MM-dd HH:mm}  {t.Type,-10} {t.Amount,9:0.00}  {t.Status}");
            }
            listLabel.text = sb.ToString();
        }

        private void BuildUI()
        {
            var canvas = WalletUiKit.EnsureCanvas("WalletCanvas");
            _root = WalletUiKit.Container(canvas.transform, "TransactionHistoryUI", new Vector2(900, 1100), Vector2.zero, WalletUiKit.Panel);
            WalletUiKit.Label(_root.transform, "TRANSACTION HISTORY", 40, new Vector2(840, 70), new Vector2(0, 480));

            var viewport = WalletUiKit.Container(_root.transform, "Viewport", new Vector2(840, 840), new Vector2(0, -20),
                                                new Color(1f, 1f, 1f, 0.04f));
            viewport.AddComponent<RectMask2D>();
            var scroll = _root.AddComponent<ScrollRect>();
            scroll.viewport = WalletUiKit.Rect(viewport);
            scroll.horizontal = false;

            var content = WalletUiKit.Container(viewport.transform, "Content", new Vector2(820, 2000), Vector2.zero);
            var crt = WalletUiKit.Rect(content);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = new Vector2(0, 0);
            scroll.content = crt;

            listLabel = WalletUiKit.Label(content.transform, "", 24, new Vector2(800, 1960), Vector2.zero, TextAnchor.UpperLeft);
            listLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            listLabel.verticalOverflow = VerticalWrapMode.Overflow;

            WalletUiKit.Btn(_root.transform, "Close", new Vector2(300, 90), new Vector2(0, -500), Close,
                            new Color(0.3f, 0.35f, 0.42f, 1f));
        }
    }
}
