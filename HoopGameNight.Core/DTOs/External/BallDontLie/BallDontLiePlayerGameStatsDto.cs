using System.Text.Json.Serialization;

namespace HoopGameNight.Core.DTOs.External.BallDontLie
{
    public class BallDontLiePlayerGameStatsDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("game")]
        public BallDontLieGameDto Game { get; set; } = new();

        [JsonPropertyName("player")]
        public BallDontLiePlayerDto Player { get; set; } = new();

        [JsonPropertyName("team")]
        public BallDontLieTeamDto Team { get; set; } = new();

        [JsonPropertyName("min")]
        public string? Min { get; set; }

        [JsonPropertyName("pts")]
        public int Pts { get; set; }

        [JsonPropertyName("ast")]
        public int Ast { get; set; }

        [JsonPropertyName("reb")]
        public int Reb { get; set; }

        [JsonPropertyName("stl")]
        public int Stl { get; set; }

        [JsonPropertyName("blk")]
        public int Blk { get; set; }

        [JsonPropertyName("turnover")]
        public int Turnover { get; set; }

        [JsonPropertyName("fgm")]
        public int Fgm { get; set; }

        [JsonPropertyName("fga")]
        public int Fga { get; set; }

        [JsonPropertyName("fg_pct")]
        public double? FgPct { get; set; }

        [JsonPropertyName("fg3m")]
        public int Fg3m { get; set; }

        [JsonPropertyName("fg3a")]
        public int Fg3a { get; set; }

        [JsonPropertyName("fg3_pct")]
        public double? Fg3Pct { get; set; }

        [JsonPropertyName("ftm")]
        public int Ftm { get; set; }

        [JsonPropertyName("fta")]
        public int Fta { get; set; }

        [JsonPropertyName("ft_pct")]
        public double? FtPct { get; set; }

        [JsonPropertyName("oreb")]
        public int Oreb { get; set; }

        [JsonPropertyName("dreb")]
        public int Dreb { get; set; }

        [JsonPropertyName("pf")]
        public int Pf { get; set; }
    }
}
