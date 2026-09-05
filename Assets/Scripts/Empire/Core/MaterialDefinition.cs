using UnityEngine;

namespace LudoGame.Empire
{
    /// <summary>
    /// One of the 7 raw materials (Wood, Iron, Copper, Silver, Rubber/Plastic,
    /// Minerals, Brick). Create one asset per material via
    /// Assets > Create > Ludo Empire > Material, or run
    /// Window > Ludo Tools > Empire > 1. Seed Starter Data to generate all 7 at once.
    /// </summary>
    [CreateAssetMenu(menuName = "Ludo Empire/Material", fileName = "Material_")]
    public class MaterialDefinition : ScriptableObject
    {
        public string displayName = "Material";
        public Sprite icon;

        [Tooltip("Silver Coins earned per unit sold at the market's base price.")]
        public int baseSellPrice = 1;
    }
}
