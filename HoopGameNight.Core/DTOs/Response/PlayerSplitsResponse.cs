using System.Collections.Generic;

namespace HoopGameNight.Core.DTOs.Response
{
    /// <summary>
    /// Response DTO for player splits (Home/Away, vs Conference, etc.)
    /// </summary>
    public class PlayerSplitsResponse
    {
        public int PlayerId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public int Season { get; set; }
        public List<PlayerSplitCategory> Splits { get; set; } = new();
    }

    /// <summary>
    /// Category of splits (e.g., "Home/Away", "vs Conference")
    /// </summary>
    public class PlayerSplitCategory
    {
        public string CategoryName { get; set; } = string.Empty;
        public List<PlayerSplitStats> Stats { get; set; } = new();
    }

    /// <summary>
    /// Individual split statistics
    /// </summary>
    public class PlayerSplitStats
    {
        public string SplitName { get; set; } = string.Empty; // "Home", "Away", "vs East", etc.
        public int GamesPlayed { get; set; }
        public decimal PPG { get; set; }
        public decimal RPG { get; set; }
        public decimal APG { get; set; }
        public decimal FGPercentage { get; set; }
        public decimal ThreePointPercentage { get; set; }
        public decimal FTPercentage { get; set; }
    }
}
