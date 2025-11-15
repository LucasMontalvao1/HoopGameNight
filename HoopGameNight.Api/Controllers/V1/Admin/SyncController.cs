using HoopGameNight.Api.Constants;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace HoopGameNight.Api.Controllers.V1.Admin
{
    /// <summary>
    /// Operações de sincronização e monitoramento
    /// </summary>
    [Route(ApiConstants.Routes.SYNC)]
    [ApiExplorerSettings(GroupName = "Admin")]
    public class SyncController : BaseApiController
    {
        private readonly IGameService _gameService;
        private readonly ITeamService _teamService;
        private readonly ISyncMetricsService _syncMetricsService;
        private readonly ICacheService _cacheService;
        private readonly ISyncHealthService _healthService;

        public SyncController(
            IGameService gameService,
            ITeamService teamService,
            ISyncMetricsService syncMetricsService,
            ICacheService cacheService,
            ISyncHealthService healthService,
            ILogger<SyncController> logger) : base(logger)
        {
            _gameService = gameService;
            _teamService = teamService;
            _syncMetricsService = syncMetricsService;
            _cacheService = cacheService;
            _healthService = healthService;
        }

        /// <summary>
        /// Sincronizar dados essenciais
        /// </summary>
        [HttpPost("essential")]
        [ProducesResponseType(typeof(ApiResponse<SyncResult>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<SyncResult>>> SyncEssential()
        {
            return await ExecuteAsync(async () =>
            {
                var startTime = DateTime.UtcNow;
                var errors = new List<string>();
                int teamsCount = 0, gamesCount = 0;

                try
                {
                    await _teamService.SyncAllTeamsAsync();
                    var teams = await _teamService.GetAllTeamsAsync();
                    teamsCount = teams.Count;
                    _cacheService.InvalidatePattern(ApiConstants.CacheKeys.TEAM_PATTERN);
                }
                catch (Exception ex)
                {
                    errors.Add($"Teams sync failed: {ex.Message}");
                    Logger.LogError(ex, "Failed to sync teams");
                }

                try
                {
                    await _gameService.SyncTodayGamesAsync();
                    var games = await _gameService.GetTodayGamesAsync();
                    gamesCount = games.Count;
                    _cacheService.InvalidatePattern(ApiConstants.CacheKeys.GAMES_PATTERN);
                }
                catch (Exception ex)
                {
                    errors.Add($"Games sync failed: {ex.Message}");
                    Logger.LogError(ex, "Failed to sync games");
                }

                var duration = DateTime.UtcNow - startTime;
                var success = errors.Count == 0;

                if (success)
                {
                    _syncMetricsService.RecordSuccess("EssentialSync", duration, teamsCount + gamesCount);
                }
                else
                {
                    _syncMetricsService.RecordFailure("EssentialSync", duration);
                }

                var result = new SyncResult
                {
                    Success = success,
                    Duration = duration,
                    TeamsCount = teamsCount,
                    GamesCount = gamesCount,
                    Errors = errors,
                    Timestamp = DateTime.UtcNow
                };

                return Ok(result, success ? "Essential sync completed" : "Sync completed with errors");
            });
        }

        /// <summary>
        /// Dashboard com status do sistema
        /// </summary>
        [HttpGet("dashboard")]
        [ProducesResponseType(typeof(ApiResponse<SyncDashboard>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<SyncDashboard>>> GetDashboard()
        {
            return await ExecuteAsync(async () =>
            {
                var teams = await _teamService.GetAllTeamsAsync();
                var todayGames = await _gameService.GetTodayGamesAsync();
                var cacheStats = _cacheService.GetStatistics();
                var syncMetrics = _syncMetricsService.GetMetrics();

                var dashboard = new SyncDashboard
                {
                    SystemHealth = _healthService.CalculateSystemHealth(
                        teams.Count, todayGames.Count, syncMetrics),
                    DataStatus = new DataStatus
                    {
                        TeamsCount = teams.Count,
                        TodayGamesCount = todayGames.Count,
                        LastSync = syncMetrics.LastSyncTime
                    },
                    CacheStatus = new CacheStatus
                    {
                        HitRate = cacheStats.HitRate,
                        TotalRequests = (int)cacheStats.TotalRequests,
                        CurrentEntries = cacheStats.CurrentEntries
                    },
                    SyncStatus = new SyncStatus
                    {
                        TotalSyncs = syncMetrics.TotalSyncs,
                        SuccessRate = syncMetrics.SuccessRate,
                        LastSuccess = syncMetrics.LastSuccessTime,
                        ConsecutiveFailures = syncMetrics.ConsecutiveFailures
                    },
                    Recommendations = _healthService.GenerateRecommendations(teams.Count, syncMetrics)
                };

                return Ok(dashboard, "Dashboard retrieved successfully");
            });
        }

        /// <summary>
        /// Limpar cache do sistema
        /// </summary>
        [HttpPost("cache/clear")]
        [ProducesResponseType(typeof(ApiResponse<CacheClearResult>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<CacheClearResult>>> ClearCache(
            [FromQuery] string? pattern = null)
        {
            return await ExecuteAsync(async () =>
            {
                var stats = _cacheService.GetStatistics();
                var entriesBefore = stats.CurrentEntries;

                if (string.IsNullOrEmpty(pattern))
                {
                    _cacheService.Clear();
                }
                else
                {
                    _cacheService.InvalidatePattern(pattern);
                }

                await Task.CompletedTask;

                var newStats = _cacheService.GetStatistics();
                var result = new CacheClearResult
                {
                    EntriesCleared = entriesBefore - newStats.CurrentEntries,
                    Pattern = pattern,
                    Timestamp = DateTime.UtcNow
                };

                return Ok(result, "Cache cleared successfully");
            });
        }

        /// <summary>
        /// Health check das APIs externas
        /// </summary>
        [HttpGet("health/external")]
        [ProducesResponseType(typeof(ApiResponse<ExternalApiHealth>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<ExternalApiHealth>>> CheckExternalApis()
        {
            return await ExecuteAsync(async () =>
            {
                var health = new ExternalApiHealth
                {
                    Message = "Using ESPN API only - BallDontLie deprecated",
                    Timestamp = DateTime.UtcNow
                };

                await Task.CompletedTask;

                return Ok(health, "External API health checked");
            });
        }

        /// <summary>
        /// Status de sincronização
        /// </summary>
        [HttpGet("status")]
        [ProducesResponseType(typeof(ApiResponse<SyncStatusResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<SyncStatusResponse>>> GetSyncStatus()
        {
            return await ExecuteAsync(async () =>
            {
                var teams = await _teamService.GetAllTeamsAsync();
                var todayGames = await _gameService.GetTodayGamesAsync();
                var metrics = _syncMetricsService.GetMetrics();

                var status = new SyncStatusResponse
                {
                    Teams = new SyncEntityStatus
                    {
                        LocalCount = teams.Count,
                        ExternalCount = 30, 
                        LastSync = metrics.LastSyncTime,
                        Status = teams.Count >= 30 ? "Complete" : "Incomplete"
                    },
                    Games = new SyncEntityStatus
                    {
                        LocalCount = todayGames.Count,
                        ExternalCount = 0, 
                        LastSync = metrics.LastSyncTime,
                        Status = todayGames.Count > 0 ? "Available" : "No games"
                    },
                    OverallHealth = new OverallSyncHealth
                    {
                        Score = _healthService.CalculateHealthScore(teams.Count, todayGames.Count, metrics),
                        Status = teams.Count >= 30 && metrics.SuccessRate > 80 ? "Healthy" : "Needs Attention",
                        Recommendation = _healthService.GetSyncRecommendation(teams.Count, todayGames.Count, metrics)
                    },
                    Timestamp = DateTime.UtcNow
                };

                return Ok(status, "Sync status retrieved");
            });
        }

        /// <summary>
        /// Reset metrics
        /// </summary>
        [HttpPost("metrics/reset")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<object>>> ResetMetrics()
        {
            return await ExecuteAsync(async () =>
            {
                _syncMetricsService.ResetMetrics();
                await Task.CompletedTask;

                return Ok((object)new
                {
                    message = "Metrics reset successfully",
                    timestamp = DateTime.UtcNow
                }, "Metrics reset");
            });
        }

        /// <summary>
        /// Sincronizar jogos futuros (próximos 10 dias)
        /// </summary>
        [HttpPost("future-games")]
        [ProducesResponseType(typeof(ApiResponse<FutureGamesSyncResult>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<FutureGamesSyncResult>>> SyncFutureGames(
            [FromQuery] int days = 10)
        {
            return await ExecuteAsync(async () =>
            {
                if (days < 1 || days > 30)
                {
                    return BadRequest<FutureGamesSyncResult>("Days must be between 1 and 30");
                }

                var startTime = DateTime.UtcNow;
                Logger.LogInformation("Iniciando sincronização de jogos futuros ({Days} dias)", days);

                try
                {
                    var syncCount = await _gameService.SyncFutureGamesAsync(days);

                    _cacheService.InvalidatePattern(ApiConstants.CacheKeys.GAMES_PATTERN);

                    var duration = DateTime.UtcNow - startTime;
                    var result = new FutureGamesSyncResult
                    {
                        Success = true,
                        GamesSynced = syncCount,
                        DaysAhead = days,
                        Duration = duration,
                        Timestamp = DateTime.UtcNow
                    };

                    Logger.LogInformation(
                        "Sincronização de jogos futuros concluída: {Count} jogos em {Duration}",
                        syncCount,
                        duration);

                    return Ok(result, $"Synced {syncCount} future games for next {days} days");
                }
                catch (Exception ex)
                {
                    var duration = DateTime.UtcNow - startTime;
                    Logger.LogError(ex, "Erro ao sincronizar jogos futuros");

                    var result = new FutureGamesSyncResult
                    {
                        Success = false,
                        GamesSynced = 0,
                        DaysAhead = days,
                        Duration = duration,
                        Error = ex.Message,
                        Timestamp = DateTime.UtcNow
                    };

                    var response = ApiResponse<FutureGamesSyncResult>.ErrorResult("Failed to sync future games");
                    response.Data = result;
                    response.RequestId = HttpContext.TraceIdentifier;
                    return base.StatusCode(500, response);
                }
            });
        }
    }

    #region DTOs

    public class SyncResult
    {
        public bool Success { get; set; }
        public TimeSpan Duration { get; set; }
        public int TeamsCount { get; set; }
        public int GamesCount { get; set; }
        public List<string> Errors { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class SyncDashboard
    {
        public string SystemHealth { get; set; } = "Unknown";
        public DataStatus DataStatus { get; set; } = new();
        public CacheStatus CacheStatus { get; set; } = new();
        public SyncStatus SyncStatus { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    public class DataStatus
    {
        public int TeamsCount { get; set; }
        public bool TeamsComplete => TeamsCount >= 30;
        public int TodayGamesCount { get; set; }
        public DateTime? LastSync { get; set; }
        public DateTime NextScheduledSync => DateTime.UtcNow.AddHours(1);
    }

    public class CacheStatus
    {
        public double HitRate { get; set; }
        public int TotalRequests { get; set; }
        public int CurrentEntries { get; set; }
        public string HitRateDisplay => $"{HitRate:P1}";
    }

    public class SyncStatus
    {
        public int TotalSyncs { get; set; }
        public double SuccessRate { get; set; }
        public DateTime? LastSuccess { get; set; }
        public int ConsecutiveFailures { get; set; }
        public string SuccessRateDisplay => $"{SuccessRate:F1}%";
    }

    public class CacheClearResult
    {
        public int EntriesCleared { get; set; }
        public string? Pattern { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ExternalApiHealth
    {
        // REMOVIDO: BallDontLie property - API deprecated
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class ApiHealthStatus
    {
        public bool IsHealthy { get; set; }
        public long ResponseTime { get; set; }
        public string? Error { get; set; }
        public DateTime LastCheck { get; set; }
    }

    public class SyncStatusResponse
    {
        public SyncEntityStatus Teams { get; set; } = new();
        public SyncEntityStatus Games { get; set; } = new();
        public OverallSyncHealth OverallHealth { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class SyncEntityStatus
    {
        public int LocalCount { get; set; }
        public int ExternalCount { get; set; }
        public bool NeedsSync => LocalCount != ExternalCount;
        public DateTime? LastSync { get; set; }
        public string Status { get; set; } = "";
    }

    public class OverallSyncHealth
    {
        public int Score { get; set; }
        public string Status { get; set; } = "";
        public string Recommendation { get; set; } = "";
    }

    public class FutureGamesSyncResult
    {
        public bool Success { get; set; }
        public int GamesSynced { get; set; }
        public int DaysAhead { get; set; }
        public TimeSpan Duration { get; set; }
        public string? Error { get; set; }
        public DateTime Timestamp { get; set; }
    }

    #endregion

    public interface ISyncHealthService
    {
        string CalculateSystemHealth(int teamsCount, int gamesCount, SyncMetrics metrics);
        List<string> GenerateRecommendations(int teamsCount, SyncMetrics metrics);
        int CalculateHealthScore(int teamsCount, int gamesCount, SyncMetrics metrics);
        string GetSyncRecommendation(int teamsCount, int gamesCount, SyncMetrics metrics);
    }

    public class SyncHealthService : ISyncHealthService
    {
        public string CalculateSystemHealth(int teamsCount, int gamesCount, SyncMetrics metrics)
        {
            if (teamsCount >= 30 && gamesCount > 0 && metrics.SuccessRate > 80)
                return "Healthy";
            return "Needs Attention";
        }

        public List<string> GenerateRecommendations(int teamsCount, SyncMetrics metrics)
        {
            var recs = new List<string>();
            if (teamsCount < 30) recs.Add("Sync teams to reach full roster.");
            if (metrics.SuccessRate < 80) recs.Add("Improve sync reliability.");
            return recs;
        }

        public int CalculateHealthScore(int teamsCount, int gamesCount, SyncMetrics metrics)
        {
            var score = 0;
            if (teamsCount >= 30) score += 40;
            if (gamesCount > 0) score += 30;
            score += (int)(metrics.SuccessRate / 2);
            return Math.Min(score, 100);
        }

        public string GetSyncRecommendation(int teamsCount, int gamesCount, SyncMetrics metrics)
        {
            if (teamsCount < 30) return "Add missing teams";
            if (gamesCount == 0) return "Check game sync";
            if (metrics.SuccessRate < 80) return "Stabilize sync process";
            return "System is running well";
        }
    }
}
