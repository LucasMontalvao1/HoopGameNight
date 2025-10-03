using System.Text.Json.Serialization;

namespace HoopGameNight.Core.DTOs.External.ESPN
{
    public class EspnPlayerStatsSplitDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("categories")]
        public List<EspnStatsCategoryDto> Categories { get; set; } = new();
    }

    public class EspnStatsCategoryDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("shortDisplayName")]
        public string ShortDisplayName { get; set; } = string.Empty;

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; } = string.Empty;

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;

        [JsonPropertyName("stats")]
        public List<EspnStatDto> Stats { get; set; } = new();
    }

    public class EspnStatDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("shortDisplayName")]
        public string ShortDisplayName { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public double Value { get; set; }

        [JsonPropertyName("displayValue")]
        public string DisplayValue { get; set; } = string.Empty;
    }
}