using HoopGameNight.Api.Constants;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Api.Controllers.V1
{
    [Route(ApiConstants.API_PREFIX + "/" + ApiConstants.API_VERSION + "/sync")]
    public class SyncController : BaseApiController
    {
        private readonly IGameService _gameService;
        private readonly ITeamService _teamService;
        private readonly IPlayerService _playerService;
        private readonly IBallDontLieService _ballDontLieService;
        private readonly IMemoryCache _cache;
        private readonly ISyncMetricsService _syncMetricsService;
        private readonly ICacheService _cacheService;

        public SyncController(
            IGameService gameService,
            ITeamService teamService,
            IPlayerService playerService,
            IBallDontLieService ballDontLieService,
            IMemoryCache cache,
            ISyncMetricsService syncMetricsService,
            ICacheService cacheService,
            ILogger<SyncController> logger) : base(logger)
        {
            _gameService = gameService;
            _teamService = teamService;
            _playerService = playerService;
            _ballDontLieService = ballDontLieService;
            _cache = cache;
            _syncMetricsService = syncMetricsService;
            _cacheService = cacheService;
        }

        #region Sincronização Principal

        /// <summary>
        /// Sincronizar todos os dados da API externa (COMPLETO)
        /// </summary>
        /// <returns>Status da sincronização completa</returns>
        [HttpPost("all")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<object>>> SyncAll()
        {
            try
            {
                Logger.LogInformation("Iniciando sincronização COMPLETA dos dados");

                var startTime = DateTime.UtcNow;
                var syncResults = new List<string>();

                // Sincroniza os times
                try
                {
                    await _teamService.SyncAllTeamsAsync();
                    _cache.Remove(ApiConstants.CacheKeys.ALL_TEAMS);
                    syncResults.Add("Times sincronizados com sucesso");
                    Logger.LogInformation("Sincronização de times concluída");
                }
                catch (Exception ex)
                {
                    syncResults.Add($"Falha na sincronização de times: {ex.Message}");
                    Logger.LogError(ex, "Falha na sincronização de times");
                }

                // Sincroniza os jogos de hoje
                try
                {
                    await _gameService.SyncTodayGamesAsync();
                    _cache.Remove(ApiConstants.CacheKeys.TODAY_GAMES);
                    syncResults.Add("Jogos de hoje sincronizados com sucesso");
                    Logger.LogInformation("Sincronização de jogos concluída");
                }
                catch (Exception ex)
                {
                    syncResults.Add($"Falha na sincronização de jogos: {ex.Message}");
                    Logger.LogError(ex, "Falha na sincronização de jogos");
                }

                // Sincroniza alguns jogadores
                try
                {
                    var popularNames = new[] { "lebron", "curry", "durant", "giannis", "luka" };
                    foreach (var name in popularNames)
                    {
                        await _playerService.SyncPlayersAsync(name);
                    }
                    syncResults.Add("Jogadores populares sincronizados com sucesso");
                    Logger.LogInformation("Sincronização de jogadores concluída");
                }
                catch (Exception ex)
                {
                    syncResults.Add($"Falha na sincronização de jogadores: {ex.Message}");
                    Logger.LogError(ex, "Falha na sincronização de jogadores");
                }

                var duration = DateTime.UtcNow - startTime;

                // Registrar métricas
                var success = !syncResults.Any(r => r.Contains("Falha"));
                var totalRecords = (await _teamService.GetAllTeamsAsync()).Count +
                                 (await _gameService.GetTodayGamesAsync()).Count;

                _syncMetricsService.RecordSyncEvent("all", success, duration, totalRecords);

                var result = (object)new
                {
                    message = "Sincronização completa finalizada",
                    duration = $"{duration.TotalSeconds:F2} segundos",
                    timestamp = DateTime.UtcNow,
                    results = syncResults,
                    nextRecommendedSync = DateTime.UtcNow.AddHours(1),
                    summary = new
                    {
                        teamsCount = (await _teamService.GetAllTeamsAsync()).Count,
                        todayGamesCount = (await _gameService.GetTodayGamesAsync()).Count
                    }
                };

                Logger.LogInformation("Sincronização completa finalizada em {Duration} segundos", duration.TotalSeconds);
                return Ok(result, "Todos os dados sincronizados com sucesso");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro durante a sincronização completa");
                _syncMetricsService.RecordSyncEvent("all", false, TimeSpan.Zero);
                throw;
            }
        }

        /// <summary>
        /// Sincronizar apenas dados essenciais (rápido)
        /// </summary>
        [HttpPost("essential")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<object>>> SyncEssential()
        {
            try
            {
                Logger.LogInformation("Iniciando sincronização de dados essenciais");

                var startTime = DateTime.UtcNow;

                await _teamService.SyncAllTeamsAsync();
                await _gameService.SyncTodayGamesAsync();

                _cache.Remove(ApiConstants.CacheKeys.ALL_TEAMS);
                _cache.Remove(ApiConstants.CacheKeys.TODAY_GAMES);

                var duration = DateTime.UtcNow - startTime;

                // Registrar métricas
                var totalRecords = (await _teamService.GetAllTeamsAsync()).Count +
                                 (await _gameService.GetTodayGamesAsync()).Count;

                _syncMetricsService.RecordSyncEvent("essential", true, duration, totalRecords);

                var result = (object)new
                {
                    message = "Dados essenciais sincronizados",
                    duration = $"{duration.TotalSeconds:F2} segundos",
                    timestamp = DateTime.UtcNow,
                    synced = new[] { "Times", "Jogos de Hoje" }
                };

                Logger.LogInformation("Sincronização essencial concluída em {Duration} segundos", duration.TotalSeconds);
                return Ok(result, "Dados essenciais sincronizados com sucesso");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro durante sincronização essencial");
                _syncMetricsService.RecordSyncEvent("essential", false, TimeSpan.Zero);
                throw;
            }
        }

        /// <summary>
        /// Forçar sincronização com prioridade e opções
        /// </summary>
        [HttpPost("force/{syncType}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<object>>> ForceSyncByType(
            string syncType,
            [FromQuery] bool clearCache = true,
            [FromQuery] bool priority = false)
        {
            try
            {
                Logger.LogInformation("🔄 Sincronização forçada solicitada: Tipo={SyncType}, LimparCache={ClearCache}, Prioridade={Priority}",
                    syncType, clearCache, priority);

                var validTypes = new[] { "teams", "games", "today", "yesterday", "week", "all" };
                if (!validTypes.Contains(syncType.ToLower()))
                {
                    return BadRequest(ApiResponse<object>.ErrorResult(
                        $"Tipo de sincronização inválido. Tipos válidos: {string.Join(", ", validTypes)}"
                    ));
                }

                var startTime = DateTime.UtcNow;
                var results = new Dictionary<string, object>();
                var overallSuccess = true;

                switch (syncType.ToLower())
                {
                    case "teams":
                        var teamsResult = await SyncTeamsWithMetrics(clearCache);
                        results["teams"] = teamsResult;
                        overallSuccess = teamsResult.Success;
                        break;

                    case "games":
                    case "today":
                        var todayResult = await SyncTodayGamesWithMetrics(clearCache);
                        results["todayGames"] = todayResult;
                        overallSuccess = todayResult.Success;
                        break;

                    case "yesterday":
                        var yesterdayResult = await SyncYesterdayGamesWithMetrics(clearCache);
                        results["yesterdayGames"] = yesterdayResult;
                        overallSuccess = yesterdayResult.Success;
                        break;

                    case "week":
                        var weekResults = await SyncWeekGamesWithMetrics(clearCache);
                        results["weekGames"] = weekResults;
                        overallSuccess = weekResults.Values.All(r => ((dynamic)r).Success);
                        break;

                    case "all":
                        var allResults = await SyncAllWithMetrics(clearCache);
                        results = allResults;
                        overallSuccess = allResults.Values.All(r => ((dynamic)r).Success);
                        break;
                }

                var duration = DateTime.UtcNow - startTime;

                // Registrar métrica
                _syncMetricsService.RecordSyncEvent(
                    $"Force-{syncType}",
                    overallSuccess,
                    duration,
                    results.Values.Sum(r => ((dynamic)r).RecordsProcessed ?? 0)
                );

                var response = new
                {
                    syncType,
                    success = overallSuccess,
                    duration = $"{duration.TotalSeconds:F2}s",
                    timestamp = DateTime.UtcNow,
                    results,
                    cacheCleared = clearCache,
                    priority
                };

                Logger.LogInformation("✅ Sincronização forçada concluída: {SyncType} em {Duration}s",
                    syncType, duration.TotalSeconds);

                return Ok((object)response, $"Sincronização forçada '{syncType}' concluída");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro durante sincronização forçada: {SyncType}", syncType);
                _syncMetricsService.RecordSyncEvent($"Force-{syncType}", false, TimeSpan.Zero);
                throw;
            }
        }

        #endregion

        #region Status e Monitoramento

        /// <summary>
        /// Dashboard completo com métricas e status do sistema
        /// </summary>
        [HttpGet("dashboard")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<object>>> GetSyncDashboard()
        {
            try
            {
                Logger.LogInformation("📊 Gerando dashboard de sincronização");

                // Coletar todas as informações
                var metrics = _syncMetricsService.GetMetrics();
                var alerts = _syncMetricsService.GetAlerts();
                var cacheStats = _cacheService.GetStatistics();
                var systemStatus = await GetSystemStatusAsync();

                var dashboard = new
                {
                    overview = new
                    {
                        status = CalculateOverallSystemHealth(systemStatus, metrics),
                        uptime = FormatTimeSpan(metrics.Uptime),
                        lastSync = metrics.LastSyncTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Nunca",
                        lastSuccess = metrics.LastSuccessfulSync?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Nunca",
                        environment = new
                        {
                            apiUrl = "https://api.balldontlie.io",
                            apiType = "Plano Gratuito",
                            limitations = new[]
                            {
                                "Sem jogos futuros",
                                "Limite de taxa: 30 req/min",
                                "Apenas dados históricos"
                            }
                        }
                    },

                    syncStatistics = new
                    {
                        global = new
                        {
                            totalSyncs = metrics.TotalSyncs,
                            successful = metrics.SuccessfulSyncs,
                            failed = metrics.FailedSyncs,
                            successRate = $"{metrics.SuccessRate:F1}%",
                            averageDuration = FormatTimeSpan(metrics.AverageDuration),
                            minDuration = FormatTimeSpan(metrics.MinDuration),
                            maxDuration = FormatTimeSpan(metrics.MaxDuration),
                            totalRecords = metrics.TotalRecordsProcessed,
                            averageRecordsPerSync = $"{metrics.AverageRecordsPerSync:F1}"
                        },
                        byType = metrics.MetricsByType.ToDictionary(
                            kvp => kvp.Key,
                            kvp => new
                            {
                                total = kvp.Value.TotalSyncs,
                                successRate = $"{kvp.Value.SuccessRate:F1}%",
                                avgDuration = FormatTimeSpan(kvp.Value.AverageDuration),
                                lastSync = kvp.Value.LastSyncTime?.ToString("HH:mm:ss") ?? "Nunca",
                                consecutiveFailures = kvp.Value.ConsecutiveFailures
                            }
                        )
                    },

                    currentData = systemStatus,

                    cacheStatistics = new
                    {
                        hitRate = $"{cacheStats.HitRate:P}",
                        totalRequests = cacheStats.TotalRequests,
                        hits = cacheStats.Hits,
                        misses = cacheStats.Misses,
                        evictions = cacheStats.Evictions,
                        currentEntries = cacheStats.CurrentEntries,
                        entriesByCategory = cacheStats.EntriesByCategory
                    },

                    recentActivity = new
                    {
                        lastSyncs = metrics.Events
                            .OrderByDescending(e => e.Timestamp)
                            .Take(20)
                            .Select(e => new
                            {
                                type = e.Type,
                                success = e.Success,
                                duration = $"{e.Duration.TotalSeconds:F2}s",
                                records = e.RecordsProcessed,
                                timestamp = e.Timestamp.ToString("HH:mm:ss"),
                                status = e.Success ? "✅" : "❌"
                            }),

                        alerts = alerts
                            .OrderByDescending(a => a.Timestamp)
                            .Take(10)
                            .Select(a => new
                            {
                                type = a.Type.ToString(),
                                severity = a.Severity.ToString(),
                                message = a.Message,
                                details = a.Details,
                                timestamp = a.Timestamp.ToString("HH:mm:ss"),
                                icon = GetAlertIcon(a.Severity)
                            })
                    },

                    recommendations = GenerateRecommendations(metrics, systemStatus, alerts),

                    nextActions = new
                    {
                        nextScheduledSync = CalculateNextSyncTime(),
                        suggestedActions = GenerateSuggestedActions(systemStatus, metrics)
                    }
                };

                return Ok((object)dashboard, "Dashboard de sincronização gerado com sucesso");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao gerar dashboard de sincronização");
                throw;
            }
        }

        /// <summary>
        /// Verificar status geral de todas as sincronizações
        /// </summary>
        [HttpGet("status")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<object>>> GetOverallSyncStatus()
        {
            try
            {
                Logger.LogInformation("Verificando status geral de sincronização");

                var localTeams = await _teamService.GetAllTeamsAsync();
                var localGames = await _gameService.GetTodayGamesAsync();
                var externalTeams = await _ballDontLieService.GetAllTeamsAsync();
                var externalGames = await _ballDontLieService.GetTodaysGamesAsync();

                var status = (object)new
                {
                    lastCheck = DateTime.UtcNow,
                    teams = new
                    {
                        local = localTeams.Count,
                        external = externalTeams.Count(),
                        needsSync = localTeams.Count != externalTeams.Count(),
                        status = localTeams.Count >= 30 ? "Completo" : "Incompleto"
                    },
                    games = new
                    {
                        local = localGames.Count,
                        external = externalGames.Count(),
                        needsSync = localGames.Count != externalGames.Count(),
                        status = localGames.Count > 0 ? "Disponível" : "Sem jogos"
                    },
                    overallHealth = new
                    {
                        score = CalculateHealthScore(localTeams.Count, localGames.Count),
                        recommendation = GetSyncRecommendation(localTeams.Count, localGames.Count)
                    },
                    cacheStatus = new
                    {
                        teamsCache = _cache.TryGetValue(ApiConstants.CacheKeys.ALL_TEAMS, out _),
                        gamesCache = _cache.TryGetValue(ApiConstants.CacheKeys.TODAY_GAMES, out _)
                    }
                };

                return Ok(status, "Status geral de sincronização obtido");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao verificar status geral de sincronização");
                throw;
            }
        }

        /// <summary>
        /// Verificar conectividade com API externa
        /// </summary>
        [HttpGet("health")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<object>>> CheckExternalApiHealth()
        {
            try
            {
                Logger.LogInformation("Verificando saúde da API externa");

                var startTime = DateTime.UtcNow;
                var isHealthy = false;
                var errorMessage = "";

                try
                {
                    var teams = await _ballDontLieService.GetAllTeamsAsync();
                    isHealthy = teams.Any();
                }
                catch (Exception ex)
                {
                    errorMessage = ex.Message;
                }

                var responseTime = DateTime.UtcNow - startTime;

                var health = (object)new
                {
                    externalApi = "Ball Don't Lie API",
                    isHealthy,
                    responseTime = $"{responseTime.TotalMilliseconds:F0}ms",
                    lastChecked = DateTime.UtcNow,
                    status = isHealthy ? "Saudável" : "Não Saudável",
                    error = string.IsNullOrEmpty(errorMessage) ? null : errorMessage,
                    recommendations = GetApiHealthRecommendations(isHealthy, responseTime)
                };

                Logger.LogInformation("Saúde da API externa: {Status}", isHealthy ? "Saudável" : "Não Saudável");
                return Ok(health, "Saúde da API externa verificada");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao verificar saúde da API externa");

                var health = (object)new
                {
                    externalApi = "Ball Don't Lie API",
                    isHealthy = false,
                    error = ex.Message,
                    lastChecked = DateTime.UtcNow,
                    status = "Falha na Conexão"
                };

                return Ok(health, "Verificação de saúde da API externa falhou");
            }
        }

        #endregion

        #region Métricas

        /// <summary>
        /// Obter métricas detalhadas por tipo
        /// </summary>
        [HttpGet("metrics/{syncType}")]
        [ProducesResponseType(typeof(ApiResponse<SyncMetrics>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<SyncMetrics>>> GetMetricsByType(string syncType)
        {
            try
            {
                var metrics = _syncMetricsService.GetMetricsByType(syncType);

                if (metrics.TotalSyncs == 0)
                {
                    return NotFound(ApiResponse<SyncMetrics>.ErrorResult(
                        $"Nenhuma métrica encontrada para o tipo de sincronização: {syncType}"
                    ));
                }

                return Ok(metrics, $"Métricas para {syncType} obtidas");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao obter métricas para o tipo: {SyncType}", syncType);
                throw;
            }
        }

        /// <summary>
        /// Resetar todas as métricas
        /// </summary>
        [HttpPost("metrics/reset")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<object>>> ResetAllMetrics()
        {
            try
            {
                Logger.LogWarning("⚠️ Resetando todas as métricas de sincronização");

                _syncMetricsService.ResetMetrics();

                return Ok((object)new
                {
                    message = "Todas as métricas foram resetadas",
                    timestamp = DateTime.UtcNow
                }, "Métricas resetadas com sucesso");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao resetar métricas");
                throw;
            }
        }

        #endregion

        #region Gerenciamento de Cache

        /// <summary>
        /// Limpar todos os caches do sistema
        /// </summary>
        [HttpPost("cache/clear")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<object>>> ClearAllCaches()
        {
            try
            {
                Logger.LogInformation("Limpando todos os caches do sistema");

                var clearedCaches = new List<string>();

                if (_cache.TryGetValue(ApiConstants.CacheKeys.ALL_TEAMS, out _))
                {
                    _cache.Remove(ApiConstants.CacheKeys.ALL_TEAMS);
                    clearedCaches.Add("Cache de times");
                }

                if (_cache.TryGetValue(ApiConstants.CacheKeys.TODAY_GAMES, out _))
                {
                    _cache.Remove(ApiConstants.CacheKeys.TODAY_GAMES);
                    clearedCaches.Add("Cache de jogos");
                }

                for (int i = 1; i <= 100; i++)
                {
                    var playerKey = string.Format(ApiConstants.CacheKeys.PLAYER_BY_ID, i);
                    if (_cache.TryGetValue(playerKey, out _))
                    {
                        _cache.Remove(playerKey);
                        clearedCaches.Add($"Cache do jogador {i}");
                    }
                }

                var result = (object)new
                {
                    message = "Caches limpos com sucesso",
                    clearedCaches,
                    totalCleared = clearedCaches.Count,
                    timestamp = DateTime.UtcNow
                };

                Logger.LogInformation("Limpos {Count} caches", clearedCaches.Count);
                return Ok(result, "Todos os caches limpos com sucesso");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao limpar caches");
                throw;
            }
        }

        /// <summary>
        /// Gerenciar cache do sistema
        /// </summary>
        [HttpPost("cache/{action}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<object>>> ManageCache(
            string action,
            [FromQuery] string? pattern = null)
        {
            try
            {
                var validActions = new[] { "clear", "stats", "remove" };
                if (!validActions.Contains(action.ToLower()))
                {
                    return BadRequest(ApiResponse<object>.ErrorResult(
                        $"Ação inválida. Ações válidas: {string.Join(", ", validActions)}"
                    ));
                }

                object result = action.ToLower() switch
                {
                    "clear" => ClearAllCache(),
                    "stats" => _cacheService.GetStatistics(),
                    "remove" => RemoveCacheByPattern(pattern ?? ""),
                    _ => null
                };

                return Ok(result, $"Cache {action} concluído");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao gerenciar cache: {Action}", action);
                throw;
            }
        }

        #endregion

        #region Métodos Auxiliares

        private async Task<object> GetSystemStatusAsync()
        {
            var teams = await _teamService.GetAllTeamsAsync();
            var todayGames = await _gameService.GetTodayGamesAsync();

            // Buscar jogos dos últimos 7 dias para estatísticas
            var recentGames = new List<GameResponse>();
            for (int i = -7; i <= 0; i++)
            {
                try
                {
                    var games = await _gameService.GetGamesByDateAsync(DateTime.Today.AddDays(i));
                    recentGames.AddRange(games);
                }
                catch { }
            }

            return new
            {
                teams = new
                {
                    count = teams.Count,
                    complete = teams.Count >= 30,
                    missing = Math.Max(0, 30 - teams.Count),
                    byConference = teams.GroupBy(t => t.Conference)
                        .ToDictionary(g => g.Key, g => g.Count())
                },
                games = new
                {
                    today = new
                    {
                        total = todayGames.Count,
                        live = todayGames.Count(g => g.IsLive),
                        completed = todayGames.Count(g => g.IsCompleted),
                        scheduled = todayGames.Count(g => !g.IsLive && !g.IsCompleted)
                    },
                    last7Days = new
                    {
                        total = recentGames.Count,
                        avgPerDay = recentGames.Count / 7.0
                    }
                },
                dataQuality = new
                {
                    teamsCompleteness = teams.Count >= 30 ? 100 : (teams.Count / 30.0 * 100),
                    hasRecentData = recentGames.Any(g => g.Date >= DateTime.Today.AddDays(-1))
                }
            };
        }

        private string CalculateOverallSystemHealth(dynamic status, SyncMetrics metrics)
        {
            var score = 0;

            // Teams completeness (30 points)
            if (status.teams.count >= 30) score += 30;
            else if (status.teams.count >= 20) score += 20;
            else if (status.teams.count >= 10) score += 10;

            // Sync success rate (30 points)
            if (metrics.SuccessRate >= 90) score += 30;
            else if (metrics.SuccessRate >= 70) score += 20;
            else if (metrics.SuccessRate >= 50) score += 10;

            // Recent sync (20 points)
            if (metrics.LastSuccessfulSync > DateTime.UtcNow.AddHours(-1)) score += 20;
            else if (metrics.LastSuccessfulSync > DateTime.UtcNow.AddHours(-6)) score += 10;

            // Data freshness (20 points)
            if (status.dataQuality.hasRecentData) score += 20;

            return score switch
            {
                >= 90 => "✅ Excelente",
                >= 70 => "🟢 Saudável",
                >= 50 => "🟡 Regular",
                >= 30 => "🟠 Ruim",
                _ => "🔴 Crítico"
            };
        }

        private List<string> GenerateRecommendations(SyncMetrics metrics, dynamic status, List<SyncAlert> alerts)
        {
            var recommendations = new List<string>();

            // Baseado em métricas
            if (metrics.SuccessRate < 90)
                recommendations.Add($"⚠️ Taxa de sucesso é {metrics.SuccessRate:F1}%. Verifique conectividade da API e logs.");

            if (status.teams.count < 30)
                recommendations.Add($"📝 Faltam {status.teams.missing} times. Execute 'sincronização forçada de times'.");

            if (metrics.LastSuccessfulSync < DateTime.UtcNow.AddHours(-2))
                recommendations.Add("⏰ Nenhuma sincronização bem-sucedida há mais de 2 horas. Verifique status do serviço em segundo plano.");

            // Baseado em alertas
            var criticalAlerts = alerts.Count(a => a.Severity == AlertSeverity.Critical);
            if (criticalAlerts > 0)
                recommendations.Add($"🚨 {criticalAlerts} alertas críticos detectados. Ação imediata necessária.");

            // Baseado em cache
            var cacheStats = _cacheService.GetStatistics();
            if (cacheStats.HitRate < 0.5)
                recommendations.Add("📊 Baixa taxa de acerto do cache. Considere ajustar duração do cache.");

            if (recommendations.Count == 0)
                recommendations.Add("✅ Sistema está saudável. Nenhuma ação imediata necessária.");

            return recommendations;
        }

        private List<string> GenerateSuggestedActions(dynamic status, SyncMetrics metrics)
        {
            var actions = new List<string>();

            if (status.teams.count < 30)
                actions.Add("POST /api/v1/sync/force/teams - Sincronizar todos os times");

            if (status.games.today.total == 0 && DateTime.Now.Hour > 10)
                actions.Add("POST /api/v1/sync/force/today - Sincronizar jogos de hoje");

            if (metrics.ConsecutiveFailures > 0)
                actions.Add("GET /api/v1/sync/health - Verificar conectividade da API");

            return actions;
        }

        private DateTime CalculateNextSyncTime()
        {
            // Simplificado - assumir intervalo de 1 hora
            return DateTime.UtcNow.AddHours(1);
        }

        private string GetAlertIcon(AlertSeverity severity)
        {
            return severity switch
            {
                AlertSeverity.Critical => "🚨",
                AlertSeverity.Warning => "⚠️",
                AlertSeverity.Info => "ℹ️",
                _ => "📌"
            };
        }

        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan == TimeSpan.Zero) return "0s";
            if (timeSpan.TotalDays >= 1)
                return $"{timeSpan.Days}d {timeSpan.Hours}h {timeSpan.Minutes}m";
            if (timeSpan.TotalHours >= 1)
                return $"{timeSpan.Hours}h {timeSpan.Minutes}m";
            if (timeSpan.TotalMinutes >= 1)
                return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
            if (timeSpan.TotalSeconds >= 1)
                return $"{timeSpan.TotalSeconds:F1}s";
            return $"{timeSpan.TotalMilliseconds:F0}ms";
        }

        private static int CalculateHealthScore(int teamsCount, int gamesCount)
        {
            var score = 0;

            // Teams score (0-50 points)
            if (teamsCount >= 30) score += 50;
            else if (teamsCount >= 20) score += 35;
            else if (teamsCount >= 10) score += 20;
            else if (teamsCount > 0) score += 10;

            // Games score (0-50 points)
            if (gamesCount >= 10) score += 50;
            else if (gamesCount >= 5) score += 35;
            else if (gamesCount >= 1) score += 20;

            return score;
        }

        private static string GetSyncRecommendation(int teamsCount, int gamesCount)
        {
            if (teamsCount == 0) return "URGENTE: Execute sincronização inicial de times";
            if (teamsCount < 30) return "Dados de times incompletos - sincronização recomendada";
            if (gamesCount == 0) return "Nenhum dado de jogos - sincronize jogos de hoje";
            return "Dados parecem saudáveis";
        }

        private static List<string> GetApiHealthRecommendations(bool isHealthy, TimeSpan responseTime)
        {
            var recommendations = new List<string>();

            if (!isHealthy)
            {
                recommendations.Add("Verifique conexão com a internet");
                recommendations.Add("Verifique configuração da chave da API");
                recommendations.Add("Tente novamente em alguns minutos");
            }
            else
            {
                if (responseTime.TotalSeconds > 5)
                    recommendations.Add("Resposta da API está lenta - considere usar cache");

                recommendations.Add("API está saudável - seguro para realizar sincronização");
            }

            return recommendations;
        }

        #endregion

        #region Métodos de Sync com Métricas

        private async Task<dynamic> SyncTeamsWithMetrics(bool clearCache)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                await _teamService.SyncAllTeamsAsync();
                if (clearCache) _cacheService.RemoveByPattern("team");

                var teams = await _teamService.GetAllTeamsAsync();
                return new { Success = true, RecordsProcessed = teams.Count };
            }
            catch (Exception ex)
            {
                return new { Success = false, Error = ex.Message, RecordsProcessed = 0 };
            }
        }

        private async Task<dynamic> SyncTodayGamesWithMetrics(bool clearCache)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                await _gameService.SyncTodayGamesAsync();
                if (clearCache) _cacheService.RemoveByPattern("game");

                var games = await _gameService.GetTodayGamesAsync();
                return new { Success = true, RecordsProcessed = games.Count };
            }
            catch (Exception ex)
            {
                return new { Success = false, Error = ex.Message, RecordsProcessed = 0 };
            }
        }

        private async Task<dynamic> SyncYesterdayGamesWithMetrics(bool clearCache)
        {
            try
            {
                var yesterday = DateTime.Today.AddDays(-1);
                var count = await _gameService.SyncGamesByDateAsync(yesterday);
                if (clearCache) _cacheService.Remove($"games_{yesterday:yyyy-MM-dd}");

                return new { Success = true, RecordsProcessed = count };
            }
            catch (Exception ex)
            {
                return new { Success = false, Error = ex.Message, RecordsProcessed = 0 };
            }
        }

        private async Task<Dictionary<string, dynamic>> SyncWeekGamesWithMetrics(bool clearCache)
        {
            var results = new Dictionary<string, dynamic>();

            for (int i = -7; i <= 0; i++)
            {
                var date = DateTime.Today.AddDays(i);
                try
                {
                    var count = await _gameService.SyncGamesByDateAsync(date);
                    if (clearCache) _cacheService.Remove($"games_{date:yyyy-MM-dd}");

                    results[date.ToString("yyyy-MM-dd")] = new
                    {
                        Success = true,
                        RecordsProcessed = count
                    };
                }
                catch (Exception ex)
                {
                    results[date.ToString("yyyy-MM-dd")] = new
                    {
                        Success = false,
                        Error = ex.Message,
                        RecordsProcessed = 0
                    };
                }
            }

            return results;
        }

        private async Task<Dictionary<string, object>> SyncAllWithMetrics(bool clearCache)
        {
            var results = new Dictionary<string, object>();

            // Teams
            results["teams"] = await SyncTeamsWithMetrics(clearCache);

            // Today's games
            results["todayGames"] = await SyncTodayGamesWithMetrics(clearCache);

            // Yesterday's games
            results["yesterdayGames"] = await SyncYesterdayGamesWithMetrics(clearCache);

            return results;
        }

        private object ClearAllCache()
        {
            var stats = _cacheService.GetStatistics();
            _cacheService.Clear();

            return new
            {
                message = "Todo cache limpo",
                clearedEntries = stats.CurrentEntries,
                timestamp = DateTime.UtcNow
            };
        }

        private object RemoveCacheByPattern(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return new { message = "Padrão é obrigatório", removed = 0 };
            }

            _cacheService.RemoveByPattern(pattern);

            return new
            {
                message = $"Entradas de cache que correspondem a '{pattern}' removidas",
                pattern,
                timestamp = DateTime.UtcNow
            };
        }

        #endregion
    }
}