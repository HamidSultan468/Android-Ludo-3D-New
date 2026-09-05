using UnityEngine;
using UnityEngine.UI;

namespace LudoGame.Empire.UI
{
    /// <summary>Always-on HUD: tracked material amount, Silver Coins, and trees felled.</summary>
    public class EmpireHudUI : MonoBehaviour
    {
        [SerializeField] private EmpireManager empireManager;
        [SerializeField] private TreeChopInput treeChopInput;
        [Tooltip("Which material to show on the HUD (e.g. Wood).")]
        [SerializeField] private MaterialDefinition trackedMaterial;

        [SerializeField] private Text materialText;
        [SerializeField] private Text silverCoinsText;
        [SerializeField] private Text treesFelledText;

        private void OnEnable()
        {
            if (empireManager != null)
            {
                empireManager.Inventory.OnMaterialChanged += HandleMaterialChanged;
                empireManager.Wallet.OnCurrencyChanged += HandleCurrencyChanged;
            }
            if (treeChopInput != null)
                treeChopInput.OnTreesFelledChanged += HandleTreesFelledChanged;

            Refresh();
        }

        private void OnDisable()
        {
            if (empireManager != null)
            {
                empireManager.Inventory.OnMaterialChanged -= HandleMaterialChanged;
                empireManager.Wallet.OnCurrencyChanged -= HandleCurrencyChanged;
            }
            if (treeChopInput != null)
                treeChopInput.OnTreesFelledChanged -= HandleTreesFelledChanged;
        }

        private void Refresh()
        {
            if (empireManager == null) return;

            if (materialText != null && trackedMaterial != null)
                materialText.text = trackedMaterial.displayName + ": " + empireManager.Inventory.GetAmount(trackedMaterial);

            if (silverCoinsText != null)
                silverCoinsText.text = empireManager.Wallet.SilverCoins + " Silver";

            if (treesFelledText != null && treeChopInput != null)
                treesFelledText.text = "Trees Felled: " + treeChopInput.TreesFelled + " / 10";
        }

        private void HandleMaterialChanged(MaterialDefinition material, int amount)
        {
            if (materialText != null && material == trackedMaterial)
                materialText.text = material.displayName + ": " + amount;
        }

        private void HandleCurrencyChanged(CurrencyType type, int amount)
        {
            if (silverCoinsText != null && type == CurrencyType.SilverCoins)
                silverCoinsText.text = amount + " Silver";
        }

        private void HandleTreesFelledChanged(int count)
        {
            if (treesFelledText != null)
                treesFelledText.text = "Trees Felled: " + count + " / 10";
        }
    }
}
