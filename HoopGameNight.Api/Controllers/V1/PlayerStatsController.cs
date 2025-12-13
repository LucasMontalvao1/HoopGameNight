using HoopGameNight.Api.Constants;
using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Helpers;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace HoopGameNight.Api.Controllers.V1
{
    /// <summary>
    /// Controller para estatísticas detalhadas de jogadores da NBA
    /// </summary>
    [Route(ApiConstants.Routes.PLAYERSTATS)]
    [ApiExplorerSettings(GroupName = "PlayerStats")]
    public class PlayerStatsController : BaseApiController
    {
        private readonly IPlayerStatsService _playerStatsService;

        public PlayerStatsController(
            IPlayerStatsService playerStatsService,
            ILogger<PlayerStatsController> logger) : base(logger)
        {
            _playerStatsService = playerStatsService;
        }


        /// <summary>
        /// Buscar estatísticas de uma temporada específica
        /// </summary>
        /// <param name="playerId">ID do jogador</param>
        /// <param name="season">Ano da temporada (ex: 2024)</param>
        /// <returns>Estatísticas da temporada</returns>
        [HttpGet(RouteConstants.PlayerStats.GET_SEASON)]
        [ProducesResponseType(typeof(ApiResponse<PlayerSeasonStatsResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<PlayerSeasonStatsResponse>>> GetPlayerSeasonStats(
            int playerId,
            int season)
        {
            return await ExecuteAsync(async () =>
            {
                Logger.LogWarning("CONTROLLER: GET /playerstats/{PlayerId}/season/{Season}", playerId, season);

                var stats = await _playerStatsService.GetPlayerSeasonStatsAsync(playerId, season);

                if (stats == null)
                {
                    Logger.LogError("CONTROLLER: Stats não encontradas - retornando 404");
                    return NotFound<PlayerSeasonStatsResponse>(
                        $"Temporada {season} não tem estatísticas do jogador {playerId}");
                }

                Logger.LogWarning("CONTROLLER: Retornando stats - PPG={PPG}, RPG={RPG}, APG={APG}",
                    stats.PPG, stats.RPG, stats.APG);

                return Ok(stats, "Estatísticas por temporada recuperadas com sucesso");
            });
        }

        /// <summary>
        /// Buscar todas as temporadas de um jogador
        /// </summary>
        /// <param name="playerId">ID do jogador</param>
        /// <returns>Lista de todas as temporadas</returns>
        [HttpGet(RouteConstants.PlayerStats.GET_ALL_SEASONS)]
        [ProducesResponseType(typeof(ApiResponse<List<PlayerSeasonStatsResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<List<PlayerSeasonStatsResponse>>>> GetPlayerAllSeasonStats(
            int playerId)
        {
            return await ExecuteAsync(async () =>
            {
                Logger.LogWarning("CONTROLLER: GET /playerstats/{PlayerId}/seasons", playerId);

                var allSeasons = await _playerStatsService.GetPlayerAllSeasonsAsync(playerId);

                if (!allSeasons.Any())
                {
                    Logger.LogError("CONTROLLER: Nenhuma temporada encontrada - retornando 404");
                    return NotFound<List<PlayerSeasonStatsResponse>>(
                        $"Nenhuma estatística de temporada encontrada para o jogador {playerId}");
                }

                Logger.LogWarning("CONTROLLER: Retornando {Count} temporadas", allSeasons.Count);

                return Ok(allSeasons, "Todas as temporadas recuperadas com sucesso");
            });
        }






    }
}
