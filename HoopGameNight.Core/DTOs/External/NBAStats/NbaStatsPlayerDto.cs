using System.Text.Json.Serialization;

namespace HoopGameNight.Core.DTOs.External.NBAStats
{
    public class NbaStatsPlayerDto
    {
        [JsonPropertyName("id")]
        public string PersonId { get; set; } = string.Empty;

        [JsonPropertyName("full_name")]
        public string FullName { get; set; } = string.Empty;

        [JsonPropertyName("first_name")]
        public string FirstName { get; set; } = string.Empty;

        [JsonPropertyName("last_name")]
        public string LastName { get; set; } = string.Empty;

        [JsonPropertyName("is_active")]
        public bool IsActive { get; set; }
    }
}
