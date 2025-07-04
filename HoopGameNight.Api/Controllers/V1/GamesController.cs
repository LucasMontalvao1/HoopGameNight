using HoopGameNight.Api.Constants;
using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Api.Controllers.V1
{
    [Route(ApiConstants.Routes.GAMES)]
    public class GamesController : BaseApiController
    {
        private readonly IGameService _gameService;
        private readonly IBallDontLieService _ballDontLieService;
        private readonly IMemoryCache _cache;

        public GamesController(
            IGameService gameService,
            IBallDontLieService ballDontLieService,
            IMemoryCache cache,
            ILogger<GamesController> logger) : base(logger)
        {
            _gameService = gameService;
            _ballDontLieService = ballDontLieService;
            _cache = cache;
        }

        /// <summary>
        /// Buscar jogos de hoje
        /// </summary>
        /// <returns>Lista de jogos do dia atual</returns>
        [HttpGet(RouteConstants.Games.GET_TODAY)]
        [ProducesResponseType(typeof(ApiResponse<List<GameResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<GameResponse>>>> GetTodayGames()
        {
            try
            {
                Logger.LogInformation("Fetching today's games");

                var cacheKey = ApiConstants.CacheKeys.TODAY_GAMES;

                if (_cache.TryGetValue(cacheKey, out List<GameResponse>? cachedGames))
                {
                    Logger.LogDebug("Returning cached today's games");
                    return Ok(cachedGames!, "Today's games (cached)");
                }

                var games = await _gameService.GetTodayGamesAsync();

                _cache.Set(cacheKey, games, TimeSpan.FromMinutes(5));

                Logger.LogInformation("Found {GameCount} games for today", games.Count);
                return Ok(games, "Today's games retrieved successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error fetching today's games");
                throw;
            }
        }

        /// <summary>
        /// Buscar jogos por data específica
        /// </summary>
        /// <param name="date">Data dos jogos (yyyy-MM-dd)</param>
        /// <returns>Lista de jogos da data especificada</returns>
        [HttpGet(RouteConstants.Games.GET_BY_DATE)]
        [ProducesResponseType(typeof(ApiResponse<List<GameResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<List<GameResponse>>>> GetGamesByDate(DateTime date)
        {
            try
            {
                Logger.LogInformation("Fetching games for date: {Date}", date.ToShortDateString());

                var games = await _gameService.GetGamesByDateAsync(date);

                Logger.LogInformation("Found {GameCount} games for {Date}", games.Count, date.ToShortDateString());
                return Ok(games, $"Games for {date:yyyy-MM-dd} retrieved successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error fetching games for date: {Date}", date);
                throw;
            }
        }

        /// <summary>
        /// Buscar jogos com filtros
        /// </summary>
        /// <param name="request">Parâmetros de busca</param>
        /// <returns>Lista paginada de jogos</returns>
        [HttpGet]
        [ProducesResponseType(typeof(PaginatedResponse<GameResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PaginatedResponse<GameResponse>>> GetGames([FromQuery] GetGamesRequest request)
        {
            try
            {
                if (!request.IsValid())
                {
                    var errorResponse = ApiResponse<object>.ErrorResult("Invalid request parameters");
                    return BadRequest(errorResponse);
                }

                Logger.LogInformation("Fetching games with filters: {@Request}", request);

                var (games, totalCount) = await _gameService.GetGamesAsync(request);

                Logger.LogInformation("Found {GameCount} games (total: {TotalCount})", games.Count, totalCount);
                return OkPaginated(games, request.Page, request.PageSize, totalCount, "Games retrieved successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error fetching games with filters: {@Request}", request);
                throw;
            }
        }

        /// <summary>
        /// Buscar jogo por ID
        /// </summary>
        /// <param name="id">ID do jogo</param>
        /// <returns>Detalhes do jogo</returns>
        [HttpGet(RouteConstants.Games.GET_BY_ID)]
        [ProducesResponseType(typeof(ApiResponse<GameResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<GameResponse>>> GetGameById(int id)
        {
            try
            {
                Logger.LogInformation("Fetching game with ID: {GameId}", id);

                var game = await _gameService.GetGameByIdAsync(id);

                if (game == null)
                {
                    Logger.LogWarning("Game not found with ID: {GameId}", id);
                    var errorResponse = ApiResponse<GameResponse>.ErrorResult($"Game with ID {id} not found");
                    return NotFound(errorResponse);
                }

                Logger.LogInformation("Game found: {GameTitle}", game.GameTitle);
                return Ok(game, "Game retrieved successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error fetching game with ID: {GameId}", id);
                throw;
            }
        }

        /// <summary>
        /// Buscar jogos por time
        /// </summary>
        /// <param name="teamId">ID do time</param>
        /// <param name="page">Página</param>
        /// <param name="pageSize">Tamanho da página</param>
        /// <returns>Lista paginada de jogos do time</returns>
        [HttpGet(RouteConstants.Games.GET_BY_TEAM)]
        [ProducesResponseType(typeof(PaginatedResponse<GameResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PaginatedResponse<GameResponse>>> GetGamesByTeam(
            int teamId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25)
        {
            try
            {
                if (page < 1 || pageSize < 1 || pageSize > 100)
                {
                    var errorResponse = ApiResponse<object>.ErrorResult("Invalid pagination parameters");
                    return BadRequest(errorResponse);
                }

                Logger.LogInformation("Fetching games for team: {TeamId}", teamId);

                var (games, totalCount) = await _gameService.GetGamesByTeamAsync(teamId, page, pageSize);

                Logger.LogInformation("Found {GameCount} games for team {TeamId}", games.Count, teamId);
                return OkPaginated(games, page, pageSize, totalCount, $"Games for team {teamId} retrieved successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error fetching games for team: {TeamId}", teamId);
                throw;
            }
        }

        /// <summary>
        /// Sincronizar jogos de hoje da API externa
        /// </summary>
        /// <returns>Resultado da sincronização</returns>
        [HttpPost("sync/today")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<object>>> SyncTodayGames()
        {
            try
            {
                Logger.LogInformation("Starting manual sync of today's games");

                await _gameService.SyncTodayGamesAsync();
                _cache.Remove(ApiConstants.CacheKeys.TODAY_GAMES);
                var games = await _gameService.GetTodayGamesAsync();

                var result = (object)new
                {
                    message = "Today's games synced successfully",
                    gameCount = games.Count,
                    timestamp = DateTime.UtcNow
                };

                Logger.LogInformation("Manual sync completed - {GameCount} games synced", games.Count);
                return Ok(result, "Today's games synced and retrieved successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during manual sync of today's games");
                throw;
            }
        }

        /// <summary>
        /// Sincronizar jogos por data específica
        /// </summary>
        /// <param name="date">Data para sincronizar (yyyy-MM-dd)</param>
        /// <returns>Resultado da sincronização</returns>
        [HttpPost("sync/date/{date}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<object>>> SyncGamesByDate(DateTime date)
        {
            try
            {
                if (date > DateTime.Today.AddDays(30))
                {
                    return BadRequest<object>("Cannot sync games more than 30 days in the future");
                }

                Logger.LogInformation("Starting sync of games for date: {Date}", date.ToShortDateString());

                // ✅ CORRIGIDO: Usar o método de sincronização do GameService
                var syncCount = await _gameService.SyncGamesByDateAsync(date);

                // Limpar cache após sincronização
                _cache.Remove(ApiConstants.CacheKeys.TODAY_GAMES);
                _cache.Remove(string.Format(ApiConstants.CacheKeys.GAMES_BY_DATE, date.ToString("yyyy-MM-dd")));

                // Buscar jogos salvos para confirmar
                var savedGames = await _gameService.GetGamesByDateAsync(date);

                var result = (object)new
                {
                    message = $"Games synced successfully for {date:yyyy-MM-dd}",
                    syncedCount = syncCount,
                    totalGamesInDb = savedGames.Count,
                    date = date.ToString("yyyy-MM-dd"),
                    timestamp = DateTime.UtcNow
                };

                Logger.LogInformation("Sync completed for {Date} - {GameCount} games synced", date.ToShortDateString(), syncCount);
                return Ok(result, "Games synced successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error syncing games for date: {Date}", date);
                throw;
            }
        }

        /// <summary>
        /// Buscar jogos diretamente da API externa (sem salvar)
        /// </summary>
        /// <returns>Jogos direto da Ball Don't Lie API</returns>
        [HttpGet("external/today")]
        [ProducesResponseType(typeof(ApiResponse<List<object>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<object>>>> GetTodayGamesFromExternal()
        {
            try
            {
                Logger.LogInformation("Fetching today's games directly from external API");

                var externalGames = await _ballDontLieService.GetTodaysGamesAsync();
                var gamesList = externalGames.Select(g => new
                {
                    id = g.Id,
                    date = g.Date,
                    homeTeam = g.HomeTeam.FullName,
                    visitorTeam = g.VisitorTeam.FullName,
                    status = g.Status,
                    homeScore = g.HomeTeamScore,
                    visitorScore = g.VisitorTeamScore
                }).ToList<object>();

                Logger.LogInformation("Retrieved {GameCount} games from external API", gamesList.Count);
                return Ok(gamesList, "Today's games from external API");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error fetching games from external API");
                throw;
            }
        }

        /// <summary>
        /// Verificar status da sincronização de jogos
        /// </summary>
        /// <returns>Status da última sincronização</returns>
        [HttpGet("sync/status")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<object>>> GetSyncStatus()
        {
            try
            {
                var localGamesCount = (await _gameService.GetTodayGamesAsync()).Count;
                var externalGamesCount = (await _ballDontLieService.GetTodaysGamesAsync()).Count();

                var status = (object)new
                {
                    localGames = localGamesCount,
                    externalGames = externalGamesCount,
                    needsSync = localGamesCount != externalGamesCount,
                    lastCheck = DateTime.UtcNow,
                    recommendation = localGamesCount != externalGamesCount ?
                        "Sync recommended - data mismatch detected" :
                        "Data is synchronized"
                };

                return Ok(status, "Sync status retrieved");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error checking sync status");
                throw;
            }
        }
    }
}