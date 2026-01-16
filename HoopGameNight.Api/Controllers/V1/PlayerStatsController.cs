using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using HoopGameNight.Api.Controllers;
using HoopGameNight.Core.DTOs.External.ESPN;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace HoopGameNight.Api.Controllers.V1
{
    [ApiController]
    [Route("api/v1/playerstats")]
    public class PlayerStatsController : BaseApiController
    {
        private readonly IPlayerStatsService _playerStatsService;

        public PlayerStatsController(IPlayerStatsService playerStatsService, ILogger<PlayerStatsController> logger) 
            : base(logger)
        {
            _playerStatsService = playerStatsService;
        }

        [HttpGet("{playerId}/season/{season}")]
        [ProducesResponseType(typeof(ApiResponse<PlayerSeasonStatsResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<PlayerSeasonStatsResponse>>> GetPlayerSeasonStats(int playerId, int season)
        {
            return await ExecuteAsync<PlayerSeasonStatsResponse>(async () =>
            {
                var stats = await _playerStatsService.GetPlayerSeasonStatsAsync(playerId, season);
                if (stats == null) return NotFound<PlayerSeasonStatsResponse>("Estatísticas da temporada não encontradas ou jogador inexistente.");
                return Ok(stats);
            });
        }

        [HttpGet("{playerId}/game/{gameId}")]
        [ProducesResponseType(typeof(ApiResponse<PlayerGameStatsDetailedResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<PlayerGameStatsDetailedResponse>>> GetPlayerGameStats(int playerId, int gameId)
        {
            return await ExecuteAsync<PlayerGameStatsDetailedResponse>(async () =>
            {
                var stats = await _playerStatsService.GetPlayerGameStatsAsync(playerId, gameId);
                if (stats == null) return NotFound<PlayerGameStatsDetailedResponse>("Estatísticas do jogo não encontradas.");
                return Ok(stats);
            });
        }

        [HttpGet("{playerId}/recent")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<PlayerGameStatsDetailedResponse>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<IEnumerable<PlayerGameStatsDetailedResponse>>>> GetRecentGames(int playerId, [FromQuery] int limit = 5)
        {
            return await ExecuteAsync<IEnumerable<PlayerGameStatsDetailedResponse>>(async () =>
            {
                var stats = await _playerStatsService.GetPlayerRecentGamesAsync(playerId, limit);
                return Ok(stats);
            });
        }

        // === DIRECT ESPN ENDPOINTS (CACHE -> ESPN) ===

        [HttpGet("{playerId}/gamelog")]
        [ProducesResponseType(typeof(ApiResponse<PlayerGamelogResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<PlayerGamelogResponse>>> GetPlayerGamelog(int playerId)
        {
            return await ExecuteAsync<PlayerGamelogResponse>(async () =>
            {
                var data = await _playerStatsService.GetPlayerGamelogFromEspnAsync(playerId);
                if (data == null) return NotFound<PlayerGamelogResponse>("Gamelog não encontrado.");
                return Ok(data);
            });
        }

        [HttpGet("{playerId}/splits")]
        [ProducesResponseType(typeof(ApiResponse<PlayerSplitsResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<PlayerSplitsResponse>>> GetPlayerSplits(int playerId)
        {
            return await ExecuteAsync<PlayerSplitsResponse>(async () =>
            {
                var data = await _playerStatsService.GetPlayerSplitsFromEspnAsync(playerId);
                if (data == null) return NotFound<PlayerSplitsResponse>("Splits não encontrados.");
                return Ok(data);
            });
        }

        [HttpGet("external/{playerId}/game/{gameId}")]
        public async Task<ActionResult<ApiResponse<object>>> GetPlayerGameStatsDirect(int playerId, int gameId)
        {
            return await ExecuteAsync<object>(async () =>
            {
                var data = await _playerStatsService.GetPlayerGameStatsDirectAsync(playerId, gameId);
                if (data == null) return NotFound<object>("Dados externos não encontrados.");
                return Ok(data);
            });
        }

        [HttpGet("{playerId}/career")]
        [ProducesResponseType(typeof(ApiResponse<PlayerCareerResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<PlayerCareerResponse>>> GetPlayerCareer(int playerId)
        {
            return await ExecuteAsync<PlayerCareerResponse>(async () =>
            {
                var data = await _playerStatsService.GetPlayerCareerStatsFromEspnAsync(playerId);
                if (data == null) return NotFound<PlayerCareerResponse>("Estatísticas de carreira não encontradas.");
                return Ok(data);
            });
        }
    }
}
