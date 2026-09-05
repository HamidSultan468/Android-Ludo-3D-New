using System;
using System.Globalization;
using UnityEngine;

namespace TangentLudoEmpire.Social
{
    public enum LeaderboardType { Daily, Weekly, AllTime }

    [Serializable]
    public class PlayerStat
    {
        public string Id;
        public string Name;
        public int Wins;
        public int Games;

        [SerializeField] private string totalWinningsRaw = "0";
        public decimal TotalWinnings
        {
            get => decimal.TryParse(totalWinningsRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;
            set => totalWinningsRaw = decimal.Round(value, 2).ToString(CultureInfo.InvariantCulture);
        }

        public static PlayerStat New(string id, string name) => new PlayerStat { Id = id, Name = name };
    }
}
