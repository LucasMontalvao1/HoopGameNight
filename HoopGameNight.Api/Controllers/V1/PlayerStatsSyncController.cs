using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using HoopGameNight.Api.Controllers;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace HoopGameNight.Api.Controllers.V1
{
    [ApiController]
    [Route("api/v1/sync/playerstats")]
    public class PlayerStatsSyncController : BaseApiController
    {
        private readonly IPlayerStatsService _playerStatsService;

        public PlayerStatsSyncController(IPlayerStatsService playerStatsService, ILogger<PlayerStatsSyncController> logger) 
            : base(logger)
        {
            _playerStatsService = playerStatsService;
        }

        [HttpPost("{playerId}/season/{season}")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<bool>>> SyncSeasonStats(int playerId, int season)
        {
            return await ExecuteAsync<bool>(async () =>
            {
                var result = await _playerStatsService.SyncPlayerSeasonStatsAsync(playerId, season);
                return Ok(result, result ? "Sincronização iniciada/concluída com sucesso." : "Falha na sincronização.");
            });
        }

        [HttpPost("{playerId}/game/{gameId}")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<bool>>> SyncGameStats(int playerId, int gameId)
        {
            return await ExecuteAsync<bool>(async () =>
            {
                var result = await _playerStatsService.SyncPlayerGameStatsAsync(playerId, gameId);
                return Ok(result, result ? "Stats do jogo sincronizados." : "Falha ao sincronizar stats do jogo.");
            });
        }
    }
}
