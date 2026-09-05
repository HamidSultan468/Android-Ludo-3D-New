using UnityEngine;
using UnityEngine.UI;

namespace TangentLudoEmpire.Wallet.UI
{
    /// <summary>
    /// Wallet home panel: shows the balance and the [Deposit] [Withdraw] [History] buttons.
    /// Self-builds a plain uGUI hierarchy if no <see cref="balanceLabel"/> is wired in the Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public class WalletUI : MonoBehaviour
    {
        [SerializeField] private Text balanceLabel;
        [SerializeField] private DepositPopup depositPopup;
        [SerializeField] private WithdrawPopup withdrawPopup;
        [SerializeField] private TransactionHistoryUI historyUI;

        private void Awake()
        {
            if (balanceLabel == null) BuildUI();
        }

        private void OnEnable()
        {
            if (MoneyWallet.Instance != null) MoneyWallet.Instance.OnBalanceChanged += Refresh;
            RefreshNow();
        }

        private void OnDisable()
        {
            if (MoneyWallet.Instance != null) MoneyWallet.Instance.OnBalanceChanged -= Refresh;
        }

        private void Refresh(decimal balance) => RefreshNow();

        private void RefreshNow()
        {
            if (balanceLabel == null) return;
            decimal bal = MoneyWallet.Instance != null ? MoneyWallet.Instance.GetBalance() : 0m;
            balanceLabel.text = $"Balance:  {bal:0.00}";
        }

        private void BuildUI()
        {
            var canvas = WalletUiKit.EnsureCanvas("WalletCanvas");
            var panel = WalletUiKit.Container(canvas.transform, "WalletPanel", new Vector2(760, 620), Vector2.zero, WalletUiKit.Panel);

            WalletUiKit.Label(panel.transform, "WALLET", 44, new Vector2(700, 70), new Vector2(0, 250));
            balanceLabel = WalletUiKit.Label(panel.transform, "Balance:  0.00", 40, new Vector2(700, 80), new Vector2(0, 150));

            WalletUiKit.Btn(panel.transform, "Deposit",  new Vector2(640, 90), new Vector2(0, 30),   OpenDeposit);
            WalletUiKit.Btn(panel.transform, "Withdraw", new Vector2(640, 90), new Vector2(0, -80),  OpenWithdraw);
            WalletUiKit.Btn(panel.transform, "History",  new Vector2(640, 90), new Vector2(0, -190), OpenHistory,
                            new Color(0.3f, 0.35f, 0.42f, 1f));
        }

        // ---- button handlers ----

        public void OpenDeposit()
        {
            if (depositPopup == null) depositPopup = GetOrCreate<DepositPopup>("DepositPopup");
            depositPopup.Open();
        }

        public void OpenWithdraw()
        {
            if (withdrawPopup == null) withdrawPopup = GetOrCreate<WithdrawPopup>("WithdrawPopup");
            withdrawPopup.Open();
        }

        public void OpenHistory()
        {
            if (historyUI == null) historyUI = GetOrCreate<TransactionHistoryUI>("TransactionHistoryUI");
            historyUI.Open();
        }

        private static T GetOrCreate<T>(string name) where T : MonoBehaviour
        {
            var found = FindAnyObjectByType<T>();
            if (found != null) return found;
            return new GameObject(name).AddComponent<T>();
        }
    }
}
