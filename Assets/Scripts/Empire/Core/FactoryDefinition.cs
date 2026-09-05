using System;
using System.Collections.Generic;
using UnityEngine;

namespace LudoGame.Empire
{
    /// <summary>How much of one material a factory costs to build.</summary>
    [Serializable]
    public class MaterialCost
    {
        public MaterialDefinition material;
        public int amount = 1;
    }

    /// <summary>
    /// A factory that can be built on the player's land (e.g. the free
    /// starter "Basic Foundry"). Create via Assets > Create > Ludo Empire >
    /// Factory, or run Window > Ludo Tools > Empire > 1. Seed Starter Data.
    /// </summary>
    [CreateAssetMenu(menuName = "Ludo Empire/Factory", fileName = "Factory_")]
    public class FactoryDefinition : ScriptableObject
    {
        public string displayName = "Factory";
        [TextArea] public string description;
        public Sprite icon;

        [Tooltip("Silver Coins needed to build (0 for the free starter Basic Foundry).")]
        public int silverCoinCost;

        public List<MaterialCost> materialCosts = new List<MaterialCost>();

        [Tooltip("Land level required to unlock this factory.")]
        public int unlockLevel = 1;
    }
}
