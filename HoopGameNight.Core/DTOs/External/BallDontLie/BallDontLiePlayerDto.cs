using System.Text.Json.Serialization;

namespace HoopGameNight.Core.DTOs.External.BallDontLie
{
    public class BallDontLiePlayerDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("first_name")]
        public string FirstName { get; set; } = string.Empty;

        [JsonPropertyName("last_name")]
        public string LastName { get; set; } = string.Empty;

        [JsonPropertyName("position")]
        public string? Position { get; set; }

        [JsonPropertyName("height_feet")]
        public int? HeightFeet { get; set; }

        [JsonPropertyName("height_inches")]
        public int? HeightInches { get; set; }

        [JsonPropertyName("weight_pounds")]
        public int? WeightPounds { get; set; }

        [JsonPropertyName("team")]
        public BallDontLieTeamDto? Team { get; set; }
    }
}