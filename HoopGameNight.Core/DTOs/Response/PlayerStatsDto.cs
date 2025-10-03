namespace HoopGameNight.Core.DTOs.Response
{
    public class PlayerDetailedResponse
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public int? JerseyNumber { get; set; }
        public string? Position { get; set; }
        public string Height { get; set; } = string.Empty;
        public string Weight { get; set; } = string.Empty;
        public DateTime? BirthDate { get; set; }
        public int? Age { get; set; }
        public string? BirthCity { get; set; }
        public string? BirthCountry { get; set; }
        public string? College { get; set; }
        public DraftInfo? Draft { get; set; }
        public TeamSummaryResponse? CurrentTeam { get; set; }
        public PlayerSeasonStatsResponse? CurrentSeasonStats { get; set; }
        public PlayerCareerStatsResponse? CareerStats { get; set; }
        public List<PlayerRecentGameResponse> RecentGames { get; set; } = new();
    }

    public class DraftInfo
    {
        public int Year { get; set; }
        public int Round { get; set; }
        public int Pick { get; set; }
    }

    public class PlayerSeasonStatsResponse
    {
        public int Season { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public int GamesPlayed { get; set; }
        public int GamesStarted { get; set; }
        public decimal PPG { get; set; }
        public decimal RPG { get; set; }
        public decimal APG { get; set; }
        public decimal SPG { get; set; }
        public decimal BPG { get; set; }
        public decimal MPG { get; set; }
        public decimal FGPercentage { get; set; }
        public decimal ThreePointPercentage { get; set; }
        public decimal FTPercentage { get; set; }
        public int TotalPoints { get; set; }
        public int TotalRebounds { get; set; }
        public int TotalAssists { get; set; }
    }

    public class PlayerCareerStatsResponse
    {
        public int TotalSeasons { get; set; }
        public int TotalGames { get; set; }
        public int TotalPoints { get; set; }
        public decimal CareerPPG { get; set; }
        public decimal CareerRPG { get; set; }
        public decimal CareerAPG { get; set; }
        public decimal CareerFGPercentage { get; set; }
        public int CareerHighPoints { get; set; }
        public int CareerHighRebounds { get; set; }
        public int CareerHighAssists { get; set; }
    }

    public class PlayerRecentGameResponse
    {
        public int GameId { get; set; }
        public DateTime GameDate { get; set; }
        public string Opponent { get; set; } = string.Empty;
        public bool IsHome { get; set; }
        public string Result { get; set; } = string.Empty;
        public int Points { get; set; }
        public int Rebounds { get; set; }
        public int Assists { get; set; }
        public int Steals { get; set; }
        public int Blocks { get; set; }
        public string Minutes { get; set; } = string.Empty;
        public string FieldGoals { get; set; } = string.Empty;
        public string ThreePointers { get; set; } = string.Empty;
        public string FreeThrows { get; set; } = string.Empty;
        public int PlusMinus { get; set; }
        public bool DoubleDouble { get; set; }
        public bool TripleDouble { get; set; }
    }

    public class PlayerComparisonResponse
    {
        public PlayerDetailedResponse Player1 { get; set; } = new();
        public PlayerDetailedResponse Player2 { get; set; } = new();
        public ComparisonStats Comparison { get; set; } = new();
    }

    public class ComparisonStats
    {
        public string BetterScorer { get; set; } = string.Empty;
        public string BetterRebounder { get; set; } = string.Empty;
        public string BetterPasser { get; set; } = string.Empty;
        public Dictionary<string, decimal> StatsDifference { get; set; } = new();
    }

    public class StatLeadersResponse
    {
        public List<StatLeader> ScoringLeaders { get; set; } = new();
        public List<StatLeader> ReboundLeaders { get; set; } = new();
        public List<StatLeader> AssistLeaders { get; set; } = new();
        public DateTime LastUpdated { get; set; }
    }

    public class StatLeader
    {
        public int Rank { get; set; }
        public int PlayerId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public string Team { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public int GamesPlayed { get; set; }
    }
}

namespace HoopGameNight.Core.DTOs.Request
{
    public class PlayerStatsRequest
    {
        public int PlayerId { get; set; }
        public int? Season { get; set; }
        public int LastGames { get; set; } = 5;
        public bool IncludeCareer { get; set; } = true;
        public bool IncludeCurrentSeason { get; set; } = true;
    }
}