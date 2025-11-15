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
        private readonly IEspnApiService _espnService;
        private readonly IMemoryCache _cache;

        public GamesController(
            IGameService gameService,
            IEspnApiService espnService,
            IMemoryCache cache,
            ILogger<GamesController> logger) : base(logger)
        {
            _gameService = gameService;
            _espnService = espnService;
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

                var games = await _gameService.GetTodayGamesAsync();

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

                if (date > DateTime.Today)
                {
                    Response.Headers.Add("X-Data-Source", "ESPN");
                }
                else
                {
                    Response.Headers.Add("X-Data-Source", "Database");
                }

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
        /// Buscar jogos de múltiplos times em um período (COM SUPORTE A JOGOS FUTUROS)
        /// </summary>
        /// <param name="request">Requisição com times e período</param>
        /// <returns>Jogos agrupados por time com estatísticas</returns>
        [HttpPost("teams/games")]
        [ProducesResponseType(typeof(ApiResponse<MultipleTeamsGamesResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<MultipleTeamsGamesResponse>>> GetGamesForMultipleTeams([FromBody] GetMultipleTeamsGamesRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<object>.ErrorResult("É obrigatório informar o corpo da requisição"));
                }

                if (!request.IsValid())
                {
                    var errors = request.GetValidationErrors();
                    var errorResponse = ApiResponse<object>.ErrorResult(
                        "Parâmetros da requisição inválidos",
                        errors
                    );
                    return BadRequest(errorResponse);
                }

                Logger.LogInformation(
                    "Buscando jogos para os times: {TeamIds} de {StartDate} até {EndDate}",
                    string.Join(",", request.TeamIds),
                    request.StartDate.ToShortDateString(),
                    request.EndDate.ToShortDateString()
                );

                var result = await _gameService.GetGamesForMultipleTeamsAsync(request);

                if (result == null)
                {
                    return NotFound(ApiResponse<MultipleTeamsGamesResponse>.ErrorResult(
                        "Nenhum jogo encontrado para os critérios especificados"
                    ));
                }

                Response.Headers.Add("X-Data-Source", result.ApiLimitations?.DataSource ?? "Database");
                Response.Headers.Add("X-Total-Games", result.AllGames.Count.ToString());

                var futureGames = result.AllGames.Count(g => g.Date > DateTime.Today);
                var pastGames = result.AllGames.Count(g => g.Date <= DateTime.Today);

                Response.Headers.Add("X-Future-Games", futureGames.ToString());
                Response.Headers.Add("X-Past-Games", pastGames.ToString());

                Logger.LogInformation(
                    "Recuperados com sucesso {GameCount} jogos para {TeamCount} times (Futuros: {FutureCount}, Passados: {PastCount})",
                    result.AllGames.Count,
                    request.TeamIds.Count,
                    futureGames,
                    pastGames
                );

                return Ok(result, "Jogos recuperados com sucesso");
            }
            catch (ArgumentException ex)
            {
                Logger.LogWarning(ex, "Argumentos inválidos para GetGamesForMultipleTeams");
                return BadRequest(ApiResponse<object>.ErrorResult(ex.Message));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao buscar jogos para múltiplos times");
                throw;
            }
        }

        /// <summary>
        /// Buscar próximos jogos de um time específico
        /// </summary>
        /// <param name="teamId">ID do time</param>
        /// <param name="days">Número de dias futuros (1-30)</param>
        /// <returns>Lista de próximos jogos</returns>
        [HttpGet("teams/{teamId}/upcoming")]
        [ProducesResponseType(typeof(ApiResponse<List<GameResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<List<GameResponse>>>> GetUpcomingGamesForTeam(
            int teamId,
            [FromQuery] int days = 7)
        {
            try
            {
                if (teamId <= 0)
                {
                    return BadRequest(ApiResponse<object>.ErrorResult("ID do time inválido"));
                }

                if (days < 1 || days > 30)
                {
                    return BadRequest(ApiResponse<object>.ErrorResult(
                        "O parâmetro de dias deve estar entre 1 e 30"
                    ));
                }

                Logger.LogInformation(
                    "Buscando próximos jogos para o time {TeamId} nos próximos {Days} dias",
                    teamId,
                    days
                );

                var upcomingGames = await _gameService.GetUpcomingGamesForTeamAsync(teamId, days);

                Response.Headers.Add("X-Data-Source", upcomingGames.Any(g => g.IsFutureGame) ? "ESPN" : "Database");

                if (upcomingGames.Count == 0)
                {
                    return Ok(
                        new List<GameResponse>(),
                        $"Nenhum próximo jogo encontrado nos próximos {days} dias"
                    );
                }

                return Ok(upcomingGames, $"Encontrados {upcomingGames.Count} próximos jogos");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao buscar próximos jogos para o time {TeamId}", teamId);
                throw;
            }
        }

        /// <summary>
        /// Buscar jogos recentes de um time específico
        /// </summary>
        /// <param name="teamId">ID do time</param>
        /// <param name="days">Número de dias passados (1-30)</param>
        /// <returns>Lista de jogos recentes</returns>
        [HttpGet("teams/{teamId}/recent")]
        [ProducesResponseType(typeof(ApiResponse<List<GameResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<List<GameResponse>>>> GetRecentGamesForTeam(
            int teamId,
            [FromQuery] int days = 7)
        {
            try
            {
                if (teamId <= 0)
                {
                    return BadRequest(ApiResponse<object>.ErrorResult("ID do time inválido"));
                }

                if (days < 1 || days > 30)
                {
                    return BadRequest(ApiResponse<object>.ErrorResult(
                        "O parâmetro de dias deve estar entre 1 e 30"
                    ));
                }

                Logger.LogInformation(
                    "Buscando jogos recentes para o time {TeamId} nos últimos {Days} dias",
                    teamId,
                    days
                );

                var recentGames = await _gameService.GetRecentGamesForTeamAsync(teamId, days);

                var summary = new
                {
                    TotalGames = recentGames.Count,
                    Wins = recentGames.Count(g => g.WinningTeam?.Id == teamId),
                    Losses = recentGames.Count(g => g.IsCompleted && g.WinningTeam?.Id != teamId),
                    HomeGames = recentGames.Count(g => g.HomeTeam.Id == teamId),
                    AwayGames = recentGames.Count(g => g.VisitorTeam.Id == teamId)
                };

                Response.Headers.Add("X-Games-Summary", System.Text.Json.JsonSerializer.Serialize(summary));

                return Ok(recentGames, $"Encontrados {recentGames.Count} jogos recentes");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao buscar jogos recentes para o time {TeamId}", teamId);
                throw;
            }
        }

        /// <summary>
        /// Buscar jogos dos times favoritos (COM SUPORTE A JOGOS FUTUROS)
        /// </summary>
        /// <param name="teamIds">Lista de IDs dos times favoritos</param>
        /// <param name="startDate">Data inicial (opcional)</param>
        /// <param name="endDate">Data final (opcional)</param>
        /// <returns>Jogos dos times favoritos</returns>
        [HttpPost("favorites")]
        [ProducesResponseType(typeof(ApiResponse<MultipleTeamsGamesResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<MultipleTeamsGamesResponse>>> GetFavoriteTeamsGames(
            [FromBody] List<int> teamIds,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                if (teamIds == null || teamIds.Count == 0)
                {
                    return BadRequest(ApiResponse<object>.ErrorResult("Pelo menos um ID de time é obrigatório"));
                }

                if (teamIds.Count > 5)
                {
                    return BadRequest(ApiResponse<object>.ErrorResult("Máximo de 5 times favoritos permitidos"));
                }

                var start = startDate ?? DateTime.Today.AddDays(-7);
                var end = endDate ?? DateTime.Today.AddDays(7);

                Logger.LogInformation(
                    "Buscando jogos para {Count} times favoritos de {StartDate} até {EndDate}",
                    teamIds.Count,
                    start.ToShortDateString(),
                    end.ToShortDateString()
                );

                var request = new GetMultipleTeamsGamesRequest
                {
                    TeamIds = teamIds,
                    StartDate = start,
                    EndDate = end,
                    GroupByTeam = true,
                    IncludeStats = true
                };

                var result = await _gameService.GetGamesForMultipleTeamsAsync(request);

                Response.Headers.Add("X-Data-Source", result.ApiLimitations?.DataSource ?? "Database");

                var favoriteSummary = new
                {
                    TotalGames = result.AllGames.Count,
                    TodayGames = result.AllGames.Count(g => g.Date.Date == DateTime.Today),
                    LiveGames = result.AllGames.Count(g => g.IsLive),
                    NextGames = result.AllGames
                        .Where(g => !g.IsCompleted && g.Date >= DateTime.Now)
                        .OrderBy(g => g.Date)
                        .Take(5)
                        .ToList()
                };

                Response.Headers.Add("X-Favorites-Summary", System.Text.Json.JsonSerializer.Serialize(favoriteSummary));

                Logger.LogInformation(
                    "Encontrados {GameCount} jogos para {TeamCount} times favoritos",
                    result.AllGames.Count,
                    teamIds.Count
                );

                return Ok(result, "Jogos dos times favoritos recuperados com sucesso");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao buscar jogos dos times favoritos");
                throw;
            }
        }

        /// <summary>
        /// Buscar calendário do mês (COM JOGOS FUTUROS)
        /// </summary>
        /// <param name="year">Ano</param>
        /// <param name="month">Mês</param>
        /// <returns>Calendário de jogos do mês</returns>
        [HttpGet("calendar/{year}/{month}")]
        [ProducesResponseType(typeof(ApiResponse<Dictionary<DateTime, List<GameResponse>>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<Dictionary<DateTime, List<GameResponse>>>>> GetMonthCalendar(
            int year,
            int month)
        {
            try
            {
                if (year < 2000 || year > 2030)
                {
                    return BadRequest(ApiResponse<object>.ErrorResult("Ano inválido"));
                }

                if (month < 1 || month > 12)
                {
                    return BadRequest(ApiResponse<object>.ErrorResult("Mês inválido"));
                }

                Logger.LogInformation("Buscando calendário para {Year}/{Month}", year, month);

                var startDate = new DateTime(year, month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                var request = new GetMultipleTeamsGamesRequest
                {
                    TeamIds = Enumerable.Range(1, 30).ToList(), 
                    StartDate = startDate,
                    EndDate = endDate,
                    GroupByTeam = false,
                    IncludeStats = false
                };

                var result = await _gameService.GetGamesForMultipleTeamsAsync(request);

                var calendar = result.AllGames
                    .GroupBy(g => g.Date.Date)
                    .ToDictionary(g => g.Key, g => g.ToList());

                Response.Headers.Add("X-Data-Source", result.ApiLimitations?.DataSource ?? "Database");
                Response.Headers.Add("X-Total-Days", calendar.Count.ToString());
                Response.Headers.Add("X-Total-Games", result.AllGames.Count.ToString());

                return Ok(calendar, $"Calendário para {month:00}/{year} recuperado com sucesso");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao buscar calendário para {Year}/{Month}", year, month);
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

                var result = new
                {
                    message = "Jogos de hoje sincronizados com sucesso",
                    gameCount = games.Count,
                    timestamp = DateTime.UtcNow
                };

                Logger.LogInformation("Sincronização manual concluída - {GameCount} jogos", games.Count);
                return Ok((object)result, "Jogos de hoje sincronizados com sucesso");
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
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<object>>> SyncGamesByDate(DateTime date)
        {
            try
            {
                if (date > DateTime.Today)
                {
                    return BadRequest(ApiResponse<object>.ErrorResult("Não é possível sincronizar jogos futuros"));
                }

                if (date < DateTime.Today.AddYears(-2))
                {
                    return BadRequest(ApiResponse<object>.ErrorResult("Data muito antiga para sincronização"));
                }

                Logger.LogInformation("Sincronizando jogos para a data: {Date}", date.ToShortDateString());

                var syncCount = await _gameService.SyncGamesByDateAsync(date);

                _cache.Remove(ApiConstants.CacheKeys.TODAY_GAMES);
                _cache.Remove(string.Format(ApiConstants.CacheKeys.GAMES_BY_DATE, date.ToString("yyyy-MM-dd")));

                var savedGames = await _gameService.GetGamesByDateAsync(date);

                var result = new
                {
                    message = $"Jogos sincronizados com sucesso para {date:yyyy-MM-dd}",
                    syncedCount = syncCount,
                    totalGamesInDb = savedGames.Count,
                    date = date.ToString("yyyy-MM-dd"),
                    timestamp = DateTime.UtcNow
                };

                Logger.LogInformation("Sincronização concluída para {Date} - {GameCount} jogos", date.ToShortDateString(), syncCount);
                return Ok((object)result, "Jogos sincronizados com sucesso");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao sincronizar jogos para a data: {Date}", date);
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

                var externalGamesCount = (await _espnService.GetGamesByDateAsync(DateTime.Today)).Count;

                var status = new
                {
                    localGames = localGamesCount,
                    externalGames = externalGamesCount,
                    needsSync = localGamesCount != externalGamesCount,
                    lastCheck = DateTime.UtcNow,
                    dataSource = "ESPN API",
                    recommendation = localGamesCount != externalGamesCount ?
                       "Sincronização recomendada - divergência detectada" :
                        "Dados sincronizados"
                };

                return Ok((object)status, "Status da sincronização recuperado");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao verificar status de sincronização");
                throw;
            }
        }

        /// <summary>
        /// Verificar calendário da temporada atual
        /// </summary>
        [HttpGet("season/current")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<object>>> GetCurrentSeasonInfo()
        {
            try
            {
                var currentYear = DateTime.Today.Year;
                var currentMonth = DateTime.Today.Month;
                string seasonStatus;
                string seasonYear;
                DateTime seasonStart;
                DateTime seasonEnd;

                if (currentMonth >= 10)
                {
                    seasonYear = $"{currentYear}-{currentYear + 1}";
                    seasonStart = new DateTime(currentYear, 10, 1);
                    seasonEnd = new DateTime(currentYear + 1, 6, 30);
                    seasonStatus = "Temporada Regular";
                }
                else if (currentMonth <= 6)
                {
                    seasonYear = $"{currentYear - 1}-{currentYear}";
                    seasonStart = new DateTime(currentYear - 1, 10, 1);
                    seasonEnd = new DateTime(currentYear, 6, 30);
                    seasonStatus = currentMonth >= 4 ? "Playoffs" : "Temporada Regular";
                }
                else
                {
                    seasonYear = $"{currentYear}-{currentYear + 1}";
                    seasonStart = new DateTime(currentYear, 10, 1);
                    seasonEnd = new DateTime(currentYear + 1, 6, 30);
                    seasonStatus = "Período de Descanso - Nenhum jogo agendado";
                }

                var info = new
                {
                    currentDate = DateTime.Today.ToString("yyyy-MM-dd"),
                    season = seasonYear,
                    status = seasonStatus,
                    seasonStart = seasonStart.ToString("yyyy-MM-dd"),
                    seasonEnd = seasonEnd.ToString("yyyy-MM-dd"),
                    isOffseason = currentMonth >= 7 && currentMonth <= 9,
                    note = currentMonth >= 7 && currentMonth <= 9
                        ? "NBA está no período de descanso. A próxima temporada começa em outubro."
                        : "Temporada está ativa. Jogos devem estar disponíveis."
                };

                return Ok((object)info, "Informações da temporada recuperadas com sucesso");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao obter informações da temporada");
                throw;
            }
        }
    }

    public class ExternalGameDto
    {
        public int Id { get; set; }
        public string Date { get; set; } = string.Empty;
        public string HomeTeam { get; set; } = string.Empty;
        public string VisitorTeam { get; set; } = string.Empty;
        public string? Status { get; set; }
        public int? HomeScore { get; set; }
        public int? VisitorScore { get; set; }
    }
}