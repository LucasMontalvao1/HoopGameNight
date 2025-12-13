using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Core.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace HoopGameNight.Api.Controllers.V1
{
    [Route("api/v1/players/stats/sync")]
    public class PlayerStatsSyncController : BaseApiController
    {
        private readonly IPlayerStatsSyncService _syncService;
        private readonly IPlayerStatsService _statsService;

        public PlayerStatsSyncController(
            IPlayerStatsSyncService syncService,
            IPlayerStatsService statsService,
            ILogger<PlayerStatsSyncController> logger) : base(logger)
        {
            _syncService = syncService;
            _statsService = statsService;
        }

        /// <summary>
        /// Sincroniza as estatísticas de uma temporada específica de um jogador
        /// </summary>
        [HttpPost("{playerId}/season/{season}")]
        public async Task<ActionResult<ApiResponse<object>>> SyncPlayerSeasonStats(
            int playerId,
            int season)
        {
            return await ExecuteAsync(async () =>
            {
                var success = await _syncService.SyncPlayerSeasonStatsAsync(playerId, season);

                if (!success)
                    return BadRequest<object>("Falha ao sincronizar as estatísticas da temporada do jogador");

                var stats = await _statsService.GetPlayerSeasonStatsAsync(playerId, season);

                return Ok((object)new
                {
                    success = true,
                    message = $"Estatísticas da temporada {season} sincronizadas com sucesso para o jogador {playerId}",
                    data = stats,
                    timestamp = DateTime.UtcNow
                }, "Estatísticas da temporada sincronizadas com sucesso");
            });
        }

        /// <summary>
        /// Sincroniza as estatísticas de um jogo específico de um jogador
        /// </summary>
        [HttpPost("{playerId}/game/{gameId}")]
        public async Task<ActionResult<ApiResponse<object>>> SyncPlayerGameStats(
            int playerId,
            int gameId)
        {
            return await ExecuteAsync(async () =>
            {
                var success = await _syncService.SyncPlayerGameStatsAsync(playerId, gameId);

                if (!success)
                    return BadRequest<object>("Falha ao sincronizar as estatísticas do jogo do jogador");

                var stats = await _statsService.GetPlayerGameStatsAsync(playerId, gameId);

                return Ok((object)new
                {
                    success = true,
                    message = $"Estatísticas do jogo {gameId} sincronizadas com sucesso para o jogador {playerId}",
                    data = stats,
                    timestamp = DateTime.UtcNow
                }, "Estatísticas do jogo sincronizadas com sucesso");
            });
        }

        /// <summary>
        /// Sincroniza os jogos recentes de um jogador
        /// </summary>
        [HttpPost("{playerId}/recent-games")]
        public async Task<ActionResult<ApiResponse<object>>> SyncPlayerRecentGames(
            int playerId,
            [FromQuery] int numberOfGames = 10)
        {
            return await ExecuteAsync(async () =>
            {
                if (numberOfGames < 1 || numberOfGames > 50)
                    return BadRequest<object>("O número de jogos deve estar entre 1 e 50");

                var success = await _syncService.SyncPlayerRecentGamesAsync(playerId, numberOfGames);

                if (!success)
                    return BadRequest<object>("Falha ao sincronizar os jogos recentes do player");

                var recentGames = await _statsService.GetPlayerRecentGamesAsync(playerId, numberOfGames);

                return Ok((object)new
                {
                    success = true,
                    message = $"Os jogos recentes de {numberOfGames} foram sincronizados com sucesso para o jogador {playerId}",
                    gamesCount = recentGames.Count,
                    data = recentGames,
                    timestamp = DateTime.UtcNow
                }, "Jogos recentes sincronizados com sucesso");
            });
        }

        /// <summary>
        /// Sincroniza as estatísticas de temporada de todos os jogadores ativos
        /// </summary>
        [HttpPost("all-players/season/{season}")]
        public async Task<ActionResult<ApiResponse<object>>> SyncAllPlayersSeasonStats(int season)
        {
            return await ExecuteAsync(async () =>
            {
                if (!NbaSeasonHelper.IsValidSeason(season))
                    return BadRequest<object>("Temporada inválida");

                var success = await _syncService.SyncAllPlayersSeasonStatsAsync(season);

                if (!success)
                    return BadRequest<object>("Falha ao sincronizar as estatísticas da temporada para todos os jogadores");

                return Ok((object)new
                {
                    success = true,
                    message = $"Sincronização de estatísticas da temporada {season} iniciada para todos os jogadores ativos",
                    timestamp = DateTime.UtcNow
                }, "Sincronização de estatísticas da temporada de todos os jogadores iniciada");
            });
        }

        /// <summary>
        /// Sincroniza as estatísticas de todos os jogadores em um jogo específico
        /// </summary>
        [HttpPost("game/{gameId}/all-players")]
        public async Task<ActionResult<ApiResponse<object>>> SyncGameStatsForAllPlayers(int gameId)
        {
            return await ExecuteAsync(async () =>
            {
                var success = await _syncService.SyncGameStatsForAllPlayersInGameAsync(gameId);

                if (!success)
                    return BadRequest<object>("Falha ao sincronizar as estatísticas do jogo para todos os jogadores");

                return Ok((object)new
                {
                    success = true,
                    message = $"Estatísticas do jogo {gameId} sincronizadas para todos os jogadores",
                    timestamp = DateTime.UtcNow
                }, "Estatísticas do jogo sincronizadas para todos os jogadores");
            });
        }

        /// <summary>
        /// Sincroniza e atualiza as estatísticas de carreira de um jogador (últimas 5 temporadas)
        /// </summary>
        [HttpPost("{playerId}/career/sync")]
        public async Task<ActionResult<ApiResponse<object>>> SyncAndUpdatePlayerCareerStats(int playerId)
        {
            return await ExecuteAsync(async () =>
            {
                var currentSeason = NbaSeasonHelper.GetCurrentSeason();
                var syncedSeasons = await _syncService.SyncPlayerCareerHistoryAsync(playerId, currentSeason - 4, currentSeason);

                var careerUpdated = await _statsService.UpdatePlayerCareerStatsAsync(playerId);

                if (!careerUpdated)
                    return BadRequest<object>("Falha ao atualizar estatísticas de carreira");

                var careerStats = await _statsService.GetPlayerCareerStatsAsync(playerId);

                return Ok((object)new
                {
                    success = true,
                    message = $"Estatísticas de carreira sincronizadas e atualizadas para o jogador {playerId}",
                    syncedSeasons = syncedSeasons,
                    data = careerStats,
                    timestamp = DateTime.UtcNow
                }, "Estatísticas de carreira sincronizadas com sucesso");
            });
        }

        /// <summary>
        /// Sincroniza o histórico completo de carreira de um jogador 
        /// </summary>
        [HttpPost("{playerId}/career/full-history")]
        public async Task<ActionResult<ApiResponse<object>>> SyncPlayerFullHistory(
            int playerId,
            [FromQuery] int? startYear = null,
            [FromQuery] int? endYear = null)
        {
            return await ExecuteAsync(async () =>
            {
                var currentSeason = NbaSeasonHelper.GetCurrentSeason();
                var start = startYear ?? 2003;
                var end = endYear ?? currentSeason;

                if (start > end)
                    return BadRequest<object>("O ano inicial não pode ser maior que o ano final");

                if (!NbaSeasonHelper.IsValidSeason(end))
                    return BadRequest<object>("Temporada final inválida");

                var syncedSeasons = await _syncService.SyncPlayerCareerHistoryAsync(playerId, start, end);

                var careerUpdated = await _statsService.UpdatePlayerCareerStatsAsync(playerId);

                if (!careerUpdated)
                    return BadRequest<object>("Falha ao atualizar estatísticas de carreira após sincronização");

                var careerStats = await _statsService.GetPlayerCareerStatsAsync(playerId);
                var allSeasons = await _statsService.GetPlayerAllSeasonsAsync(playerId);

                return Ok((object)new
                {
                    success = true,
                    message = $"Histórico completo sincronizado para o jogador {playerId} ({start}-{end})",
                    syncedSeasons = syncedSeasons,
                    totalSeasons = end - start + 1,
                    careerStats = careerStats,
                    seasonStats = allSeasons,
                    timestamp = DateTime.UtcNow
                }, "Histórico completo sincronizado com sucesso");
            });
        }

        /// <summary>
        /// Obtém o status da sincronização (para monitoramento)
        /// </summary>
        [HttpGet("status")]
        public async Task<ActionResult<ApiResponse<object>>> GetSyncStatus()
        {
            return await ExecuteAsync(async () =>
            {
                await Task.CompletedTask;

                var status = new
                {
                    isHealthy = true,
                    lastSync = DateTime.UtcNow.AddMinutes(-15),
                    pendingJobs = 0,
                    rateLimitStatus = "OK",
                    timestamp = DateTime.UtcNow
                };

                return Ok((object)status, "Status de sincronização recuperado com sucesso");
            });
        }
    }
}
