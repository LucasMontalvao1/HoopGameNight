using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HoopGameNight.Core.DTOs.External.ESPN
{
    // --- GAMES / EVENTS ---
    public class EspnGameDetailDto
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public EspnGameHeaderDto? Header { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public EspnBoxscoreDto? Boxscore { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public EspnGameStatusDto? Status { get; set; }
    }

    public class EspnGameHeaderDto
    {
        [JsonIgnore]
        public string? Uid { get; set; }
        
        [JsonPropertyName("uid")]
        public string? _JsonUid { set => Uid = value; }
        public string? Date { get; set; }
        public bool Completed { get; set; }
        public EspnSeasonRefDto? Season { get; set; }
    }

    public class EspnEventDto
    {
        public string? Id { get; set; }
        public string? Date { get; set; }
        public string? Name { get; set; }
        public string? ShortName { get; set; }
        public List<EspnCompetitionDto>? Competitions { get; set; }
    }

    public class EspnCompetitionDto
    {
        public string? Id { get; set; }
        public string? Date { get; set; }
        public List<EspnCompetitorDto>? Competitors { get; set; }
    }

    public class EspnCompetitorDto
    {
        [JsonIgnore]
        public string? Uid { get; set; }

        [JsonPropertyName("uid")]
        public string? _JsonUid { set => Uid = value; }
        public string? Type { get; set; }
        public int Order { get; set; }
        public string? HomeAway { get; set; }
        public EspnTeamRefDto? Team { get; set; }
        public JsonElement? Score { get; set; }
    }



    public class EspnBoxscoreDto
    {
        public List<EspnBoxscoreTeamDto>? Teams { get; set; }
        public List<EspnBoxscorePlayerDto>? Players { get; set; }
    }

    public class EspnBoxscoreTeamDto 
    {
        public EspnTeamRefDto? Team { get; set; }
        public List<EspnStatDto>? Statistics { get; set; }
    }

    public class EspnBoxscorePlayerDto
    {
         public EspnTeamRefDto? Team { get; set; }
         public List<EspnAthleteStatDto>? Statistics { get; set; }
    }

    public class EspnAthleteStatDto 
    {
        public List<string>? Names { get; set; }
        public List<string>? Keys { get; set; }
        public List<EspnAthleteEntryDto>? Athletes { get; set; }
    }

    public class EspnAthleteEntryDto
    {
        public EspnAthleteRefDto? Athlete { get; set; }
        public List<string>? Stats { get; set; }
    }



    public class EspnPlaysDto
    {
        public List<EspnPlayDto>? Items { get; set; }
    }

    public class EspnPlayDto
    {
        public string? Id { get; set; }
        public string? Type { get; set; }
        public string? Text { get; set; }
        public string? AwayScore { get; set; }
        public string? HomeScore { get; set; }
        public EspnClockDto? Clock { get; set; }
    }

    public class EspnClockDto
    {
        public string? DisplayValue { get; set; }
    }

    public class EspnGameLeadersDto
    {
         public List<EspnLeaderCategoryDto>? Leaders { get; set; }
    }

    public class EspnLeaderCategoryDto
    {
        public string? Name { get; set; }
        public string? DisplayName { get; set; }
        public List<EspnLeaderDto>? Leaders { get; set; }
    }

    public class EspnLeaderDto
    {
        public string? DisplayValue { get; set; }
        public EspnAthleteRefDto? Athlete { get; set; }
    }

    public class EspnGameStatusDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? State { get; set; }
        public bool Completed { get; set; }
        public string? Detail { get; set; }
        public int Period { get; set; }
        public string? DisplayClock { get; set; }
    }

    public class EspnGameRosterDto
    {
        public List<EspnRosterEntryDto>? Entries { get; set; } 
        // A API da ESPN pode retornar atletas em 'Entries' ou diretamente em 'Athletes' dependendo do endpoint (Game vs Team)
        public List<EspnAthleteRefDto>? Athletes { get; set; }
    }
    
    public class EspnRosterEntryDto
    {
        public string? PlayerId { get; set; }
        public string? FullName { get; set; }
    }

    // --- TEAMS ---
    public class EspnTeamStatisticsDto
    {
        // Estrutura genérica da ESPN que pareia nomes de estatísticas com seus respectivos valores numéricos
         public List<string>? Names { get; set; }
         public List<double>? Values { get; set; }
    }

    public class EspnTeamLeadersDto
    {
          public List<EspnLeaderCategoryDto>? Categories { get; set; } // Mapeia categorias como 'Points', 'Rebounds', etc.
    }


    public class EspnLeadersDto
    {
        public List<EspnLeaderCategoryDto>? LeagueLeaders { get; set; }
    }


    // --- STANDINGS / CONTENT ---
    public class EspnStandingsDto
    {
        public string? Name { get; set; } 
        public List<EspnStandingEntryDto>? Children { get; set; } // Conferences/Divisions often in children
        public List<EspnStandingEntryDto>? Entries { get; set; }
    }

    public class EspnStandingEntryDto
    {
        public string? Name { get; set; }
        public List<EspnStandingStatDto>? Stats { get; set; }
        public EspnTeamRefDto? Team { get; set; }
    }

    public class EspnStandingStatDto
    {
        public string? Name { get; set; }
        public string? DisplayValue { get; set; }
        public string? Value { get; set; }
    }

    public class EspnInjuriesDto
    {
        public List<EspnTeamInjuryListDto>? Teams { get; set; }
    }

    public class EspnTeamInjuryListDto
    {
        public EspnTeamRefDto? Team { get; set; }
        public List<EspnInjuryDto>? Injuries { get; set; }
    }

    public class EspnInjuryDto
    {
        public EspnAthleteRefDto? Athlete { get; set; }
        public string? Status { get; set; }
        public string? Description { get; set; }
        public string? Date { get; set; }
    }

    public class EspnTransactionsDto
    {
        public List<EspnTransactionDto>? Events { get; set; }
    }

    public class EspnTransactionDto
    {
        public string? Date { get; set; }
        public string? Description { get; set; }
        public EspnTeamRefDto? Team { get; set; }
    }

    // --- PLAYERS EXTENDED ---
    public class EspnPlayerSplitsDto
    {
         public List<EspnSplitCategoryDto>? SplitCategories { get; set; }
    }

    public class EspnSplitCategoryDto
    {
        public string? Name { get; set; }
        public List<EspnSplitDto>? Splits { get; set; }
    }

    public class EspnSplitDto
    {
        public string? Name { get; set; }
        public string? DisplayName { get; set; }
        public string? Abbreviation { get; set; }
        public List<string>? Stats { get; set; } // Lista de valores brutos que seguem a ordem definida no cabeçalho do Split
    }

    public class EspnPlayerGamelogDto
    {
        [JsonPropertyName("seasonTypes")]
        public List<EspnSeasonGamelogDto>? SeasonTypes { get; set; }

        [JsonPropertyName("events")]
        public Dictionary<string, System.Text.Json.JsonElement>? Events { get; set; }
    }
    
    public class EspnSeasonGamelogDto
    {
        public string? DisplayName { get; set; }
        public List<EspnGamelogCategoryDto>? Categories { get; set; }
        public List<EspnGamelogEventDto>? Events { get; set; }
    }

    public class EspnGamelogCategoryDto
    {
        public string? Name { get; set; }
        public string? DisplayName { get; set; }
        public List<EspnGamelogEventDto>? Events { get; set; }
    }

    public class EspnGamelogEventMetadataDto
    {
        public string? Id { get; set; }
        public string? GameDate { get; set; }
        public string? GameResult { get; set; }
        public EspnOpponentDto? Opponent { get; set; }
        public EspnOpponentDto? Team { get; set; }
        public string? HomeTeamId { get; set; }
        public string? AwayTeamId { get; set; }
        public string? Score { get; set; }
    }

    public class EspnGamelogTeamDto
    {
        public string? Id { get; set; }
        public string? HomeAway { get; set; }
        public bool? Winner { get; set; }
        public EspnOpponentDto? Team { get; set; }
    }

    public class EspnGamelogEventDto
    {
        public string? EventId { get; set; }
        [JsonPropertyName("stats")]
        public List<string>? Stats { get; set; } 
    }

    public class EspnOpponentDto
    {
        public string? Id { get; set; }
        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }
        [JsonPropertyName("abbreviation")]
        public string? Abbreviation { get; set; }
        [JsonPropertyName("logo")]
        public string? Logo { get; set; }
    }


    // --- SEASONS ---
    public class EspnSeasonsDto
    {
        public List<EspnSeasonRefDto>? Items { get; set; }
    }

    public class EspnSeasonDto
    {
         public int Year { get; set; }
         public string? StartDate { get; set; }
         public string? EndDate { get; set; }
    }

    public class EspnSeasonTypesDto
    {
         public List<EspnSeasonTypeDto>? Items { get; set; }
    }

    public class EspnSeasonTypeDto
    {
        public string? Id { get; set; }
        public int Type { get; set; }
        public string? Name { get; set; }
        public string? Abbreviation { get; set; }
    }

    public class EspnSeasonEventsDto
    {
        public List<EspnEventDto>? Events { get; set; }
    }

    public class EspnSeasonStandingsDto
    {
        public List<EspnStandingEntryDto>? Children { get; set; }
    }

    // ===== TEAMS (from EspnTeamDto.cs) =====
    
    /// <summary>
    /// DTO para times da ESPN API
    /// </summary>
    public class EspnTeamDto
    {
        public string Id { get; set; } = string.Empty;
        [JsonIgnore]
        public string Uid { get; set; } = string.Empty;
        
        [JsonPropertyName("uid")]
        public string? _JsonUid { set => Uid = value ?? string.Empty; }

        [JsonIgnore]
        public string Slug { get; set; } = string.Empty;

        [JsonPropertyName("slug")]
        public string? _JsonSlug { set => Slug = value ?? string.Empty; }
        public string Location { get; set; } = string.Empty; 
        public string Name { get; set; } = string.Empty;
        public string Abbreviation { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty; 
        public string ShortDisplayName { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string AlternateColor { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsAllStar { get; set; }

        // Logos
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<EspnLogoDto> Logos { get; set; } = new();
    }

    public class EspnLogoDto
    {
        [JsonIgnore]
        public string Href { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public string Alt { get; set; } = string.Empty;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> Rel { get; set; } = new();
    }

    public class EspnTeamRefDto
    {
        [JsonIgnore]
        public string Ref { get; set; } = string.Empty;

        [JsonPropertyName("$ref")]
        public string? _JsonRef { set => Ref = value ?? string.Empty; }

        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DisplayName { get; set; }

        [JsonPropertyName("abbreviation")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Abbreviation { get; set; }
    }

    // ===== PLAYERS / ATHLETES (from EspnPlayerStatsDto.cs) =====
    
    public class EspnPlayerStatsDto
    {
        [JsonIgnore]
        public string Ref { get; set; } = string.Empty;

        [JsonPropertyName("$ref")]
        public string? _JsonRef { set => Ref = value ?? string.Empty; }

        [JsonPropertyName("athlete")]
        public EspnAthleteRefDto Athlete { get; set; } = new();

        [JsonPropertyName("splits")]
        public EspnPlayerStatsSplitDto Splits { get; set; } = new();

        [JsonPropertyName("season")]
        public EspnSeasonRefDto? Season { get; set; }

        [JsonPropertyName("team")]
        public EspnTeamRefDto? Team { get; set; }

        [JsonIgnore]
        [JsonPropertyName("seasonType")]
        public object? SeasonType { get; set; }

        [JsonPropertyName("seasonTypeId")]
        public int SeasonTypeId { get; set; } = 2; // Identificador da temporada (2 = Regular Season, 3 = Postseason)
    }

    public class EspnAthleteRefDto
    {
        [JsonIgnore]
        public string Ref { get; set; } = string.Empty;

        [JsonPropertyName("$ref")]
        public string? _JsonRef { set => Ref = value ?? string.Empty; }

        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
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
        [JsonIgnore]
        public string Ref { get; set; } = string.Empty;

        [JsonPropertyName("$ref")]
        public string? _JsonRef { set => Ref = value ?? string.Empty; }
        
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonIgnore]
        public string Uid { get; set; } = string.Empty;

        [JsonPropertyName("uid")]
        public string? _JsonUid { set => Uid = value ?? string.Empty; }

        [JsonIgnore]
        public string Guid { get; set; } = string.Empty;

        [JsonPropertyName("guid")]
        public string? _JsonGuid { set => Guid = value ?? string.Empty; }

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

        [JsonIgnore]
        public string DateOfBirth { get; set; } = string.Empty;

        [JsonPropertyName("dateOfBirth")]
        public string? _JsonDateOfBirth { set => DateOfBirth = value ?? string.Empty; }

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
        [JsonIgnore]
        public string Ref { get; set; } = string.Empty;

        [JsonPropertyName("$ref")]
        public string? _JsonRef { set => Ref = value ?? string.Empty; }

        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; } = string.Empty;
    }

    // ===== STATISTICS (from EspnPlayerStatsSplitDto.cs) =====
    
    public class EspnPlayerStatsSplitDto
    {
        [JsonIgnore]
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonIgnore]
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonIgnore]
        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; } = string.Empty;

        [JsonIgnore]
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

        [JsonIgnore]
        [JsonPropertyName("shortDisplayName")]
        public string ShortDisplayName { get; set; } = string.Empty;

        [JsonIgnore]
        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; } = string.Empty;

        [JsonIgnore]
        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;

        [JsonPropertyName("stats")]
        public List<EspnStatDto> Stats { get; set; } = new();
    }

    public class EspnStatDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonIgnore]
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonIgnore]
        [JsonPropertyName("shortDisplayName")]
        public string ShortDisplayName { get; set; } = string.Empty;

        [JsonIgnore]
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonIgnore]
        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; } = string.Empty;

        [JsonIgnore]
        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("value")]
        public double Value { get; set; }

        [JsonPropertyName("displayValue")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string DisplayValue { get; set; } = string.Empty;
    }

    public class EspnStatisticsRefDto
    {
        [JsonIgnore]
        public string Ref { get; set; } = string.Empty;

        [JsonPropertyName("$ref")]
        public string? _JsonRef { set => Ref = value ?? string.Empty; }
    }

    // ===== SEASONS (from EspnPlayerStatsDto.cs) =====
    
    public class EspnSeasonRefDto
    {
        [JsonIgnore]
        public string Ref { get; set; } = string.Empty;

        [JsonPropertyName("$ref")]
        public string? _JsonRef { set => Ref = value ?? string.Empty; }

        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("startDate")]
        public string? StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string? EndDate { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("type")]
        public int Type { get; set; }
    }

}
