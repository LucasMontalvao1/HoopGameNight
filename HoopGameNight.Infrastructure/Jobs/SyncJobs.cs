using System;
using System.Linq;
using System.Threading.Tasks;
using HoopGameNight.Core.Enums;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;
using RedLockNet;
using Hangfire;
using HoopGameNight.Core.Constants;

namespace HoopGameNight.Infrastructure.Jobs
{
    public class SyncJobs
    {
        private readonly IGameService _gameService;
        private readonly IGameSyncService _gameSyncService;
        private readonly ITeamService _teamService;
        private readonly IPlayerService _playerService;
        private readonly IPlayerStatsService _playerStatsService;
        private readonly IPlayerStatsSyncService _playerStatsSyncService;
        private readonly IBackgroundSyncService _backgroundSyncService;
        private readonly ICacheService _cacheService;
        private readonly IDistributedLockFactory? _lockFactory;
        private readonly ILogger<SyncJobs> _logger;

        public SyncJobs(
            IGameService gameService,
            IGameSyncService gameSyncService,
            ITeamService teamService,
            IPlayerService playerService,
            IPlayerStatsService playerStatsService,
            IPlayerStatsSyncService playerStatsSyncService,
            IBackgroundSyncService backgroundSyncService,
            ICacheService cacheService,
            ILogger<SyncJobs> logger,
            IDistributedLockFactory? lockFactory = null)
        {
            _gameService = gameService;
            _gameSyncService = gameSyncService;
            _teamService = teamService;
            _playerService = playerService;
            _playerStatsService = playerStatsService;
            _playerStatsSyncService = playerStatsSyncService;
            _backgroundSyncService = backgroundSyncService;
            _cacheService = cacheService;
            _lockFactory = lockFactory;
            _logger = logger;
        }

