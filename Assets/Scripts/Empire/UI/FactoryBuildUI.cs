using UnityEngine;
using UnityEngine.UI;

namespace LudoGame.Empire.UI
{
    /// <summary>A "Build [Factory]" button - shows cost, and builds it on click.</summary>
    public class FactoryBuildUI : MonoBehaviour
    {
        [SerializeField] private EmpireManager empireManager;
        [SerializeField] private FactoryDefinition factory;
        [SerializeField] private Button buildButton;
        [SerializeField] private Text label;

        private void OnEnable()
        {
            if (buildButton != null) buildButton.onClick.AddListener(HandleBuildClicked);
            if (empireManager != null) empireManager.Inventory.OnMaterialChanged += HandleAnyChange;

            Refresh();
        }

        private void OnDisable()
        {
            if (buildButton != null) buildButton.onClick.RemoveListener(HandleBuildClicked);
            if (empireManager != null) empireManager.Inventory.OnMaterialChanged -= HandleAnyChange;
        }

        private void HandleBuildClicked()
        {
            if (empireManager == null || factory == null) return;
            if (empireManager.TryBuildFactory(factory))
                Refresh();
        }

        private void HandleAnyChange(MaterialDefinition material, int amount) => Refresh();

        private void Refresh()
        {
            if (empireManager == null || factory == null) return;

            bool built = empireManager.HasBuilt(factory);
            if (label != null)
            {
                label.text = built
                    ? (factory.displayName + " (Built)")
                    : ("Build " + factory.displayName + (factory.silverCoinCost > 0 ? " - " + factory.silverCoinCost + " Silver" : " - Free"));
            }
            if (buildButton != null)
                buildButton.interactable = !built;
        }
    }
}
