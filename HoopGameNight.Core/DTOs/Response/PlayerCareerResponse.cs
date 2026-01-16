using System.Collections.Generic;

namespace HoopGameNight.Core.DTOs.Response
{
    /// <summary>
    /// Response DTO for player career statistics
    /// Combines career totals with season-by-season breakdown
    /// </summary>
    public class PlayerCareerResponse
    {
        public int PlayerId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        
        /// <summary>
        /// Career totals and averages (reuses existing DTO)
        /// </summary>
        public PlayerCareerStatsResponse CareerTotals { get; set; } = new();
        
        /// <summary>
        /// Season-by-season statistics (reuses existing DTO)
        /// </summary>
        public List<PlayerSeasonStatsResponse> SeasonStats { get; set; } = new();
    }
}
