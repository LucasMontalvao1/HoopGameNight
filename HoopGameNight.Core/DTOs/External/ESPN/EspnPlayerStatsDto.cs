using System.Text.Json.Serialization;

namespace HoopGameNight.Core.DTOs.External.ESPN
{
    public class EspnPlayerStatsDto
    {
        [JsonPropertyName("$ref")]
        public string Ref { get; set; } = string.Empty;

        [JsonPropertyName("athlete")]
        public EspnAthleteRefDto Athlete { get; set; } = new();

        [JsonPropertyName("splits")]
        public EspnPlayerStatsSplitDto Splits { get; set; } = new();
    }

    public class EspnAthleteRefDto
    {
        [JsonPropertyName("$ref")]
        public string Ref { get; set; } = string.Empty;
    }

    public class EspnAthletesListDto
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("pageIndex")]
        public int PageIndex { get; set; }

        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; }

        [JsonPropertyName("pageCount")]
        public int PageCount { get; set; }

        [JsonPropertyName("items")]
        public List<EspnAthleteRefDto> Items { get; set; } = new();
    }

    public class EspnPlayerDetailsDto
    {
        [JsonPropertyName("$ref")]
        public string Ref { get; set; } = string.Empty;

        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("uid")]
        public string Uid { get; set; } = string.Empty;

        [JsonPropertyName("guid")]
        public string Guid { get; set; } = string.Empty;

        [JsonPropertyName("firstName")]
        public string FirstName { get; set; } = string.Empty;

        [JsonPropertyName("lastName")]
        public string LastName { get; set; } = string.Empty;

        [JsonPropertyName("fullName")]
        public string FullName { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("shortName")]
        public string ShortName { get; set; } = string.Empty;

        [JsonPropertyName("weight")]
        public double Weight { get; set; }

        [JsonPropertyName("displayWeight")]
        public string DisplayWeight { get; set; } = string.Empty;

        [JsonPropertyName("height")]
        public double Height { get; set; }

        [JsonPropertyName("displayHeight")]
        public string DisplayHeight { get; set; } = string.Empty;

        [JsonPropertyName("age")]
        public int Age { get; set; }

        [JsonPropertyName("dateOfBirth")]
        public string DateOfBirth { get; set; } = string.Empty;

        [JsonPropertyName("debutYear")]
        public int DebutYear { get; set; }

        [JsonPropertyName("jersey")]
        public string Jersey { get; set; } = string.Empty;

        [JsonPropertyName("position")]
        public EspnPositionDto? Position { get; set; }

        [JsonPropertyName("team")]
        public EspnTeamRefDto? Team { get; set; }

        [JsonPropertyName("statistics")]
        public EspnStatisticsRefDto? Statistics { get; set; }

        [JsonPropertyName("active")]
        public bool Active { get; set; }
    }

    public class EspnPositionDto
    {
        [JsonPropertyName("$ref")]
        public string Ref { get; set; } = string.Empty;

        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; } = string.Empty;
    }

    public class EspnTeamRefDto
    {
        [JsonPropertyName("$ref")]
        public string Ref { get; set; } = string.Empty;
    }

    public class EspnStatisticsRefDto
    {
        [JsonPropertyName("$ref")]
        public string Ref { get; set; } = string.Empty;
    }
}