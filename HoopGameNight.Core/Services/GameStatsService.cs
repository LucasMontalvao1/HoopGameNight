using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HoopGameNight.Core.DTOs.External.ESPN;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Core.Models.Entities;
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
        private readonly ILogger<GameStatsService> _logger;

        public GameStatsService(
            IGameRepository gameRepository,
            IPlayerRepository playerRepository,
            IPlayerStatsRepository statsRepository,
            ITeamRepository teamRepository,
            IEspnApiService espnService,
            ICacheService cacheService,
            ILogger<GameStatsService> logger)
        {
            _gameRepository = gameRepository;
            _playerRepository = playerRepository;
            _statsRepository = statsRepository;
            _teamRepository = teamRepository;
            _espnService = espnService;
            _cacheService = cacheService;
            _logger = logger;
        }

        #region Buscar Estatísticas por Jogo

        /// <summary>
        /// Busca estatísticas de TODOS os jogadores em um jogo específico
        /// </summary>
        public async Task<GamePlayerStatsResponse?> GetGamePlayerStatsAsync(int gameId)
        {
            var cacheKey = $"game_all_stats_{gameId}";

            // 1. Tentar cache
            var cached = await _cacheService.GetAsync<GamePlayerStatsResponse>(cacheKey);
            if (cached != null) return cached;

            // 2. Buscar do banco
            var game = await _gameRepository.GetByIdAsync(gameId);
            if (game == null) return null;

            var allStats = await _statsRepository.GetGamePlayerStatsDetailedAsync(gameId);

            // 3. Se não tem dados no banco, sincronizar da ESPN
            if (!allStats.Any())
            {
                _logger.LogInformation("Stats do jogo {GameId} não encontradas. Sincronizando...", gameId);
                await SyncGameStatsAsync(gameId);
                allStats = await _statsRepository.GetGamePlayerStatsDetailedAsync(gameId);
            }

            // 4. Montar resposta
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

            // 5. Cachear
            await _cacheService.SetAsync(cacheKey, response, TimeSpan.FromMinutes(10));
            return response;
        }

        /// <summary>
        /// Busca líderes estatísticos de um jogo
        /// </summary>
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

                var cacheKey = $"game_leaders_{gameId}";
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
                    await _cacheService.SetAsync(cacheKey, response, TimeSpan.FromMinutes(15));
                }
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting game leaders for game {GameId}", gameId);
                return null;
            }
        }

        /// <summary>
        /// Busca boxscore completo de um jogo
        /// </summary>
        public async Task<EspnBoxscoreDto?> GetGameBoxscoreAsync(int gameId)
        {
            var game = await _gameRepository.GetByIdAsync(gameId);
            if (string.IsNullOrEmpty(game?.ExternalId)) return null;

            var cacheKey = $"game_boxscore_{gameId}";
            var cached = await _cacheService.GetAsync<EspnBoxscoreDto>(cacheKey);
            if (cached != null) return cached;

            var boxscore = await _espnService.GetGameBoxscoreAsync(game.ExternalId);
            if (boxscore != null)
            {
                await _cacheService.SetAsync(cacheKey, boxscore, TimeSpan.FromMinutes(5));
            }
            return boxscore;
        }

        #endregion

        #region Sincronização de Estatísticas

        /// <summary>
        /// Sincroniza estatísticas de TODOS os jogadores de um jogo
        /// Usa o BOXSCORE da ESPN (mais eficiente que buscar jogador por jogador)
        /// </summary>
        public async Task<bool> SyncGameStatsAsync(int gameId)
        {
            try
            {
                _logger.LogInformation("🔄 Iniciando sincronização de stats do jogo {GameId}", gameId);

                // 1. Buscar informações do jogo
                var game = await _gameRepository.GetByIdAsync(gameId);
                if (game == null || string.IsNullOrEmpty(game.ExternalId))
                {
                    _logger.LogWarning("Jogo {GameId} não encontrado ou sem ExternalId", gameId);
                    return false;
                }

                // 2. Buscar BOXSCORE da ESPN (contém stats de TODOS os jogadores)
                var boxscore = await _espnService.GetGameBoxscoreAsync(game.ExternalId);
                if (boxscore?.Players == null || !boxscore.Players.Any())
                {
                    _logger.LogWarning("Boxscore vazio para jogo {GameId}", gameId);
                    return false;
                }

                int syncedCount = 0;
                int errorCount = 0;

                // 3. Processar stats de cada time
                foreach (var teamStats in boxscore.Players)
                {
                    if (teamStats.Statistics == null) continue;

                    // Determinar qual é o TeamId do sistema
                    var espnTeamId = teamStats.Team?.Id;
                    if (string.IsNullOrEmpty(espnTeamId)) continue;

                    int systemTeamId = await MapEspnTeamToSystemIdAsync(espnTeamId);
                    if (systemTeamId == 0) continue;

                    // 4. Processar cada jogador
                    foreach (var athleteStat in teamStats.Statistics)
                    {
                        if (athleteStat.Athletes == null) continue;

                        foreach (var athleteEntry in athleteStat.Athletes)
                        {
                            try
                            {
                                // Extrair ESPN Player ID
                                var espnPlayerId = athleteEntry.Athlete?.Ref?.Split('/').LastOrDefault();
                                if (string.IsNullOrEmpty(espnPlayerId)) continue;

                                // Mapear para Player do sistema
                                var systemPlayerId = await MapEspnPlayerToSystemIdAsync(espnPlayerId);
                                if (systemPlayerId == 0)
                                {
                                    _logger.LogDebug("Jogador ESPN {EspnId} não encontrado no sistema", espnPlayerId);
                                    continue;
                                }

                                // 5. Criar entidade de stats
                                var gameStats = ParseBoxscoreToGameStats(
                                    athleteStat,
                                    athleteEntry,
                                    systemPlayerId,
                                    gameId,
                                    systemTeamId
                                );

                                // 6. Salvar no banco
                                await _statsRepository.UpsertGameStatsAsync(gameStats);
                                syncedCount++;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Erro ao processar jogador em jogo {GameId}", gameId);
                                errorCount++;
                            }
                        }
                    }
                }

                _logger.LogInformation(
                    "✅ Sincronização do jogo {GameId} concluída: {Synced} jogadores, {Errors} erros",
                    gameId, syncedCount, errorCount
                );

                // Invalidar cache
                await _cacheService.RemoveAsync($"game_all_stats_{gameId}");
                await _cacheService.RemoveAsync($"game_leaders_{gameId}");

                return syncedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro crítico ao sincronizar jogo {GameId}", gameId);
                return false;
            }
        }

        #endregion

        #region Mapeamento e Parsing

        /// <summary>
        /// Mapeia ESPN Team ID para System Team ID
        /// </summary>
        private async Task<int> MapEspnTeamToSystemIdAsync(string espnTeamId)
        {
            // Buscar time pelo ExternalId
            var team = await _teamRepository.GetByExternalIdAsync(int.Parse(espnTeamId));
            return team?.Id ?? 0;
        }

        /// <summary>
        /// Mapeia ESPN Player ID para System Player ID
        /// </summary>
        private async Task<int> MapEspnPlayerToSystemIdAsync(string externalId)
        {
            // Buscar jogador pelo EspnId - O repositório agora aceita int
            if (!int.TryParse(externalId, out var espnId)) return 0;
            var player = await _playerRepository.GetByExternalIdAsync(espnId);
            return player?.Id ?? 0;
        }

        /// <summary>
        /// Converte dados do Boxscore da ESPN para PlayerGameStats
        /// </summary>
        private PlayerGameStats ParseBoxscoreToGameStats(
            EspnAthleteStatDto statCategory,
            EspnAthleteEntryDto athleteEntry,
            int playerId,
            int gameId,
            int teamId)
        {
            var stats = new PlayerGameStats
            {
                PlayerId = playerId,
                GameId = gameId,
                TeamId = teamId,
                DidNotPlay = false,
                IsStarter = false
            };

            // Os nomes das estatísticas estão em statCategory.Names
            // Os valores estão em athleteEntry.Stats
            if (statCategory.Names == null || athleteEntry.Stats == null) return stats;

            for (int i = 0; i < statCategory.Names.Count && i < athleteEntry.Stats.Count; i++)
            {
                var statName = statCategory.Names[i]?.ToLowerInvariant() ?? "";
                var statValue = athleteEntry.Stats[i] ?? "0";

                try
                {
                    switch (statName)
                    {
                        case "min":
                        case "minutes":
                            if (statValue.Contains(":"))
                            {
                                var parts = statValue.Split(':');
                                if (parts.Length == 2)
                                {
                                    stats.MinutesPlayed = int.Parse(parts[0]);
                                    stats.SecondsPlayed = int.Parse(parts[1]);
                                }
                            }
                            break;
                        case "pts": case "points": stats.Points = int.Parse(statValue); break;
                        case "fgm": stats.FieldGoalsMade = int.Parse(statValue); break;
                        case "fga": stats.FieldGoalsAttempted = int.Parse(statValue); break;
                        case "3pm": stats.ThreePointersMade = int.Parse(statValue); break;
                        case "3pa": stats.ThreePointersAttempted = int.Parse(statValue); break;
                        case "ftm": stats.FreeThrowsMade = int.Parse(statValue); break;
                        case "fta": stats.FreeThrowsAttempted = int.Parse(statValue); break;
                        case "oreb": stats.OffensiveRebounds = int.Parse(statValue); break;
                        case "dreb": stats.DefensiveRebounds = int.Parse(statValue); break;
                        case "reb": case "totalrebounds": stats.TotalRebounds = int.Parse(statValue); break;
                        case "ast": case "assists": stats.Assists = int.Parse(statValue); break;
                        case "stl": case "steals": stats.Steals = int.Parse(statValue); break;
                        case "blk": case "blocks": stats.Blocks = int.Parse(statValue); break;
                        case "to": case "turnovers": stats.Turnovers = int.Parse(statValue); break;
                        case "pf": case "fouls": stats.PersonalFouls = int.Parse(statValue); break;
                        case "+/-": case "plusminus": stats.PlusMinus = int.Parse(statValue); break;
                    }
                }
                catch
                {
                    // Ignora erros de parsing individual
                }
            }

            return stats;
        }

        /// <summary>
        /// Mapeia ESPN Game Leaders para GameLeadersResponse simplificado
        /// </summary>
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

                if (espnLeaders.Leaders == null || !espnLeaders.Leaders.Any())
                {
                    _logger.LogDebug("No leaders data found for game {GameId}", game.Id);
                    return response;
                }

                foreach (var category in espnLeaders.Leaders)
                {
                    if (category?.Leaders == null || !category.Leaders.Any()) continue;

                    var categoryName = category.Name?.ToLowerInvariant() ?? category.DisplayName?.ToLowerInvariant() ?? "";

                    foreach (var leader in category.Leaders)
                    {
                        if (leader?.Athlete == null) continue;

                        try
                        {
                            // Extract player ID from athlete ref
                            var espnPlayerId = leader.Athlete.Ref?.Split('/').LastOrDefault();
                            if (string.IsNullOrEmpty(espnPlayerId)) continue;

                            // Map to system player
                            var systemPlayerId = await MapEspnPlayerToSystemIdAsync(espnPlayerId);
                            if (systemPlayerId == 0) continue;

                            var player = await _playerRepository.GetByIdAsync(systemPlayerId);
                            if (player == null) continue;

                            var statLeader = new StatLeader
                            {
                                PlayerId = systemPlayerId,
                                PlayerName = player.FullName,
                                Team = player.Team?.Abbreviation ?? "",
                                Value = decimal.TryParse(leader.DisplayValue, out var val) ? val : 0,
                                GamesPlayed = 1 // Game leaders are for single game
                            };

                            // Determine which team and category
                            var isHomeTeam = player.TeamId == game.HomeTeamId;
                            var teamLeaders = isHomeTeam ? response.HomeTeamLeaders : response.VisitorTeamLeaders;

                            // Assign to appropriate category
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