using System;
using UnityEngine;

namespace LudoGame.Empire
{
    /// <summary>The 3 currencies from the design doc's "3-Currency System" pillar.</summary>
    public enum CurrencyType
    {
        /// <summary>Premium currency - skips factory resource timers (not used yet in this milestone).</summary>
        Diamonds,
        /// <summary>Earned currency - sell resources for this, spend it on tools/build costs.</summary>
        SilverCoins,
        /// <summary>Most valuable - rerolls a dice in the main Ludo game (not used yet in this milestone).</summary>
        GoldCoins,
    }

    /// <summary>
    /// The player's wallet for all 3 currencies. Only Silver Coins are
    /// actually earned/spent by anything in this first milestone (selling
    /// materials, buying tools) - Diamonds and Gold Coins are wired up and
    /// ready for the later Factories/Ludo-integration pillars.
    /// </summary>
    [Serializable]
    public class CurrencyWallet
    {
        [SerializeField] private int diamonds;
        [SerializeField] private int silverCoins;
        [SerializeField] private int goldCoins;

        public int Diamonds => diamonds;
        public int SilverCoins => silverCoins;
        public int GoldCoins => goldCoins;

        /// <summary>Raised whenever a currency changes, with (type, newTotal).</summary>
        public event Action<CurrencyType, int> OnCurrencyChanged;

        public int Get(CurrencyType type)
        {
            switch (type)
            {
                case CurrencyType.Diamonds: return diamonds;
                case CurrencyType.SilverCoins: return silverCoins;
                case CurrencyType.GoldCoins: return goldCoins;
                default: return 0;
            }
        }

        public void Add(CurrencyType type, int amount)
        {
            if (amount <= 0) return;
            SetInternal(type, Get(type) + amount);
        }

        public bool TrySpend(CurrencyType type, int amount)
        {
            if (amount <= 0) return true;
            if (Get(type) < amount) return false;

            SetInternal(type, Get(type) - amount);
            return true;
        }

        private void SetInternal(CurrencyType type, int newValue)
        {
            newValue = Mathf.Max(0, newValue);
            switch (type)
            {
                case CurrencyType.Diamonds: diamonds = newValue; break;
                case CurrencyType.SilverCoins: silverCoins = newValue; break;
                case CurrencyType.GoldCoins: goldCoins = newValue; break;
            }
            OnCurrencyChanged?.Invoke(type, newValue);
        }
    }
}
