using HoopGameNight.Core.DTOs.External.ESPN;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Enums;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Core.Models.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AutoMapper;

namespace HoopGameNight.Core.Services
{
    public class PlayerStatsService : IPlayerStatsService
    {
        private readonly IPlayerStatsRepository _statsRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IGameRepository _gameRepository;
        private readonly ITeamRepository _teamRepository;
        private readonly IEspnApiService _espnApiService;
        private readonly IMemoryCache _cache;
        private readonly IGameService _gameService;
        private readonly IGameSyncService _gameSyncService;
        private readonly IBackgroundSyncQueue _syncQueue;
        private readonly IEspnParser _espnParser;
        private readonly ITeamService _teamService;
        private readonly IMapper _mapper;
        private readonly ILogger<PlayerStatsService> _logger;

        private const int CACHE_MINUTES = 30;

        public PlayerStatsService(
            IPlayerStatsRepository statsRepository,
            IPlayerRepository playerRepository,
            IGameRepository gameRepository,
            ITeamRepository teamRepository,
            IEspnApiService espnApiService,
            IGameService gameService,
            IGameSyncService gameSyncService,
            IBackgroundSyncQueue syncQueue,
            IEspnParser espnParser,
            ITeamService teamService,
            IMemoryCache cache,
            IMapper mapper,
            ILogger<PlayerStatsService> logger)
        {
            _statsRepository = statsRepository;
            _playerRepository = playerRepository;
            _gameRepository = gameRepository;
            _teamRepository = teamRepository;
            _espnApiService = espnApiService;
            _gameService = gameService;
            _gameSyncService = gameSyncService;
            _syncQueue = syncQueue;
            _espnParser = espnParser;
            _teamService = teamService;
            _cache = cache;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<PlayerSeasonStatsResponse?> GetPlayerSeasonStatsAsync(int playerId, int season)
        {
             var cacheKey = $"player_season_stats_{playerId}_{season}";
             if (_cache.TryGetValue(cacheKey, out PlayerSeasonStatsResponse? cached)) return cached;

             var stats = await _statsRepository.GetSeasonStatsFromViewAsync(playerId, season);
             
             if (stats == null)
             {
                 var syncLockKey = $"sync_lock_player_{playerId}";
                 if (!_cache.TryGetValue(syncLockKey, out _))
                 {
                     _logger.LogInformation("Stats da temporada não encontrados no banco. Enfileirando Sync para Player {PlayerId} Season {Season}", playerId, season);
                     _cache.Set(syncLockKey, true, TimeSpan.FromMinutes(5));
                     _syncQueue.EnqueuePriorityPlayerSync(playerId);
                 }
                 return null; 
             }

             if (stats != null) _cache.Set(cacheKey, stats, TimeSpan.FromMinutes(CACHE_MINUTES));
             return stats;
        }

        public async Task<PlayerGameStatsDetailedResponse?> GetPlayerGameStatsAsync(int playerId, int gameId)
        {
            var cacheKey = $"game_stats_{playerId}_{gameId}";
            if (_cache.TryGetValue(cacheKey, out PlayerGameStatsDetailedResponse? cached)) return cached;

            var stats = await _statsRepository.GetPlayerGameStatsDetailedAsync(playerId, gameId);
            if (stats == null)
            {
               var syncLockKey = $"sync_lock_player_{playerId}";
               if (!_cache.TryGetValue(syncLockKey, out _))
               {
                   _logger.LogInformation("Stats do jogo não encontrados no banco. Enfileirando Sync para Player {PlayerId} Game {GameId}", playerId, gameId);
                   _cache.Set(syncLockKey, true, TimeSpan.FromMinutes(5));
                   _syncQueue.EnqueuePriorityPlayerSync(playerId);
               }
               return null;
            }
            if(stats != null) _cache.Set(cacheKey, stats, TimeSpan.FromMinutes(CACHE_MINUTES));
            return stats;
        }

        public async Task<PlayerGamelogResponse?> GetPlayerGamelogFromEspnAsync(int playerId)
        {
            try
            {
                var player = await _playerRepository.GetByIdAsync(playerId);
                if (player == null || string.IsNullOrEmpty(player.EspnId))
                {
                    _logger.LogWarning("Player {PlayerId} not found or missing EspnId", playerId);
                    return null;
                }

                var key = $"gamelog_v42_{playerId}"; 

                if (_cache.TryGetValue(key, out PlayerGamelogResponse? cachedResponse))
                {
                    _logger.LogInformation("Gamelog retornado do cache para Player {PlayerId}", playerId);
                    return cachedResponse;
                }

                _logger.LogInformation("Buscando gamelog da ESPN para Player {PlayerId}", playerId);                _logger.LogInformation("Gamelog Sync: Iniciando para Player {PlayerId} (ESPN: {EspnId})", playerId, player.EspnId);
                var espnData = await _espnApiService.GetPlayerGamelogAsync(player.EspnId!);
                
                if (espnData == null)
                {
                    _logger.LogWarning("Gamelog Sync: ESPN API retornou NULL para Player {EspnId}", player.EspnId);
                    return null;
                }

                _logger.LogInformation("Gamelog Sync: Dados recebidos da ESPN. SeasonTypes: {STCount}, Names: {NamesCount}", 
                    espnData.SeasonTypes?.Count ?? 0, espnData.Names?.Count ?? 0);

                var response = MapEspnGamelogToResponse(espnData, playerId, player.FullName, 100);

                if (response != null && response.Games.Any())
                {
                    var updatedSeasons = new HashSet<(int, int)>();
                    int syncsThisRequest = 0;
                    const int maxSyncsAllowed = 3;

                    foreach (var g in response.Games)
                    {
                        try
                        {
                            var gameInDb = await _gameRepository.GetByExternalIdAsync(g.EventId);
                            
                            if (gameInDb == null)
                            {
                                if (syncsThisRequest < maxSyncsAllowed)
                                {
                                    _logger.LogInformation("Gamelog Sync: Jogo {EventId} não encontrado. Sincronizando...", g.EventId);
                                    await _gameSyncService.SyncGameByIdAsync(g.EventId);
                                    gameInDb = await _gameRepository.GetByExternalIdAsync(g.EventId);
                                    syncsThisRequest++;
                                }
                                else
                                {
                                    _logger.LogInformation("Gamelog Sync: Pulando sincronização do Jogo {EventId} para evitar timeout (limite de 3).", g.EventId);
                                }
                            }

                            if (gameInDb == null && g.GameDate > DateTime.MinValue)
                            {
                                _logger.LogInformation("Gamelog Sync: Fallback - Buscando jogo por times e data ({Date})", g.GameDate.ToShortDateString());
                                
                                var gamesOnDate = await _gameRepository.GetGamesByDateAsync(g.GameDate.Date);
                                gameInDb = gamesOnDate.FirstOrDefault(x => 
                                    (x.HomeTeamId == player.TeamId || x.VisitorTeamId == player.TeamId) &&
                                    x.Date.Date == g.GameDate.Date);

                                if (gameInDb != null)
                                {
                                    _logger.LogInformation("Gamelog Sync: Fallback bem sucedido! Encontrado jogo {GameId} por data/times.", gameInDb.Id);
                                    
                                    if (gameInDb.ExternalId != g.EventId)
                                    {
                                        _logger.LogInformation("Gamelog Sync: Unificando ExternalId {Old} -> {New}", gameInDb.ExternalId, g.EventId);
                                        gameInDb.ExternalId = g.EventId;
                                        await _gameRepository.UpdateAsync(gameInDb);
                                    }
                                }
                            }

                            if (gameInDb == null)
                            {
                                _logger.LogWarning("Gamelog Sync: Falha ao garantir jogo {EventId}. Pulando...", g.EventId);
                                continue;
                            }

                            int internalGameId = gameInDb.Id;
                            g.GameId = internalGameId;

                            bool needsUpdate = false;
                            if (gameInDb.HomeTeamScore == null || gameInDb.HomeTeamScore == 0 || gameInDb.Date.Year < 2000)
                            {
                                if (!string.IsNullOrEmpty(g.Score) && g.Score.Contains("-"))
                                {
                                    var scoreParts = g.Score.Replace(" ", "").Split('-');
                                    if (scoreParts.Length == 2 && int.TryParse(scoreParts[0], out int s1) && int.TryParse(scoreParts[1], out int s2))
                                    {
                                        if (g.IsHome)
                                        {
                                            gameInDb.HomeTeamScore = s1;
                                            gameInDb.VisitorTeamScore = s2;
                                        }
                                        else
                                        {
                                            gameInDb.VisitorTeamScore = s1; 
                                            gameInDb.HomeTeamScore = s2;
                                        }
                                        needsUpdate = true;
                                    }
                                }
                                
                                if (g.GameDate > DateTime.MinValue && (gameInDb.Date.Year < 2000 || gameInDb.Date != g.GameDate.Date))
                                {
                                    gameInDb.Date = g.GameDate.Date;
                                    gameInDb.DateTime = g.GameDate;
                                    needsUpdate = true;
                                }

                                if (needsUpdate)
                                {
                                    gameInDb.Status = GameStatus.Final;
                                    await _gameRepository.UpdateAsync(gameInDb);
                                    _logger.LogInformation("Gamelog Sync: Metadados do jogo {GameId} atualizados (Score: {Score}, Date: {Date})", 
                                        internalGameId, g.Score, g.GameDate.ToShortDateString());
                                }
                            }

                            int minutes = 0;
                            if (!string.IsNullOrEmpty(g.Minutes))
                            {
                                if (g.Minutes.Contains(":"))
                                {
                                    var parts = g.Minutes.Split(':');
                                    if (parts.Length == 2 && int.TryParse(parts[0], out int m))
                                        minutes = m;
                                }
                                else if (int.TryParse(g.Minutes, out int m2))
                                {
                                    minutes = m2;
                                }
                            }

                            int fgm = 0, fga = 0;
                            if (!string.IsNullOrEmpty(g.FieldGoals))
                            {
                                var fgParts = g.FieldGoals.Replace("/", "-").Split('-');
                                if (fgParts.Length == 2)
                                {
                                    int.TryParse(fgParts[0], out fgm);
                                    int.TryParse(fgParts[1], out fga);
                                }
                            }

                            int tpm = 0, tpa = 0;
                            if (!string.IsNullOrEmpty(g.ThreePointers))
                            {
                                var tpParts = g.ThreePointers.Replace("/", "-").Split('-');
                                if (tpParts.Length == 2)
                                {
                                    int.TryParse(tpParts[0], out tpm);
                                    int.TryParse(tpParts[1], out tpa);
                                }
                            }

                            int ftm = 0, fta = 0;
                            if (!string.IsNullOrEmpty(g.FreeThrows))
                            {
                                var ftParts = g.FreeThrows.Replace("/", "-").Split('-');
                                if (ftParts.Length == 2)
                                {
                                    int.TryParse(ftParts[0], out ftm);
                                    int.TryParse(ftParts[1], out fta);
                                }
                            }

                            int systemicTeamId = g.TeamId > 0 
                                ? await _teamService.MapEspnTeamToSystemIdAsync(g.TeamId.ToString(), "") 
                                : (player.TeamId ?? 0);

                            if (systemicTeamId == 0) systemicTeamId = player.TeamId ?? 0;

                            var statsEntity = new PlayerGameStats
                            {
                                PlayerId = playerId,
                                GameId = internalGameId, 
                                TeamId = systemicTeamId, 
                                Points = g.Points,
                                Assists = g.Assists,
                                TotalRebounds = g.Rebounds,
                                Steals = g.Steals,
                                Blocks = g.Blocks,
                                MinutesPlayed = minutes,
                                FieldGoalsMade = fgm,
                                FieldGoalsAttempted = fga,
                                ThreePointersMade = tpm,
                                ThreePointersAttempted = tpa,
                                FreeThrowsMade = ftm,
                                FreeThrowsAttempted = fta,
                                PlusMinus = g.PlusMinus ?? 0
                            };

                            await _statsRepository.UpsertGameStatsAsync(statsEntity);
                            _logger.LogInformation("Gamelog: Estatísticas persistidas para o Game {GameId} (External: {Ext})", internalGameId, g.EventId);

                            updatedSeasons.Add((gameInDb.Season, gameInDb.PostSeason ? 3 : 2));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Falha na persistência de estatísticas (gamelog) para o Player {PlayerId}, Game {GameId}", playerId, g.GameId);
                        }
                    }

                    foreach (var s in updatedSeasons)
                    {
                        try
                        {
                            _logger.LogInformation("Agregando stats de temporada: Player {PlayerId}, Season {Season}, Type {Type}",
                                playerId, s.Item1, s.Item2);
                            await _statsRepository.AggregateSeasonStatsAsync(playerId, s.Item1, s.Item2);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Erro ao agregar temporada {Season}/{Type} para Player {PlayerId}",
                                s.Item1, s.Item2, playerId);
                        }
                    }
                }

                _cache.Set(key, response, TimeSpan.FromMinutes(15));
                _logger.LogInformation("Gamelog persistido em cache para o Player {PlayerId}", playerId);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting gamelog for player {PlayerId}", playerId);
                return null;
            }
        }

