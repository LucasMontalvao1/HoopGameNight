using System.Text.Json.Serialization;

namespace HoopGameNight.Core.DTOs.External.BallDontLie
{
    public class BallDontLiePlayerSeasonStatsDto
    {
        [JsonPropertyName("player_id")]
        public int PlayerId { get; set; }

        [JsonPropertyName("season")]
        public int Season { get; set; }

        [JsonPropertyName("games_played")]
        public int GamesPlayed { get; set; }

        [JsonPropertyName("min")]
        public string? Min { get; set; }

        [JsonPropertyName("pts")]
        public double Pts { get; set; }

        [JsonPropertyName("reb")]
        public double Reb { get; set; }

        [JsonPropertyName("ast")]
        public double Ast { get; set; }

        [JsonPropertyName("stl")]
        public double Stl { get; set; }

        [JsonPropertyName("blk")]
        public double Blk { get; set; }

        [JsonPropertyName("turnover")]
        public double Turnover { get; set; }

        [JsonPropertyName("fg_pct")]
        public double? FgPct { get; set; }

        [JsonPropertyName("fg3_pct")]
        public double? Fg3Pct { get; set; }

        [JsonPropertyName("ft_pct")]
        public double? FtPct { get; set; }
    }
}
