using System;

namespace HoopGameNight.Core.DTOs.Response
{
    /// <summary>
    /// Response DTO for game leaders (top performers in a specific game)
    /// </summary>
    public class GameLeadersResponse
    {
        public int GameId { get; set; }
        public DateTime GameDate { get; set; }
        public string HomeTeam { get; set; } = string.Empty;
        public string VisitorTeam { get; set; } = string.Empty;
        public TeamGameLeaders HomeTeamLeaders { get; set; } = new();
        public TeamGameLeaders VisitorTeamLeaders { get; set; } = new();
    }

    /// <summary>
    /// Leaders for a specific team in a game
    /// </summary>
    public class TeamGameLeaders
    {
        public string TeamName { get; set; } = string.Empty;
        
        /// <summary>
        /// Top scorer in the game (reuses existing StatLeader DTO)
        /// </summary>
        public StatLeader? PointsLeader { get; set; }
        
        /// <summary>
        /// Top rebounder in the game (reuses existing StatLeader DTO)
        /// </summary>
        public StatLeader? ReboundsLeader { get; set; }
        
        /// <summary>
        /// Top assist leader in the game (reuses existing StatLeader DTO)
        /// </summary>
        public StatLeader? AssistsLeader { get; set; }
    }
}
