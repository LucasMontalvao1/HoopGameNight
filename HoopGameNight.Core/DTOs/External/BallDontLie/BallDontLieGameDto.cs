using System.Text.Json.Serialization;

namespace HoopGameNight.Core.DTOs.External.BallDontLie
{
    public class BallDontLieGameDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("home_team")]
        public BallDontLieTeamDto HomeTeam { get; set; } = new();

        [JsonPropertyName("visitor_team")]
        public BallDontLieTeamDto VisitorTeam { get; set; } = new();

        [JsonPropertyName("home_team_score")]
        public int? HomeTeamScore { get; set; }

        [JsonPropertyName("visitor_team_score")]
        public int? VisitorTeamScore { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("period")]
        public int? Period { get; set; }

        [JsonPropertyName("time")]
        public string? Time { get; set; }

        [JsonPropertyName("postseason")]
        public bool Postseason { get; set; }

        [JsonPropertyName("season")]
        public int Season { get; set; }
    }
}