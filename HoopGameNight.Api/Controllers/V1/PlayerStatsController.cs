using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace HoopGameNight.Api.Controllers.V1
{
    [Route("api/v1/players/stats")]
    public class PlayerStatsController : BaseApiController
    {
        private readonly IPlayerStatsService _playerStatsService;

        public PlayerStatsController(
            IPlayerStatsService playerStatsService,
            ILogger<PlayerStatsController> logger) : base(logger)
        {
            _playerStatsService = playerStatsService;
        }

        [HttpGet("{playerId}")]
        public async Task<ActionResult<ApiResponse<PlayerDetailedResponse>>> GetPlayerStats(
            int playerId,
            [FromQuery] int? season = null,
            [FromQuery] bool includeCareer = true,
            [FromQuery] bool includeRecent = true,
            [FromQuery] int recentGamesCount = 5)
        {
            return await ExecuteAsync(async () =>
            {
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
                    return NotFound<PlayerDetailedResponse>($"Jogador {playerId} nao encontrado");

                return Ok(stats, "Estatisticas coletadas com sucesso");
            });
        }

        [HttpGet("{playerId}/season/{season}")]
        public async Task<ActionResult<ApiResponse<PlayerSeasonStatsResponse>>> GetPlayerSeasonStats(
            int playerId,
            int season)
        {
            return await ExecuteAsync(async () =>
            {
                var stats = await _playerStatsService.GetPlayerSeasonStatsAsync(playerId, season);

                if (stats == null)
                    return NotFound<PlayerSeasonStatsResponse>($"Temporada {season} nao tem estatisticas do jogador {playerId}");

                return Ok(stats, "Status por temporada buscado com sucesso");
            });
        }

        [HttpGet("{playerId}/seasons")]
        public async Task<ActionResult<ApiResponse<List<PlayerSeasonStatsResponse>>>> GetPlayerAllSeasonStats(int playerId)
        {
            return await ExecuteAsync(async () =>
            {
                var allSeasons = await _playerStatsService.GetPlayerAllSeasonsAsync(playerId);

                if (!allSeasons.Any())
                    return NotFound<List<PlayerSeasonStatsResponse>>($"Nenhuma estatística de temporada encontrada para o jogador {playerId}");

                return Ok(allSeasons, "Todas as temporadas recuperadas com sucesso");
            });
        }

        [HttpGet("{playerId}/career")]
        public async Task<ActionResult<ApiResponse<PlayerCareerStatsResponse>>> GetPlayerCareerStats(int playerId)
        {
            return await ExecuteAsync(async () =>
            {
                var careerStats = await _playerStatsService.GetPlayerCareerStatsAsync(playerId);

                if (careerStats == null)
                    return NotFound<PlayerCareerStatsResponse>($"Estatísticas de carreira não encontradas para o jogador {playerId}");

                return Ok(careerStats, "Estatísticas de carreira recuperadas com sucesso");
            });
        }

        [HttpGet("{playerId}/recent-games")]
        public async Task<ActionResult<ApiResponse<List<PlayerRecentGameResponse>>>> GetPlayerRecentGames(
            int playerId,
            [FromQuery] int limit = 5)
        {
            return await ExecuteAsync(async () =>
            {
                if (limit < 1 || limit > 20)
                    return BadRequest<List<PlayerRecentGameResponse>>("O limite deve estar entre 1 e 20");

                var recentGames = await _playerStatsService.GetPlayerRecentGamesAsync(playerId, limit);
                return Ok(recentGames, "Jogos recentes recuperados com sucesso");
            });
        }

        [HttpGet("{playerId}/games/{gameId}")]
        public async Task<ActionResult<ApiResponse<PlayerRecentGameResponse>>> GetPlayerGameStats(
            int playerId,
            int gameId)
        {
            return await ExecuteAsync(async () =>
            {
                var gameStats = await _playerStatsService.GetPlayerGameStatsAsync(playerId, gameId);

                if (gameStats == null)
                    return NotFound<PlayerRecentGameResponse>($"Estatísticas do jogo {gameId} não encontradas para o jogador {playerId}");

                return Ok(gameStats, "Estatísticas do jogo recuperadas com sucesso");
            });
        }

        [HttpGet("compare/{player1Id}/{player2Id}")]
        public async Task<ActionResult<ApiResponse<PlayerComparisonResponse>>> ComparePlayerStats(
            int player1Id,
            int player2Id,
            [FromQuery] int? season = null)
        {
            return await ExecuteAsync(async () =>
            {
                var comparison = await _playerStatsService.ComparePlayersAsync(player1Id, player2Id, season);

                if (comparison == null)
                    return NotFound<PlayerComparisonResponse>("Um ou ambos os jogadores não foram encontrados");

                return Ok(comparison, "Comparação de jogadores concluída com sucesso");
            });
        }

        [HttpGet("leaders")]
        public async Task<ActionResult<ApiResponse<StatLeadersResponse>>> GetStatLeaders(
            [FromQuery] int? season = null,
            [FromQuery] int minGames = 10,
            [FromQuery] int limit = 10)
        {
            return await ExecuteAsync(async () =>
            {
                if (limit < 1 || limit > 50)
                    return BadRequest<StatLeadersResponse>("O limite deve estar entre 1 e 50");

                if (minGames < 1)
                    return BadRequest<StatLeadersResponse>("O número mínimo de jogos deve ser maior que 0");

                var leaders = await _playerStatsService.GetStatLeadersAsync(
                    season ?? DateTime.Now.Year,
                    minGames,
                    limit);

                return Ok(leaders, "Líderes de estatísticas recuperados com sucesso");
            });
        }

        [HttpPost("{playerId}/career/update")]
        public async Task<ActionResult<ApiResponse<object>>> UpdatePlayerCareerStats(int playerId)
        {
            return await ExecuteAsync(async () =>
            {
                var updated = await _playerStatsService.UpdatePlayerCareerStatsAsync(playerId);

                if (!updated)
                    return NotFound<object>($"Não foi possível atualizar as estatísticas de carreira do jogador {playerId}");

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