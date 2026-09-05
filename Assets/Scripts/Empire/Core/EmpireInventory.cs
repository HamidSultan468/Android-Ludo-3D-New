using System;
using System.Collections.Generic;

namespace LudoGame.Empire
{
    /// <summary>Holds how much of each material the player has gathered.</summary>
    public class EmpireInventory
    {
        private readonly Dictionary<MaterialDefinition, int> amounts = new Dictionary<MaterialDefinition, int>();

        /// <summary>Raised whenever a material's amount changes, with (material, newTotal).</summary>
        public event Action<MaterialDefinition, int> OnMaterialChanged;

        public int GetAmount(MaterialDefinition material)
        {
            if (material == null) return 0;
            return amounts.TryGetValue(material, out int amount) ? amount : 0;
        }

        public void Add(MaterialDefinition material, int amount)
        {
            if (material == null || amount <= 0) return;

            int newAmount = GetAmount(material) + amount;
            amounts[material] = newAmount;
            OnMaterialChanged?.Invoke(material, newAmount);
        }

        public bool TrySpend(MaterialDefinition material, int amount)
        {
            if (material == null || amount <= 0) return true;
            if (GetAmount(material) < amount) return false;

            int newAmount = GetAmount(material) - amount;
            amounts[material] = newAmount;
            OnMaterialChanged?.Invoke(material, newAmount);
            return true;
        }
    }
}
