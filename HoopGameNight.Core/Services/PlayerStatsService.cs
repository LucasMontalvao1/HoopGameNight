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
using System.Threading.Tasks;

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
        private readonly ILogger<PlayerStatsService> _logger;

        private const int CACHE_MINUTES = 30;

        public PlayerStatsService(
            IPlayerStatsRepository statsRepository,
            IPlayerRepository playerRepository,
            IGameRepository gameRepository,
            ITeamRepository teamRepository,
            IEspnApiService espnApiService,
            IGameService gameService,
            IMemoryCache cache,
            ILogger<PlayerStatsService> logger)
        {
            _statsRepository = statsRepository;
            _playerRepository = playerRepository;
            _gameRepository = gameRepository;
            _teamRepository = teamRepository;
            _espnApiService = espnApiService;
            _gameService = gameService;
            _cache = cache;
            _logger = logger;
        }

        public async Task<PlayerSeasonStatsResponse?> GetPlayerSeasonStatsAsync(int playerId, int season)
        {
             var stats = await _statsRepository.GetSeasonStatsFromViewAsync(playerId, season);
             
             if (stats == null)
             {
                 _logger.LogInformation("Stats da temporada não encontrados no banco. Iniciando Sync para Player {PlayerId} Season {Season}", playerId, season);
                 await SyncPlayerSeasonStatsAsync(playerId, season);
                 stats = await _statsRepository.GetSeasonStatsFromViewAsync(playerId, season);
             }
             return stats;
        }

        public async Task<PlayerGameStatsDetailedResponse?> GetPlayerGameStatsAsync(int playerId, int gameId)
        {
            var cacheKey = $"game_stats_{playerId}_{gameId}";
            if (_cache.TryGetValue(cacheKey, out PlayerGameStatsDetailedResponse? cached)) return cached;

            var stats = await _statsRepository.GetPlayerGameStatsDetailedAsync(playerId, gameId);
            if (stats == null)
            {
               _logger.LogInformation("Stats do jogo não encontrados no banco. Iniciando Sync para Player {PlayerId} Game {GameId}", playerId, gameId);
               await SyncPlayerGameStatsAsync(playerId, gameId);
               stats = await _statsRepository.GetPlayerGameStatsDetailedAsync(playerId, gameId);
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

                var key = $"gamelog_v2_{playerId}"; // Mudei versão do cache

                // VERIFICAR CACHE PRIMEIRO
                if (_cache.TryGetValue(key, out PlayerGamelogResponse? cachedResponse))
                {
                    _logger.LogInformation("Gamelog retornado do cache para Player {PlayerId}", playerId);
                    return cachedResponse;
                }

                // BUSCAR DA ESPN
                _logger.LogInformation("Buscando gamelog da ESPN para Player {PlayerId}", playerId);
                var espnData = await _espnApiService.GetPlayerGamelogAsync(player.EspnId!);
                if (espnData == null) return null;

                var response = MapEspnGamelogToResponse(espnData, playerId, player.FullName);

                // --- PERSISTÊNCIA SÍNCRONA ---
                if (response != null && response.Games.Any())
                {
                    var updatedSeasons = new HashSet<(int Year, int Type)>();

                    foreach (var g in response.Games)
                    {
                        try
                        {
                            // Verificar se o jogo existe usando External ID (ESPN ID)
                            var gameInDb = await _gameRepository.GetByExternalIdAsync(g.GameId.ToString());
                            
                            if (gameInDb == null)
                            {
                                _logger.LogInformation("Gamelog Sync: Jogo {GameId} não encontrado. Sincronizando...", g.GameId);
                                await _gameService.SyncGameByIdAsync(g.GameId.ToString());
                                // Tentar buscar novamente após sync
                                gameInDb = await _gameRepository.GetByExternalIdAsync(g.GameId.ToString());
                            }

                            if (gameInDb == null)
                            {
                                _logger.LogWarning("Gamelog Sync: Falha ao garantir jogo {GameId}. Pulando...", g.GameId);
                                continue;
                            }

                            // Usar o ID interno do banco para a FK
                            int internalGameId = gameInDb.Id;

                            // Parsear minutagem
                            int minutes = 0;
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

                            // Parsear Field Goals (formato: "X-Y" ou "X/Y")
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

                            // Parsear Three Pointers
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

                            // Parsear Free Throws
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

                            var statsEntity = new PlayerGameStats
                            {
                                PlayerId = playerId,
                                GameId = internalGameId, // USA ID INTERNO
                                TeamId = player.TeamId ?? 0,
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
                                PlusMinus = g.PlusMinus
                            };

                            await _statsRepository.UpsertGameStatsAsync(statsEntity);
                            _logger.LogInformation("Gamelog: Estatísticas persistidas para o Game {GameId} (External: {Ext})", internalGameId, g.GameId);

                            updatedSeasons.Add((gameInDb.Season, gameInDb.PostSeason ? 3 : 2));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Falha na persistência de estatísticas (gamelog) para o Player {PlayerId}, Game {GameId}", playerId, g.GameId);
                        }
                    }

                    // --- AGREGAÇÃO DE TEMPORADA ---
                    foreach (var season in updatedSeasons)
                    {
                        try
                        {
                            _logger.LogInformation("📊 Agregando stats de temporada: Player {PlayerId}, Season {Season}, Type {Type}",
                                playerId, season.Year, season.Type);
                            await _statsRepository.AggregateSeasonStatsAsync(playerId, season.Year, season.Type);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Erro ao agregar temporada {Season}/{Type} para Player {PlayerId}",
                                season.Year, season.Type, playerId);
                        }
                    }
                }

                // CACHEAR APENAS APÓS PERSISTIR TUDO
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

        public async Task<IEnumerable<PlayerGameStatsDetailedResponse>> GetPlayerRecentGamesAsync(int playerId, int limit = 5)
        {
            var stats = await _statsRepository.GetPlayerRecentGamesDetailedAsync(playerId, limit);
            if (stats == null || !stats.Any())
            {
                _logger.LogInformation("Nenhum dado recente no banco para Player {PlayerId}. Iniciando Sync...", playerId);
                // Temporada atual (2025-26 -> 2026)
                var currentSeason = DateTime.Now.Month >= 10 ? DateTime.Now.Year + 1 : DateTime.Now.Year;
                await SyncPlayerSeasonStatsAsync(playerId, currentSeason);
                stats = await _statsRepository.GetPlayerRecentGamesDetailedAsync(playerId, limit);
            }
            return stats;
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

                // Determinar o TeamId correto para o jogador neste jogo
                int actualTeamId = player.TeamId ?? 0;
                if (game.HomeTeamId > 0 && game.VisitorTeamId > 0)
                {
                    // Se o jogador não estiver no time atual (troca?), tentamos inferir do jogo
                    // Por padrão usamos o player.TeamId se ele for um dos times do jogo
                    if (player.TeamId != game.HomeTeamId && player.TeamId != game.VisitorTeamId)
                    {
                        // TODO: Logica mais avançada para trocas históricas
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

                // NBA Season dates: October to July of next year
                var startDate = new DateTime(season - 1, 10, 1);
                var endDate = new DateTime(season, 7, 30);
                
                var games = await _gameRepository.GetByTeamAsync(player.TeamId.Value, startDate, endDate);
                
                if (games == null || !games.Any())
                {
                    _logger.LogWarning("SyncSeason: Nenhum jogo encontrado no banco para o time {TeamId} na season {Season}. Certifique-se de que os jogos foram sincronizados.", player.TeamId, season);
                    return false;
                }

                _logger.LogInformation("SyncSeason: Encontrados {Count} jogos para sincronizar.", games.Count());

                int syncedCount = 0;
                foreach(var gm in games)
                {
                    // Verifica se o jogo já aconteceu ou está ao vivo (Status Final ou InProgress)
                    // Se o jogo for futuro ou cancelado, pula.
                    if (gm.Status == GameStatus.Scheduled || gm.Status == GameStatus.Postponed || gm.Status == GameStatus.Cancelled) continue;

                    if(await SyncPlayerGameStatsAsync(playerId, gm.Id))
                    {
                        syncedCount++;
                    }
                }
                _logger.LogInformation("SyncSeason: Finalizado. {Synced} jogos sincronizados.", syncedCount);
                return syncedCount > 0;
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

                var key = $"career_v24_{playerId}"; // Bumped version for playoff persistence fix

                // VERIFICAR CACHE
                if (_cache.TryGetValue(key, out PlayerCareerResponse? cachedResponse))
                {
                    _logger.LogInformation("Career retornado do cache para Player {PlayerId}", playerId);
                    return cachedResponse;
                }

                // BUSCAR DA ESPN
                _logger.LogInformation("Buscando career da ESPN para Player {PlayerId}", playerId);
                var espnData = await _espnApiService.GetPlayerCareerStatsAsync(player.EspnId!);
                if (espnData == null || !espnData.Any()) return null;

                // Resolve team names and map ESPN IDs to Database IDs
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
                        else if (extId == 9) // ESPN's Golden State ID is 9
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

                    if (string.IsNullOrEmpty(st.Team.DisplayName) && player.TeamId.HasValue)
                    {
                        if (teamMapById.TryGetValue(player.TeamId.Value, out var playerTeam))
                        {
                            st.Team.Id = playerTeam.Id.ToString();
                            st.Team.DisplayName = playerTeam.FullName;
                            st.Team.Abbreviation = playerTeam.Abbreviation;
                        }
                    }
                }

                var response = MapEspnCareerToResponse(espnData, playerId, player.FullName);

                // --- PERSISTÊNCIA ---
                if (response?.CareerTotals != null)
                {
                    // 1. SALVAR CAREER TOTALS
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

                    // 2. SALVAR CADA TEMPORADA INDIVIDUAL NA player_season_stats
                    // Use the deduplicated season stats from the response instead of raw ESPN data
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

                // CACHEAR APÓS PERSISTIR
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

        // === MAPPER ===

        private PlayerSeasonStats? MapEspnSeasonStatsToEntity(EspnPlayerStatsDto seasonData, int playerId)
        {
            if (seasonData == null || seasonData.Splits?.Categories == null) return null;

            var entity = new PlayerSeasonStats
            {
                PlayerId = playerId,
                Season = seasonData.Season?.Year ?? 0,
                SeasonTypeId = seasonData.SeasonTypeId,
                TeamId = int.TryParse(seasonData.Team?.Id, out var teamId) && teamId > 0 ? teamId : null
            };

            foreach (var cat in seasonData.Splits.Categories)
            {
                if (cat.Stats == null) continue;

                foreach (var s in cat.Stats)
                {
                    var key = s.Name?.ToLower() ?? "";
                    var val = s.Value;

                    switch (key)
                    {
                        case "gamesplayed": case "gp": entity.GamesPlayed = (int)val; break;
                        case "gamesstarted": case "gs": entity.GamesStarted = (int)val; break;
                        case "minutes": case "min": entity.MinutesPlayed = (decimal)val; break;
                        case "points": case "pts": entity.Points = (int)val; break;
                        case "fieldgoalsmade": case "fgm": entity.FieldGoalsMade = (int)val; break;
                        case "fieldgoalsattempted": case "fga": entity.FieldGoalsAttempted = (int)val; break;
                        case "fieldgoalpercentage": case "fg%": entity.FieldGoalPercentage = val > 0 ? Math.Min((decimal)val, 99.999m) : null; break;
                        case "threepointersmade": case "3pm": case "threepointfieldgoalsmade": entity.ThreePointersMade = (int)val; break;
                        case "threepointersattempted": case "3pa": case "threepointfieldgoalsattempted": entity.ThreePointersAttempted = (int)val; break;
                        case "threepointpercentage": case "3p%": case "threepointfieldgoalpercentage": entity.ThreePointPercentage = val > 0 ? Math.Min((decimal)val, 99.999m) : null; break;
                        case "freethrowsmade": case "ftm": entity.FreeThrowsMade = (int)val; break;
                        case "freethrowsattempted": case "fta": entity.FreeThrowsAttempted = (int)val; break;
                        case "freethrowpercentage": case "ft%": entity.FreeThrowPercentage = val > 0 ? Math.Min((decimal)val, 99.999m) : null; break;
                        case "offensiverebounds": case "oreb": entity.OffensiveRebounds = (int)val; break;
                        case "defensiverebounds": case "dreb": entity.DefensiveRebounds = (int)val; break;
                        case "totalrebounds": case "reb": entity.TotalRebounds = (int)val; break;
                        case "assists": case "ast": entity.Assists = (int)val; break;
                        case "steals": case "stl": entity.Steals = (int)val; break;
                        case "blocks": case "blk": entity.Blocks = (int)val; break;
                        case "turnovers": case "to": entity.Turnovers = (int)val; break;
                        case "personalfouls": case "pf": entity.PersonalFouls = (int)val; break;
                        case "avgpoints": case "ppg": entity.AvgPoints = (decimal)val; break;
                        case "avgrebounds": case "rpg": entity.AvgRebounds = (decimal)val; break;
                        case "avgassists": case "apg": entity.AvgAssists = (decimal)val; break;
                        case "avgminutes": case "mpg": entity.AvgMinutes = (decimal)val; break;
                    }
                }
            }

            return entity;
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
                                case "threepointfieldgoalsmade": case "3pm": case "threepointfieldgoals": stats.ThreePointersMade = (int)val; break;
                                case "threepointfieldgoalsattempted": case "3pa": stats.ThreePointersAttempted = (int)val; break;
                                case "freethrowsmade": case "ftm": stats.FreeThrowsMade = (int)val; break;
                                case "freethrowsattempted": case "fta": stats.FreeThrowsAttempted = (int)val; break;
                                case "plusminus": case "+/-": stats.PlusMinus = (int)val; break;
                            }
                        } catch { /* Ignora erro de cast */ }
                    }
                }
            }
            return stats;
        }

        // === NEW MAPPERS FOR SIMPLIFIED DTOs ===

        private PlayerGamelogResponse? MapEspnGamelogToResponse(EspnPlayerGamelogDto espnData, int playerId, string playerName)
        {
            try
            {
                var response = new PlayerGamelogResponse
                {
                    PlayerId = playerId,
                    PlayerName = playerName,
                    Season = DateTime.Now.Year // TODO: Extract from ESPN data if available
                };

                var games = new List<PlayerRecentGameResponse>();

                // ESPN gamelog structure: 
                // 1. SeasonTypes (Standard, etc.) -> Categories (By Month etc.) -> Events (Games with Stats)
                // 2. SeasonTypes -> Events (Flat list)
                // 3. Events (Root dictionary, glossary only, no stats)
                
                IEnumerable<EspnGamelogEventDto> events = Enumerable.Empty<EspnGamelogEventDto>();
                
                if (espnData.SeasonTypes != null && espnData.SeasonTypes.Any())
                {
                     // Flatten all events from all season types and categories
                     events = espnData.SeasonTypes.SelectMany(st => 
                        (st.Categories?.SelectMany(c => c.Events ?? Enumerable.Empty<EspnGamelogEventDto>()) ?? Enumerable.Empty<EspnGamelogEventDto>())
                        .Concat(st.Events ?? Enumerable.Empty<EspnGamelogEventDto>())
                     );
                }


                // Filter out events without stats
                Console.WriteLine($"DEBUG: Found {events.Count()} raw events.");
                var validEvents = events.Where(evt => evt.Stats != null && evt.Stats.Any()).DistinctBy(e => e.EventId);
                Console.WriteLine($"DEBUG: Found {validEvents.Count()} valid events with stats.");
                
                foreach (var evt in validEvents)
                {
                    if (evt?.Stats == null || !evt.Stats.Any()) continue;
                    
                    // Identify the opponent from metadata
                    var opponentName = "Unknown";
                    var opponentId = "";
                    string? gameDateStr = null;
                    string? gameResult = null;
                    bool isHome = false;

                    if (espnData.Events != null && espnData.Events.TryGetValue(evt.EventId ?? "", out var meta))
                    {
                        var opt = GetProperty(meta, "opponent");
                        var tpt = GetProperty(meta, "team");
                        var htId = GetProperty(meta, "homeTeamId").GetString();
                        
                        opponentName = GetProperty(opt, "displayName").GetString() ?? "Unknown";
                        opponentId = GetProperty(opt, "id").GetString() ?? "";
                        gameDateStr = GetProperty(meta, "gameDate").GetString();
                        gameResult = GetProperty(meta, "gameResult").GetString();
                        
                        var pTeamId = GetProperty(tpt, "id").GetString();
                        isHome = pTeamId != null && htId != null && pTeamId == htId;
                    }

                    try
                    {
                        var game = new PlayerRecentGameResponse
                        {
                            GameId = int.TryParse(evt.EventId ?? "0", out var gId) ? gId : 0,
                            GameDate = gameDateStr != null && DateTime.TryParse(gameDateStr, out var dt) ? dt : DateTime.MinValue,
                            Opponent = opponentName ?? "Unknown",
                            Result = gameResult ?? "",
                            IsHome = isHome
                        };

                        // Final verified indices for NBA Gamelog v3:
                        // 13: PTS, 1: FG, 3: 3PT, 5: FT, 7: REB, 8: AST, 9: BLK, 10: STL, 0: MIN
                        if (evt.Stats.Count > 13) game.Points = int.TryParse(evt.Stats[13], out var pts) ? pts : 0;
                        if (evt.Stats.Count > 7) game.Rebounds = int.TryParse(evt.Stats[7], out var reb) ? reb : 0;
                        if (evt.Stats.Count > 8) game.Assists = int.TryParse(evt.Stats[8], out var ast) ? ast : 0;
                        if (evt.Stats.Count > 10) game.Steals = int.TryParse(evt.Stats[10], out var stl) ? stl : 0;
                        if (evt.Stats.Count > 9) game.Blocks = int.TryParse(evt.Stats[9], out var blk) ? blk : 0;
                        if (evt.Stats.Count > 0) game.Minutes = evt.Stats[0] ?? "0";
                        if (evt.Stats.Count > 1) game.FieldGoals = evt.Stats[1] ?? "";
                        if (evt.Stats.Count > 3) game.ThreePointers = evt.Stats[3] ?? "";
                        if (evt.Stats.Count > 5) game.FreeThrows = evt.Stats[5] ?? "";

                        games.Add(game);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error parsing gamelog event for player {PlayerId}", playerId);
                    }
                }

                response.Games = games;
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mapping gamelog for player {PlayerId}", playerId);
                return null;
            }
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

                                // Defensive parsing of stats
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
                var uniqueSeasons = new Dictionary<string, PlayerSeasonStatsResponse>(); // Key: "season_type"
                
                int totalGames = 0, totalPoints = 0, totalRebs = 0, totalAsts = 0;
                decimal totalPPG = 0, totalRPG = 0, totalAPG = 0, totalFG = 0;
                int highPts = 0, highRebs = 0, highAsts = 0;
                int seasonCount = 0;

                foreach (var seasonData in espnData)
                {
                    if (seasonData?.Splits?.Categories == null) continue;

                    try
                    {
                        var seasonStats = new PlayerSeasonStatsResponse
                        {
                            Season = seasonData.Season?.Year ?? 0,
                            SeasonName = seasonData.Season?.Year > 1900 ? $"{seasonData.Season.Year - 1}/{seasonData.Season.Year}" : "Unknown",
                            SeasonType = seasonData.SeasonTypeId, // Usar SeasonTypeId mapeado
                            TeamId = int.TryParse(seasonData.Team?.Id, out var teamId) && teamId > 0 ? teamId : 0,
                            TeamName = seasonData.Team?.DisplayName ?? seasonData.Team?.Abbreviation ?? "Unknown"
                        };

                        // Parse stats from categories
                        foreach (var category in seasonData.Splits.Categories)
                        {
                            if (category?.Stats == null) continue;

                            foreach (var stat in category.Stats)
                            {
                                var key = stat.Name?.ToLower() ?? "";
                                var val = stat.Value;

                                try
                                {
                                    switch (key)
                                    {
                                        case "gamesplayed": case "gp": seasonStats.GamesPlayed = (int)val; break;
                                        case "gamesstarted": case "gs": seasonStats.GamesStarted = (int)val; break;
                                        case "points": case "pts": case "totalpoints": seasonStats.TotalPoints = (int)val; break;
                                        case "avgpoints": case "ppg": seasonStats.PPG = (decimal)val; break;
                                        case "rebounds": case "reb": case "totalrebounds": seasonStats.TotalRebounds = (int)val; break;
                                        case "avgrebound": case "avgrebounds": case "avgtotalrebounds": case "rpg": seasonStats.RPG = (decimal)val; break;
                                        case "assists": case "ast": case "totalassists": seasonStats.TotalAssists = (int)val; break;
                                        case "avgassists": case "apg": seasonStats.APG = (decimal)val; break;
                                        case "steals": case "stl": case "totalsteals": seasonStats.Steals = (int)val; break;
                                        case "avgsteals": case "spg": seasonStats.SPG = (decimal)val; break;
                                        case "blocks": case "blk": case "totalblocks": seasonStats.Blocks = (int)val; break;
                                        case "avgblocks": case "bpg": seasonStats.BPG = (decimal)val; break;
                                        case "turnovers": case "to": case "totalturnovers": seasonStats.Turnovers = (int)val; break;
                                        case "personalfouls": case "pf": case "totalpersonalfouls": seasonStats.PersonalFouls = (int)val; break;
                                        case "fieldgoalsmade": case "fgm": case "totalfieldgoalsmade": seasonStats.FieldGoalsMade = (int)val; break;
                                        case "fieldgoalsattempted": case "fga": case "totalfieldgoalsattempted": seasonStats.FieldGoalsAttempted = (int)val; break;
                                        case "threepointfieldgoalsmade": case "3pm": case "totalthreepointfieldgoalsmade": case "threepointersmade": seasonStats.ThreePointersMade = (int)val; break;
                                        case "threepointfieldgoalsattempted": case "3pa": case "totalthreepointfieldgoalsattempted": case "threepointersattempted": seasonStats.ThreePointersAttempted = (int)val; break;
                                        case "freethrowsmade": case "ftm": case "totalfreethrowsmade": seasonStats.FreeThrowsMade = (int)val; break;
                                        case "freethrowsattempted": case "fta": case "totalfreethrowsattempted": seasonStats.FreeThrowsAttempted = (int)val; break;
                                        case "offensiverebounds": case "oreb": case "totaloffensiverebounds": seasonStats.OffensiveRebounds = (int)val; break;
                                        case "defensiverebounds": case "dreb": case "totaldefensiverebounds": seasonStats.DefensiveRebounds = (int)val; break;
                                        case "totalminutes": case "min": case "minutes": seasonStats.MinutesPlayed = (decimal)val; break;
                                        case "avgminutes": case "averageminutes": case "mpg": seasonStats.MPG = (decimal)val; break;
                                        case "fieldgoalpercentage": case "fg%": case "fieldgoalpct": case "fgpct": seasonStats.FGPercentage = (decimal)val; break;
                                        case "threepointpercentage": case "3p%": case "threepointfieldgoalpct": case "3ptpct": case "threepointpct": case "threepointfieldgoalpercentage": seasonStats.ThreePointPercentage = (decimal)val; break;
                                        case "freethrowpercentage": case "ft%": case "freethrowpct": case "ftpct": seasonStats.FTPercentage = (decimal)val; break;
                                    }
                                }
                                catch { /* Ignore individual stat parsing errors */ }
                            }
                        }

                        if (seasonStats.GamesPlayed > 0)
                        {
                            // Create unique key: season + seasonType + teamId
                            var uniqueKey = $"{seasonStats.Season}_{seasonStats.SeasonType}_{seasonStats.TeamId}";
                            
                            // Only add if not already present (deduplication)
                            if (!uniqueSeasons.ContainsKey(uniqueKey))
                            {
                                uniqueSeasons[uniqueKey] = seasonStats;
                                _logger.LogDebug("Adding season to list: Player {PlayerId}, Season {Season}, Type {Type}, Team {TeamId}", 
                                    playerId, seasonStats.Season, seasonStats.SeasonType, seasonStats.TeamId);
                                seasonsList.Add(seasonStats);
                                
                                // Agregação de carreira (only regular season)
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
                                
                                // Recordes (podem ser de qualquer tipo de season)
                                // NOTE: These represent SEASON highs, not single-game records
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

                // Calculate career totals
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

        private DateTime ParseEspnDate(string? dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr)) return DateTime.MinValue;

            try
            {
                var culture = System.Globalization.CultureInfo.InvariantCulture;
                // Base formats without year
                var baseFormats = new[] { "ddd M/d", "ddd MM/dd" };
                
                // Try to find a year where the Day of Week matches the Date
                // We check: Next Year, Current Year, Last Year, Year-2
                // This handles the "Season 2024/2025" vs "System Time 2026" mismatch perfectly,
                // because Jan 1 2025 is Wednesday (matches "Wed 1/1") while Jan 1 2026 is Thursday.
                var now = DateTime.Now;
                var currentYear = now.Year;
                var yearsToTry = new[] { currentYear + 1, currentYear, currentYear - 1, currentYear - 2 };

                foreach (var year in yearsToTry)
                {
                    // Construct string with Year, e.g. "Wed 1/1 2025"
                    var input = $"{dateStr} {year}";
                    var formatsWithYear = baseFormats.Select(f => $"{f} yyyy").ToArray();

                    if (DateTime.TryParseExact(input, formatsWithYear, culture, System.Globalization.DateTimeStyles.None, out var dt))
                    {
                        return dt;
                    }
                }

                _logger.LogDebug("Failed to find matching year for ESPN date: {DateStr}", dateStr);
                return DateTime.MinValue;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        private System.Text.Json.JsonElement GetProperty(System.Text.Json.JsonElement? element, string name)
        {
            if (element == null || element.Value.ValueKind == System.Text.Json.JsonValueKind.Undefined) return default;
            if (element.Value.TryGetProperty(name, out var val)) return val;
            
            // Try PascalCase
            var pascal = char.ToUpper(name[0]) + name.Substring(1);
            if (element.Value.TryGetProperty(pascal, out val)) return val;
            
            return default;
        }
    }
}
