using HoopGameNight.Api.Constants;
using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.DTOs.Response;
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
        /// Buscar estatísticas completas de um jogador
        /// </summary>
        /// <param name="playerId">ID do jogador</param>
        /// <param name="season">Temporada específica (opcional)</param>
        /// <param name="includeCareer">Incluir estatísticas de carreira</param>
        /// <param name="includeRecent">Incluir jogos recentes</param>
        /// <param name="recentGamesCount">Quantidade de jogos recentes (1-20)</param>
        /// <returns>Estatísticas detalhadas do jogador</returns>
        [HttpGet(RouteConstants.PlayerStats.GET_STATS)]
        [ProducesResponseType(typeof(ApiResponse<PlayerDetailedResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<PlayerDetailedResponse>>> GetPlayerStats(
            int playerId,
            [FromQuery] int? season = null,
            [FromQuery] bool includeCareer = true,
            [FromQuery] bool includeRecent = true,
            [FromQuery] int recentGamesCount = 5)
        {
            return await ExecuteAsync(async () =>
            {
                if (recentGamesCount < 1 || recentGamesCount > 20)
                {
                    return BadRequest<PlayerDetailedResponse>("O número de jogos recentes deve estar entre 1 e 20");
                }

                var request = new PlayerStatsRequest
                {
                    PlayerId = playerId,
                    Season = season,
                    LastGames = recentGamesCount,
                    IncludeCareer = includeCareer,
                    IncludeCurrentSeason = true
                };

                var stats = await _playerStatsService.GetPlayerDetailedStatsAsync(request);

                if (stats == null)
                {
                    return NotFound<PlayerDetailedResponse>($"Jogador {playerId} não encontrado");
                }

                return Ok(stats, "Estatísticas coletadas com sucesso");
            });
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
                var stats = await _playerStatsService.GetPlayerSeasonStatsAsync(playerId, season);

                if (stats == null)
                {
                    return NotFound<PlayerSeasonStatsResponse>(
                        $"Temporada {season} não tem estatísticas do jogador {playerId}");
                }

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
                var allSeasons = await _playerStatsService.GetPlayerAllSeasonsAsync(playerId);

                if (!allSeasons.Any())
                {
                    return NotFound<List<PlayerSeasonStatsResponse>>(
                        $"Nenhuma estatística de temporada encontrada para o jogador {playerId}");
                }

                return Ok(allSeasons, "Todas as temporadas recuperadas com sucesso");
            });
        }

        /// <summary>
        /// Buscar estatísticas de carreira do jogador
        /// </summary>
        /// <param name="playerId">ID do jogador</param>
        /// <returns>Estatísticas acumuladas de carreira</returns>
        [HttpGet(RouteConstants.PlayerStats.GET_CAREER)]
        [ProducesResponseType(typeof(ApiResponse<PlayerCareerStatsResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<PlayerCareerStatsResponse>>> GetPlayerCareerStats(int playerId)
        {
            return await ExecuteAsync(async () =>
            {
                var careerStats = await _playerStatsService.GetPlayerCareerStatsAsync(playerId);

                if (careerStats == null)
                {
                    return NotFound<PlayerCareerStatsResponse>(
                        $"Estatísticas de carreira não encontradas para o jogador {playerId}");
                }

                return Ok(careerStats, "Estatísticas de carreira recuperadas com sucesso");
            });
        }

        /// <summary>
        /// Buscar jogos recentes de um jogador
        /// </summary>
        /// <param name="playerId">ID do jogador</param>
        /// <param name="limit">Quantidade de jogos (1-20)</param>
        /// <returns>Lista de jogos recentes com estatísticas</returns>
        [HttpGet(RouteConstants.PlayerStats.GET_RECENT_GAMES)]
        [ProducesResponseType(typeof(ApiResponse<List<PlayerRecentGameResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<List<PlayerRecentGameResponse>>>> GetPlayerRecentGames(
            int playerId,
            [FromQuery] int limit = 5)
        {
            return await ExecuteAsync(async () =>
            {
                if (limit < 1 || limit > 20)
                {
                    return BadRequest<List<PlayerRecentGameResponse>>("O limite deve estar entre 1 e 20");
                }

                var recentGames = await _playerStatsService.GetPlayerRecentGamesAsync(playerId, limit);

                return Ok(recentGames, "Jogos recentes recuperados com sucesso");
            });
        }

        /// <summary>
        /// Buscar estatísticas de um jogo específico
        /// </summary>
        /// <param name="playerId">ID do jogador</param>
        /// <param name="gameId">ID do jogo</param>
        /// <returns>Estatísticas do jogador no jogo</returns>
        [HttpGet(RouteConstants.PlayerStats.GET_GAME_STATS)]
        [ProducesResponseType(typeof(ApiResponse<PlayerRecentGameResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<PlayerRecentGameResponse>>> GetPlayerGameStats(
            int playerId,
            int gameId)
        {
            return await ExecuteAsync(async () =>
            {
                var gameStats = await _playerStatsService.GetPlayerGameStatsAsync(playerId, gameId);

                if (gameStats == null)
                {
                    return NotFound<PlayerRecentGameResponse>(
                        $"Estatísticas do jogo {gameId} não encontradas para o jogador {playerId}");
                }

                return Ok(gameStats, "Estatísticas do jogo recuperadas com sucesso");
            });
        }

        /// <summary>
        /// Comparar estatísticas de dois jogadores
        /// </summary>
        /// <param name="player1Id">ID do primeiro jogador</param>
        /// <param name="player2Id">ID do segundo jogador</param>
        /// <param name="season">Temporada para comparação (opcional)</param>
        /// <returns>Comparação detalhada entre os jogadores</returns>
        [HttpGet(RouteConstants.PlayerStats.COMPARE)]
        [ProducesResponseType(typeof(ApiResponse<PlayerComparisonResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<PlayerComparisonResponse>>> ComparePlayerStats(
            int player1Id,
            int player2Id,
            [FromQuery] int? season = null)
        {
            return await ExecuteAsync(async () =>
            {
                if (player1Id == player2Id)
                {
                    return BadRequest<PlayerComparisonResponse>("Não é possível comparar um jogador consigo mesmo");
                }

                var comparison = await _playerStatsService.ComparePlayersAsync(player1Id, player2Id, season);

                if (comparison == null)
                {
                    return NotFound<PlayerComparisonResponse>("Um ou ambos os jogadores não foram encontrados");
                }

                return Ok(comparison, "Comparação de jogadores concluída com sucesso");
            });
        }

        /// <summary>
        /// Buscar líderes de estatísticas da liga
        /// </summary>
        /// <param name="season">Temporada (padrão: atual)</param>
        /// <param name="minGames">Mínimo de jogos disputados</param>
        /// <param name="limit">Quantidade de líderes (1-50)</param>
        /// <returns>Líderes em pontos, rebotes e assistências</returns>
        [HttpGet(RouteConstants.PlayerStats.LEADERS)]
        [ProducesResponseType(typeof(ApiResponse<StatLeadersResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<StatLeadersResponse>>> GetStatLeaders(
            [FromQuery] int? season = null,
            [FromQuery] int minGames = 10,
            [FromQuery] int limit = 10)
        {
            return await ExecuteAsync(async () =>
            {
                if (limit < 1 || limit > 50)
                {
                    return BadRequest<StatLeadersResponse>("O limite deve estar entre 1 e 50");
                }

                if (minGames < 1)
                {
                    return BadRequest<StatLeadersResponse>("O número mínimo de jogos deve ser maior que 0");
                }

                var leaders = await _playerStatsService.GetStatLeadersAsync(
                    season ?? DateTime.Now.Year,
                    minGames,
                    limit);

                return Ok(leaders, "Líderes de estatísticas recuperados com sucesso");
            });
        }

        /// <summary>
        /// Atualizar estatísticas de carreira de um jogador
        /// </summary>
        /// <param name="playerId">ID do jogador</param>
        /// <returns>Status da atualização</returns>
        [HttpPost(RouteConstants.PlayerStats.UPDATE_CAREER)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<object>>> UpdatePlayerCareerStats(int playerId)
        {
            return await ExecuteAsync(async () =>
            {
                var updated = await _playerStatsService.UpdatePlayerCareerStatsAsync(playerId);

                if (!updated)
                {
                    return NotFound<object>(
                        $"Não foi possível atualizar as estatísticas de carreira do jogador {playerId}");
                }

                var result = new
                {
                    message = "Estatísticas de carreira atualizadas com sucesso",
                    playerId = playerId,
                    timestamp = DateTime.UtcNow
                };

                return Ok((object)result, "Estatísticas de carreira atualizadas com sucesso");
            });
        }
    }
}