        /// <summary>
        /// Job mestre da madrugada (03:00 AM) que orquestra rosters, gaps de stats e refresh de cache
        /// </summary>
        [AutomaticRetry(Attempts = 2)]
        [Queue("sync")]
        public async Task DawnMasterSyncJobAsync()
        {
            _logger.LogInformation("HANGFIRE: Iniciando DawnMasterSyncJobAsync");
            await _backgroundSyncService.DawnMasterSyncAsync();
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

        private async Task ExecuteGameSyncLogic(bool bypassCache = false)
        {
            _logger.LogInformation("Iniciando Sincronização de Jogos (BypassCache: {Bypass})", bypassCache);

            var teams = await _teamService.GetAllTeamsAsync();
            if (teams.Count < 30)
            {
                await _teamService.SyncAllTeamsAsync();
            }

            await _gameSyncService.SyncGamesByDateAsync(DateTime.Today.AddDays(-1), bypassCache);
            await _gameSyncService.SyncTodayGamesAsync(bypassCache);
            await _gameSyncService.SyncFutureGamesAsync(7);

            _logger.LogInformation("Sincronização de Jogos concluída com sucesso");
        }

        [AutomaticRetry(Attempts = 1)]
        [Queue("sync")]
        public async Task SyncLiveGamesAsync()
        {
            var resource = "lock:hangfire:live_game_sync";
            var expiry = TimeSpan.FromSeconds(45);
            
            if (_lockFactory != null)
            {
                using (var redLock = await _lockFactory.CreateLockAsync(resource, expiry))
                {
                    if (!redLock.IsAcquired) return;
                    await ExecuteLiveGameSyncLogic();
                }
            }
            else
            {
                await ExecuteLiveGameSyncLogic();
            }
        }

        private async Task ExecuteLiveGameSyncLogic()
        {
            var todayGames = await _gameService.GetTodayGamesAsync();
            
            // Verifica se há algum jogo acontecendo agora ou prestes a começar
            var activeGames = todayGames.Any(g => 
                g.Status == GameStatus.Live.ToString() || 
                (g.Status == GameStatus.Scheduled.ToString() && g.DateTime >= DateTime.UtcNow.AddMinutes(-10) && g.DateTime <= DateTime.UtcNow.AddMinutes(10))
            );

            if (activeGames)
            {
                _logger.LogInformation("LIVE SYNC: Jogos ativos detectados. Sincronizando...");
                await _gameSyncService.SyncTodayGamesAsync(bypassCache: true);
            }
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

            _logger.LogInformation("Limpando cache de líderes");
            await _cacheService.RemoveByPatternAsync("leaders_scoring_*");
            await _cacheService.RemoveByPatternAsync("leaders_rebounds_*");
            await _cacheService.RemoveByPatternAsync("leaders_assists_*");

            _logger.LogInformation("Sincronização de Estatísticas concluída");
        }
        [AutomaticRetry(Attempts = 0)]
        [Queue("sync")]
        public async Task SyncManualEssentialJobAsync()
        {
            _logger.LogInformation("MANUAL SYNC: Iniciando Sincronização Essencial (On-Demand)");
            
            // 1. Sincronizar Jogos (Ontem, Hoje, Futuro)
            await ExecuteGameSyncLogic(bypassCache: true);
            
            // 2. Sincronizar Jogadores dos times que jogam hoje/ontem
            var yesterdayGames = await _gameService.GetGamesByDateAsync(DateTime.Today.AddDays(-1));
            var todayGames = await _gameService.GetTodayGamesAsync();
            
            var affectedTeamIds = yesterdayGames.Concat(todayGames)
                .SelectMany(g => new[] { g.HomeTeamId, g.VisitorTeamId })
                .Distinct()
                .ToList();
                
            _logger.LogInformation("MANUAL SYNC: Sincronizando elencos para {Count} times detectados entre ontem e hoje", affectedTeamIds.Count);
            
            foreach (var teamId in affectedTeamIds)
            {
                try 
                {
                    await _playerService.SyncPlayersAsync(teamId.ToString());
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Falha ao sincronizar elenco do time {TeamId}: {Error}", teamId, ex.Message);
                }
            }

            _cacheService.Clear();
            _logger.LogInformation("MANUAL SYNC: Sincronização Essencial concluída com sucesso (Jogos + {Count} Times)", affectedTeamIds.Count);
        }

        [AutomaticRetry(Attempts = 0)]
        [Queue("sync")]
        public async Task SyncManualFullJobAsync()
        {
            _logger.LogInformation("MANUAL SYNC: Iniciando Sincronização TOTAL (On-Demand)");
            
            // 1. Teams
            _logger.LogInformation("Step 1/4: Sincronizando Times...");
            await _teamService.SyncAllTeamsAsync();
            
            // 2. Players
            _logger.LogInformation("Step 2/4: Sincronizando Jogadores...");
            await _playerService.SyncPlayersAsync(null);
            
            var (players, total) = await _playerService.GetAllPlayersAsync(1, 1500);
            _logger.LogInformation("Step 3/4: Sincronizando Estatísticas para {Count} jogadores...", total);

            int statsSyncedCount = 0;
            foreach (var p in players)
            {
                try 
                {
                    await _playerStatsService.GetPlayerCareerStatsFromEspnAsync(p.Id);
                    await _playerStatsService.GetPlayerGamelogFromEspnAsync(p.Id);
                    statsSyncedCount++;
                    
                    if (statsSyncedCount % 50 == 0)
                        _logger.LogInformation("Progresso Stats: {Count}/{Total} concluídos", statsSyncedCount, total);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Falha ao sincronizar dados do jogador {PlayerId}: {Error}", p.Id, ex.Message);
                }
            }

            // 4. Games
            _logger.LogInformation("Step 4/4: Sincronizando Jogos de Hoje...");
            await _gameSyncService.SyncTodayGamesAsync(bypassCache: true);

            _cacheService.Clear();
            _logger.LogInformation("MANUAL SYNC: Sincronização TOTAL concluída com sucesso!");
        }

        [AutomaticRetry(Attempts = 0)]
        [Queue("sync")]
        public async Task SyncManualTodayGamesJobAsync()
        {
            _logger.LogInformation("MANUAL SYNC: Sincronizando jogos de hoje");
            await _gameSyncService.SyncTodayGamesAsync(bypassCache: true);
            _cacheService.Remove(CacheKeys.TodayGames());
            _logger.LogInformation("MANUAL SYNC: Jogos de hoje sincronizados");
        }

        [AutomaticRetry(Attempts = 0)]
        [Queue("sync")]
        public async Task SyncManualGamesByDateJobAsync(DateTime date)
        {
            _logger.LogInformation("MANUAL SYNC: Sincronizando jogos para a data {Date}", date.ToString("yyyy-MM-dd"));
            await _gameSyncService.SyncGamesByDateAsync(date, bypassCache: true);
            _cacheService.Remove(CacheKeys.GamesByDate(date));
            _logger.LogInformation("MANUAL SYNC: Jogos para a data {Date} sincronizados", date.ToString("yyyy-MM-dd"));
        }
    }
}