        public async Task<PlayerSplitsResponse?> GetPlayerSplitsFromEspnAsync(int playerId)
        {
            try
            {
                var player = await _playerRepository.GetByIdAsync(playerId);
                if (player == null || string.IsNullOrEmpty(player.EspnId))
                {
                    _logger.LogWarning("Player {PlayerId} not found or missing EspnId", playerId);
                    return null;
                }

                var key = $"splits_{playerId}";
                return await _cache.GetOrCreateAsync(key, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60);

                    var espnData = await _espnApiService.GetPlayerSplitsAsync(player.EspnId!);
                    if (espnData == null) return null;

                    return MapEspnSplitsToResponse(espnData, playerId, player.FullName);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting splits for player {PlayerId}", playerId);
                return null;
            }
        }

        public async Task<PlayerGamelogResponse?> GetPlayerRecentGamesAsync(int playerId, int limit = 20)
        {
            var cacheKey = $"player_recent_games_{playerId}_{limit}";
            if (_cache.TryGetValue(cacheKey, out PlayerGamelogResponse? cached)) return cached;

            var player = await _playerRepository.GetByIdAsync(playerId);
            if (player == null) return null;

            var stats = await _statsRepository.GetPlayerRecentGamesDetailedAsync(playerId, limit);
            if (stats == null || !stats.Any())
            {
                _cache.Remove(cacheKey); 
                var syncLockKey = $"sync_lock_player_{playerId}";
                if (!_cache.TryGetValue(syncLockKey, out _))
                {
                    _logger.LogInformation("Nenhum dado recente no banco para Player {PlayerId}. Enfileirando Sync...", playerId);
                    _cache.Set(syncLockKey, true, TimeSpan.FromMinutes(5));
                    _syncQueue.EnqueuePriorityPlayerSync(playerId);
                }
                return null;
            }

            if (stats.Count() < limit)
            {
                var lastSync = await _statsRepository.GetLastSyncDateForPlayerAsync(playerId);
                if (!lastSync.HasValue || lastSync.Value.Date < DateTime.Now.Date)
                {
                    var syncLockKey = $"sync_lock_player_{playerId}";
                    if (!_cache.TryGetValue(syncLockKey, out _))
                    {
                        _logger.LogInformation("Poucos jogos no banco para Player {PlayerId} ({Count}/{Limit}). Enfileirando Sync prioritário...", playerId, stats.Count(), limit);
                        _cache.Set(syncLockKey, true, TimeSpan.FromMinutes(5));
                        _syncQueue.EnqueuePriorityPlayerSync(playerId);
                    }
                }
            }

            if (stats == null) return null;

            var response = new PlayerGamelogResponse
            {
                PlayerId = playerId,
                PlayerName = player.FullName,
                Season = DateTime.Now.Month >= 10 ? DateTime.Now.Year : DateTime.Now.Year - 1, 
                Games = stats.Select(s =>
                {
                    var recent = new PlayerRecentGameResponse
                    {
                        GameId = s.GameId,
                        GameDate = s.GameDate,
                        OpponentId = s.OpponentId.ToString(),
                        Opponent = s.OpponentAbbreviation,
                        OpponentAbbreviation = s.OpponentAbbreviation,
                        OpponentLogo = $"https://a.espncdn.com/i/teamlogos/nba/500/{s.OpponentAbbreviation.ToLower()}.png",
                        IsHome = s.IsHome,
                        Result = s.Result,

                        Points = s.Points,
                        Rebounds = s.TotalRebounds,
                        Assists = s.Assists,
                        Steals = s.Steals,
                        Blocks = s.Blocks,
                        Turnovers = s.Turnovers,
                        PersonalFouls = s.PersonalFouls,

                        Minutes = s.MinutesFormatted,

                        FieldGoals = s.FieldGoalsFormatted,
                        ThreePointers = s.ThreePointersFormatted,
                        FreeThrows = s.FreeThrowsFormatted,

                        FieldGoalsMade = s.FieldGoalsMade,
                        FieldGoalsAttempted = s.FieldGoalsAttempted,
                        ThreePointersMade = s.ThreePointersMade,
                        ThreePointersAttempted = s.ThreePointersAttempted,
                        FreeThrowsMade = s.FreeThrowsMade,
                        FreeThrowsAttempted = s.FreeThrowsAttempted,
                        FieldGoalPercentage = s.FieldGoalPercentage,
                        ThreePointPercentage = s.ThreePointPercentage,
                        FreeThrowPercentage = s.FreeThrowPercentage,

                        OffensiveRebounds = s.OffensiveRebounds,
                        DefensiveRebounds = s.DefensiveRebounds,

                        PlusMinus = s.PlusMinus,
                        DoubleDouble = s.DoubleDouble,
                        TripleDouble = s.TripleDouble,

                        EventId = s.GameId.ToString(),
                        TeamId = s.TeamId
                    };
                    return recent;
                }).ToList()
            };

            _cache.Set(cacheKey, response, TimeSpan.FromMinutes(CACHE_MINUTES));
            return response;
        }

