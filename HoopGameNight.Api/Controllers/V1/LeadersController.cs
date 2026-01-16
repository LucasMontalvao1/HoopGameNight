using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using HoopGameNight.Api.Constants;
using HoopGameNight.Core.DTOs.External.ESPN;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace HoopGameNight.Api.Controllers.V1
{
    [ApiController]
    [Route("api/v1/leaders")]
    public class LeadersController : BaseApiController
    {
        private readonly IGameService _gameService;

        public LeadersController(IGameService gameService, ILogger<LeadersController> logger) : base(logger)
        {
            _gameService = gameService;
        }

        /// <summary>
        /// Busca os líderes estatísticos de um jogo específico
        /// </summary>
        [HttpGet("game/{gameId}")]
        [ProducesResponseType(typeof(ApiResponse<GameLeadersResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<GameLeadersResponse>>> GetGameLeaders(int gameId)
        {
            return await ExecuteAsync<GameLeadersResponse>(async () =>
            {
                var leaders = await _gameService.GetGameLeadersAsync(gameId);
                if (leaders == null) return NotFound<GameLeadersResponse>("Líderes do jogo não encontrados.");
                return Ok(leaders);
            });
        }

        /// <summary>
        /// Busca os líderes estatísticos de um time (Season Leaders)
        /// </summary>
        [HttpGet("team/{teamId}")]
        [ProducesResponseType(typeof(ApiResponse<TeamSeasonLeadersResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<TeamSeasonLeadersResponse>>> GetTeamLeaders(int teamId)
        {
            return await ExecuteAsync<TeamSeasonLeadersResponse>(async () =>
            {
                var leaders = await _gameService.GetTeamLeadersAsync(teamId);
                if (leaders == null) return NotFound<TeamSeasonLeadersResponse>("Líderes do time não encontrados.");
                return Ok(leaders);
            });
        }
    }
}
