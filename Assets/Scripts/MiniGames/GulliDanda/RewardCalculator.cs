using UnityEngine;

namespace LudoGame.MiniGames.GulliDanda
{
    /// <summary>Pure calculation of how many coins a hit is worth, based on distance and how the turn ended.</summary>
    public static class RewardCalculator
    {
        private const float CoinsPerMeter = 2f;
        private const int MinimumCoins = 0;

        /// <summary>
        /// Coins earned for a single hit. A mid-air catch scores nothing (the hit never
        /// counted); a Kotha hit on the return throw still keeps the distance-based coins,
        /// since the batter is only given out AFTER the distance was already achieved.
        /// </summary>
        public static int CalculateCoins(float distanceMeters, bool caughtMidAir)
        {
            if (caughtMidAir) return MinimumCoins;

            int coins = Mathf.RoundToInt(Mathf.Max(distanceMeters, 0f) * CoinsPerMeter);
            return Mathf.Max(coins, MinimumCoins);
        }
    }
}
