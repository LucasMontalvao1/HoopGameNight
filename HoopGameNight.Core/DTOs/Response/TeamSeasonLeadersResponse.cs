namespace HoopGameNight.Core.DTOs.Response
{
    /// <summary>
    /// Response DTO for team season leaders (top performers for a team in a season)
    /// </summary>
    public class TeamSeasonLeadersResponse
    {
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public int Season { get; set; }
        
        /// <summary>
        /// All leader categories (reuse existing StatLeader DTO)
        /// </summary>
        public StatLeader? PointsLeader { get; set; }
        public StatLeader? ReboundsLeader { get; set; }
        public StatLeader? AssistsLeader { get; set; }
        public StatLeader? StealsLeader { get; set; }
        public StatLeader? BlocksLeader { get; set; }
        public StatLeader? FGPercentageLeader { get; set; }
        public StatLeader? ThreePointPercentageLeader { get; set; }
    }
}
