namespace HoopGameNight.Core.DTOs.Response
{
    /// <summary>
    /// Response com jogos de múltiplos times
    /// </summary>
    public class MultipleTeamsGamesResponse
    {
        /// <summary>
        /// Jogos agrupados por ID do time
        /// </summary>
        public Dictionary<int, List<GameResponse>> GamesByTeam { get; set; } = new();

        /// <summary>
        /// Lista única de todos os jogos 
        /// </summary>
        public List<GameResponse> AllGames { get; set; } = new();

        /// <summary>
        /// Estatísticas gerais
        /// </summary>
        public GamesStatsResponse Stats { get; set; } = new();

        /// <summary>
        /// Período consultado
        /// </summary>
        public DateRangeInfo DateRange { get; set; } = new();

        /// <summary>
        /// Informações sobre limitações da API
        /// </summary>
        public ApiLimitationInfo ApiLimitations { get; set; } = new();
    }

    /// <summary>
    /// Estatísticas dos jogos
    /// </summary>
    public class GamesStatsResponse
    {
        public int TotalTeams { get; set; }
        public int TotalGames { get; set; }
        public int LiveGames { get; set; }
        public int CompletedGames { get; set; }
        public int ScheduledGames { get; set; }
        public Dictionary<int, TeamGamesStats> TeamStats { get; set; } = new();
    }

    /// <summary>
    /// Estatísticas por time
    /// </summary>
    public class TeamGamesStats
    {
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string TeamAbbreviation { get; set; } = string.Empty;
        public int TotalGames { get; set; }
        public int HomeGames { get; set; }
        public int AwayGames { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public GameResponse? NextGame { get; set; }
        public GameResponse? LastGame { get; set; }
        public string WinPercentage => TotalGames > 0
            ? $"{(Wins / (double)TotalGames * 100):F1}%"
            : "0.0%";
    }

    /// <summary>
    /// Informações do período
    /// </summary>
    public class DateRangeInfo
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalDays { get; set; }
        public bool IncludesFutureDates { get; set; }
        public bool IncludesPastDates { get; set; }
        public bool IncludesToday { get; set; }
    }

    /// <summary>
    /// Informações sobre limitações da API
    /// </summary>
    public class ApiLimitationInfo
    {
        public bool HasLimitations { get; set; }
        public List<string> Limitations { get; set; } = new();
        public string DataSource { get; set; } = "Database";
        public string Recommendation { get; set; } = string.Empty;
    }
}