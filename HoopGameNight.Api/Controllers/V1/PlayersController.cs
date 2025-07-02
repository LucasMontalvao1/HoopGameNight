using HoopGameNight.Api.Constants;
using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Api.Controllers.V1
{
    [Route(ApiConstants.Routes.PLAYERS)]
    public class PlayersController : BaseApiController
    {
        private readonly IPlayerService _playerService;
        private readonly IBallDontLieService _ballDontLieService;
        private readonly IMemoryCache _cache;

        public PlayersController(
            IPlayerService playerService,
            IBallDontLieService ballDontLieService,
            IMemoryCache cache,
            ILogger<PlayersController> logger) : base(logger)
        {
            _playerService = playerService;
            _ballDontLieService = ballDontLieService;
            _cache = cache;
        }

        /// <summary>
        /// Buscar jogadores com filtros
        /// </summary>
        /// <param name="request">Parâmetros de busca</param>
        /// <returns>Lista paginada de jogadores</returns>
        [HttpGet(RouteConstants.Players.SEARCH)]
        [ProducesResponseType(typeof(PaginatedResponse<PlayerResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PaginatedResponse<PlayerResponse>>> SearchPlayers([FromQuery] SearchPlayerRequest request)
        {
            try
            {
                if (!request.IsValid())
                {
                    var errorResponse = ApiResponse<object>.ErrorResult("Invalid search parameters. Provide search term, team ID, or position.");
                    return BadRequest(errorResponse);
                }

                Logger.LogInformation("Searching players with criteria: {@Request}", request);

                var (players, totalCount) = await _playerService.SearchPlayersAsync(request);

                Logger.LogInformation("Found {PlayerCount} players (total: {TotalCount})", players.Count, totalCount);
                return OkPaginated(players, request.Page, request.PageSize, totalCount, "Players retrieved successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error searching players with criteria: {@Request}", request);
                throw;
            }
        }

        /// <summary>
        /// Buscar jogador por ID
        /// </summary>
        /// <param name="id">ID do jogador</param>
        /// <returns>Detalhes do jogador</returns>
        [HttpGet(RouteConstants.Players.GET_BY_ID)]
        [ProducesResponseType(typeof(ApiResponse<PlayerResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<PlayerResponse>>> GetPlayerById(int id)
        {
            try
            {
                Logger.LogInformation("Fetching player with ID: {PlayerId}", id);

                var cacheKey = string.Format(ApiConstants.CacheKeys.PLAYER_BY_ID, id);

                if (_cache.TryGetValue(cacheKey, out PlayerResponse? cachedPlayer))
                {
                    Logger.LogDebug("Returning cached player: {PlayerId}", id);
                    return Ok(cachedPlayer!, "Player retrieved successfully (cached)");
                }

                var player = await _playerService.GetPlayerByIdAsync(id);

                if (player == null)
                {
                    Logger.LogWarning("Player not found with ID: {PlayerId}", id);
                    var errorResponse = ApiResponse<PlayerResponse>.ErrorResult($"Player with ID {id} not found");
                    return NotFound(errorResponse);
                }

                _cache.Set(cacheKey, player, TimeSpan.FromHours(1));

                Logger.LogInformation("Player found: {PlayerName}", player.FullName);
                return Ok(player, "Player retrieved successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error fetching player with ID: {PlayerId}", id);
                throw;
            }
        }

        /// <summary>
        /// Buscar jogadores por time
        /// </summary>
        /// <param name="teamId">ID do time</param>
        /// <param name="page">Página</param>
        /// <param name="pageSize">Tamanho da página</param>
        /// <returns>Lista paginada de jogadores do time</returns>
        [HttpGet(RouteConstants.Players.GET_BY_TEAM)]
        [ProducesResponseType(typeof(PaginatedResponse<PlayerResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PaginatedResponse<PlayerResponse>>> GetPlayersByTeam(
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

                Logger.LogInformation("Fetching players for team: {TeamId}", teamId);

                var (players, totalCount) = await _playerService.GetPlayersByTeamAsync(teamId, page, pageSize);

                Logger.LogInformation("Found {PlayerCount} players for team {TeamId}", players.Count, teamId);
                return OkPaginated(players, page, pageSize, totalCount, $"Players for team {teamId} retrieved successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error fetching players for team: {TeamId}", teamId);
                throw;
            }
        }

        // ============================================
        // NOVOS ENDPOINTS DE SYNC
        // ============================================

        /// <summary>
        /// Sincronizar jogadores por termo de busca
        /// </summary>
        /// <param name="search">Termo de busca (opcional)</param>
        /// <returns>Resultado da sincronização</returns>
        [HttpPost("sync")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<object>>> SyncPlayers([FromQuery] string? search = null)
        {
            try
            {
                var searchTerm = search ?? "a";
                Logger.LogInformation("Starting manual sync of players with search: {Search}", searchTerm);

                await _playerService.SyncPlayersAsync(searchTerm);

                var result = (object)new
                {
                    message = "Players synced successfully",
                    searchTerm = searchTerm,
                    timestamp = DateTime.UtcNow
                };

                Logger.LogInformation("Manual sync of players completed");
                return Ok(result, "Players synced successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during manual sync of players");
                throw;
            }
        }

        /// <summary>
        /// Buscar jogadores diretamente da API externa
        /// </summary>
        /// <param name="search">Termo de busca</param>
        /// <returns>Jogadores direto da Ball Don't Lie API</returns>
        [HttpGet("external/search")]
        [ProducesResponseType(typeof(ApiResponse<List<object>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<List<object>>>> SearchPlayersFromExternal([FromQuery] string search)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(search) || search.Length < 2)
                {
                    return BadRequest<List<object>>("Search term must be at least 2 characters");
                }

                Logger.LogInformation("Searching players directly from external API: {Search}", search);

                var externalPlayers = await _ballDontLieService.SearchPlayersAsync(search);
                var playersList = externalPlayers.Select(p => new
                {
                    id = p.Id,
                    firstName = p.FirstName,
                    lastName = p.LastName,
                    position = p.Position,
                    height = p.HeightFeet.HasValue && p.HeightInches.HasValue ?
                        $"{p.HeightFeet}'{p.HeightInches}\"" : "N/A",
                    weight = p.WeightPounds.HasValue ? $"{p.WeightPounds} lbs" : "N/A",
                    team = p.Team?.FullName ?? "Free Agent"
                }).ToList<object>();

                Logger.LogInformation("Found {PlayerCount} players from external API", playersList.Count);
                return Ok(playersList, $"Players matching '{search}' from external API");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error searching players from external API");
                throw;
            }
        }

        /// <summary>
        /// Verificar status da sincronização de jogadores
        /// </summary>
        /// <param name="search">Termo para verificar</param>
        /// <returns>Status da sincronização</returns>
        [HttpGet("sync/status")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<object>>> GetSyncStatus([FromQuery] string? search = "lebron")
        {
            try
            {
                var searchRequest = new SearchPlayerRequest
                {
                    Search = search,
                    Page = 1,
                    PageSize = 10
                };

                var (localPlayers, localTotal) = await _playerService.SearchPlayersAsync(searchRequest);
                var externalPlayers = await _ballDontLieService.SearchPlayersAsync(search ?? "lebron");

                var status = (object)new
                {
                    searchTerm = search ?? "lebron",
                    localPlayers = localTotal,
                    externalPlayers = externalPlayers.Count(),
                    sampleLocalPlayers = localPlayers.Take(3).Select(p => p.FullName),
                    lastCheck = DateTime.UtcNow,
                    recommendation = localTotal == 0 ?
                        "Initial player sync recommended" :
                        "Some players data available"
                };

                return Ok(status, "Player sync status retrieved");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error checking player sync status");
                throw;
            }
        }
    }
}