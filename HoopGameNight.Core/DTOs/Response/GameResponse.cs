using System;

namespace HoopGameNight.Core.DTOs.Response
{
    public class GameResponse
    {
        public int Id { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public DateTime DateTime { get; set; }
        public TeamSummaryResponse HomeTeam { get; set; } = new();
        public TeamSummaryResponse VisitorTeam { get; set; } = new();
        public int? HomeTeamScore { get; set; }
        public int? VisitorTeamScore { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusDisplay { get; set; } = string.Empty;
        public int? Period { get; set; }
        public string? TimeRemaining { get; set; }
        public bool PostSeason { get; set; }
        public int Season { get; set; }
        public string Score { get; set; } = string.Empty;
        public string GameTitle { get; set; } = string.Empty;
        public bool IsLive { get; set; }
        public bool IsCompleted { get; set; }
        public TeamSummaryResponse? WinningTeam { get; set; }
        public bool IsFutureGame { get; set; }
        public string DataSource { get; set; } = "ESPN"; 
        public FutureGameInfo? FutureGameInfo { get; set; }
    }

    /// <summary>
    /// Informações adicionais para jogos futuros
    /// </summary>
    public class FutureGameInfo
    {
        public string? TvChannel { get; set; }
        public string? BettingLine { get; set; }
        public string? Venue { get; set; }
        public string? Weather { get; set; }
    }

    public class GameSummaryResponse
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string HomeTeam { get; set; } = string.Empty;
        public string VisitorTeam { get; set; } = string.Empty;
        public string Score { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsLive { get; set; }
    }
}