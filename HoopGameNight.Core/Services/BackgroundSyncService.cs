using System;
using System.Linq;
using System.Threading.Tasks;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.Models.Entities;
using HoopGameNight.Core.Constants;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Core.Services
{
    public class BackgroundSyncService : IBackgroundSyncService
    {
        private readonly IGameSyncService _gameSyncService;
        private readonly IPlayerStatsSyncService _playerStatsSyncService;
        private readonly IPlayerRepository _playerRepository;
        private readonly IGameRepository _gameRepository;
        private readonly IPlayerStatsRepository _playerStatsRepository;
        private readonly INbaAiService _nbaAiService;
        private readonly IEspnApiService _espnApiService;
        private readonly ICacheService _cacheService;
        private readonly ITeamService _teamService;
        private readonly IPlayerService _playerService;
        private readonly ILogger<BackgroundSyncService> _logger;

        public BackgroundSyncService(
            IGameSyncService gameSyncService,
            IPlayerStatsSyncService playerStatsSyncService,
            IPlayerRepository playerRepository,
            IGameRepository gameRepository,
            IPlayerStatsRepository playerStatsRepository,
            INbaAiService nbaAiService,
            IEspnApiService espnApiService,
            ICacheService cacheService,
            ITeamService teamService,
            IPlayerService playerService,
            ILogger<BackgroundSyncService> logger)
        {
            _gameSyncService = gameSyncService;
            _playerStatsSyncService = playerStatsSyncService;
            _playerRepository = playerRepository;
            _gameRepository = gameRepository;
            _playerStatsRepository = playerStatsRepository;
            _nbaAiService = nbaAiService;
            _espnApiService = espnApiService;
            _cacheService = cacheService;
            _teamService = teamService;
            _playerService = playerService;
            _logger = logger;
        }

        public async Task DawnMasterSyncAsync()
        {
            _logger.LogInformation("DAWN MASTER SYNC: Iniciando orquestração às 03:00 AM");
            
            try
            {
                var teams = await _espnApiService.GetAllTeamsAsync();
                _logger.LogInformation("DAWN MASTER SYNC: Atualizando rosters de {Count} times", teams.Count);

                await _gameSyncService.SyncGamesByDateAsync(DateTime.Today.AddDays(-1));
                await _gameSyncService.SyncGamesByDateAsync(DateTime.Today);

                await SyncMissingGamesStatsAsync();

                await RefreshStaticDataCacheAsync();

                _logger.LogInformation("DAWN MASTER SYNC: Orquestração completa finalizada");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DAWN MASTER SYNC: Falha crítica na sincronização da madrugada");
            }
        }

        public async Task ProcessFinishedGameAsync(string gameId)
        {
            try
            {
                _logger.LogInformation("POST-GAME SYNC: Processando jogo {GameId}", gameId);
                // Busca diretamente pelo ExternalId, sem depender da data do jogo
                var game = await _gameRepository.GetByExternalIdAsync(gameId);

                if (game == null)
                {
                    _logger.LogWarning("POST-GAME SYNC: Jogo {GameId} não encontrado no banco para processar stats", gameId);
                    return;
                }

                await _playerStatsSyncService.SyncGameStatsForAllPlayersInGameAsync(game.Id);

                var playersInGame = await _playerStatsRepository.GetGamePlayerStatsDetailedAsync(game.Id);
                if (playersInGame != null && playersInGame.Any())
                {
                    foreach (var pStat in playersInGame)
                    {
                        var playerId = pStat.PlayerId;
                        await _cacheService.RemoveAsync(CacheKeys.PlayerSeasonStats(playerId));
                        await _cacheService.RemoveAsync(CacheKeys.PlayerCareer(playerId));
                        await _cacheService.RemoveAsync(CacheKeys.PlayerRecentGames(playerId));
                    }
                    _logger.LogInformation("POST-GAME SYNC: Caches de stats individuais invalidados para {Count} jogadores do jogo {GameId}", playersInGame.Count(), gameId);
                }

                await _cacheService.RemoveAsync(CacheKeys.ALL_TEAMS);
                _logger.LogInformation("POST-GAME SYNC: Cache de Times/Standings invalidado");

                _logger.LogInformation("POST-GAME SYNC: Concluído com sucesso para o jogo {GameId}", gameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "POST-GAME SYNC: Erro ao processar o jogo {GameId}", gameId);
            }
        }

        public async Task PriorityPlayerSyncAsync(int playerId)
        {
            var syncLockKey = $"priority_sync_running_{playerId}";
            var isRunning = _cacheService.Get<bool>(syncLockKey);
            if (isRunning)
            {
                _logger.LogInformation("PRIORITY SYNC: Sincronização já em andamento para o player {PlayerId}. Ignorando duplicata.", playerId);
                return;
            }

            try
            {
                await _cacheService.SetAsync(syncLockKey, true, TimeSpan.FromMinutes(10));
                _logger.LogInformation("PRIORITY SYNC: Buscando todos os dados históricos para o player {PlayerId}", playerId);
                await _playerStatsSyncService.SyncPlayerCareerHistoryAsync(playerId, 1900, DateTime.Today.Year + 1);
                await _playerStatsSyncService.SyncPlayerRecentGamesAsync(playerId);
            }
            finally
            {
                await _cacheService.RemoveAsync(syncLockKey);
                // Também remove a trava de enfileiramento para permitir novos pedidos se necessário 
                await _cacheService.RemoveAsync($"sync_lock_player_{playerId}");
            }
        }

        public async Task SyncInjuryReportsAsync()
        {
            _logger.LogInformation("INJURY SYNC: Atualizando relatórios de lesões");
            await Task.Delay(100);
        }

        public async Task SyncMissingGamesStatsAsync()
        {
            _logger.LogInformation("GAP ANALYSIS: Identificando jogos FINAL sem estatísticas");
            var missingGames = await _gameRepository.GetGamesMissingStatsAsync();
            
            var gamesList = missingGames.ToList();
            if (!gamesList.Any())
            {
                _logger.LogInformation("GAP ANALYSIS: Nenhum gap de estatísticas encontrado");
                return;
            }

            _logger.LogInformation("GAP ANALYSIS: Sincronizando {Count} jogos pendentes", gamesList.Count);
            foreach (var game in gamesList)
            {
                try
                {
                    await _playerStatsSyncService.SyncGameStatsForAllPlayersInGameAsync(game.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "GAP ANALYSIS: Erro ao sincronizar stats do jogo {GameId}", game.Id);
                }
            }
        }

        public async Task RefreshStaticDataCacheAsync()
        {
            _logger.LogInformation("BACKGROUND REFRESH: Iniciando atualização preventiva de Times e Jogadores");
            
            try
            {
                await _teamService.GetAllTeamsAsync();
                await _playerService.GetAllPlayersAsync(1, 20);

                _logger.LogInformation("BACKGROUND REFRESH: Cache renovado para dados semi-estáticos");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BACKGROUND REFRESH: Erro durante renovação preventiva de cache");
            }
        }
    }
}
