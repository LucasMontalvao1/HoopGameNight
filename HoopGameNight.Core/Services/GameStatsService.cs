using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HoopGameNight.Core.DTOs.External.ESPN;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Core.Models.Entities;
using HoopGameNight.Core.Enums;
using HoopGameNight.Core.Constants;
using HoopGameNight.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Core.Services
{
    /// <summary>
    /// Serviço especializado para estatísticas de jogos
    /// Responsabilidade: Sincronizar e buscar stats de jogadores EM jogos específicos
    /// </summary>
    public class GameStatsService : IGameStatsService
    {
        private readonly IGameRepository _gameRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IPlayerStatsRepository _statsRepository;
        private readonly ITeamRepository _teamRepository;
        private readonly IEspnApiService _espnService;
        private readonly ICacheService _cacheService;
        private readonly ITeamService _teamService;
        private readonly IEspnParser _espnParser;
        private readonly ILogger<GameStatsService> _logger;

        public GameStatsService(
            IGameRepository gameRepository,
            IPlayerRepository playerRepository,
            IPlayerStatsRepository statsRepository,
            ITeamRepository teamRepository,
            IEspnApiService espnService,
            ICacheService cacheService,
            ITeamService teamService,
            IEspnParser espnParser,
            ILogger<GameStatsService> logger)
        {
            _gameRepository = gameRepository;
            _playerRepository = playerRepository;
            _statsRepository = statsRepository;
            _teamRepository = teamRepository;
            _espnService = espnService;
            _cacheService = cacheService;
            _teamService = teamService;
            _espnParser = espnParser;
            _logger = logger;
        }

        #region Buscar Estatísticas por Jogo

        public async Task<GamePlayerStatsResponse?> GetGamePlayerStatsAsync(int gameId)
        {
            try
            {
                var game = await _gameRepository.GetByIdAsync(gameId);
                if (game == null) return null;

                var cacheKey = $"game_all_stats_{gameId}";
                var cached = await _cacheService.GetAsync<GamePlayerStatsResponse>(cacheKey);

                bool isFinal = game.Status == GameStatus.Final || game.Status == GameStatus.Postponed || game.Status == GameStatus.Cancelled;

                if (cached != null)
                {
                    int cachedTotalScore = (cached.HomeScore ?? 0) + (cached.VisitorScore ?? 0);
                    int actualGameTotalScore = (game.HomeTeamScore ?? 0) + (game.VisitorTeamScore ?? 0);

                    if (cachedTotalScore == actualGameTotalScore)
                    {
                        return cached;
                    }
                    else
                    {
                        _logger.LogInformation("Placar do jogo {GameId} em cache ({Cached}) diverge do banco ({Actual}). Invalidando cache do boxscore...", gameId, cachedTotalScore, actualGameTotalScore);
                    }
                }

                var allStats = await _statsRepository.GetGamePlayerStatsDetailedAsync(gameId);

                bool needsSync = !allStats.Any();

                if (!needsSync)
                {
                    int totalStatsPoints = allStats.Sum(s => s.Points);
                    int totalGamePoints = (game.HomeTeamScore ?? 0) + (game.VisitorTeamScore ?? 0);
                    
                    if (totalStatsPoints != totalGamePoints)
                    {
                         needsSync = true;
                    }
                }

                if (needsSync)
                {
                    _logger.LogInformation("Stats do jogo {GameId} desatualizadas ou ausentes. Sincronizando...", gameId);
                    await SyncGameStatsAsync(gameId);
                    allStats = await _statsRepository.GetGamePlayerStatsDetailedAsync(gameId);
                }

                var response = new GamePlayerStatsResponse
                {
                    GameId = gameId,
                    GameDate = game.Date,
                    HomeTeam = game.HomeTeam?.Name ?? "",
                    VisitorTeam = game.VisitorTeam?.Name ?? "",
                    HomeScore = game.HomeTeamScore,
                    VisitorScore = game.VisitorTeamScore,
                    HomeTeamStats = allStats.Where(s => s.TeamId == game.HomeTeamId).ToList(),
                    VisitorTeamStats = allStats.Where(s => s.TeamId == game.VisitorTeamId).ToList()
                };

                var cacheTime = isFinal ? CacheDurations.NoExpiration : TimeSpan.FromSeconds(30);
                await _cacheService.SetAsync(cacheKey, response, cacheTime);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar stats do jogo {GameId}", gameId);
                return null;
            }
        }

        public async Task<GameLeadersResponse?> GetGameLeadersAsync(int gameId)
        {
            try
            {
                var game = await _gameRepository.GetByIdAsync(gameId);
                if (game == null || string.IsNullOrEmpty(game.ExternalId))
                {
                    _logger.LogWarning("Game {GameId} not found or missing ExternalId", gameId);
                    return null;
                }

                var cacheKey = CacheKeys.GameLeaders(gameId);
                var cached = await _cacheService.GetAsync<GameLeadersResponse>(cacheKey);
                if (cached != null) return cached;

                var espnLeaders = await _espnService.GetGameLeadersAsync(game.ExternalId);
                if (espnLeaders == null)
                {
                    _logger.LogWarning("ESPN leaders not found for game {GameId}", gameId);
                    return null;
                }

                var response = await MapEspnGameLeadersToResponse(espnLeaders, game);
                if (response != null)
                {
                    var cacheTime = game.IsCompleted ? CacheDurations.NoExpiration : TimeSpan.FromSeconds(30);
                    await _cacheService.SetAsync(cacheKey, response, cacheTime);
                }
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting game leaders for game {GameId}", gameId);
                return null;
            }
        }

        public async Task<EspnBoxscoreDto?> GetGameBoxscoreAsync(int gameId)
        {
            var game = await _gameRepository.GetByIdAsync(gameId);
            if (string.IsNullOrEmpty(game?.ExternalId)) return null;

            var cacheKey = CacheKeys.GameBoxscore(gameId);
            var cached = await _cacheService.GetAsync<EspnBoxscoreDto>(cacheKey);
            if (cached != null) return cached;

            var boxscore = await _espnService.GetGameBoxscoreAsync(game.ExternalId);
            if (boxscore != null)
            {
                var cacheTime = game.IsCompleted ? CacheDurations.NoExpiration : TimeSpan.FromSeconds(30);
                await _cacheService.SetAsync(cacheKey, boxscore, cacheTime);
            }
            return boxscore;
        }

        #endregion

        #region Sincronização de Estatísticas

        public async Task<bool> SyncGameStatsAsync(int gameId)
        {
            try
            {
                _logger.LogInformation("Iniciando sincronização de stats do jogo {GameId}", gameId);

                var game = await _gameRepository.GetByIdAsync(gameId);
                if (game == null || string.IsNullOrEmpty(game.ExternalId))
                {
                    _logger.LogWarning("Jogo {GameId} não encontrado ou sem ExternalId", gameId);
                    return false;
                }

                var boxscore = await _espnService.GetGameBoxscoreAsync(game.ExternalId);
                if (boxscore?.Players == null || !boxscore.Players.Any())
                {
                    _logger.LogWarning("Boxscore vazio para jogo {GameId}", gameId);
                    return false;
                }

                int syncedCount = 0;
                var playerStatsDict = new Dictionary<int, PlayerGameStats>();

                foreach (var teamStats in boxscore.Players)
                {
                    if (teamStats.Statistics == null) continue;

                    var espnTeamId = teamStats.Team?.Id;
                    if (string.IsNullOrEmpty(espnTeamId)) continue;

                    int systemTeamId = await _teamService.MapEspnTeamToSystemIdAsync(espnTeamId, teamStats.Team?.Abbreviation ?? "");
                    if (systemTeamId == 0) continue;

                    foreach (var athleteStat in teamStats.Statistics)
                    {
                        if (athleteStat.Athletes == null) continue;

                        foreach (var athleteEntry in athleteStat.Athletes)
                        {
                            try
                            {
                                var espnPlayerId = athleteEntry.Athlete?.Id;
                                if (string.IsNullOrEmpty(espnPlayerId)) espnPlayerId = _espnParser.ExtractIdFromRef(athleteEntry.Athlete?.Ref);
                                if (string.IsNullOrEmpty(espnPlayerId)) continue;

                                var systemPlayerId = await GetOrCreateSystemPlayerAsync(espnPlayerId, systemTeamId);
                                if (systemPlayerId == 0) continue;

                                var currentCategoryStats = _espnParser.ParseBoxscoreToGameStats(
                                    athleteStat,
                                    athleteEntry,
                                    systemPlayerId,
                                    gameId,
                                    systemTeamId
                                );

                                if (playerStatsDict.TryGetValue(systemPlayerId, out var existingStats))
                                {
                                    _espnParser.MergeGameStats(existingStats, currentCategoryStats);
                                }
                                else
                                {
                                    playerStatsDict[systemPlayerId] = currentCategoryStats;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug("Erro ao agrupar stats para jogador no jogo {GameId}: {Message}", gameId, ex.Message);
                            }
                        }
                    }
                }

                foreach (var stats in playerStatsDict.Values)
                {
                    try
                    {
                        await _statsRepository.UpsertGameStatsAsync(stats);
                        syncedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao salvar stats agregadas para jogador {PId} no jogo {GameId}", stats.PlayerId, gameId);
                    }
                }

                _logger.LogInformation("Sincronização do jogo {GameId} concluída: {Synced} jogadores salvos/atualizados", gameId, syncedCount);

                await _cacheService.RemoveAsync($"game_all_stats_{gameId}");
                await _cacheService.RemoveAsync($"game_leaders_{gameId}");

                return syncedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro crítico ao sincronizar jogo {GameId}", gameId);
                return false;
            }
        }

        #endregion

        #region Auxiliares de Banco (Player/Team Management)

        private async Task<int> GetOrCreateSystemPlayerAsync(string externalId, int teamId)
        {
            if (!int.TryParse(externalId, out var espnId)) return 0;
            
            var player = await _playerRepository.GetByExternalIdAsync(espnId);
            if (player != null) return player.Id;

            try
            {
                _logger.LogInformation("Jogador ESPN {EspnId} não encontrado. Buscando detalhes para criação...", espnId);
                var details = await _espnService.GetPlayerDetailsAsync(externalId);
                
                if (details == null) return 0;

                var newPlayer = new Player
                {
                    ExternalId = espnId,
                    EspnId = externalId,
                    FirstName = details.FirstName ?? details.DisplayName?.Split(' ').FirstOrDefault() ?? "Unknown",
                    LastName = details.LastName ?? details.DisplayName?.Split(' ').LastOrDefault() ?? "Player",
                    TeamId = teamId,
                    Position = _espnParser.ParsePosition(details.Position?.Abbreviation),
                    HeightFeet = 0,
                    HeightInches = 0,
                    WeightPounds = 0,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _playerRepository.InsertAsync(newPlayer);
                _logger.LogInformation("Jogador criado com sucesso: {FullName} (ID: {Id})", newPlayer.FullName, newPlayer.Id);
                return newPlayer.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar jogador ESPN {EspnId}", externalId);
                return 0;
            }
        }

        private async Task<int> MapEspnPlayerToSystemIdAsync(string externalId)
        {
            if (!int.TryParse(externalId, out var espnId)) return 0;
            var player = await _playerRepository.GetByExternalIdAsync(espnId);
            return player?.Id ?? 0;
        }

        #endregion

        #region Mapeamento de Líderes

        private async Task<GameLeadersResponse?> MapEspnGameLeadersToResponse(EspnGameLeadersDto espnLeaders, Game game)
        {
            try
            {
                var response = new GameLeadersResponse
                {
                    GameId = game.Id,
                    GameDate = game.Date,
                    HomeTeam = game.HomeTeam?.Name ?? "",
                    VisitorTeam = game.VisitorTeam?.Name ?? "",
                    HomeTeamLeaders = new TeamGameLeaders { TeamName = game.HomeTeam?.Name ?? "" },
                    VisitorTeamLeaders = new TeamGameLeaders { TeamName = game.VisitorTeam?.Name ?? "" }
                };

                if (espnLeaders.Leaders == null || !espnLeaders.Leaders.Any()) return response;

                var playersCache = new Dictionary<int, Player>();

                foreach (var category in espnLeaders.Leaders)
                {
                    if (category?.Leaders == null || !category.Leaders.Any()) continue;

                    var categoryName = category.Name?.ToLowerInvariant() ?? category.DisplayName?.ToLowerInvariant() ?? "";

                    foreach (var leader in category.Leaders)
                    {
                        if (leader?.Athlete == null) continue;

                        try
                        {
                            var espnPlayerId = _espnParser.ExtractIdFromRef(leader.Athlete.Ref);
                            if (string.IsNullOrEmpty(espnPlayerId)) continue;

                            var systemPlayerId = await MapEspnPlayerToSystemIdAsync(espnPlayerId);
                            if (systemPlayerId == 0) continue;

                            if (!playersCache.TryGetValue(systemPlayerId, out var player))
                            {
                                player = await _playerRepository.GetByIdAsync(systemPlayerId);
                                if (player != null) playersCache[systemPlayerId] = player;
                            }

                            if (player == null) continue;

                            var statLeader = new StatLeader
                            {
                                PlayerId = systemPlayerId,
                                PlayerName = player.FullName,
                                Team = player.Team?.Abbreviation ?? "",
                                Value = _espnParser.SafeParseDecimal(leader.DisplayValue),
                                GamesPlayed = 1
                            };

                            var isHomeTeam = player.TeamId == game.HomeTeamId;
                            var teamLeaders = isHomeTeam ? response.HomeTeamLeaders : response.VisitorTeamLeaders;

                            if (categoryName.Contains("point") || categoryName.Contains("scoring"))
                            {
                                if (teamLeaders.PointsLeader == null || statLeader.Value > teamLeaders.PointsLeader.Value)
                                    teamLeaders.PointsLeader = statLeader;
                            }
                            else if (categoryName.Contains("rebound"))
                            {
                                if (teamLeaders.ReboundsLeader == null || statLeader.Value > teamLeaders.ReboundsLeader.Value)
                                    teamLeaders.ReboundsLeader = statLeader;
                            }
                            else if (categoryName.Contains("assist"))
                            {
                                if (teamLeaders.AssistsLeader == null || statLeader.Value > teamLeaders.AssistsLeader.Value)
                                    teamLeaders.AssistsLeader = statLeader;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Error parsing leader for game {GameId}", game.Id);
                        }
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mapping game leaders for game {GameId}", game.Id);
                return null;
            }
        }

        #endregion
    }
}