using UnityEngine;

namespace LudoGame.Empire
{
    /// <summary>
    /// One of the 3 tools (Hammer, Chisel, Cutter) that speed up gathering.
    /// Create via Assets > Create > Ludo Empire > Tool, or run
    /// Window > Ludo Tools > Empire > 1. Seed Starter Data.
    /// </summary>
    [CreateAssetMenu(menuName = "Ludo Empire/Tool", fileName = "Tool_")]
    public class ToolDefinition : ScriptableObject
    {
        public string displayName = "Tool";
        public Sprite icon;
        public int silverCoinCost = 10;

        [Tooltip("Multiplies material yield while this tool is owned (e.g. 1.5 = +50%). The best owned tool's multiplier applies.")]
        public float gatherYieldMultiplier = 1.5f;
    }
}
