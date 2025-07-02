using HoopGameNight.Api.Constants;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Interfaces.Services;
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

        public SyncController(
            IGameService gameService,
            ITeamService teamService,
            IPlayerService playerService,
            IBallDontLieService ballDontLieService,
            IMemoryCache cache,
            ILogger<SyncController> logger) : base(logger)
        {
            _gameService = gameService;
            _teamService = teamService;
            _playerService = playerService;
            _ballDontLieService = ballDontLieService;
            _cache = cache;
        }

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
                Logger.LogInformation("🚀 Starting COMPLETE data synchronization");

                var startTime = DateTime.UtcNow;
                var syncResults = new List<string>();

                // 1. Sync teams first (dependency for games and players)
                try
                {
                    await _teamService.SyncAllTeamsAsync();
                    _cache.Remove(ApiConstants.CacheKeys.ALL_TEAMS);
                    syncResults.Add("✅ Teams synced successfully");
                    Logger.LogInformation("✅ Teams sync completed");
                }
                catch (Exception ex)
                {
                    syncResults.Add($"❌ Teams sync failed: {ex.Message}");
                    Logger.LogError(ex, "❌ Teams sync failed");
                }

                // 2. Sync today's games
                try
                {
                    await _gameService.SyncTodayGamesAsync();
                    _cache.Remove(ApiConstants.CacheKeys.TODAY_GAMES);
                    syncResults.Add("✅ Today's games synced successfully");
                    Logger.LogInformation("✅ Games sync completed");
                }
                catch (Exception ex)
                {
                    syncResults.Add($"❌ Games sync failed: {ex.Message}");
                    Logger.LogError(ex, "❌ Games sync failed");
                }

                // 3. Sync popular players (sample)
                try
                {
                    var popularNames = new[] { "lebron", "curry", "durant", "giannis", "luka" };
                    foreach (var name in popularNames)
                    {
                        await _playerService.SyncPlayersAsync(name);
                    }
                    syncResults.Add("✅ Popular players synced successfully");
                    Logger.LogInformation("✅ Players sync completed");
                }
                catch (Exception ex)
                {
                    syncResults.Add($"❌ Players sync failed: {ex.Message}");
                    Logger.LogError(ex, "❌ Players sync failed");
                }

                var duration = DateTime.UtcNow - startTime;

                var result = (object)new
                {
                    message = "Complete synchronization finished",
                    duration = $"{duration.TotalSeconds:F2} seconds",
                    timestamp = DateTime.UtcNow,
                    results = syncResults,
                    nextRecommendedSync = DateTime.UtcNow.AddHours(1),
                    summary = new
                    {
                        teamsCount = (await _teamService.GetAllTeamsAsync()).Count,
                        todayGamesCount = (await _gameService.GetTodayGamesAsync()).Count
                    }
                };

                Logger.LogInformation("🎉 Complete sync finished in {Duration} seconds", duration.TotalSeconds);
                return Ok(result, "All data synchronized successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "❌ Error during complete synchronization");
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
                Logger.LogInformation("⚡ Starting essential data sync");

                var startTime = DateTime.UtcNow;

                await _teamService.SyncAllTeamsAsync();
                await _gameService.SyncTodayGamesAsync();

                _cache.Remove(ApiConstants.CacheKeys.ALL_TEAMS);
                _cache.Remove(ApiConstants.CacheKeys.TODAY_GAMES);

                var duration = DateTime.UtcNow - startTime;

                var result = (object)new
                {
                    message = "Essential data synchronized",
                    duration = $"{duration.TotalSeconds:F2} seconds",
                    timestamp = DateTime.UtcNow,
                    synced = new[] { "Teams", "Today's Games" }
                };

                Logger.LogInformation("⚡ Essential sync completed in {Duration} seconds", duration.TotalSeconds);
                return Ok(result, "Essential data synchronized successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during essential sync");
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
                Logger.LogInformation("📊 Checking overall sync status");

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
                        status = localTeams.Count >= 30 ? "✅ Complete" : "⚠️ Incomplete"
                    },
                    games = new
                    {
                        local = localGames.Count,
                        external = externalGames.Count(),
                        needsSync = localGames.Count != externalGames.Count(),
                        status = localGames.Count > 0 ? "✅ Available" : "⚠️ No games"
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

                return Ok(status, "Overall sync status retrieved");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error checking overall sync status");
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
                Logger.LogInformation("🔍 Checking external API health");

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
                    status = isHealthy ? "✅ Healthy" : "❌ Unhealthy",
                    error = string.IsNullOrEmpty(errorMessage) ? null : errorMessage,
                    recommendations = GetApiHealthRecommendations(isHealthy, responseTime)
                };

                Logger.LogInformation("🔍 External API health: {Status}", isHealthy ? "Healthy" : "Unhealthy");
                return Ok(health, "External API health checked");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error checking external API health");

                var health = (object)new
                {
                    externalApi = "Ball Don't Lie API",
                    isHealthy = false,
                    error = ex.Message,
                    lastChecked = DateTime.UtcNow,
                    status = "❌ Connection Failed"
                };

                return Ok(health, "External API health check failed");
            }
        }

        /// <summary>
        /// Limpar todos os caches do sistema
        /// </summary>
        [HttpPost("cache/clear")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<object>>> ClearAllCaches()
        {
            try
            {
                Logger.LogInformation("🧹 Clearing all system caches");

                var clearedCaches = new List<string>();

                if (_cache.TryGetValue(ApiConstants.CacheKeys.ALL_TEAMS, out _))
                {
                    _cache.Remove(ApiConstants.CacheKeys.ALL_TEAMS);
                    clearedCaches.Add("Teams cache");
                }

                if (_cache.TryGetValue(ApiConstants.CacheKeys.TODAY_GAMES, out _))
                {
                    _cache.Remove(ApiConstants.CacheKeys.TODAY_GAMES);
                    clearedCaches.Add("Games cache");
                }

                for (int i = 1; i <= 100; i++)
                {
                    var playerKey = string.Format(ApiConstants.CacheKeys.PLAYER_BY_ID, i);
                    if (_cache.TryGetValue(playerKey, out _))
                    {
                        _cache.Remove(playerKey);
                        clearedCaches.Add($"Player {i} cache");
                    }
                }

                var result = (object)new
                {
                    message = "Caches cleared successfully",
                    clearedCaches,
                    totalCleared = clearedCaches.Count,
                    timestamp = DateTime.UtcNow
                };

                Logger.LogInformation("🧹 Cleared {Count} caches", clearedCaches.Count);
                return Ok(result, "All caches cleared successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error clearing caches");
                throw;
            }
        }

        // ============================================
        // HELPER METHODS
        // ============================================

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
            if (teamsCount == 0) return "🚨 URGENT: Run initial teams sync";
            if (teamsCount < 30) return "⚠️ Teams data incomplete - sync recommended";
            if (gamesCount == 0) return "📅 No games data - sync today's games";
            return "✅ Data looks healthy";
        }

        private static List<string> GetApiHealthRecommendations(bool isHealthy, TimeSpan responseTime)
        {
            var recommendations = new List<string>();

            if (!isHealthy)
            {
                recommendations.Add("Check internet connection");
                recommendations.Add("Verify API key configuration");
                recommendations.Add("Try again in a few minutes");
            }
            else
            {
                if (responseTime.TotalSeconds > 5)
                    recommendations.Add("API response is slow - consider caching");

                recommendations.Add("API is healthy - safe to perform sync");
            }

            return recommendations;
        }
    }
}