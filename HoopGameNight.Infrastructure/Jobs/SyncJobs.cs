using System;
using System.Linq;
using System.Threading.Tasks;
using HoopGameNight.Core.Enums;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;
using RedLockNet;
using Hangfire;

namespace HoopGameNight.Infrastructure.Jobs
{
    public class SyncJobs
    {
        private readonly IGameService _gameService;
        private readonly ITeamService _teamService;
        private readonly IPlayerStatsSyncService _playerStatsSyncService;
        private readonly IDistributedLockFactory? _lockFactory;
        private readonly ILogger<SyncJobs> _logger;

        public SyncJobs(
            IGameService gameService,
            ITeamService teamService,
            IPlayerStatsSyncService playerStatsSyncService,
            ILogger<SyncJobs> logger,
            IDistributedLockFactory? lockFactory = null)
        {
            _gameService = gameService;
            _teamService = teamService;
            _playerStatsSyncService = playerStatsSyncService;
            _lockFactory = lockFactory;
            _logger = logger;
        }

        [AutomaticRetry(Attempts = 3)]
        [Queue("sync")]
        public async Task SyncGamesAsync()
        {
            var resource = "lock:hangfire:game_sync";
            var expiry = TimeSpan.FromMinutes(10);
            
            if (_lockFactory != null)
            {
                using (var redLock = await _lockFactory.CreateLockAsync(resource, expiry))
                {
                    if (!redLock.IsAcquired)
                    {
                        _logger.LogWarning("Não foi possível adquirir lock para SyncGamesAsync. Outro job deve estar rodando.");
                        return;
                    }

                    await ExecuteGameSyncLogic();
                }
            }
            else
            {
                await ExecuteGameSyncLogic();
            }
        }

        private async Task ExecuteGameSyncLogic()
        {
            _logger.LogInformation("Iniciando Sincronização de Jogos");

            var teams = await _teamService.GetAllTeamsAsync();
            if (teams.Count < 30)
            {
                await _teamService.SyncAllTeamsAsync();
            }

            // Sincronizar Jogos de Ontem, Hoje e Próximos 7 dias
            await _gameService.SyncGamesByDateAsync(DateTime.Today.AddDays(-1));
            await _gameService.SyncTodayGamesAsync();
            await _gameService.SyncFutureGamesAsync(7);

            _logger.LogInformation("Sincronização de Jogos concluída com sucesso");
        }

        [AutomaticRetry(Attempts = 2)]
        [Queue("stats")]
        public async Task SyncPlayerStatsAsync()
        {
            var resource = "lock:hangfire:player_stats_sync";
            var expiry = TimeSpan.FromMinutes(30);

            if (_lockFactory != null)
            {
                using (var redLock = await _lockFactory.CreateLockAsync(resource, expiry))
                {
                    if (!redLock.IsAcquired)
                    {
                        _logger.LogWarning("Não foi possível adquirir lock para SyncPlayerStatsAsync.");
                        return;
                    }

                    await ExecutePlayerStatsSyncLogic();
                }
            }
            else
            {
                await ExecutePlayerStatsSyncLogic();
            }
        }

        private async Task ExecutePlayerStatsSyncLogic()
        {
            _logger.LogInformation("Iniciando Sincronização de Estatísticas de Jogadores");

            var todayGames = await _gameService.GetTodayGamesAsync();
            var completedGames = todayGames.Where(g => g.Status == GameStatus.Final.ToString() || g.IsCompleted).ToList();

            foreach (var game in completedGames)
            {
                _logger.LogInformation("Sincronizando estatísticas para o jogo finalizado: {GameId}", game.Id);
                await _playerStatsSyncService.SyncGameStatsForAllPlayersInGameAsync(game.Id);
            }

            _logger.LogInformation("Sincronização de Estatísticas concluída");
        }
    }
}