        public async Task<bool> SyncPlayerGameStatsAsync(int playerId, int gameId)
        {
            try
            {
                var player = await _playerRepository.GetByIdAsync(playerId);
                var game = await _gameRepository.GetByIdAsync(gameId);

                if (player == null || game == null) 
                {
                    _logger.LogWarning("Falha no Sync: Player ({PId}) ou Game ({GId}) nulo.", playerId, gameId);
                    return false;
                }
                
                if (string.IsNullOrEmpty(player.EspnId) || string.IsNullOrEmpty(game.ExternalId))
                {
                    _logger.LogWarning("Falha no Sync: EspnId ou ExternalId ausente. P: {PEspn}, G: {GEspn}", player.EspnId, game.ExternalId);
                    return false;
                }

                _logger.LogInformation("Buscando dados na ESPN: Player {PEspn}, Game {GEspn}", player.EspnId, game.ExternalId);
                var espnStats = await _espnApiService.GetPlayerGameStatsAsync(player.EspnId!, game.ExternalId!);
                
                if (espnStats == null)
                {
                    _logger.LogWarning("Falha no Sync: Dados não retornados pela API ESPN.");
                    return false;
                }

                int actualTeamId = player.TeamId ?? 0;
                if (game.HomeTeamId > 0 && game.VisitorTeamId > 0)
                {
                    if (player.TeamId != game.HomeTeamId && player.TeamId != game.VisitorTeamId)
                    {
                        // TODO: Logica mais avançada para trocas 
                    }
                }

                var entity = MapEspnStatsToEntity(espnStats, playerId, gameId, actualTeamId);
                if (entity != null)
                {
                    await _statsRepository.UpsertGameStatsAsync(entity);
                    _logger.LogInformation("Stats sincronizados com sucesso: P {PlayerId} G {GameId}", playerId, gameId);
                    return true;
                }
                _logger.LogWarning("Falha no Sync: Erro no mapeamento da entidade.");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao sincronizar stats do jogo {GameId} para player {PlayerId}", gameId, playerId);
                return false;
            }
        }

        public async Task<bool> SyncPlayerSeasonStatsAsync(int playerId, int season)
        {
            try
            {
                var player = await _playerRepository.GetByIdAsync(playerId);
                if (player == null) return false;
                
                if (player.TeamId == null)
                {
                    _logger.LogWarning("SyncSeason: Player {PlayerId} não possui TeamId associado.", playerId);
                    return false;
                }

                var espnSeasonStats = await _espnApiService.GetPlayerSeasonStatsAsync(player.EspnId!, season + 1);
                
                if (espnSeasonStats == null || string.IsNullOrEmpty(espnSeasonStats.Athlete?.Id))
                {
                    _logger.LogInformation("SyncSeason: API de estatísticas falhou para Player {PlayerId}. Forçando sync do Gamelog local antes de agregar...", playerId);
                    
                    await GetPlayerGamelogFromEspnAsync(playerId);
                    
                    await _statsRepository.AggregateSeasonStatsAsync(playerId, season, 2); 
                    
                    var aggregated = await _statsRepository.GetSeasonStatsAsync(playerId, season);
                    return aggregated != null;
                }

                var seasonEntity = _espnParser.ParseSeasonStats(espnSeasonStats, playerId);
                if (seasonEntity != null)
                {
                    seasonEntity.Season = season; 
                    await _statsRepository.UpsertSeasonStatsAsync(seasonEntity);
                    _logger.LogInformation("SyncSeason: Estatísticas de temporada regular persistidas para Player {PlayerId}, Season {Season}", playerId, season);
                }
                else
                {
                    _logger.LogWarning("SyncSeason: Falha no mapeamento das estatísticas de temporada regular para Player {PlayerId}, Season {Season}", playerId, season);
                }

                var espnPlayoffsStats = await _espnApiService.GetPlayerSeasonStatsAsync(player.EspnId!, season + 1, 3); 
                if (espnPlayoffsStats != null)
                {
                    var playoffsEntity = _espnParser.ParseSeasonStats(espnPlayoffsStats, playerId);
                    if (playoffsEntity != null)
                    {
                        playoffsEntity.Season = season;
                        await _statsRepository.UpsertSeasonStatsAsync(playoffsEntity);
                        _logger.LogInformation("SyncSeason: Estatísticas de playoffs persistidas para Player {PlayerId}, Season {Season}", playerId, season);
                    }
                    else
                    {
                        _logger.LogWarning("SyncSeason: Falha no mapeamento das estatísticas de playoffs para Player {PlayerId}, Season {Season}", playerId, season);
                    }
                }
                else
                {
                    _logger.LogInformation("SyncSeason: Nenhum dado de playoffs encontrado para Player {PlayerId}, Season {Season}", playerId, season);
                }

                return seasonEntity != null || espnPlayoffsStats != null;
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Erro ao sincronizar season stats player {PId} season {S}", playerId, season);
                return false;
            }
        }

        public async Task<object?> GetPlayerGameStatsDirectAsync(int playerId, int gameId)
        {
             var player = await _playerRepository.GetByIdAsync(playerId);
             var game = await _gameRepository.GetByIdAsync(gameId);
             if (player?.EspnId != null && game?.ExternalId != null)
                return await _espnApiService.GetPlayerGameStatsAsync(player.EspnId, game.ExternalId);
             return null;
        }

        public async Task<bool> UpdatePlayerCareerStatsAsync(int playerId)
        {
             await Task.CompletedTask;
             return true; 
        }

