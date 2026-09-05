using UnityEngine;
using UnityEngine.UI;

namespace LudoGame.Empire.UI
{
    /// <summary>
    /// A "Sell Wood" button: sells all of one material at the market's base
    /// price for Silver Coins. This is the local Milestone 1 stand-in for the
    /// later "player-driven global market" pillar (which needs a live server).
    /// </summary>
    public class MarketUI : MonoBehaviour
    {
        [SerializeField] private EmpireManager empireManager;
        [SerializeField] private MaterialDefinition material;
        [SerializeField] private Button sellButton;
        [SerializeField] private Text sellButtonLabel;

        private void OnEnable()
        {
            if (sellButton != null) sellButton.onClick.AddListener(HandleSellClicked);
            if (empireManager != null) empireManager.Inventory.OnMaterialChanged += HandleMaterialChanged;

            RefreshLabel();
        }

        private void OnDisable()
        {
            if (sellButton != null) sellButton.onClick.RemoveListener(HandleSellClicked);
            if (empireManager != null) empireManager.Inventory.OnMaterialChanged -= HandleMaterialChanged;
        }

        private void HandleSellClicked()
        {
            if (empireManager == null || material == null) return;
            empireManager.SellAll(material);
        }

        private void HandleMaterialChanged(MaterialDefinition changed, int amount)
        {
            if (changed == material) RefreshLabel();
        }

        private void RefreshLabel()
        {
            if (sellButtonLabel == null || empireManager == null || material == null) return;

            int amount = empireManager.Inventory.GetAmount(material);
            sellButtonLabel.text = "Sell " + amount + " " + material.displayName + " (" + (amount * material.baseSellPrice) + " Silver)";
        }
    }
}
