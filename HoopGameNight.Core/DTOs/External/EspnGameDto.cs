using System;

namespace HoopGameNight.Core.DTOs.External
{
    public class EspnGameDto
    {
        public string Id { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string HomeTeamId { get; set; } = string.Empty;
        public string AwayTeamId { get; set; } = string.Empty;
        public string HomeTeamName { get; set; } = string.Empty;
        public string AwayTeamName { get; set; } = string.Empty;
        public string HomeTeamAbbreviation { get; set; } = string.Empty;
        public string AwayTeamAbbreviation { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int? HomeTeamScore { get; set; }
        public int? AwayTeamScore { get; set; }
        public int? Period { get; set; } 
        public string? TimeRemaining { get; set; } 
        public bool? IsPostseason { get; set; } 
        public int? Season { get; set; } 
        public string? LineScoreJson { get; set; }
        public string? GameLeadersJson { get; set; }
        public string? HomeTeamRecord { get; set; }
        public string? AwayTeamRecord { get; set; }
    }
}