        public async Task<PlayerCareerResponse?> GetPlayerCareerStatsFromEspnAsync(int playerId)
        {
            try
            {
                var player = await _playerRepository.GetByIdAsync(playerId);
                if (player == null || string.IsNullOrEmpty(player.EspnId))
                {
                    _logger.LogWarning("Player {PlayerId} not found or missing EspnId", playerId);
                    return null;
                }

                var key = $"career_v40_{playerId}"; 

                if (_cache.TryGetValue(key, out PlayerCareerResponse? cachedResponse))
                {
                    _logger.LogInformation("Career retornado do cache para Player {PlayerId}", playerId);
                    return cachedResponse;
                }

                _logger.LogInformation("Buscando career da ESPN para Player {PlayerId}", playerId);
                var espnData = await _espnApiService.GetPlayerCareerStatsAsync(player.EspnId!);
                if (espnData == null || !espnData.Any()) return null;

                var allDbTeams = await _teamRepository.GetAllAsync();
                var teamMapByExtId = allDbTeams.Where(t => t.ExternalId > 0).ToDictionary(t => t.ExternalId, t => t);
                var teamMapById = allDbTeams.ToDictionary(t => t.Id, t => t);

                foreach (var st in espnData)
                {
                    if (st.Team == null) st.Team = new EspnTeamRefDto();

                    string? espnTeamIdValue = st.Team.Id;
                    
                    if (string.IsNullOrEmpty(espnTeamIdValue))
                    {
                         var match = System.Text.RegularExpressions.Regex.Match(st.Team.Ref, @"teams/(\d+)");
                         if (match.Success) espnTeamIdValue = match.Groups[1].Value;
                    }

                    if (!string.IsNullOrEmpty(espnTeamIdValue) && int.TryParse(espnTeamIdValue, out var extId))
                    {
                        if (teamMapByExtId.TryGetValue(extId, out var dbTeam))
                        {
                            st.Team.Id = dbTeam.Id.ToString();
                            st.Team.DisplayName = dbTeam.FullName;
                            st.Team.Abbreviation = dbTeam.Abbreviation;
                        }
                        else if (extId == 9) 
                        {
                            var gsw = allDbTeams.FirstOrDefault(t => t.Abbreviation == "GS" || t.Abbreviation == "GSW" || t.FullName.Contains("Warriors"));
                            if (gsw != null)
                            {
                                st.Team.Id = gsw.Id.ToString();
                                st.Team.DisplayName = gsw.FullName;
                                st.Team.Abbreviation = gsw.Abbreviation;
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(st.Team.DisplayName))
                    {
                        if (player.TeamId.HasValue && teamMapById.TryGetValue(player.TeamId.Value, out var playerTeam))
                        {
                             if (string.IsNullOrEmpty(espnTeamIdValue))
                             {
                                st.Team.Id = playerTeam.Id.ToString();
                                st.Team.DisplayName = playerTeam.FullName;
                                st.Team.Abbreviation = playerTeam.Abbreviation;
                             }
                        }
                    }
                }

                var response = MapEspnCareerToResponse(espnData, playerId, player.FullName);

                if (response?.CareerTotals != null)
                {
                    var careerEntity = new PlayerCareerStats
                    {
                        PlayerId = playerId,
                        TotalSeasons = response.CareerTotals.TotalSeasons,
                        TotalGames = response.CareerTotals.TotalGames,
                        TotalPoints = response.CareerTotals.TotalPoints,
                        CareerPPG = response.CareerTotals.CareerPPG,
                        CareerRPG = response.CareerTotals.CareerRPG,
                        CareerAPG = response.CareerTotals.CareerAPG,
                        CareerFgPercentage = response.CareerTotals.CareerFGPercentage,
                        HighestPointsGame = response.CareerTotals.CareerHighPoints,
                        HighestReboundsGame = response.CareerTotals.CareerHighRebounds,
                        HighestAssistsGame = response.CareerTotals.CareerHighAssists
                    };

                    await _statsRepository.UpsertCareerStatsAsync(careerEntity);
                    _logger.LogInformation("Estatísticas de carreira persistidas para o Player {PlayerId}", playerId);

                    if (response.SeasonStats != null)
                    {
                        foreach (var seasonStats in response.SeasonStats)
                        {
                            try
                            {
                                var seasonEntity = new PlayerSeasonStats
                                {
                                    PlayerId = playerId,
                                    Season = seasonStats.Season,
                                    SeasonTypeId = seasonStats.SeasonType,
                                    TeamId = seasonStats.TeamId,
                                    GamesPlayed = seasonStats.GamesPlayed,
                                    GamesStarted = seasonStats.GamesStarted,
                                    MinutesPlayed = seasonStats.MinutesPlayed,
                                    Points = seasonStats.TotalPoints,
                                    FieldGoalsMade = seasonStats.FieldGoalsMade,
                                    FieldGoalsAttempted = seasonStats.FieldGoalsAttempted,
                                    FieldGoalPercentage = seasonStats.FGPercentage > 0 ? Math.Min(seasonStats.FGPercentage, 99.999m) : null,
                                    ThreePointersMade = seasonStats.ThreePointersMade,
                                    ThreePointersAttempted = seasonStats.ThreePointersAttempted,
                                    ThreePointPercentage = seasonStats.ThreePointPercentage > 0 ? Math.Min(seasonStats.ThreePointPercentage, 99.999m) : null,
                                    FreeThrowsMade = seasonStats.FreeThrowsMade,
                                    FreeThrowsAttempted = seasonStats.FreeThrowsAttempted,
                                    FreeThrowPercentage = seasonStats.FTPercentage > 0 ? Math.Min(seasonStats.FTPercentage, 99.999m) : null,
                                    OffensiveRebounds = seasonStats.OffensiveRebounds,
                                    DefensiveRebounds = seasonStats.DefensiveRebounds,
                                    TotalRebounds = seasonStats.TotalRebounds,
                                    Assists = seasonStats.TotalAssists,
                                    Steals = seasonStats.Steals,
                                    Blocks = seasonStats.Blocks,
                                    Turnovers = seasonStats.Turnovers,
                                    PersonalFouls = seasonStats.PersonalFouls,
                                    AvgPoints = seasonStats.PPG,
                                    AvgRebounds = seasonStats.RPG,
                                    AvgAssists = seasonStats.APG,
                                    AvgSteals = seasonStats.SPG,
                                    AvgBlocks = seasonStats.BPG,
                                    AvgTurnovers = seasonStats.TPG,
                                    AvgFouls = seasonStats.FPG,
                                    AvgMinutes = seasonStats.MPG
                                };

                                await _statsRepository.UpsertSeasonStatsAsync(seasonEntity);
                                _logger.LogInformation("Estatísticas de temporada persistidas: Player {PlayerId}, Season {Season}, Tipo {Type} (Points: {Points})",
                                    playerId, seasonEntity.Season, seasonEntity.SeasonTypeId, seasonEntity.Points);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Falha na persistência de estatísticas de temporada: Player {PlayerId}, Season {Season}, Tipo {Type}",
                                    playerId, seasonStats.Season, seasonStats.SeasonType);
                            }
                        }
                    }
                }

                _cache.Set(key, response, TimeSpan.FromHours(6));
                _logger.LogInformation("Estatísticas de carreira persistidas em cache para o Player {PlayerId}", playerId);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting career stats for player {PlayerId}", playerId);
                return null;
            }
        }

        private PlayerGameStats? MapEspnStatsToEntity(EspnPlayerStatsDto? espnData, int playerId, int gameId, int teamId)
        {
            if (espnData == null) return null;

            var stats = new PlayerGameStats
            {
                PlayerId = playerId,
                GameId = gameId,
                TeamId = teamId, 
                MinutesPlayed = 0,
                SecondsPlayed = 0,
                Points = 0,
                Assists = 0,
                TotalRebounds = 0,
                DefensiveRebounds = 0,
                OffensiveRebounds = 0,
                Steals = 0,
                Blocks = 0,
                Turnovers = 0,
                PersonalFouls = 0,
                FieldGoalsMade = 0,
                FieldGoalsAttempted = 0,
                ThreePointersMade = 0,
                ThreePointersAttempted = 0,
                FreeThrowsMade = 0,
                FreeThrowsAttempted = 0,
                PlusMinus = 0 
            };
            
            if (espnData.Splits?.Categories != null)
            {
                foreach (var cat in espnData.Splits.Categories)
                {
                    if (cat.Stats == null) continue;

                    foreach (var s in cat.Stats)
                    {
                        var key = s.Name?.ToLower() ?? "";
                        var val = s.Value; // double
                        var displayVal = s.DisplayValue;

                        try {
                            switch (key)
                            {
                                case "points": case "pts": stats.Points = (int)val; break;
                                case "assists": case "ast": stats.Assists = (int)val; break;
                                case "totalrebounds": case "rebounds": case "reb": stats.TotalRebounds = (int)val; break;
                                case "defensiverebounds": case "dreb": stats.DefensiveRebounds = (int)val; break;
                                case "offensiverebounds": case "oreb": stats.OffensiveRebounds = (int)val; break;
                                case "steals": case "stl": stats.Steals = (int)val; break;
                                case "blocks": case "blk": stats.Blocks = (int)val; break;
                                case "turnovers": case "to": stats.Turnovers = (int)val; break;
                                case "fouls": case "pf": stats.PersonalFouls = (int)val; break;
                                case "minutes": case "min": 
                                    if (!string.IsNullOrEmpty(displayVal))
                                    {
                                        var parts = displayVal.Split(':');
                                        if (parts.Length == 2 && int.TryParse(parts[0], out int m) && int.TryParse(parts[1], out int sec))
                                        {
                                            stats.MinutesPlayed = m;
                                            stats.SecondsPlayed = sec;
                                        }
                                        else if (int.TryParse(displayVal, out int m2))
                                        {
                                             stats.MinutesPlayed = m2;
                                        }
                                    }
                                    else
                                    {
                                        stats.MinutesPlayed = (int)val;
                                    }
                                    break;
                                
                                case "fieldgoalsmade": case "fgm": stats.FieldGoalsMade = (int)val; break;
                                case "fieldgoalsattempted": case "fga": stats.FieldGoalsAttempted = (int)val; break;
                                case "fieldgoals": case "fg":
                                    if (!string.IsNullOrEmpty(displayVal) && displayVal.Contains("/"))
                                    {
                                        var parts = displayVal.Split('/');
                                        if (int.TryParse(parts[0], out int ftm) && int.TryParse(parts[1], out int fta))
                                        {
                                            stats.FieldGoalsMade = ftm;
                                            stats.FieldGoalsAttempted = fta;
                                        }
                                    }
                                    else if (!string.IsNullOrEmpty(displayVal) && displayVal.Contains("-"))
                                    {
                                        var parts = displayVal.Split('-');
                                        if (int.TryParse(parts[0], out int ftm) && int.TryParse(parts[1], out int fta))
                                        {
                                            stats.FieldGoalsMade = ftm;
                                            stats.FieldGoalsAttempted = fta;
                                        }
                                    }
                                    break;

                                case "threepointfieldgoalsmade": case "3pm": stats.ThreePointersMade = (int)val; break;
                                case "threepointfieldgoalsattempted": case "3pa": stats.ThreePointersAttempted = (int)val; break;
                                case "threepointfieldgoals": case "3p":
                                    if (!string.IsNullOrEmpty(displayVal) && displayVal.Contains("/"))
                                    {
                                        var parts = displayVal.Split('/');
                                        if (int.TryParse(parts[0], out int m) && int.TryParse(parts[1], out int a))
                                        {
                                            stats.ThreePointersMade = m;
                                            stats.ThreePointersAttempted = a;
                                        }
                                    }
                                    else if (!string.IsNullOrEmpty(displayVal) && displayVal.Contains("-"))
                                    {
                                        var parts = displayVal.Split('-');
                                        if (int.TryParse(parts[0], out int m) && int.TryParse(parts[1], out int a))
                                        {
                                            stats.ThreePointersMade = m;
                                            stats.ThreePointersAttempted = a;
                                        }
                                    }
                                    break;

                                case "freethrowsmade": case "ftm": stats.FreeThrowsMade = (int)val; break;
                                case "freethrowsattempted": case "fta": stats.FreeThrowsAttempted = (int)val; break;
                                case "freethrows": case "ft":
                                    if (!string.IsNullOrEmpty(displayVal) && displayVal.Contains("/"))
                                    {
                                        var parts = displayVal.Split('/');
                                        if (int.TryParse(parts[0], out int ftm) && int.TryParse(parts[1], out int fta))
                                        {
                                            stats.FreeThrowsMade = ftm;
                                            stats.FreeThrowsAttempted = fta;
                                        }
                                    }
                                    else if (!string.IsNullOrEmpty(displayVal) && displayVal.Contains("-"))
                                    {
                                        var parts = displayVal.Split('-');
                                        if (int.TryParse(parts[0], out int ftm) && int.TryParse(parts[1], out int fta))
                                        {
                                            stats.FreeThrowsMade = ftm;
                                            stats.FreeThrowsAttempted = fta;
                                        }
                                    }
                                    break;

                                case "plusminus": case "+/-": stats.PlusMinus = (int)val; break;
                            }
                        } catch { /* Ignora erro de cast */ }
                    }
                }
            }
            return stats;
        }

        private PlayerGamelogResponse? MapEspnGamelogToResponse(EspnPlayerGamelogDto espnData, int playerId, string playerName, int limit = 20)
        {
            try
            {
                var response = new PlayerGamelogResponse
                {
                    PlayerId = playerId,
                    PlayerName = playerName,
                    Season = DateTime.Now.Year 
                };

                var recentGamesList = new List<PlayerRecentGameResponse>();
                var gamesList = new List<EspnGamelogEventDto>();

                if (espnData.SeasonTypes != null)
                {
                     foreach (var st in espnData.SeasonTypes)
                     {
                         if (st.Categories != null)
                         {
                             foreach (var cat in st.Categories)
                             {
                                 if (cat.Events != null) gamesList.AddRange(cat.Events);
                             }
                         }
                         if (st.Events != null) gamesList.AddRange(st.Events);
                     }
                }

                if (espnData.Entries != null)
                {
                    gamesList.AddRange(espnData.Entries);
                }

                var validEvents = gamesList
                    .Where(evt => !string.IsNullOrEmpty(evt.EventId) || !string.IsNullOrEmpty(evt.Id))
                    .GroupBy(e => (e.EventId ?? e.Id) ?? string.Empty)
                    .Select(g => {
                        var e = g.First();
                        if (string.IsNullOrEmpty(e.EventId)) e.EventId = e.Id;
                        return e;
                    });
                
                var names = espnData.Names ?? new List<string>();
                int GetIndex(params string[] aliases) 
                {
                    return names.FindIndex(n => 
                        aliases.Any(a => n.Equals(a, StringComparison.OrdinalIgnoreCase) || 
                                       n.Contains(a, StringComparison.OrdinalIgnoreCase)));
                }

                int idxPoints = GetIndex("points", "pts", "totalPoints");
                int idxRebounds = GetIndex("rebounds", "reb", "totalRebounds");
                int idxAssists = GetIndex("assists", "ast", "totalAssists");
                int idxSteals = GetIndex("steals", "stl", "totalSteals");
                int idxBlocks = GetIndex("blocks", "blk", "totalBlocks");
                int idxMinutes = GetIndex("minutes", "min", "mpg");
                int idxFG = GetIndex("fieldGoalsMade", "fieldGoals", "fgm-a", "fg", "fg-a");
                int idx3P = GetIndex("threePointFieldGoalsMade", "threePoint", "3pm-a", "3p", "3p-a");
                int idxFT = GetIndex("freeThrowsMade", "freeThrows", "ftm-a", "ft", "ft-a");
                int idxPlusMinus = GetIndex("plusMinus", "pm", "+/-", "pts+/-");
                int idxTurnovers = GetIndex("turnovers", "to", "tov", "turnover");

                _logger.LogInformation("Gamelog Mapping: Indices found - Pts:{Pts}, Reb:{Reb}, Ast:{Ast}, PM:{PM}, NamesCount:{NamesCount}", 
                    idxPoints, idxRebounds, idxAssists, idxPlusMinus, names.Count);

                foreach (var evt in validEvents)
                {
                    if (evt == null) continue;
                    
                    if (evt.Stats == null || !evt.Stats.Any())
                    {
                        _logger.LogWarning("Gamelog Mapping: Event {EventId} has NO STATS array!", evt.EventId ?? evt.Id);
                    }
                    
                    var effectiveId = evt.EventId ?? "";
                    var opponentName = "Unknown";
                    var opponentId = "";
                    string? gameDateStr = null;
                    string? gameResult = null;
                    string? score = "";
                    bool isHome = false;
                    int evtTeamId = 0;
                    DateTime gDate = DateTime.MinValue;
                    EspnGamelogEventMetadataDto? metaObj = null;
                    
                    if (DateTime.TryParse(evt.GameDate, out var fallbackDate)) gDate = fallbackDate;

                    if (espnData.Events != null && espnData.Events.TryGetValue(effectiveId, out var meta))
                    {
                        try 
                        { 
                            metaObj = JsonSerializer.Deserialize<EspnGamelogEventMetadataDto>(meta.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); 
                        } 
                        catch { }

                         if (metaObj == null)
                        {
                             try
                             {
                                gameDateStr = GetStringSafe(meta, "gameDate") ?? GetStringSafe(meta, "date") ?? GetStringSafe(meta, "dateTime") ?? GetStringSafe(meta, "game_date");
                                gameResult = GetStringSafe(meta, "gameResult");
                                score = GetStringSafe(meta, "score") ?? "";
                                
                                var opponent = GetPropertySafe(meta, "opponent");
                                opponentName = GetStringSafe(opponent, "displayName") ?? "Unknown";
                                opponentId = GetStringSafe(opponent, "id") ?? "";

                                var teamProp = GetPropertySafe(meta, "team");
                                var pTeamIdStr = GetStringSafe(teamProp, "id");
                                if (pTeamIdStr != null) int.TryParse(pTeamIdStr, out evtTeamId);
                                    
                                var homeTeamIdProp = GetPropertySafe(meta, "homeTeamId");
                                if (homeTeamIdProp.ValueKind != JsonValueKind.Undefined)
                                {
                                    var htIdStr = homeTeamIdProp.GetString();
                                    isHome = pTeamIdStr != null && htIdStr != null && pTeamIdStr == htIdStr;
                                }
                             }
                             catch (Exception ex) { _logger.LogDebug("Error extracting meta properties: {Msg}", ex.Message); }
                        }
                    }

                    if (metaObj != null || gameDateStr != null || gDate > DateTime.MinValue)
                    {
                        if (metaObj != null || gameDateStr != null)
                        {
                            var dStr = metaObj?.GameDate ?? gameDateStr;
                            if (!string.IsNullOrEmpty(dStr)) DateTime.TryParse(dStr, out gDate);
                        }

                        try
                        {
                            var game = new PlayerRecentGameResponse
                            {
                                EventId = evt.EventId ?? "",
                                GameDate = gDate > DateTime.MinValue ? gDate : DateTime.MinValue,
                                Opponent = metaObj?.Opponent?.DisplayName ?? opponentName,
                                OpponentId = metaObj?.Opponent?.Id ?? opponentId,
                                OpponentAbbreviation = metaObj?.Opponent?.Abbreviation ?? "",
                                OpponentLogo = metaObj?.Opponent?.Logo ?? "",
                                IsHome = metaObj != null 
                                    ? (metaObj.Team?.Id != null && metaObj.HomeTeamId != null && metaObj.Team.Id.Equals(metaObj.HomeTeamId, StringComparison.OrdinalIgnoreCase)) 
                                    : isHome,
                                Result = metaObj?.GameResult ?? gameResult ?? "",
                                Score = metaObj?.Score ?? score ?? "",
                                TeamId = metaObj != null ? (int.TryParse(metaObj.Team?.Id, out var pId) ? pId : 0) : evtTeamId
                            };

                            if (idxPoints >= 0 && evt.Stats?.Count > idxPoints) game.Points = int.TryParse(evt.Stats[idxPoints], out var pts) ? pts : 0;
                            if (idxRebounds >= 0 && evt.Stats?.Count > idxRebounds) game.Rebounds = int.TryParse(evt.Stats[idxRebounds], out var reb) ? reb : 0;
                            if (idxAssists >= 0 && evt.Stats?.Count > idxAssists) game.Assists = int.TryParse(evt.Stats[idxAssists], out var ast) ? ast : 0;
                            if (idxSteals >= 0 && evt.Stats?.Count > idxSteals) game.Steals = int.TryParse(evt.Stats[idxSteals], out var stl) ? stl : 0;
                            if (idxBlocks >= 0 && evt.Stats?.Count > idxBlocks) game.Blocks = int.TryParse(evt.Stats[idxBlocks], out var blk) ? blk : 0;
                            
                            if (idxMinutes >= 0 && evt.Stats?.Count > idxMinutes) game.Minutes = evt.Stats[idxMinutes] ?? "0";
                            
                            if (idxFG >= 0 && evt.Stats?.Count > idxFG)
                            {
                                game.FieldGoals = evt.Stats[idxFG] ?? "";
                                if (!string.IsNullOrEmpty(game.FieldGoals))
                                {
                                    var parts = game.FieldGoals.Replace("/", "-").Split('-');
                                    if (parts.Length == 2 && int.TryParse(parts[0], out int m) && int.TryParse(parts[1], out int a))
                                    {
                                        game.FieldGoalsMade = m;
                                        game.FieldGoalsAttempted = a;
                                        if (a > 0) game.FieldGoalPercentage = Math.Round((decimal)m / a * 100, 1);
                                    }
                                }
                            }

                            if (idx3P >= 0 && evt.Stats?.Count > idx3P)
                            {
                                game.ThreePointers = evt.Stats[idx3P] ?? "";
                                if (!string.IsNullOrEmpty(game.ThreePointers))
                                {
                                    var parts = game.ThreePointers.Replace("/", "-").Split('-');
                                    if (parts.Length == 2 && int.TryParse(parts[0], out int m) && int.TryParse(parts[1], out int a))
                                    {
                                        game.ThreePointersMade = m;
                                        game.ThreePointersAttempted = a;
                                        if (a > 0) game.ThreePointPercentage = Math.Round((decimal)m / a * 100, 1);
                                    }
                                }
                            }

                            if (idxFT >= 0 && evt.Stats?.Count > idxFT)
                            {
                                game.FreeThrows = evt.Stats[idxFT] ?? "";
                                if (!string.IsNullOrEmpty(game.FreeThrows))
                                {
                                    var parts = game.FreeThrows.Replace("/", "-").Split('-');
                                    if (parts.Length == 2 && int.TryParse(parts[0], out int m) && int.TryParse(parts[1], out int a))
                                    {
                                        game.FreeThrowsMade = m;
                                        game.FreeThrowsAttempted = a;
                                        if (a > 0) game.FreeThrowPercentage = Math.Round((decimal)m / a * 100, 1);
                                    }
                                }
                            }
                            
                            if (idxTurnovers >= 0 && evt.Stats?.Count > idxTurnovers) game.Turnovers = int.TryParse(evt.Stats[idxTurnovers], out var tov) ? tov : 0;
                             if (idxPlusMinus >= 0 && evt.Stats?.Count > idxPlusMinus)
                             {
                                 var pmStr = evt.Stats[idxPlusMinus];
                                 game.PlusMinus = !string.IsNullOrEmpty(pmStr) 
                                     ? (int.TryParse(pmStr, out var pm) ? pm : _espnParser.SafeParseInt(pmStr)) 
                                     : null;
                             }

                            recentGamesList.Add(game);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Error parsing gamelog event for player {PlayerId}", playerId);
                        }
                    }
                }

                response.Games = recentGamesList
                    .OrderByDescending(g => g.GameDate) 
                    .ToList();
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mapping gamelog for player {PlayerId}", playerId);
                return null;
            }
        }

        private JsonElement GetPropertySafe(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object) return default;
            if (element.TryGetProperty(propertyName, out var prop)) return prop;
            
            foreach (var p in element.EnumerateObject())
            {
                if (p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                    return p.Value;
            }
            
            return default;
        }

        private string? GetStringSafe(JsonElement element, string propertyName)
        {
            var prop = GetPropertySafe(element, propertyName);
            return prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;
        }

        private PlayerSplitsResponse? MapEspnSplitsToResponse(EspnPlayerSplitsDto espnData, int playerId, string playerName)
        {
            try
            {
                var response = new PlayerSplitsResponse
                {
                    PlayerId = playerId,
                    PlayerName = playerName,
                    Season = DateTime.Now.Year
                };

                var splitCategories = new List<PlayerSplitCategory>();

                if (espnData.SplitCategories != null)
                {
                    foreach (var category in espnData.SplitCategories)
                    {
                        if (category?.Splits == null) continue;

                        var splitCategory = new PlayerSplitCategory
                        {
                            CategoryName = category.Name ?? "Unknown",
                            Stats = new List<PlayerSplitStats>()
                        };

                        foreach (var split in category.Splits)
                        {
                            if (split?.Stats == null || !split.Stats.Any()) continue;

                            try
                            {
                                var splitStats = new PlayerSplitStats
                                {
                                    SplitName = split.DisplayName ?? split.Name ?? "Unknown",
                                    GamesPlayed = split.Stats.Count > 0 && int.TryParse(split.Stats[0], out var gp) ? gp : 0
                                };

                                if (split.Stats.Count > 1 && decimal.TryParse(split.Stats[1], out var ppg)) splitStats.PPG = ppg;
                                if (split.Stats.Count > 2 && decimal.TryParse(split.Stats[2], out var rpg)) splitStats.RPG = rpg;
                                if (split.Stats.Count > 3 && decimal.TryParse(split.Stats[3], out var apg)) splitStats.APG = apg;
                                if (split.Stats.Count > 4 && decimal.TryParse(split.Stats[4], out var fg)) splitStats.FGPercentage = fg;
                                if (split.Stats.Count > 5 && decimal.TryParse(split.Stats[5], out var tp)) splitStats.ThreePointPercentage = tp;
                                if (split.Stats.Count > 6 && decimal.TryParse(split.Stats[6], out var ft)) splitStats.FTPercentage = ft;

                                splitCategory.Stats.Add(splitStats);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug(ex, "Error parsing split for player {PlayerId}", playerId);
                            }
                        }

                        if (splitCategory.Stats.Any())
                        {
                            splitCategories.Add(splitCategory);
                        }
                    }
                }

                response.Splits = splitCategories;
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mapping splits for player {PlayerId}", playerId);
                return null;
            }
        }

        private PlayerCareerResponse? MapEspnCareerToResponse(IEnumerable<EspnPlayerStatsDto> espnData, int playerId, string playerName)
        {
            try
            {
                var response = new PlayerCareerResponse
                {
                    PlayerId = playerId,
                    PlayerName = playerName,
                    CareerTotals = new PlayerCareerStatsResponse(),
                    SeasonStats = new List<PlayerSeasonStatsResponse>()
                };

                var seasonsList = new List<PlayerSeasonStatsResponse>();
                var uniqueSeasons = new Dictionary<string, PlayerSeasonStatsResponse>(); 
                
                int totalGames = 0, totalPoints = 0, totalRebs = 0, totalAsts = 0;
                decimal totalPPG = 0, totalRPG = 0, totalAPG = 0, totalFG = 0;
                int highPts = 0, highRebs = 0, highAsts = 0;
                int seasonCount = 0;

                foreach (var seasonData in espnData)
                {
                    if (seasonData == null) continue;

                    try
                    {
                        var seasonStats = new PlayerSeasonStatsResponse
                        {
                            Season = (seasonData.Season?.Year ?? 1) - 1,
                            SeasonName = seasonData.Season?.Year > 1900 ? $"{seasonData.Season.Year - 1}/{seasonData.Season.Year}" : "Unknown",
                            SeasonType = seasonData.SeasonTypeId, 
                            TeamId = int.TryParse(seasonData.Team?.Id, out var teamId) && teamId > 0 ? teamId : 0,
                            TeamName = seasonData.Team?.DisplayName ?? seasonData.Team?.Abbreviation ?? "Unknown"
                        };

                        if (seasonData.Splits?.Categories != null)
                        {
                            foreach (var category in seasonData.Splits.Categories)
                            {
                                if (category?.Stats == null) continue;

                                var categoryName = category.Name?.ToLowerInvariant() ?? "";
                                bool isAverageCategory = categoryName.Contains("average") || categoryName.Contains("pergame") || categoryName.Contains("pg");

                            foreach (var stat in category.Stats)
                            {
                                var key = (stat.Name ?? stat.Abbreviation ?? stat.Label ?? "").ToLower();
                                var val = stat.Value;

                                if (val == 0 && !string.IsNullOrEmpty(stat.DisplayValue) && stat.DisplayValue != "0" && stat.DisplayValue != "0.0")
                                {
                                     val = (double)_espnParser.SafeParseDecimal(stat.DisplayValue);
                                }

                                if (string.IsNullOrEmpty(key)) continue;

                                try
                                {
                                    switch (key)
                                    {
                                        case "gamesplayed": case "gp": seasonStats.GamesPlayed = (int)val; break;
                                        case "gamesstarted": case "gs": seasonStats.GamesStarted = (int)val; break;
                                        case "points": case "pts": case "totalpoints": 
                                            if (isAverageCategory) seasonStats.PPG = (decimal)val;
                                            else { seasonStats.TotalPoints = (int)val; seasonStats.Points = (int)val; }
                                            break;
                                        case "avgpoints": case "ppg": case "pointspergame": seasonStats.PPG = (decimal)val; break;
                                        case "rebounds": case "reb": case "totalrebounds": 
                                            if (isAverageCategory) seasonStats.RPG = (decimal)val;
                                            else seasonStats.TotalRebounds = (int)val; 
                                            break;
                                        case "avgrebound": case "avgrebounds": case "avgtotalrebounds": case "rpg": case "reboundspergame": seasonStats.RPG = (decimal)val; break;
                                        case "assists": case "ast": case "totalassists": 
                                            if (isAverageCategory) seasonStats.APG = (decimal)val;
                                            else seasonStats.TotalAssists = (int)val; 
                                            break;
                                        case "avgassists": case "apg": case "assistspergame": seasonStats.APG = (decimal)val; break;
                                        case "steals": case "stl": case "totalsteals": 
                                            if (isAverageCategory) seasonStats.SPG = (decimal)val;
                                            else seasonStats.Steals = (int)val; 
                                            break;
                                        case "avgsteals": case "spg": case "stealspergame": seasonStats.SPG = (decimal)val; break;
                                        case "blocks": case "blk": case "totalblocks": 
                                            if (isAverageCategory) seasonStats.BPG = (decimal)val;
                                            else seasonStats.Blocks = (int)val; 
                                            break;
                                        case "avgblocks": case "bpg": case "blockspergame": seasonStats.BPG = (decimal)val; break;
                                        case "turnovers": case "to": case "tov": case "totalturnovers": 
                                            if (isAverageCategory) seasonStats.TPG = (decimal)val;
                                            else seasonStats.Turnovers = (int)val; 
                                            break;
                                        case "avgturnovers": case "tpg": case "turnoverspergame": seasonStats.TPG = (decimal)val; break;
                                        case "personalfouls": case "pf": case "fouls": case "totalpersonalfouls": 
                                            if (isAverageCategory) seasonStats.FPG = (decimal)val;
                                            else seasonStats.PersonalFouls = (int)val; 
                                            break;
                                        case "avgpersonalfouls": case "avgfouls": case "fpg": case "foulspergame": seasonStats.FPG = (decimal)val; break;
                                        case "fieldgoalsmade": case "fgm": case "totalfieldgoalsmade": seasonStats.FieldGoalsMade = (int)val; break;
                                        case "fieldgoalsattempted": case "fga": case "totalfieldgoalsattempted": seasonStats.FieldGoalsAttempted = (int)val; break;
                                        case "avgfieldgoalsmade-avgfieldgoalsattempted": case "fieldgoalsmade-fieldgoalsattempted": case "fg":
                                            if (!string.IsNullOrEmpty(stat.DisplayValue) && stat.DisplayValue.Contains("-"))
                                            {
                                                var parts = stat.DisplayValue.Split('-');
                                                if (parts.Length == 2 && double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double m) && double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double a))
                                                {
                                                    seasonStats.FieldGoalsMade = isAverageCategory ? (int)Math.Round(m * seasonStats.GamesPlayed) : (int)m;
                                                    seasonStats.FieldGoalsAttempted = isAverageCategory ? (int)Math.Round(a * seasonStats.GamesPlayed) : (int)a;
                                                }
                                            }
                                            break;
                                        case "threepointfieldgoalsmade": case "3pm": case "totalthreepointfieldgoalsmade": case "threepointersmade": seasonStats.ThreePointersMade = (int)val; break;
                                        case "threepointfieldgoalsattempted": case "3pa": case "totalthreepointfieldgoalsattempted": case "threepointersattempted": seasonStats.ThreePointersAttempted = (int)val; break;
                                        case "avgthreepointfieldgoalsmade-avgthreepointfieldgoalsattempted": case "threepointfieldgoalsmade-threepointfieldgoalsattempted": case "3pt": case "3p":
                                            if (!string.IsNullOrEmpty(stat.DisplayValue) && stat.DisplayValue.Contains("-"))
                                            {
                                                var parts = stat.DisplayValue.Split('-');
                                                if (parts.Length == 2 && double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double m) && double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double a))
                                                {
                                                    seasonStats.ThreePointersMade = isAverageCategory ? (int)Math.Round(m * seasonStats.GamesPlayed) : (int)m;
                                                    seasonStats.ThreePointersAttempted = isAverageCategory ? (int)Math.Round(a * seasonStats.GamesPlayed) : (int)a;
                                                }
                                            }
                                            break;
                                        case "freethrowsmade": case "ftm": case "totalfreethrowsmade": seasonStats.FreeThrowsMade = (int)val; break;
                                        case "freethrowsattempted": case "fta": case "totalfreethrowsattempted": seasonStats.FreeThrowsAttempted = (int)val; break;
                                        case "avgfreethrowsmade-avgfreethrowsattempted": case "freethrowsmade-freethrowsattempted": case "ft":
                                            if (!string.IsNullOrEmpty(stat.DisplayValue) && stat.DisplayValue.Contains("-"))
                                            {
                                                var parts = stat.DisplayValue.Split('-');
                                                if (parts.Length == 2 && double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double m) && double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double a))
                                                {
                                                    seasonStats.FreeThrowsMade = isAverageCategory ? (int)Math.Round(m * seasonStats.GamesPlayed) : (int)m;
                                                    seasonStats.FreeThrowsAttempted = isAverageCategory ? (int)Math.Round(a * seasonStats.GamesPlayed) : (int)a;
                                                }
                                            }
                                            break;
                                        case "offensiverebounds": case "oreb": case "totaloffensiverebounds": seasonStats.OffensiveRebounds = (int)val; break;
                                        case "avgoffensiverebounds": case "avgoffensiverebound": case "or":
                                            if (seasonStats.GamesPlayed > 0) seasonStats.OffensiveRebounds = (int)Math.Round((decimal)val * seasonStats.GamesPlayed);
                                            break;
                                        case "defensiverebounds": case "dreb": case "totaldefensiverebounds": seasonStats.DefensiveRebounds = (int)val; break;
                                        case "avgdefensiverebounds": case "avgdefensiverebound": case "dr":
                                            if (seasonStats.GamesPlayed > 0) seasonStats.DefensiveRebounds = (int)Math.Round((decimal)val * seasonStats.GamesPlayed);
                                            break;
                                        case "totalminutes": case "min": case "minutes": 
                                            if (isAverageCategory) seasonStats.MPG = (decimal)val;
                                            else seasonStats.MinutesPlayed = (decimal)val; 
                                            break;
                                        case "avgminutes": case "averageminutes": case "mpg": case "minutespergame": seasonStats.MPG = (decimal)val; break;
                                        case "fieldgoalpercentage": case "fg%": case "fieldgoalpct": case "fgpct": case "fieldgoalperc": seasonStats.FGPercentage = (decimal)val; break;
                                        case "threepointpercentage": case "3p%": case "threepointfieldgoalpct": case "3ptpct": case "threepointpct": case "threepointfieldgoalpercentage": seasonStats.ThreePointPercentage = (decimal)val; break;
                                        case "freethrowpercentage": case "ft%": case "freethrowpct": case "ftpct": case "freethrowperc": seasonStats.FTPercentage = (decimal)val; break;
                                    }
                                }
                                catch { /* Ignore individual stat parsing errors */ }
                                }
                            }
                        }

