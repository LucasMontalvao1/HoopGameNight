﻿using HoopGameNight.Api.Constants;
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
                    var errorResponse = ApiResponse<object>.ErrorResult("Parâmetros de pesquisa inválidos. Informe o termo de pesquisa, ID da equipe ou posição.");
                    return BadRequest(errorResponse);
                }

                Logger.LogInformation("Procurando jogadores com critérios: {@Request}", request);

                var (players, totalCount) = await _playerService.SearchPlayersAsync(request);

                Logger.LogInformation("Foram encontrados {PlayerCount} jogadores (total: {TotalCount})", players.Count, totalCount);
                return OkPaginated(players, request.Page, request.PageSize, totalCount, "Jogadores recuperados com sucesso");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao pesquisar jogadores com o critério: {@Request}", request);
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
                Logger.LogInformation("Buscando jogador com ID: {PlayerId}", id);

                var cacheKey = string.Format(ApiConstants.CacheKeys.PLAYER_BY_ID, id);

                if (_cache.TryGetValue(cacheKey, out PlayerResponse? cachedPlayer))
                {
                    Logger.LogDebug("Retornando jogador em cache: {PlayerId}", id);
                    return Ok(cachedPlayer!, "Jogador recuperado com sucesso (armazenado em cache)");
                }

                var player = await _playerService.GetPlayerByIdAsync(id);

                if (player == null)
                {
                    Logger.LogWarning("Jogador não encontrado com ID: {PlayerId}", id);
                    var errorResponse = ApiResponse<PlayerResponse>.ErrorResult($"Jogador com ID {id} não encontrado");
                    return NotFound(errorResponse);
                }

                _cache.Set(cacheKey, player, TimeSpan.FromHours(1));

                Logger.LogInformation("Jogador encontrado: {PlayerName}", player.FullName);
                return Ok(player, "Jogador recuperado com sucesso");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao buscar jogador com ID: {PlayerId}", id);
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
                    var errorResponse = ApiResponse<object>.ErrorResult("Parâmetros de paginação inválidos");
                    return BadRequest(errorResponse);
                }

                Logger.LogInformation("Buscando jogadores para o time: {TeamId}", teamId);

                var (players, totalCount) = await _playerService.GetPlayersByTeamAsync(teamId, page, pageSize);

                Logger.LogInformation("Foram encontrados {PlayerCount} jogadores para a equipe {TeamId}", players.Count, teamId);
                return OkPaginated(players, page, pageSize, totalCount, $"Jogadores da equipe {teamId} recuperados com sucesso");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao buscar jogadores para o time: {TeamId}", teamId);
                throw;
            }
        }

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
                Logger.LogInformation("Iniciando a sincronização manual de jogadores com a pesquisa: {Search}", searchTerm);

                await _playerService.SyncPlayersAsync(searchTerm);

                var result = (object)new
                {
                    message = "Jogadores sincronizados com sucesso",
                    searchTerm = searchTerm,
                    timestamp = DateTime.UtcNow
                };

                Logger.LogInformation("Sincronização manual de jogadores concluída");
                return Ok(result, "Jogadores sincronizados com sucesso");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro durante a sincronização manual dos jogadores");
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
                    return BadRequest<List<object>>("O termo de pesquisa deve ter pelo menos 2 caracteres");
                }

                Logger.LogInformation("Pesquisando jogadores diretamente da API externa: {Pesquisar}", search);

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

                Logger.LogInformation("Foram encontrados {PlayerCount} jogadores na API externa", playersList.Count);
                return Ok(playersList, $"Jogadores que correspondem a '{search}' da API externa");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao pesquisar jogadores na API externa");
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
                        "Sincronização inicial de jogadores recomendada" :
                        "Dados de alguns jogadores disponíveis"
                };

                return Ok(status, "Status de sincronização do jogador recuperado");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao verificar o status de sincronização do player");
                throw;
            }
        }
    }
}