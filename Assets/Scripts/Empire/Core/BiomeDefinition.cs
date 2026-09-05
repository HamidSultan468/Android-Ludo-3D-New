using UnityEngine;

namespace LudoGame.Empire
{
    /// <summary>
    /// A starting biome (Forest or Snow) the player picks once at the start.
    /// Create via Assets > Create > Ludo Empire > Biome, or run
    /// Window > Ludo Tools > Empire > 1. Seed Starter Data.
    /// </summary>
    [CreateAssetMenu(menuName = "Ludo Empire/Biome", fileName = "Biome_")]
    public class BiomeDefinition : ScriptableObject
    {
        public string displayName = "Biome";
        [TextArea] public string description;
        public Sprite previewImage;

        [Tooltip("The material this biome's land is gathered for (Wood for Forest, Minerals for Snow, etc.).")]
        public MaterialDefinition primaryMaterial;
    }
}