                        if (seasonStats.GamesPlayed > 0)
                        {
                            if (seasonStats.TotalPoints == 0 && seasonStats.PPG > 0) 
                            {
                                seasonStats.TotalPoints = (int)Math.Round(seasonStats.PPG * seasonStats.GamesPlayed);
                                seasonStats.Points = seasonStats.TotalPoints;
                            }
                            if (seasonStats.TotalRebounds == 0 && seasonStats.RPG > 0) seasonStats.TotalRebounds = (int)Math.Round(seasonStats.RPG * seasonStats.GamesPlayed);
                            if (seasonStats.TotalAssists == 0 && seasonStats.APG > 0) seasonStats.TotalAssists = (int)Math.Round(seasonStats.APG * seasonStats.GamesPlayed);
                            if (seasonStats.MinutesPlayed == 0 && seasonStats.MPG > 0) seasonStats.MinutesPlayed = Math.Round(seasonStats.MPG * (decimal)seasonStats.GamesPlayed, 1);

                            if (seasonStats.Steals == 0 && seasonStats.SPG > 0) seasonStats.Steals = (int)Math.Round(seasonStats.SPG * seasonStats.GamesPlayed);
                            if (seasonStats.Blocks == 0 && seasonStats.BPG > 0) seasonStats.Blocks = (int)Math.Round(seasonStats.BPG * seasonStats.GamesPlayed);
                            if (seasonStats.Turnovers == 0 && seasonStats.TPG > 0) seasonStats.Turnovers = (int)Math.Round(seasonStats.TPG * seasonStats.GamesPlayed);
                            if (seasonStats.PersonalFouls == 0 && seasonStats.FPG > 0) seasonStats.PersonalFouls = (int)Math.Round(seasonStats.FPG * seasonStats.GamesPlayed);
                            
                            if (seasonStats.OffensiveRebounds == 0 && seasonStats.DefensiveRebounds == 0 && seasonStats.TotalRebounds > 0)
                            {
                                seasonStats.DefensiveRebounds = (int)Math.Round(seasonStats.TotalRebounds * 0.75m); // Roughly 75% are def rebs
                                seasonStats.OffensiveRebounds = seasonStats.TotalRebounds - seasonStats.DefensiveRebounds;
                            }
                        }

