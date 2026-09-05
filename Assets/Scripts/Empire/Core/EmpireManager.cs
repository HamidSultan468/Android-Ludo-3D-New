using System;
using System.Collections.Generic;
using UnityEngine;

namespace LudoGame.Empire
{
    /// <summary>
    /// Central state for the Ludo Empire meta-game: selected biome, wallet,
    /// inventory, owned tools, and built factories. This is a local,
    /// single-player prototype for "Milestone 1" - there is no multiplayer or
    /// cloud save yet (those pillars need Photon/FishNet and a backend
    /// service, which are a separate, later setup decision).
    ///
    /// Setup (in the Unity Editor):
    /// 1. Put this on an empty "EmpireManager" GameObject (or run
    ///    Window > Ludo Tools > Empire > 2. Build Milestone 1 Scene, which
    ///    creates one for you).
    /// 2. Other scripts read/write through EmpireManager.Instance.
    /// </summary>
    public class EmpireManager : MonoBehaviour
    {
        private static EmpireManager instance;

        /// <summary>Self-healing like GridManager.Instance in the main Ludo game - re-finds a live EmpireManager if the cached one was destroyed.</summary>
        public static EmpireManager Instance
        {
            get
            {
                if (instance == null)
                    instance = FindAnyObjectByType<EmpireManager>();
                return instance;
            }
        }

        public BiomeDefinition SelectedBiome { get; private set; }
        public CurrencyWallet Wallet { get; } = new CurrencyWallet();
        public EmpireInventory Inventory { get; } = new EmpireInventory();

        private readonly HashSet<ToolDefinition> ownedTools = new HashSet<ToolDefinition>();
        private readonly HashSet<FactoryDefinition> builtFactories = new HashSet<FactoryDefinition>();

        /// <summary>Raised once, when the player picks their starting biome.</summary>
        public event Action<BiomeDefinition> OnBiomeSelected;

        /// <summary>Raised when a tool is successfully purchased.</summary>
        public event Action<ToolDefinition> OnToolPurchased;

        /// <summary>Raised when a factory is successfully built.</summary>
        public event Action<FactoryDefinition> OnFactoryBuilt;

        /// <summary>Raised when a material is sold at the market: (material, amountSold, silverCoinsEarned).</summary>
        public event Action<MaterialDefinition, int, int> OnMaterialSold;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public void SelectBiome(BiomeDefinition biome)
        {
            if (biome == null) return;
            SelectedBiome = biome;
            OnBiomeSelected?.Invoke(biome);
        }

        public bool OwnsTool(ToolDefinition tool) => tool != null && ownedTools.Contains(tool);

        public bool HasBuilt(FactoryDefinition factory) => factory != null && builtFactories.Contains(factory);

        /// <summary>The best (highest) gather-yield multiplier from any owned tool. 1 if none owned yet.</summary>
        public float GetGatherMultiplier()
        {
            float best = 1f;
            foreach (ToolDefinition tool in ownedTools)
                if (tool.gatherYieldMultiplier > best)
                    best = tool.gatherYieldMultiplier;
            return best;
        }

        public bool TryBuyTool(ToolDefinition tool)
        {
            if (tool == null || OwnsTool(tool)) return false;
            if (!Wallet.TrySpend(CurrencyType.SilverCoins, tool.silverCoinCost)) return false;

            ownedTools.Add(tool);
            OnToolPurchased?.Invoke(tool);
            return true;
        }

        /// <summary>
        /// Sells every unit of a material the player currently holds, at the
        /// material's base price. This is a simple local placeholder for the
        /// future "player-driven global market" pillar, which needs a live
        /// server to support other players' buy/sell orders.
        /// </summary>
        public int SellAll(MaterialDefinition material)
        {
            if (material == null) return 0;
            return Sell(material, Inventory.GetAmount(material));
        }

        public int Sell(MaterialDefinition material, int amount)
        {
            if (material == null || amount <= 0) return 0;
            if (!Inventory.TrySpend(material, amount)) return 0;

            int earned = amount * material.baseSellPrice;
            Wallet.Add(CurrencyType.SilverCoins, earned);
            OnMaterialSold?.Invoke(material, amount, earned);
            return earned;
        }

        public bool TryBuildFactory(FactoryDefinition factory)
        {
            if (factory == null || HasBuilt(factory)) return false;

            foreach (MaterialCost cost in factory.materialCosts)
                if (Inventory.GetAmount(cost.material) < cost.amount)
                    return false;

            if (Wallet.Get(CurrencyType.SilverCoins) < factory.silverCoinCost) return false;

            foreach (MaterialCost cost in factory.materialCosts)
                Inventory.TrySpend(cost.material, cost.amount);
            Wallet.TrySpend(CurrencyType.SilverCoins, factory.silverCoinCost);

            builtFactories.Add(factory);
            OnFactoryBuilt?.Invoke(factory);
            return true;
        }
    }
}
