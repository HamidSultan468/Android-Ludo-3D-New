using System;
using System.Globalization;
using UnityEngine;

namespace TangentLudoEmpire.Social
{
    /// <summary>Task C.4. Field names PascalCased (Time/IsWin) per this codebase's C# convention - the
    /// spec's lowercase "time"/"isWin" would violate it (and IsWin/Time are used from LeaderboardManager's
    /// windowed-sum query, so casing here isn't cosmetic-only).</summary>
    [Serializable]
    public class GameRecord
    {
        public string Id;
        public bool IsWin;
        public long TimeUtcMs;

        [SerializeField] private string entryRaw = "0";
        public decimal Entry
        {
            get => decimal.TryParse(entryRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;
            set => entryRaw = decimal.Round(value, 2).ToString(CultureInfo.InvariantCulture);
        }

        [SerializeField] private string winRaw = "0";
        public decimal Win
        {
            get => decimal.TryParse(winRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;
            set => winRaw = decimal.Round(value, 2).ToString(CultureInfo.InvariantCulture);
        }

        public DateTime Time => DateTimeOffset.FromUnixTimeMilliseconds(TimeUtcMs).UtcDateTime;

        public static GameRecord New(decimal entry, decimal win, bool isWin) => new GameRecord
        {
            Id = "GR_" + Guid.NewGuid().ToString("N").Substring(0, 12),
            Entry = entry,
            Win = win,
            IsWin = isWin,
            TimeUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
    }
}
