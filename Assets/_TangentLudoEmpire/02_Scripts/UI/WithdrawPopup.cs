using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TangentLudoEmpire.Payments;
using TangentLudoEmpire.Services;
using TangentLudoEmpire.Kyc;

namespace TangentLudoEmpire.Wallet.UI
{
    /// <summary>
    /// Phase 3.3 Task 6 (replaces the Phase 3 mock-gateway version). Logic: Check KYC -&gt; Check
    /// AntiFraud -&gt; Check Balance -&gt; RequestWithdraw. These are UX fast-path pre-checks for instant
    /// feedback - <see cref="MoneyWallet.RequestWithdraw"/> re-validates all of them itself and is the
    /// actual authority; a UI-only check can never be trusted to gate real money on its own.
    /// If RequestWithdraw resolves Paid (amount &lt;= 10000, auto-payout): shows the payout ref.
    /// If it resolves Pending (amount &gt; 10000): shows "Admin Approval Pending".
    /// </summary>
    [DisallowMultipleComponent]
    public class WithdrawPopup : MonoBehaviour
    {
        private static readonly PaymentProvider[] Providers = { PaymentProvider.JazzCash, PaymentProvider.Easypaisa };

        [SerializeField] private Dropdown providerDropdown;
        [SerializeField] private InputField amountField;
        [SerializeField] private InputField accountField;
        [SerializeField] private Text statusLabel;
        [SerializeField] private Button confirmButton;

        private GameObject _root;

        private void Awake()
        {
            if (amountField == null) BuildUI();
            Close();
        }

        public void Open()  { if (_root != null) _root.SetActive(true); SetStatus(""); }
        public void Close() { if (_root != null) _root.SetActive(false); }

        public void Confirm()
        {
            if (!decimal.TryParse(amountField != null ? amountField.text : "", NumberStyles.Number,
                                  CultureInfo.InvariantCulture, out var amount) || amount <= 0m)
            {
                SetStatus("Enter a valid amount.");
                return;
            }
            string account = accountField != null ? accountField.text : "";
            if (string.IsNullOrWhiteSpace(account)) { SetStatus("Enter a payout account."); return; }

            var wallet = MoneyWallet.Instance;
            if (wallet == null) { SetStatus("Wallet unavailable."); return; }

            // ---- UX fast-path pre-checks (Task 6) - RequestWithdraw re-checks all of these itself ----
            if (KycManager.Instance == null || !KycManager.Instance.CanWithdraw())
            {
                SetStatus("Identity verification (KYC) required before withdrawing.");
                return;
            }
            if (AntiFraudManager.Instance != null && !AntiFraudManager.Instance.CanTransact("withdraw", amount))
            {
                SetStatus("Withdrawal limit reached for now - try a smaller amount or again later.");
                return;
            }
            if (wallet.GetBalance() < amount)
            {
                SetStatus("Insufficient balance.");
                return;
            }

            int idx = Mathf.Clamp(providerDropdown != null ? providerDropdown.value : 0, 0, Providers.Length - 1);
            string provider = Providers[idx].ToString();

            SetBusy(true);
            AuditLog.Log("WITHDRAW_ATTEMPT", $"{amount:0.00} via {provider}", account);

            wallet.RequestWithdraw(amount, provider, account, (result, refOrId) =>
            {
                SetBusy(false);
                switch (result)
                {
                    case WithdrawResult.Paid:
                        SetStatus($"Withdrawn {amount:0.00} via {provider}. Ref {refOrId}");
                        Invoke(nameof(Close), 1.2f);
                        break;
                    case WithdrawResult.Pending:
                        SetStatus("Request submitted. Admin Approval Pending.");
                        Invoke(nameof(Close), 1.5f);
                        break;
                    case WithdrawResult.Rejected:
                    default:
                        SetStatus($"Withdraw failed: {refOrId}");
                        AuditLog.Log("WITHDRAW_REJECTED", $"{amount:0.00} via {provider}", refOrId);
                        break;
                }
            });
        }

        private void SetBusy(bool busy) { if (confirmButton != null) confirmButton.interactable = !busy; }
        private void SetStatus(string s) { if (statusLabel != null) statusLabel.text = s; if (!string.IsNullOrEmpty(s)) Debug.Log("[WithdrawPopup] " + s); }

        private void BuildUI()
        {
            var canvas = WalletUiKit.EnsureCanvas("WalletCanvas");
            _root = WalletUiKit.Container(canvas.transform, "WithdrawPopup", new Vector2(760, 680), Vector2.zero, WalletUiKit.Panel);
            WalletUiKit.Label(_root.transform, "WITHDRAW", 42, new Vector2(700, 70), new Vector2(0, 280));

            providerDropdown = WalletUiKit.DropdownField(_root.transform, new[] { "JazzCash", "Easypaisa" },
                                                         new Vector2(620, 80), new Vector2(0, 190));
            amountField  = WalletUiKit.Input(_root.transform, "Amount",  new Vector2(620, 90), new Vector2(0, 90), numeric: true);
            accountField = WalletUiKit.Input(_root.transform, "Account", new Vector2(620, 90), new Vector2(0, -10), numeric: false);
            statusLabel  = WalletUiKit.Label(_root.transform, "", 24, new Vector2(700, 60), new Vector2(0, -90));
            confirmButton = WalletUiKit.Btn(_root.transform, "Confirm", new Vector2(300, 90), new Vector2(-160, -220), Confirm);
            WalletUiKit.Btn(_root.transform, "Cancel", new Vector2(300, 90), new Vector2(160, -220), Close,
                            new Color(0.3f, 0.35f, 0.42f, 1f));
        }
    }
}
