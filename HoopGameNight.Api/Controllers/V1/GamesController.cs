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
                Logger.LogInformation("Buscando jogos de hoje");

                var cacheKey = ApiConstants.CacheKeys.TODAY_GAMES;

                if (_cache.TryGetValue(cacheKey, out List<GameResponse>? cachedGames))
                {
                    Logger.LogDebug("Retornando jogos do cache");
                    return Ok(cachedGames!, "Jogos de hoje (cache)");
                }

                var games = await _gameService.GetTodayGamesAsync();

                _cache.Set(cacheKey, games, TimeSpan.FromMinutes(5));

                Logger.LogInformation("Encontrados {GameCount} jogos para hoje", games.Count);
                return Ok(games, "Jogos de hoje recuperados com sucesso");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao buscar jogos de hoje");
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
                Logger.LogInformation("Buscando jogos para a data: {Date}", date.ToShortDateString());

                var games = await _gameService.GetGamesByDateAsync(date);

                Logger.LogInformation("Encontrados {GameCount} jogos para a data {Date}", games.Count, date.ToShortDateString());
                return Ok(games, $"Jogos do dia {date:yyyy-MM-dd} recuperados com sucesso");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao buscar jogos por data: {Date}", date);
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
                    var errorResponse = ApiResponse<object>.ErrorResult("Request incorreto");
                    return BadRequest(errorResponse);
                }

                Logger.LogInformation("Buscando jogos com filtros: {@Request}", request);

                var (games, totalCount) = await _gameService.GetGamesAsync(request);

                Logger.LogInformation("Encontrados {GameCount} jogos (total: {TotalCount})", games.Count, totalCount);
                return OkPaginated(games, request.Page, request.PageSize, totalCount, "Jogos recuperados com sucesso");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao buscar jogos com filtros: {@Request}", request);
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
                Logger.LogInformation("Buscando jogo com ID: {GameId}", id);

                var game = await _gameService.GetGameByIdAsync(id);

                if (game == null)
                {
                    Logger.LogWarning("Jogo não encontrado com ID: {GameId}", id);
                    var errorResponse = ApiResponse<GameResponse>.ErrorResult($"Jogo com ID {id} não encontrado");
                    return NotFound(errorResponse);
                }

                Logger.LogInformation("Jogo encontrado: {GameTitle}", game.GameTitle);
                return Ok(game, "Jogo recuperado com sucesso");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao buscar jogo com ID: {GameId}", id);
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
                    var errorResponse = ApiResponse<object>.ErrorResult("Parâmetros de paginação inválidos");
                    return BadRequest(errorResponse);
                }

                Logger.LogInformation("Buscando jogos para o time: {TeamId}", teamId);

                var (games, totalCount) = await _gameService.GetGamesByTeamAsync(teamId, page, pageSize);

                Logger.LogInformation("Encontrados {GameCount} jogos para o time {TeamId}", games.Count, teamId);
                return OkPaginated(games, page, pageSize, totalCount, $"Jogos do time {teamId} recuperados com sucesso");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao buscar jogos por time: {TeamId}", teamId);
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
                Logger.LogInformation("Iniciando sincronização manual dos jogos de hoje");

                await _gameService.SyncTodayGamesAsync();
                _cache.Remove(ApiConstants.CacheKeys.TODAY_GAMES);
                var games = await _gameService.GetTodayGamesAsync();

                var result = (object)new
                {
                    message = "Jogos de hoje sincronizados com sucesso",
                    gameCount = games.Count,
                    timestamp = DateTime.UtcNow
                };

                Logger.LogInformation("Sincronização manual concluída - {GameCount} jogos", games.Count);
                return Ok(result, "Jogos de hoje sincronizados com sucesso");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro na sincronização manual dos jogos de hoje");
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
                    return BadRequest<object>("Não é possível sincronizar jogos com mais de 30 dias no futuro");
                }

                Logger.LogInformation("Sincronizando jogos para a data: {Date}", date.ToShortDateString());

                var syncCount = await _gameService.SyncGamesByDateAsync(date);

                _cache.Remove(ApiConstants.CacheKeys.TODAY_GAMES);
                _cache.Remove(string.Format(ApiConstants.CacheKeys.GAMES_BY_DATE, date.ToString("yyyy-MM-dd")));

                var savedGames = await _gameService.GetGamesByDateAsync(date);

                var result = (object)new
                {
                    message = $"Jogos sincronizados com sucesso para {date:yyyy-MM-dd}",
                    syncedCount = syncCount,
                    totalGamesInDb = savedGames.Count,
                    date = date.ToString("yyyy-MM-dd"),
                    timestamp = DateTime.UtcNow
                };

                Logger.LogInformation("Sincronização concluída para {Date} - {GameCount} jogos", date.ToShortDateString(), syncCount);
                return Ok(result, "Jogos sincronizados com sucesso");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao sincronizar jogos para a data: {Date}", date);
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
                Logger.LogInformation("Buscando jogos de hoje diretamente da API externa");

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

                Logger.LogInformation("Recuperados {GameCount} jogos da API externa", gamesList.Count);
                return Ok(gamesList, "Jogos de hoje da API externa");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao buscar jogos da API externa");
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
                       "Sincronização recomendada - divergência detectada" :
                        "Dados sincronizado"
                };

                return Ok(status, "Status da sincronização recuperado");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao verificar status de sincronização");
                throw;
            }
        }
    }
}