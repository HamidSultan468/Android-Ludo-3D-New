using UnityEngine;
using UnityEngine.UI;

namespace LudoGame.Empire.UI
{
    /// <summary>
    /// The one-time "choose your biome" screen (Forest or Snow). Hides
    /// itself once a biome is picked, or immediately on start if one was
    /// already selected.
    /// </summary>
    public class BiomeSelectUI : MonoBehaviour
    {
        [SerializeField] private EmpireManager empireManager;
        [SerializeField] private GameObject panel;
        [SerializeField] private Button forestButton;
        [SerializeField] private BiomeDefinition forestBiome;
        [SerializeField] private Button snowButton;
        [SerializeField] private BiomeDefinition snowBiome;

        private void Start()
        {
            if (empireManager != null && empireManager.SelectedBiome != null)
            {
                if (panel != null) panel.SetActive(false);
                return;
            }

            if (forestButton != null) forestButton.onClick.AddListener(() => Choose(forestBiome));
            if (snowButton != null) snowButton.onClick.AddListener(() => Choose(snowBiome));
        }

        private void Choose(BiomeDefinition biome)
        {
            if (empireManager == null || biome == null) return;

            empireManager.SelectBiome(biome);
            if (panel != null) panel.SetActive(false);
        }
    }
}
