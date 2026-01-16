
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using HoopGameNight.Api.Constants;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace HoopGameNight.Api.Controllers.V1
{

    [Route(ApiConstants.Routes.GAMESSTATS)]
    public class GameStatsController : BaseApiController
    {
        private readonly IGameService _gameService;
        private readonly IEspnApiService _espnService;
        private readonly IMemoryCache _cache;
        private readonly IGameStatsService _gameStatsService;

        public GameStatsController(
            IGameService gameService,
            IEspnApiService espnService,
            IMemoryCache cache,
            IGameStatsService gameStatsService,
            ILogger<GamesController> logger) : base(logger)
        {
            _gameService = gameService;
            _espnService = espnService;
            _gameStatsService = gameStatsService;
            _cache = cache;
        }

        [HttpGet(RouteConstants.GamesStats.GET_GAMES_STATS)]
        public async Task<ActionResult<ApiResponse<GamePlayerStatsResponse>>> GetGameStats(int gameId)
        {
            return await ExecuteAsync<GamePlayerStatsResponse>(async () =>
            {
                var stats = await _gameStatsService.GetGamePlayerStatsAsync(gameId);
                if (stats == null) return NotFound<GamePlayerStatsResponse>("Stats do jogo não encontradas");
                return Ok(stats);
            });
        }
    }
}
