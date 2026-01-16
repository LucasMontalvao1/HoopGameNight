using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using HoopGameNight.Api.Constants;
using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace HoopGameNight.Api.Controllers.V1
{
    /// <summary>
    /// Controller para gerenciamento de jogadores da NBA
    /// </summary>
    [Route(ApiConstants.Routes.PLAYERS)]
    public class PlayersController : BaseApiController
    {
        private readonly IPlayerService _playerService;

        public PlayersController(
            IPlayerService playerService,
            ILogger<PlayersController> logger) : base(logger)
        {
            _playerService = playerService;
        }

        /// <summary>
        /// Buscar jogadores com filtros
        /// </summary>
        /// <param name="request">Parâmetros de busca (nome, time ou posição)</param>
        /// <returns>Lista paginada de jogadores</returns>
        [HttpGet(RouteConstants.Players.SEARCH)]
        [ProducesResponseType(typeof(PaginatedResponse<PlayerResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PaginatedResponse<PlayerResponse>>> SearchPlayers(
            [FromQuery] SearchPlayerRequest request)
        {
            if (!request.IsValid())
            {
                var errorResponse = ApiResponse<object>.ErrorResult(
                    "Parâmetros de pesquisa inválidos. Informe o termo de pesquisa, ID da equipe ou posição.");
                return BadRequest(errorResponse);
            }

            Logger.LogInformation("Buscando jogadores com critérios: {@Request}", request);

            var (players, totalCount) = await _playerService.SearchPlayersAsync(request);

            Logger.LogInformation(
                "Encontrados {PlayerCount} jogadores (total: {TotalCount})",
                players.Count,
                totalCount);

            return OkPaginated(
                players,
                request.Page,
                request.PageSize,
                totalCount,
                "Jogadores recuperados com sucesso");
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
            return await ExecuteAsync(async () =>
            {
                Logger.LogInformation("Buscando jogador com ID: {PlayerId}", id);

                var player = await _playerService.GetPlayerByIdAsync(id);

                if (player == null)
                {
                    Logger.LogWarning("Jogador não encontrado com ID: {PlayerId}", id);
                    return NotFound<PlayerResponse>($"Jogador com ID {id} não encontrado");
                }

                Logger.LogInformation("Jogador encontrado: {PlayerName}", player.FullName);
                return Ok(player, "Jogador recuperado com sucesso");
            });
        }

        /// <summary>
        /// Buscar jogadores por time
        /// </summary>
        /// <param name="teamId">ID do time</param>
        /// <param name="page">Página (padrão: 1)</param>
        /// <param name="pageSize">Tamanho da página (padrão: 25, máximo: 100)</param>
        /// <returns>Lista paginada de jogadores do time</returns>
        [HttpGet(RouteConstants.Players.GET_BY_TEAM)]
        [ProducesResponseType(typeof(PaginatedResponse<PlayerResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PaginatedResponse<PlayerResponse>>> GetPlayersByTeam(
            int teamId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25)
        {
            if (!IsValidPagination(page, pageSize, out var errorMessage))
            {
                var errorResponse = ApiResponse<object>.ErrorResult(errorMessage!);
                return BadRequest(errorResponse);
            }

            Logger.LogInformation("Buscando jogadores para o time: {TeamId}", teamId);

            var (players, totalCount) = await _playerService.GetPlayersByTeamAsync(teamId, page, pageSize);

            Logger.LogInformation(
                "Encontrados {PlayerCount} jogadores para a equipe {TeamId}",
                players.Count,
                teamId);

            return OkPaginated(
                players,
                page,
                pageSize,
                totalCount,
                $"Jogadores da equipe {teamId} recuperados com sucesso");
        }

        /// <summary>
        /// Listar todos os jogadores (sem filtro)
        /// </summary>
        /// <param name="page">Página (padrão: 1)</param>
        /// <param name="pageSize">Tamanho da página (padrão: 20, máximo: 100)</param>
        /// <returns>Lista paginada de todos os jogadores</returns>
        [HttpGet]
        [ProducesResponseType(typeof(PaginatedResponse<PlayerResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PaginatedResponse<PlayerResponse>>> GetAllPlayers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (!IsValidPagination(page, pageSize, out var errorMessage))
            {
                var errorResponse = ApiResponse<object>.ErrorResult(errorMessage!);
                return BadRequest(errorResponse);
            }

            Logger.LogInformation("Listando todos os jogadores - Página: {Page}, Tamanho: {PageSize}", page, pageSize);

            var (players, totalCount) = await _playerService.GetAllPlayersAsync(page, pageSize);

            Logger.LogInformation(
                "Encontrados {PlayerCount} jogadores (total: {TotalCount})",
                players.Count,
                totalCount);

            return OkPaginated(
                players,
                page,
                pageSize,
                totalCount,
                "Jogadores recuperados com sucesso");
        }

        /// <summary>
        /// Buscar jogadores por posição
        /// </summary>
        /// <param name="position">Posição (PG, SG, SF, PF, C)</param>
        /// <param name="page">Página</param>
        /// <param name="pageSize">Tamanho da página</param>
        /// <returns>Lista paginada de jogadores da posição</returns>
        [HttpGet("position/{position}")]
        [ProducesResponseType(typeof(PaginatedResponse<PlayerResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PaginatedResponse<PlayerResponse>>> GetPlayersByPosition(
            string position,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25)
        {
            if (!IsValidPagination(page, pageSize, out var errorMessage))
            {
                var errorResponse = ApiResponse<object>.ErrorResult(errorMessage!);
                return BadRequest(errorResponse);
            }

            var positionsInfo = new[]
            {
                new { Sigla = "PG", Ingles = "Point Guard", Portugues = "Armador" },
                new { Sigla = "SG", Ingles = "Shooting Guard", Portugues = "Ala-Armador" },
                new { Sigla = "SF", Ingles = "Small Forward", Portugues = "Ala" },
                new { Sigla = "PF", Ingles = "Power Forward", Portugues = "Ala-Pivô" },
                new { Sigla = "C",  Ingles = "Center", Portugues = "Pivô" },
                new { Sigla = "G",  Ingles = "Guard", Portugues = "Armador/Genérico" },
                new { Sigla = "F",  Ingles = "Forward", Portugues = "Ala/Genérico" }
            };

            var selectedPosition = positionsInfo.FirstOrDefault(p => p.Sigla.Equals(position?.ToUpper()));

            if (selectedPosition == null)
            {
                var validSiglas = string.Join(", ", positionsInfo.Select(p => p.Sigla));
                return BadRequest(ApiResponse<object>.ErrorResult(
                    $"Posição inválida. Use: {validSiglas}"));
            }

            Logger.LogInformation("Buscando jogadores por posição: {Position}, Page: {Page}, PageSize: {PageSize}",
                position, page, pageSize);

            var request = new SearchPlayerRequest
            {
                Position = position.ToUpper(),
                Page = page,
                PageSize = pageSize
            };

            var (players, totalCount) = await _playerService.SearchPlayersAsync(request);

            Logger.LogInformation(
                "Encontrados {PlayerCount} jogadores na posição {Position} (total: {TotalCount})",
                players.Count,
                position,
                totalCount);

            // Debug: mostrar posições dos jogadores retornados
            if (players.Any())
            {
                var positions = players.Select(p => p.Position).Distinct();
                Logger.LogInformation("Posições retornadas: {Positions}", string.Join(", ", positions));
            }

            return OkPaginated(
                players,
                page,
                pageSize,
                totalCount,
                $"Jogadores da posição {position} recuperados com sucesso");
        }
    }
}
