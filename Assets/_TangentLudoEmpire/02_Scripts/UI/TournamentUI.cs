using UnityEngine;
using UnityEngine.UI;
using TangentLudoEmpire.Wallet.UI;

namespace TangentLudoEmpire.Tournament
{
    /// <summary>
    /// Task B.2. SPEC CORRECTION: asked for "TournamentUI.cs : Canvas" - <c>Canvas</c> is Unity's built-in
    /// rendering component, not something game-logic scripts subclass (same correction already applied to
    /// WalletUI/DepositPopup/etc. in Phase 3). This is a MonoBehaviour that builds its own Canvas via the
    /// same WalletUiKit self-building pattern as every other Phase 3/4 UI script.
    /// </summary>
    [DisallowMultipleComponent]
    public class TournamentUI : MonoBehaviour
    {
        private GameObject _root;
        private Text _infoLabel;
        private Text _bracketLabel;
        private Text _statusLabel;

        private void Awake() { if (_root == null) BuildUI(); }

        private void OnEnable()
        {
            if (TournamentManager.Instance != null)
            {
                TournamentManager.Instance.OnTournamentStarted += _ => Refresh();
                TournamentManager.Instance.OnTournamentCompleted += _ => Refresh();
            }
            Refresh();
        }

        public void Refresh()
        {
            var t = TournamentManager.Instance != null ? TournamentManager.Instance.Current : null;
            if (t == null) { _infoLabel.text = "No active tournament."; _bracketLabel.text = ""; return; }

            _infoLabel.text = $"Entry Fee: {t.EntryFee:0.00}   Prize Pool: {t.PrizePool:0.00}   " +
                              $"Players: {t.Players.Count}/{t.MaxPlayers}   Status: {t.Status}";

            var bracket = TournamentManager.Instance.Bracket;
            if (bracket.Count == 0) { _bracketLabel.text = "(bracket not seeded yet - fills at max players)"; return; }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("BRACKET (Round 1):");
            for (int i = 0; i < bracket.Count; i++)
                sb.AppendLine($"  Match {i + 1}: {bracket[i][0]}  vs  {bracket[i][1]}");
            if (t.Status == TournamentStatus.Completed)
            {
                sb.AppendLine();
                sb.AppendLine($"Winner: {t.WinnerId}" + (string.IsNullOrEmpty(t.RunnerUpId) ? "" : $"   Runner-up: {t.RunnerUpId}"));
            }
            _bracketLabel.text = sb.ToString();
        }

        public void OnRegisterClicked(float fee)
        {
            string error = TournamentManager.Instance == null ? "Tournament system unavailable" : null;
            bool ok = error == null && TournamentManager.Instance.RegisterTournament((decimal)fee, out error);
            _statusLabel.text = ok ? "Registered." : $"Register failed: {error}";
            Refresh();
        }

        private void BuildUI()
        {
            var canvas = WalletUiKit.EnsureCanvas("WalletCanvas");
            _root = WalletUiKit.Container(canvas.transform, "TournamentUI", new Vector2(800, 900), Vector2.zero, WalletUiKit.Panel);
            WalletUiKit.Label(_root.transform, "TOURNAMENT", 40, new Vector2(740, 60), new Vector2(0, 400));

            _infoLabel = WalletUiKit.Label(_root.transform, "", 24, new Vector2(740, 60), new Vector2(0, 340));
            WalletUiKit.Btn(_root.transform, "Register (100)", new Vector2(300, 80), new Vector2(0, 260), () => OnRegisterClicked(100f));
            _statusLabel = WalletUiKit.Label(_root.transform, "", 22, new Vector2(740, 40), new Vector2(0, 200));

            var bracketBox = WalletUiKit.Container(_root.transform, "BracketBox", new Vector2(740, 480), new Vector2(0, -60),
                                                   new Color(1f, 1f, 1f, 0.04f));
            _bracketLabel = WalletUiKit.Label(bracketBox.transform, "", 22, new Vector2(700, 460), Vector2.zero, TextAnchor.UpperLeft);

            _root.SetActive(false);
        }

        public void Open()  { if (_root != null) { _root.SetActive(true); Refresh(); } }
        public void Close() { if (_root != null) _root.SetActive(false); }
    }
}