                        if (seasonStats.GamesPlayed > 0)
                        {
                            var uniqueKey = $"{seasonStats.Season}_{seasonStats.SeasonType}_{seasonStats.TeamId}";
                            
                            if (!uniqueSeasons.ContainsKey(uniqueKey))
                            {
                                uniqueSeasons[uniqueKey] = seasonStats;
                                _logger.LogDebug("Adding season to list: Player {PlayerId}, Season {Season}, Type {Type}, Team {TeamId}", 
                                    playerId, seasonStats.Season, seasonStats.SeasonType, seasonStats.TeamId);
                                seasonsList.Add(seasonStats);
                                
                                if (seasonStats.SeasonType == 2)
                                {
                                    totalGames += seasonStats.GamesPlayed;
                                    totalPoints += seasonStats.TotalPoints;
                                    totalRebs += seasonStats.TotalRebounds;
                                    totalAsts += seasonStats.TotalAssists;
                                    
                                    totalPPG += seasonStats.PPG; 
                                    totalRPG += seasonStats.RPG;
                                    totalAPG += seasonStats.APG;
                                    totalFG += seasonStats.FGPercentage;
                                    
                                    seasonCount++;
                                }
                                
                                if (seasonStats.TotalPoints > highPts) highPts = seasonStats.TotalPoints;
                                if (seasonStats.TotalRebounds > highRebs) highRebs = seasonStats.TotalRebounds;
                                if (seasonStats.TotalAssists > highAsts) highAsts = seasonStats.TotalAssists;
                            }
                            else
                            {
                                _logger.LogWarning("Duplicate season detected and skipped: Player {PlayerId}, Season {Season}, Type {Type}", 
                                    playerId, seasonStats.Season, seasonStats.SeasonType);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error parsing season stats for player {PlayerId}", playerId);
                    }
                }

