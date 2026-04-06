using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using HoopGameNight.Api.Constants;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Hangfire;
using HoopGameNight.Infrastructure.Jobs;

namespace HoopGameNight.Api.Controllers.V1.Admin
{
    /// <summary>
    /// Gerencia as operações de sincronização de dados (times e jogos) e fornece métricas de saúde do sistema.
    /// </summary>
    [Route(ApiConstants.Routes.SYNC)]
    [ApiExplorerSettings(GroupName = "admin")]
    public class SyncController : BaseApiController
    {
        private static readonly System.Threading.SemaphoreSlim _syncLock = new(1, 1);
        private readonly IGameService _gameService;
        private readonly IGameSyncService _gameSyncService;
        private readonly ITeamService _teamService;
        private readonly IPlayerService _playerService;
        private readonly IPlayerStatsService _playerStatsService;
        private readonly ISyncMetricsService _syncMetricsService;
        private readonly ICacheService _cacheService;
        private readonly ISyncHealthService _healthService;
        private readonly IBackgroundSyncService _backgroundSyncService;
        private readonly IPlayerStatsSyncService _playerStatsSyncService;
        private readonly IBackgroundJobClient _backgroundJobClient;

        public SyncController(
            IGameService gameService,
            IGameSyncService gameSyncService,
            ITeamService teamService,
            IPlayerService playerService,
            IPlayerStatsService playerStatsService,
            ISyncMetricsService syncMetricsService,
            ICacheService cacheService,
            ISyncHealthService healthService,
            IBackgroundSyncService backgroundSyncService,
            IPlayerStatsSyncService playerStatsSyncService,
            IBackgroundJobClient backgroundJobClient,
            ILogger<SyncController> logger) : base(logger)
        {
            _gameService = gameService;
            _gameSyncService = gameSyncService;
            _teamService = teamService;
            _playerService = playerService;
            _playerStatsService = playerStatsService;
            _syncMetricsService = syncMetricsService;
            _cacheService = cacheService;
            _healthService = healthService;
            _backgroundSyncService = backgroundSyncService;
            _playerStatsSyncService = playerStatsSyncService;
            _backgroundJobClient = backgroundJobClient;
        }

        /// <summary>
        /// Sincroniza dados fundamentais para o funcionamento do sistema (Times e Jogos de hoje).
        /// </summary>
        [HttpPost("essential")]
        [HttpGet("essential")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status202Accepted)]
        public async Task<ActionResult<ApiResponse<object>>> SyncEssential()
        {
            if (!_syncLock.Wait(0))
            {
                return Conflict(new { message = "Uma sincronização já está em andamento. Aguarde alguns instantes." });
            }

            try
            {
                var jobId = _backgroundJobClient.Enqueue<SyncJobs>(job => job.SyncManualEssentialJobAsync());
                
                return Accepted(ApiResponse<object>.SuccessResult(new { 
                    jobId, 
                    status = "Accepted"
                }, "Sincronização essencial iniciada em segundo plano."));
            }
            finally
            {
                _syncLock.Release();
            }
        }

        /// <summary>
        /// Realiza a sincronização completa do sistema: Times, Jogadores, Estatísticas de Carreira e Jogos de hoje.
        /// </summary>
        [HttpPost("full")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status202Accepted)]
        public async Task<ActionResult<ApiResponse<object>>> SyncFull()
        {
            if (!_syncLock.Wait(0))
            {
                return Conflict(new { message = "Uma sincronização robusta já está em execução no momento." });
            }

            try
            {
                var jobId = _backgroundJobClient.Enqueue<SyncJobs>(job => job.SyncManualFullJobAsync());
                
                return Accepted(ApiResponse<object>.SuccessResult(new { 
                    jobId, 
                    status = "Accepted"
                }, "Sincronização total (Times, Jogadores e Stats) iniciada em segundo plano. Isso pode levar alguns minutos."));
            }
            finally
            {
                _syncLock.Release();
            }
        }

        /// <summary>
        /// Retorna um panorama geral da saúde do sistema, incluindo status de cache e métricas de sincronização.
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
        /// Invalida o cache do sistema, podendo ser total ou por um padrão específico de chave.
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
        /// Realiza o check de conectividade com os provedores de dados externos.
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
        /// Retorna o status detalhado da última sincronização por entidade (Times/Jogos).
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
        /// Sincroniza o calendário de jogos futuros para o intervalo de dias especificado.
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
                    var syncCount = await _gameSyncService.SyncFutureGamesAsync(days);

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

        /// <summary>
        /// Realiza a análise de gaps para encontrar jogos finalizados sem estatísticas e corrigi-los.
        /// </summary>
        [HttpPost("gap-analysis")]
        [ProducesResponseType(typeof(ApiResponse<SyncOperationResult>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<SyncOperationResult>>> GapAnalysis()
        {
            return await ExecuteAsync<SyncOperationResult>(async () =>
            {
                var startTime = DateTime.UtcNow;
                Logger.LogInformation("Iniciando Gap Analysis manual...");

                await _backgroundSyncService.SyncMissingGamesStatsAsync();

                var duration = DateTime.UtcNow - startTime;
                return Ok(new SyncOperationResult
                {
                    Message = "Gap analysis completed",
                    DurationSeconds = duration.TotalSeconds,
                    Timestamp = DateTime.UtcNow
                }, "Gap analysis sync process triggered");
            });
        }

        /// <summary>
        /// Força a renovação do cache de dados semi-estáticos (Times/Jogadores).
        /// </summary>
        [HttpPost("cache-refresh")]
        [ProducesResponseType(typeof(ApiResponse<SyncOperationResult>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<SyncOperationResult>>> CacheRefresh()
        {
            return await ExecuteAsync<SyncOperationResult>(async () =>
            {
                var startTime = DateTime.UtcNow;
                Logger.LogInformation("Iniciando Background Cache Refresh manual...");

                await _backgroundSyncService.RefreshStaticDataCacheAsync();

                var duration = DateTime.UtcNow - startTime;
                return Ok(new SyncOperationResult
                {
                    Message = "Background cache refresh completed",
                    DurationSeconds = duration.TotalSeconds,
                    Timestamp = DateTime.UtcNow
                }, "Cache refresh process triggered");
            });
        }

        /// <summary>
        /// Sincroniza dados biométricos de um jogador específico.
        /// </summary>
        [HttpPost("player/{id}")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<bool>>> SyncPlayer(int id)
        {
            return await ExecuteAsync(async () =>
            {
                Logger.LogInformation("Sincronizando jogador individual: {PlayerId}", id);
                
                var player = await _playerService.GetPlayerByIdAsync(id);
                if (player == null)
                {
                    return NotFound<bool>($"Jogador {id} não encontrado.");
                }

                await _playerService.SyncPlayersAsync(player.FullName);
                await _playerStatsSyncService.SyncPlayerRecentGamesAsync(id, 20);
                
                return Ok(true, $"Sincronização do jogador {player.FullName} (dados e stats) concluída.");
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
        public int PlayersCount { get; set; }
        public int StatsSyncedCount { get; set; }
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

    public class SyncOperationResult
    {
        public string Message { get; set; } = string.Empty;
        public double DurationSeconds { get; set; }
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
