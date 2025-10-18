using System.Text.Json.Serialization;

namespace HoopGameNight.Core.DTOs.External.NBAStats
{
    public class NbaStatsSeasonStatsDto
    {
        [JsonPropertyName("games_played")]
        public int GamesPlayed { get; set; }

        [JsonPropertyName("pts")]
        public decimal Points { get; set; }

        [JsonPropertyName("reb")]
        public decimal Rebounds { get; set; }

        [JsonPropertyName("ast")]
        public decimal Assists { get; set; }

        [JsonPropertyName("stl")]
        public decimal Steals { get; set; }

        [JsonPropertyName("blk")]
        public decimal Blocks { get; set; }

        [JsonPropertyName("turnover")]
        public decimal Turnovers { get; set; }

        [JsonPropertyName("fg_pct")]
        public decimal FieldGoalPercentage { get; set; }

        [JsonPropertyName("fg3_pct")]
        public decimal ThreePointPercentage { get; set; }

        [JsonPropertyName("ft_pct")]
        public decimal FreeThrowPercentage { get; set; }

        [JsonPropertyName("min")]
        public string Minutes { get; set; } = "0:00";
    }
}