                response.CareerTotals.TotalSeasons = seasonCount;
                response.CareerTotals.TotalGames = totalGames;
                response.CareerTotals.TotalPoints = totalPoints;
                
                response.CareerTotals.CareerPPG = totalGames > 0 ? (decimal)totalPoints / totalGames : 0;
                response.CareerTotals.CareerRPG = totalGames > 0 ? (decimal)totalRebs / totalGames : 0;
                response.CareerTotals.CareerAPG = totalGames > 0 ? (decimal)totalAsts / totalGames : 0;
                
                response.CareerTotals.CareerFGPercentage = seasonCount > 0 ? totalFG / seasonCount : 0;
                
                response.CareerTotals.CareerHighPoints = highPts;
                response.CareerTotals.CareerHighRebounds = highRebs;
                response.CareerTotals.CareerHighAssists = highAsts;

                response.SeasonStats = seasonsList;
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mapping career stats for player {PlayerId}", playerId);
                return null;
            }
        }
        public async Task<StatLeadersResponse> GetLeagueLeadersAsync(int season)
        {
            var cacheKey = $"league_leaders_v3_{season}";
            if (_cache.TryGetValue(cacheKey, out StatLeadersResponse? cached)) return cached!;

            var response = new StatLeadersResponse { LastUpdated = DateTime.UtcNow };

            try {
                // Pontos - Offensive Index 0
                response.ScoringLeaders = await MapEspnByAthleteStats("points", "offensive.avgPoints:desc", "offensive", 0);
                
                // Assistências - Offensive Index 10
                response.AssistLeaders = await MapEspnByAthleteStats("assists", "offensive.avgAssists:desc", "offensive", 10);
                
                // 3 Pontos - Offensive Index 4
                response.ThreePointLeaders = await MapEspnByAthleteStats("3p", "offensive.avgThreePointFieldGoalsMade:desc", "offensive", 4);
                
                // Rebotes - General Index 11
                response.ReboundLeaders = await MapEspnByAthleteStats("rebounds", "general.avgRebounds:desc", "general", 11);
                
                // Roubos - Defensive Index 0
                response.StealsLeaders = await MapEspnByAthleteStats("steals", "defensive.avgSteals:desc", "defensive", 0);
                
                // Tocos - Defensive Index 1
                response.BlocksLeaders = await MapEspnByAthleteStats("blocks", "defensive.avgBlocks:desc", "defensive", 1);

                // Fallback para banco local se ESPN falhar totalmente (apenas se Scoring estiver vazio)
                if (response.ScoringLeaders.Count == 0)
                {
                    var scoringData = await _statsRepository.GetScoringLeadersAsync(season, 10, 5);
                    response.ScoringLeaders = scoringData.Select((d, index) => new StatLeader {
                        PlayerId = d.PlayerId ?? 0,
                        ExternalId = d.ExternalId,
                        PlayerName = d.PlayerName?.ToString() ?? "Unknown",
                        Team = d.TeamAbbreviation?.ToString() ?? "-",
                        Value = d.AverageValue ?? 0m,
                        Rank = index + 1
                    }).ToList();
                }

                _cache.Set(cacheKey, response, TimeSpan.FromHours(2));
            } catch (Exception ex) {
                _logger.LogError(ex, "Error fetching league leaders for season {Season}", season);
            }

            return response;
        }

        private async Task<List<StatLeader>> MapEspnByAthleteStats(string categoryName, string sort, string targetCategory, int statIndex)
        {
            var list = new List<StatLeader>();
            try
            {
                var json = await _espnApiService.GetStatsByAthleteAsync(categoryName, 10, sort);
                if (json.ValueKind == JsonValueKind.Undefined || !json.TryGetProperty("athletes", out var athletes)) return list;

                int rank = 1;
                foreach (var entry in athletes.EnumerateArray())
                {
                    var athleteObj = GetPropertySafe(entry, "athlete");
                    var name = GetPropertySafe(athleteObj, "displayName").GetString() ?? "Unknown";
                    var extIdStr = GetPropertySafe(athleteObj, "id").GetString();
                    int? extId = int.TryParse(extIdStr, out var id) ? id : (int?)null;
                    
                    var teams = GetPropertySafe(athleteObj, "teams");
                    var teamAbbr = "-";
                    if (teams.ValueKind == JsonValueKind.Array && teams.GetArrayLength() > 0)
                    {
                        teamAbbr = GetPropertySafe(teams[0], "abbreviation").GetString() ?? "-";
                    }

                    decimal val = 0;
                    if (entry.TryGetProperty("categories", out var categories))
                    {
                        foreach (var cat in categories.EnumerateArray())
                        {
                            if (GetPropertySafe(cat, "name").GetString() == targetCategory && cat.TryGetProperty("values", out var values))
                            {
                                var statsArray = values.EnumerateArray().ToList();
                                if (statsArray.Count > statIndex)
                                {
                                    val = (decimal)statsArray[statIndex].GetDouble();
                                    break;
                                }
                            }
                        }
                    }

                    var playerId = 0;
                    if (!string.IsNullOrEmpty(extIdStr))
                    {
                        var player = await _playerRepository.GetByEspnIdAsync(extIdStr);
                        if (player != null) playerId = player.Id;
                    }

                    list.Add(new StatLeader {
                        Rank = rank++,
                        ExternalId = extId,
                        PlayerName = name,
                        Team = teamAbbr,
                        Value = Math.Round(val, 1),
                        PlayerId = playerId
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error mapping stats for {Category}", categoryName);
            }
            return list;
        }

        private System.Text.Json.JsonElement GetProperty(System.Text.Json.JsonElement? element, string name)
        {
            if (element == null || element.Value.ValueKind == System.Text.Json.JsonValueKind.Undefined) return default;
            if (element.Value.TryGetProperty(name, out var val)) return val;
            
            var pascal = char.ToUpper(name[0]) + name.Substring(1);
            if (element.Value.TryGetProperty(pascal, out val)) return val;
            
            return default;
        }


    }
}
