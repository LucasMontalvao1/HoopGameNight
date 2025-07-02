using System.Text.Json.Serialization;

namespace HoopGameNight.Core.DTOs.External.BallDontLie
{
    public class BallDontLieApiResponse<T>
    {
        [JsonPropertyName("data")]
        public List<T> Data { get; set; } = new();

        [JsonPropertyName("meta")]
        public BallDontLieMetaDto Meta { get; set; } = new();
    }

    public class BallDontLieMetaDto
    {
        [JsonPropertyName("total_pages")]
        public int TotalPages { get; set; }

        [JsonPropertyName("current_page")]
        public int CurrentPage { get; set; }

        [JsonPropertyName("next_page")]
        public int? NextPage { get; set; }

        [JsonPropertyName("per_page")]
        public int PerPage { get; set; }

        [JsonPropertyName("total_count")]
        public int TotalCount { get; set; }
    }
}