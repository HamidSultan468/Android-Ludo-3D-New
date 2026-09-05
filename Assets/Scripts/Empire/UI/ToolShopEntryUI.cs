using UnityEngine;
using UnityEngine.UI;

namespace LudoGame.Empire.UI
{
    /// <summary>One row in the tool shop: shows a tool's name/cost and buys it on click.</summary>
    public class ToolShopEntryUI : MonoBehaviour
    {
        [SerializeField] private EmpireManager empireManager;
        [SerializeField] private ToolDefinition tool;
        [SerializeField] private Button buyButton;
        [SerializeField] private Text label;

        private void OnEnable()
        {
            if (buyButton != null) buyButton.onClick.AddListener(HandleBuyClicked);
            Refresh();
        }

        private void OnDisable()
        {
            if (buyButton != null) buyButton.onClick.RemoveListener(HandleBuyClicked);
        }

        private void HandleBuyClicked()
        {
            if (empireManager == null || tool == null) return;
            if (empireManager.TryBuyTool(tool))
                Refresh();
        }

        private void Refresh()
        {
            if (empireManager == null || tool == null) return;

            bool owned = empireManager.OwnsTool(tool);
            if (label != null)
                label.text = owned ? (tool.displayName + " (Owned)") : (tool.displayName + " - " + tool.silverCoinCost + " Silver");
            if (buyButton != null)
                buyButton.interactable = !owned;
        }
    }
}
