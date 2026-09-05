using UnityEngine;
using UnityEngine.UI;
using TangentLudoEmpire.Wallet.UI; // WalletUiKit - internal, same assembly (no asmdef in this project)

namespace TangentLudoEmpire.Polish
{
    /// <summary>
    /// Task C.3. SPEC CORRECTION: "LegalPagesUI.cs : Canvas" - built as a self-building MonoBehaviour via
    /// WalletUiKit, same correction as every other "X : Canvas" ask (see LoadingScreenManager, TournamentUI).
    /// Three tabs (Terms &amp; Conditions / Privacy Policy / Refund Policy) load their body text from
    /// <c>Resources/LegalText/*.txt</c> as <see cref="TextAsset"/>s - editable without a recompile, and a
    /// human/legal reviewer can replace the placeholder copy without touching this script.
    /// Always shows the "18+ Age Restriction / Play Responsibly" banner regardless of which tab is open.
    /// </summary>
    [DisallowMultipleComponent]
    public class LegalPagesUI : MonoBehaviour
    {
        private enum Page { Terms, Privacy, Refund }

        private GameObject _root;
        private Text _titleLabel;
        private Text _bodyLabel;

        private void Awake() { if (_root == null) BuildUI(); }

        public void Open()
        {
            if (_root == null) BuildUI();
            _root.SetActive(true);
            ShowPage(Page.Terms);
        }

        public void Close() { if (_root != null) _root.SetActive(false); }

        private void ShowPage(Page page)
        {
            string resourceName = page switch
            {
                Page.Terms => "TermsAndConditions",
                Page.Privacy => "PrivacyPolicy",
                Page.Refund => "RefundPolicy",
                _ => "TermsAndConditions",
            };

            var asset = Resources.Load<TextAsset>("LegalText/" + resourceName);
            _titleLabel.text = ToTitle(page);
            _bodyLabel.text = asset != null ? asset.text
                : $"({resourceName}.txt not found in Resources/LegalText/ - see MIGRATION_NOTES Phase 5.)";
        }

        private static string ToTitle(Page page) => page switch
        {
            Page.Terms => "Terms & Conditions",
            Page.Privacy => "Privacy Policy",
            Page.Refund => "Refund Policy",
            _ => "",
        };

        private void BuildUI()
        {
            var canvas = WalletUiKit.EnsureCanvas("WalletCanvas");
            _root = WalletUiKit.Container(canvas.transform, "LegalPagesUI", new Vector2(1000, 1760), Vector2.zero, WalletUiKit.Panel);

            // Required 18+ / Play Responsibly banner - always visible, independent of the active tab.
            var banner = WalletUiKit.Container(_root.transform, "AgeBanner", new Vector2(960, 70), new Vector2(0, 830), WalletUiKit.Danger);
            WalletUiKit.Label(banner.transform, "18+  AGE RESTRICTED   |   PLAY RESPONSIBLY", 24, new Vector2(940, 60), Vector2.zero);

            _titleLabel = WalletUiKit.Label(_root.transform, "", 32, new Vector2(900, 50), new Vector2(0, 750));

            WalletUiKit.Btn(_root.transform, "Terms",   new Vector2(280, 70), new Vector2(-320, 680), () => ShowPage(Page.Terms));
            WalletUiKit.Btn(_root.transform, "Privacy", new Vector2(280, 70), new Vector2(0, 680),    () => ShowPage(Page.Privacy));
            WalletUiKit.Btn(_root.transform, "Refund",  new Vector2(280, 70), new Vector2(320, 680),  () => ShowPage(Page.Refund));

            var bodyBox = WalletUiKit.Container(_root.transform, "BodyBox", new Vector2(940, 1280), new Vector2(0, 20), new Color(1f, 1f, 1f, 0.04f));
            _bodyLabel = WalletUiKit.Label(bodyBox.transform, "", 20, new Vector2(900, 1220), Vector2.zero, TextAnchor.UpperLeft);

            WalletUiKit.Btn(_root.transform, "Close", new Vector2(220, 70), new Vector2(0, -840), Close);

            _root.SetActive(false);
        }

#if UNITY_EDITOR
        /// <summary>EDITOR ONLY - compiled out of every player build.</summary>
        public bool Editor_IsOpen => _root != null && _root.activeSelf;
        public string Editor_CurrentBodyText => _bodyLabel != null ? _bodyLabel.text : "";
        public void Editor_ShowPage(int pageIndex) => ShowPage((Page)Mathf.Clamp(pageIndex, 0, 2));
#endif
    }
}
