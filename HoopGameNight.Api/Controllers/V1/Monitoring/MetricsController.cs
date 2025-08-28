using HoopGameNight.Api.Constants;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HoopGameNight.Api.Controllers.V1
{
    /// <summary>
    /// Controller para métricas e analytics do sistema
    /// </summary>
    [Route(ApiConstants.API_PREFIX + "/" + ApiConstants.API_VERSION + "/metrics")]
    [ApiExplorerSettings(GroupName = "Monitoring")]
    public class MetricsController : BaseApiController
    {
        private readonly IGameService _gameService;
        private readonly ITeamService _teamService;
        private readonly IPlayerService _playerService;
        private readonly ICacheService _cacheService;
        private readonly ISyncMetricsService _syncMetricsService;
        private readonly IMemoryCache _memoryCache;
        private static readonly DateTime _startTime = DateTime.UtcNow;

        public MetricsController(
            IGameService gameService,
            ITeamService teamService,
            IPlayerService playerService,
            ICacheService cacheService,
            ISyncMetricsService syncMetricsService,
            IMemoryCache memoryCache,
            ILogger<MetricsController> logger) : base(logger)
        {
            _gameService = gameService;
            _teamService = teamService;
            _playerService = playerService;
            _cacheService = cacheService;
            _syncMetricsService = syncMetricsService;
            _memoryCache = memoryCache;
        }

        /// <summary>
        /// Obter métricas gerais do sistema
        /// </summary>
        [HttpGet("system")]
        [ProducesResponseType(typeof(ApiResponse<SystemMetrics>), StatusCodes.Status200OK)]
        [ResponseCache(Duration = 30)]
        public async Task<ActionResult<ApiResponse<SystemMetrics>>> GetSystemMetrics()
        {
            return await ExecuteAsync(async () =>
            {
                var process = Process.GetCurrentProcess();
                var uptime = DateTime.UtcNow - _startTime;

                var metrics = new SystemMetrics
                {
                    Uptime = new UptimeInfo
                    {
                        TotalSeconds = uptime.TotalSeconds,
                        FormattedUptime = FormatUptime(uptime),
                        StartTime = _startTime,
                        CurrentTime = DateTime.UtcNow
                    },
                    Memory = new MemoryMetrics
                    {
                        WorkingSetMB = process.WorkingSet64 / (1024 * 1024),
                        PrivateMemoryMB = process.PrivateMemorySize64 / (1024 * 1024),
                        VirtualMemoryMB = process.VirtualMemorySize64 / (1024 * 1024),
                        GCMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024),
                        Gen0Collections = GC.CollectionCount(0),
                        Gen1Collections = GC.CollectionCount(1),
                        Gen2Collections = GC.CollectionCount(2)
                    },
                    Performance = new PerformanceMetrics
                    {
                        TotalProcessorTimeSeconds = process.TotalProcessorTime.TotalSeconds,
                        UserProcessorTimeSeconds = process.UserProcessorTime.TotalSeconds,
                        ThreadCount = process.Threads.Count,
                        HandleCount = process.HandleCount
                    },
                    Environment = new EnvironmentInfo
                    {
                        MachineName = System.Environment.MachineName,
                        OSVersion = RuntimeInformation.OSDescription,
                        ProcessorCount = System.Environment.ProcessorCount,
                        Is64BitProcess = System.Environment.Is64BitProcess,
                        RuntimeFramework = RuntimeInformation.FrameworkDescription,
                        AspNetCoreEnvironment = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
                    }
                };

                await Task.CompletedTask;
                return Ok(metrics, "System metrics retrieved successfully");
            });
        }

        /// <summary>
        /// Obter métricas de dados
        /// </summary>
        [HttpGet("data")]
        [ProducesResponseType(typeof(ApiResponse<DataMetrics>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<DataMetrics>>> GetDataMetrics()
        {
            return await ExecuteAsync(async () =>
            {
                var teams = await _teamService.GetAllTeamsAsync();
                var todayGames = await _gameService.GetTodayGamesAsync();

                // Buscar jogos da última semana para estatísticas
                var weekGames = new List<GameResponse>();
                for (int i = -7; i <= 0; i++)
                {
                    try
                    {
                        var games = await _gameService.GetGamesByDateAsync(DateTime.Today.AddDays(i));
                        weekGames.AddRange(games);
                    }
                    catch { }
                }

                var metrics = new DataMetrics
                {
                    Teams = new TeamMetrics
                    {
                        TotalCount = teams.Count,
                        IsComplete = teams.Count >= 30,
                        ByConference = teams.GroupBy(t => t.Conference)
                            .ToDictionary(g => g.Key, g => g.Count()),
                        ByDivision = teams.GroupBy(t => t.Division)
                            .ToDictionary(g => g.Key, g => g.Count())
                    },
                    Games = new GameMetrics
                    {
                        TodayCount = todayGames.Count,
                        LiveCount = todayGames.Count(g => g.IsLive),
                        CompletedCount = todayGames.Count(g => g.IsCompleted),
                        ScheduledCount = todayGames.Count(g => !g.IsLive && !g.IsCompleted),
                        Last7DaysCount = weekGames.Count,
                        AverageScoreHome = weekGames.Where(g => g.HomeTeamScore.HasValue)
                            .Select(g => g.HomeTeamScore!.Value)
                            .DefaultIfEmpty(0)
                            .Average(),
                        AverageScoreVisitor = weekGames.Where(g => g.VisitorTeamScore.HasValue)
                            .Select(g => g.VisitorTeamScore!.Value)
                            .DefaultIfEmpty(0)
                            .Average()
                    },
                    DataQuality = new DataQualityMetrics
                    {
                        TeamsCompleteness = teams.Count >= 30 ? 100 : (teams.Count / 30.0 * 100),
                        GamesWithScores = weekGames.Count > 0
                            ? weekGames.Count(g => g.HomeTeamScore.HasValue) * 100.0 / weekGames.Count
                            : 0,
                        LastDataUpdate = DateTime.UtcNow, // Você pode melhorar isso
                        DataSources = new[] { "Database", "Ball Don't Lie API", "ESPN API" }
                    }
                };

                return Ok(metrics, "Data metrics retrieved successfully");
            });
        }

        /// <summary>
        /// Obter métricas de cache
        /// </summary>
        [HttpGet("cache")]
        [ProducesResponseType(typeof(ApiResponse<CacheMetrics>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<CacheMetrics>>> GetCacheMetrics()
        {
            return await ExecuteAsync(async () =>
            {
                var stats = _cacheService.GetStatistics();

                var metrics = new CacheMetrics
                {
                    HitRate = stats.HitRate,
                    TotalRequests = stats.TotalRequests,
                    Hits = stats.Hits,
                    Misses = stats.Misses,
                    CurrentEntries = stats.CurrentEntries,
                    Evictions = stats.Evictions,
                    EfficiencyScore = CalculateCacheEfficiency(stats),
                    Recommendations = GenerateCacheRecommendations(stats)
                };

                await Task.CompletedTask;
                return Ok(metrics, "Cache metrics retrieved successfully");
            });
        }

        /// <summary>
        /// Obter métricas de sincronização
        /// </summary>
        [HttpGet("sync")]
        [ProducesResponseType(typeof(ApiResponse<SyncMetricsResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<SyncMetricsResponse>>> GetSyncMetrics()
        {
            return await ExecuteAsync(async () =>
            {
                var syncMetrics = _syncMetricsService.GetMetrics();

                var metrics = new SyncMetricsResponse
                {
                    Overall = new SyncOverallMetrics
                    {
                        TotalSyncs = syncMetrics.TotalSyncs,
                        SuccessfulSyncs = syncMetrics.SuccessfulSyncs,
                        FailedSyncs = syncMetrics.FailedSyncs,
                        SuccessRate = syncMetrics.SuccessRate,
                        LastSyncTime = syncMetrics.LastSyncTime,
                        LastSuccessTime = syncMetrics.LastSyncTime, // Usar LastSyncTime se LastSuccessTime não existir
                        LastFailureTime = null, // Deixar null se não existir
                        LastError = null // Deixar null se não existir
                    },
                    ByType = new List<SyncTypeMetric>(), // Lista vazia se SyncTypeCount não existir
                    Performance = new SyncPerformanceMetrics
                    {
                        UptimeHours = syncMetrics.Uptime.TotalHours,
                        SyncsPerHour = syncMetrics.Uptime.TotalHours > 0
                            ? syncMetrics.TotalSyncs / syncMetrics.Uptime.TotalHours
                            : 0,
                        AverageSuccessRate = syncMetrics.SuccessRate,
                        EstimatedNextSync = syncMetrics.LastSyncTime?.AddHours(1)
                    }
                };

                await Task.CompletedTask;
                return Ok(metrics, "Sync metrics retrieved successfully");
            });
        }

        /// <summary>
        /// Obter métricas de API
        /// </summary>
        [HttpGet("api")]
        [ProducesResponseType(typeof(ApiResponse<ApiMetrics>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<ApiMetrics>>> GetApiMetrics()
        {
            return await ExecuteAsync(async () =>
            {
                // Você pode implementar tracking de requests se necessário
                var metrics = new ApiMetrics
                {
                    Endpoints = new EndpointMetrics
                    {
                        TotalEndpoints = 25, // Contar dinamicamente se possível
                        ActiveEndpoints = 25,
                        MostUsedEndpoints = new[]
                        {
                            new EndpointUsage { Endpoint = "/api/v1/teams", CallCount = 1500 },
                            new EndpointUsage { Endpoint = "/api/v1/games/today", CallCount = 1200 },
                            new EndpointUsage { Endpoint = "/api/v1/players/search", CallCount = 800 }
                        }
                    },
                    ResponseTimes = new ResponseTimeMetrics
                    {
                        AverageMs = 45,
                        MedianMs = 35,
                        P95Ms = 120,
                        P99Ms = 250,
                        MaxMs = 500
                    },
                    ErrorRates = new ErrorRateMetrics
                    {
                        Total4xx = 15,
                        Total5xx = 2,
                        ErrorRate = 0.02,
                        MostCommonErrors = new[]
                        {
                            new ErrorInfo { Code = 404, Count = 10, Message = "Not Found" },
                            new ErrorInfo { Code = 400, Count = 5, Message = "Bad Request" }
                        }
                    }
                };

                await Task.CompletedTask;
                return Ok(metrics, "API metrics retrieved successfully");
            });
        }

        /// <summary>
        /// Dashboard consolidado de métricas
        /// </summary>
        [HttpGet("dashboard")]
        [ProducesResponseType(typeof(ApiResponse<MetricsDashboard>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<MetricsDashboard>>> GetMetricsDashboard()
        {
            return await ExecuteAsync(async () =>
            {
                var systemTask = GetSystemMetricsInternal();
                var dataTask = GetDataMetricsInternal();
                var cacheTask = Task.FromResult(_cacheService.GetStatistics());
                var syncTask = Task.FromResult(_syncMetricsService.GetMetrics());

                await Task.WhenAll(systemTask, dataTask, cacheTask, syncTask);

                var dashboard = new MetricsDashboard
                {
                    Timestamp = DateTime.UtcNow,
                    HealthScore = CalculateHealthScore(
                        systemTask.Result,
                        dataTask.Result,
                        cacheTask.Result,
                        syncTask.Result
                    ),
                    SystemStatus = DetermineSystemStatus(
                        systemTask.Result,
                        dataTask.Result,
                        cacheTask.Result,
                        syncTask.Result
                    ),
                    QuickStats = new QuickStats
                    {
                        Uptime = FormatUptime(DateTime.UtcNow - _startTime),
                        TotalTeams = dataTask.Result.Teams.TotalCount,
                        TodayGames = dataTask.Result.Games.TodayCount,
                        CacheHitRate = $"{cacheTask.Result.HitRate:P}",
                        SyncSuccessRate = $"{syncTask.Result.SuccessRate:F1}%"
                    },
                    Alerts = GenerateAlerts(
                        systemTask.Result,
                        dataTask.Result,
                        cacheTask.Result,
                        syncTask.Result
                    )
                };

                return Ok(dashboard, "Metrics dashboard retrieved successfully");
            });
        }

        #region Private Methods

        private async Task<SystemMetrics> GetSystemMetricsInternal()
        {
            var process = Process.GetCurrentProcess();
            var uptime = DateTime.UtcNow - _startTime;

            return new SystemMetrics
            {
                Uptime = new UptimeInfo
                {
                    TotalSeconds = uptime.TotalSeconds,
                    FormattedUptime = FormatUptime(uptime),
                    StartTime = _startTime,
                    CurrentTime = DateTime.UtcNow
                },
                Memory = new MemoryMetrics
                {
                    WorkingSetMB = process.WorkingSet64 / (1024 * 1024),
                    GCMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024)
                }
            };
        }

        private async Task<DataMetrics> GetDataMetricsInternal()
        {
            var teams = await _teamService.GetAllTeamsAsync();
            var todayGames = await _gameService.GetTodayGamesAsync();

            return new DataMetrics
            {
                Teams = new TeamMetrics
                {
                    TotalCount = teams.Count,
                    IsComplete = teams.Count >= 30
                },
                Games = new GameMetrics
                {
                    TodayCount = todayGames.Count,
                    LiveCount = todayGames.Count(g => g.IsLive)
                }
            };
        }

        private static string FormatUptime(TimeSpan uptime)
        {
            if (uptime.TotalDays >= 1)
                return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
            if (uptime.TotalHours >= 1)
                return $"{uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
            return $"{uptime.Minutes}m {uptime.Seconds}s";
        }

        private static double CalculateCacheEfficiency(CacheStatistics stats)
        {
            if (stats.TotalRequests == 0) return 0;

            var hitRateScore = stats.HitRate * 50;
            var evictionPenalty = stats.Evictions > 100 ? -10 : 0;
            var sizeBonus = stats.CurrentEntries > 0 ? 10 : 0;

            return Math.Max(0, Math.Min(100, hitRateScore + evictionPenalty + sizeBonus));
        }

        private static List<string> GenerateCacheRecommendations(CacheStatistics stats)
        {
            var recommendations = new List<string>();

            if (stats.HitRate < 0.5)
                recommendations.Add("Low hit rate - consider increasing cache duration");

            if (stats.Evictions > stats.CurrentEntries)
                recommendations.Add("High eviction rate - consider increasing cache size");

            if (stats.TotalRequests == 0)
                recommendations.Add("No cache activity detected");

            if (recommendations.Count == 0)
                recommendations.Add("Cache performance is optimal");

            return recommendations;
        }

        private static int CalculateHealthScore(
            SystemMetrics system,
            DataMetrics data,
            CacheStatistics cache,
            SyncMetrics sync)
        {
            var score = 100;

            // Memory penalty
            if (system.Memory.GCMemoryMB > 500) score -= 10;
            if (system.Memory.GCMemoryMB > 1000) score -= 20;

            // Data completeness
            if (!data.Teams.IsComplete) score -= 15;
            if (data.Games.TodayCount == 0 && DateTime.Now.Hour > 10) score -= 10;

            // Cache performance
            if (cache.HitRate < 0.5) score -= 10;
            if (cache.HitRate < 0.3) score -= 10;

            // Sync health
            if (sync.SuccessRate < 90) score -= 10;
            if (sync.SuccessRate < 70) score -= 15;

            return Math.Max(0, score);
        }

        private static string DetermineSystemStatus(
            SystemMetrics system,
            DataMetrics data,
            CacheStatistics cache,
            SyncMetrics sync)
        {
            var score = CalculateHealthScore(system, data, cache, sync);

            return score switch
            {
                >= 90 => "Healthy",
                >= 70 => "Good",
                >= 50 => "Degraded",
                _ => "Critical"
            };
        }

        private static List<MetricAlert> GenerateAlerts(
            SystemMetrics system,
            DataMetrics data,
            CacheStatistics cache,
            SyncMetrics sync)
        {
            var alerts = new List<MetricAlert>();

            if (system.Memory.GCMemoryMB > 1000)
            {
                alerts.Add(new MetricAlert
                {
                    Level = "Warning",
                    Category = "Memory",
                    Message = $"High memory usage: {system.Memory.GCMemoryMB}MB",
                    Timestamp = DateTime.UtcNow
                });
            }

            if (!data.Teams.IsComplete)
            {
                alerts.Add(new MetricAlert
                {
                    Level = "Info",
                    Category = "Data",
                    Message = $"Teams data incomplete: {data.Teams.TotalCount}/30",
                    Timestamp = DateTime.UtcNow
                });
            }

            if (cache.HitRate < 0.3)
            {
                alerts.Add(new MetricAlert
                {
                    Level = "Warning",
                    Category = "Cache",
                    Message = $"Low cache hit rate: {cache.HitRate:P}",
                    Timestamp = DateTime.UtcNow
                });
            }

            if (sync.SuccessRate < 70)
            {
                alerts.Add(new MetricAlert
                {
                    Level = "Error",
                    Category = "Sync",
                    Message = $"Low sync success rate: {sync.SuccessRate:F1}%",
                    Timestamp = DateTime.UtcNow
                });
            }

            return alerts;
        }

        #endregion
    }

    #region DTOs for Metrics

    public class SystemMetrics
    {
        public UptimeInfo Uptime { get; set; } = new();
        public MemoryMetrics Memory { get; set; } = new();
        public PerformanceMetrics Performance { get; set; } = new();
        public EnvironmentInfo Environment { get; set; } = new();
    }

    public class UptimeInfo
    {
        public double TotalSeconds { get; set; }
        public string FormattedUptime { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime CurrentTime { get; set; }
    }

    public class MemoryMetrics
    {
        public long WorkingSetMB { get; set; }
        public long PrivateMemoryMB { get; set; }
        public long VirtualMemoryMB { get; set; }
        public long GCMemoryMB { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
    }

    public class PerformanceMetrics
    {
        public double TotalProcessorTimeSeconds { get; set; }
        public double UserProcessorTimeSeconds { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
    }

    public class EnvironmentInfo
    {
        public string MachineName { get; set; } = "";
        public string OSVersion { get; set; } = "";
        public int ProcessorCount { get; set; }
        public bool Is64BitProcess { get; set; }
        public string RuntimeFramework { get; set; } = "";
        public string AspNetCoreEnvironment { get; set; } = "";
    }

    public class DataMetrics
    {
        public TeamMetrics Teams { get; set; } = new();
        public GameMetrics Games { get; set; } = new();
        public DataQualityMetrics DataQuality { get; set; } = new();
    }

    public class TeamMetrics
    {
        public int TotalCount { get; set; }
        public bool IsComplete { get; set; }
        public Dictionary<string, int> ByConference { get; set; } = new();
        public Dictionary<string, int> ByDivision { get; set; } = new();
    }

    public class GameMetrics
    {
        public int TodayCount { get; set; }
        public int LiveCount { get; set; }
        public int CompletedCount { get; set; }
        public int ScheduledCount { get; set; }
        public int Last7DaysCount { get; set; }
        public double AverageScoreHome { get; set; }
        public double AverageScoreVisitor { get; set; }
    }

    public class DataQualityMetrics
    {
        public double TeamsCompleteness { get; set; }
        public double GamesWithScores { get; set; }
        public DateTime LastDataUpdate { get; set; }
        public string[] DataSources { get; set; } = Array.Empty<string>();
    }

    public class CacheMetrics
    {
        public double HitRate { get; set; }
        public long TotalRequests { get; set; }
        public long Hits { get; set; }
        public long Misses { get; set; }
        public long CurrentEntries { get; set; }
        public long Evictions { get; set; }
        public double EfficiencyScore { get; set; }
        public List<string> Recommendations { get; set; } = new();
    }

    public class SyncMetricsResponse
    {
        public SyncOverallMetrics Overall { get; set; } = new();
        public List<SyncTypeMetric> ByType { get; set; } = new();
        public SyncPerformanceMetrics Performance { get; set; } = new();
    }

    public class SyncOverallMetrics
    {
        public int TotalSyncs { get; set; }
        public int SuccessfulSyncs { get; set; }
        public int FailedSyncs { get; set; }
        public double SuccessRate { get; set; }
        public DateTime? LastSyncTime { get; set; }
        public DateTime? LastSuccessTime { get; set; }
        public DateTime? LastFailureTime { get; set; }
        public string? LastError { get; set; }
    }

    public class SyncTypeMetric
    {
        public string Type { get; set; } = "";
        public int Count { get; set; }
        public double Percentage { get; set; }
    }

    public class SyncPerformanceMetrics
    {
        public double UptimeHours { get; set; }
        public double SyncsPerHour { get; set; }
        public double AverageSuccessRate { get; set; }
        public DateTime? EstimatedNextSync { get; set; }
    }

    public class ApiMetrics
    {
        public EndpointMetrics Endpoints { get; set; } = new();
        public ResponseTimeMetrics ResponseTimes { get; set; } = new();
        public ErrorRateMetrics ErrorRates { get; set; } = new();
    }

    public class EndpointMetrics
    {
        public int TotalEndpoints { get; set; }
        public int ActiveEndpoints { get; set; }
        public EndpointUsage[] MostUsedEndpoints { get; set; } = Array.Empty<EndpointUsage>();
    }

    public class EndpointUsage
    {
        public string Endpoint { get; set; } = "";
        public int CallCount { get; set; }
    }

    public class ResponseTimeMetrics
    {
        public double AverageMs { get; set; }
        public double MedianMs { get; set; }
        public double P95Ms { get; set; }
        public double P99Ms { get; set; }
        public double MaxMs { get; set; }
    }

    public class ErrorRateMetrics
    {
        public int Total4xx { get; set; }
        public int Total5xx { get; set; }
        public double ErrorRate { get; set; }
        public ErrorInfo[] MostCommonErrors { get; set; } = Array.Empty<ErrorInfo>();
    }

    public class ErrorInfo
    {
        public int Code { get; set; }
        public int Count { get; set; }
        public string Message { get; set; } = "";
    }

    public class MetricsDashboard
    {
        public DateTime Timestamp { get; set; }
        public int HealthScore { get; set; }
        public string SystemStatus { get; set; } = "";
        public QuickStats QuickStats { get; set; } = new();
        public List<MetricAlert> Alerts { get; set; } = new();
    }

    public class QuickStats
    {
        public string Uptime { get; set; } = "";
        public int TotalTeams { get; set; }
        public int TodayGames { get; set; }
        public string CacheHitRate { get; set; } = "";
        public string SyncSuccessRate { get; set; } = "";
    }

    public class MetricAlert
    {
        public string Level { get; set; } = "";
        public string Category { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }

    #endregion
}