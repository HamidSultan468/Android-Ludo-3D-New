using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TangentLudoEmpire.Payments;
using TangentLudoEmpire.Services;

namespace TangentLudoEmpire.Wallet.UI
{
    /// <summary>
    /// Provider [JazzCash | Easypaisa] + amount + mobile number -> the selected
    /// <see cref="IPaymentGateway"/>.Deposit -> on success, <see cref="MoneyWallet.AddFunds"/> with the
    /// returned gateway ref (which the anti-cheat gate then verifies). Text + Button only, plus one
    /// legacy Dropdown for the provider picker.
    /// </summary>
    [DisallowMultipleComponent]
    public class DepositPopup : MonoBehaviour
    {
        private static readonly PaymentProvider[] Providers = { PaymentProvider.JazzCash, PaymentProvider.Easypaisa };

        [SerializeField] private Dropdown providerDropdown;
        [SerializeField] private InputField amountField;
        [SerializeField] private InputField phoneField;
        [SerializeField] private Text statusLabel;
        [SerializeField] private Button confirmButton;

        private GameObject _root;
        private Coroutine _spinner;

        private void Awake()
        {
            if (amountField == null) BuildUI();
            Close();
        }

        public void Open()  { if (_root != null) _root.SetActive(true); SetStatus(""); }
        public void Close() { StopSpinner(); if (_root != null) _root.SetActive(false); }

        public void Confirm()
        {
            if (!decimal.TryParse(amountField != null ? amountField.text : "", NumberStyles.Number,
                                  CultureInfo.InvariantCulture, out var amount) || amount <= 0m)
            {
                SetStatus("Enter a valid amount.");
                return;
            }

            string phone = phoneField != null ? phoneField.text : "";
            if (string.IsNullOrWhiteSpace(phone))
            {
                SetStatus("Enter a mobile number.");
                return;
            }

            if (MoneyWallet.Instance == null) { SetStatus("Wallet unavailable."); return; }

            int idx = Mathf.Clamp(providerDropdown != null ? providerDropdown.value : 0, 0, Providers.Length - 1);
            var provider = Providers[idx];
            var gateway = PaymentGatewayFactory.GetGateway(provider);

            SetBusy(true);
            StartSpinner(gateway.GatewayName);
            AuditLog.Log("DEPOSIT_ATTEMPT", $"{amount:0.00} via {gateway.GatewayName}", phone);

            // Phase 3.2: gateway.Deposit's callback fires only once money has actually moved - JazzCash/
            // Easypaisa run the WebView + pending-deposit + anti-cheat gate internally (see MoneyWallet.
            // BeginPendingDeposit/ConfirmPendingDeposit) and Mock credits internally too, so the UI must
            // NOT call MoneyWallet.AddFunds itself here - that would double-credit.
            gateway.Deposit(amount, phone, (ok, refOrErr) =>
            {
                SetBusy(false);
                StopSpinner();
                if (ok)
                {
                    SetStatus($"Deposited {amount:0.00} via {gateway.GatewayName}. Ref {refOrErr}");
                    Invoke(nameof(Close), 1.2f);
                }
                else
                {
                    SetStatus($"{gateway.GatewayName} deposit failed: {refOrErr}");
                    AuditLog.Log("DEPOSIT_GATEWAY_FAIL", $"{amount:0.00} via {gateway.GatewayName}", refOrErr);
                }
            });
        }

        private void SetBusy(bool busy) { if (confirmButton != null) confirmButton.interactable = !busy; }
        private void SetStatus(string s) { if (statusLabel != null) statusLabel.text = s; if (!string.IsNullOrEmpty(s)) Debug.Log("[DepositPopup] " + s); }

        // ---- text-only "spinner" (Text + Button constraint - no art) ----

        private void StartSpinner(string gatewayName)
        {
            StopSpinner();
            _spinner = StartCoroutine(SpinnerRoutine(gatewayName));
        }

        private void StopSpinner()
        {
            if (_spinner != null) { StopCoroutine(_spinner); _spinner = null; }
        }

        private IEnumerator SpinnerRoutine(string gatewayName)
        {
            string[] frames = { ".", "..", "..." };
            int i = 0;
            while (true)
            {
                if (statusLabel != null) statusLabel.text = $"Contacting {gatewayName}{frames[i % frames.Length]}";
                i++;
                yield return new WaitForSecondsRealtime(0.4f);
            }
        }

        private void BuildUI()
        {
            var canvas = WalletUiKit.EnsureCanvas("WalletCanvas");
            _root = WalletUiKit.Container(canvas.transform, "DepositPopup", new Vector2(760, 760), Vector2.zero, WalletUiKit.Panel);
            WalletUiKit.Label(_root.transform, "DEPOSIT", 42, new Vector2(700, 70), new Vector2(0, 320));

            providerDropdown = WalletUiKit.DropdownField(_root.transform, new[] { "JazzCash", "Easypaisa" },
                                                         new Vector2(620, 80), new Vector2(0, 220));
            amountField = WalletUiKit.Input(_root.transform, "Amount", new Vector2(620, 90), new Vector2(0, 120), numeric: true);
            phoneField  = WalletUiKit.Input(_root.transform, "Mobile number (03XXXXXXXXX)", new Vector2(620, 90), new Vector2(0, 20), numeric: false);
            statusLabel = WalletUiKit.Label(_root.transform, "", 24, new Vector2(700, 60), new Vector2(0, -60));
            confirmButton = WalletUiKit.Btn(_root.transform, "Confirm", new Vector2(300, 90), new Vector2(-160, -260), Confirm);
            WalletUiKit.Btn(_root.transform, "Cancel", new Vector2(300, 90), new Vector2(160, -260), Close,
                            new Color(0.3f, 0.35f, 0.42f, 1f));
        }
    }
}
