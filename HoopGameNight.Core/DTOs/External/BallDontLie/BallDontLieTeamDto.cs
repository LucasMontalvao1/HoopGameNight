using System.Text.Json.Serialization;

namespace HoopGameNight.Core.DTOs.External.BallDontLie
{
    public class BallDontLieTeamDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; } = string.Empty;

        [JsonPropertyName("city")]
        public string City { get; set; } = string.Empty;

        [JsonPropertyName("conference")]
        public string Conference { get; set; } = string.Empty;

        [JsonPropertyName("division")]
        public string Division { get; set; } = string.Empty;

        [JsonPropertyName("full_name")]
        public string FullName { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}