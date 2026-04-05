using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Core.Models.Entities;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Core.Services
{
    public class PlayerStatsSyncService : IPlayerStatsSyncService
    {
        private readonly IPlayerStatsRepository _statsRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IGameRepository _gameRepository;
        private readonly IEspnApiService _espnApiService;
        private readonly IEspnParser _espnParser;
        private readonly ITeamService _teamService;
        private readonly ILogger<PlayerStatsSyncService> _logger;

        public PlayerStatsSyncService(
            IPlayerStatsRepository statsRepository,
            IPlayerRepository playerRepository,
            IGameRepository gameRepository,
            IEspnApiService espnApiService,
            IEspnParser espnParser,
            ITeamService teamService,
            ILogger<PlayerStatsSyncService> logger)
        {
            _statsRepository = statsRepository;
            _playerRepository = playerRepository;
            _gameRepository = gameRepository;
            _espnApiService = espnApiService;
            _espnParser = espnParser;
            _teamService = teamService;
            _logger = logger;
        }

        public async Task<bool> SyncPlayerGameStatsAsync(int playerId, int gameId)
        {
            try
            {
                var player = await _playerRepository.GetByIdAsync(playerId);
                var game = await _gameRepository.GetByIdAsync(gameId);

                if (player == null || game == null || string.IsNullOrEmpty(player.EspnId) || string.IsNullOrEmpty(game.ExternalId))
                    return false;

                var espnStats = await _espnApiService.GetPlayerGameStatsAsync(player.EspnId, game.ExternalId);
                if (espnStats?.Splits?.Categories == null) return false;

                var gameStats = new PlayerGameStats
                {
                    PlayerId = playerId,
                    GameId = gameId,
                    TeamId = player.TeamId ?? 0
                };

                foreach (var category in espnStats.Splits.Categories)
                {
                    if (category.Stats == null) continue;

                    foreach (var stat in category.Stats)
                    {
                        UpdateGameStats(gameStats, stat.Name?.ToLowerInvariant(), stat.DisplayValue, stat.Value);
                    }
                }

                await _statsRepository.UpsertGameStatsAsync(gameStats);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing player {PlayerId} stats for game {GameId}", playerId, gameId);
                return false;
            }
        }

        private void UpdateGameStats(PlayerGameStats stats, string? name, string? displayValue, double value)
        {
            switch (name)
            {
                case "minutes":
                case "min":
                    if (!string.IsNullOrEmpty(displayValue) && displayValue.Contains(":"))
                    {
                        var parts = displayValue.Split(':');
                        stats.MinutesPlayed = _espnParser.SafeParseInt(parts[0]);
                        stats.SecondsPlayed = parts.Length > 1 ? _espnParser.SafeParseInt(parts[1]) : 0;
                    }
                    else
                    {
                        stats.MinutesPlayed = (int)value;
                    }
                    break;

                case "points": case "pts": case "totalpoints": stats.Points = (int)value; break;
                case "fieldgoalsmade": case "fgm": stats.FieldGoalsMade = (int)value; break;
                case "fieldgoalsattempted": case "fga": stats.FieldGoalsAttempted = (int)value; break;
                case "threepointfieldgoalsmade": case "3pm": case "3ptm": stats.ThreePointersMade = (int)value; break;
                case "threepointfieldgoalsattempted": case "3pa": case "3pta": stats.ThreePointersAttempted = (int)value; break;
                case "freethrowsmade": case "ftm": stats.FreeThrowsMade = (int)value; break;
                case "freethrowsattempted": case "fta": stats.FreeThrowsAttempted = (int)value; break;
                case "offensiverebounds": case "oreb": stats.OffensiveRebounds = (int)value; break;
                case "defensiverebounds": case "dreb": stats.DefensiveRebounds = (int)value; break;
                case "totalrebounds": case "reb": stats.TotalRebounds = (int)value; break;
                case "assists": case "ast": stats.Assists = (int)value; break;
                case "steals": case "stl": stats.Steals = (int)value; break;
                case "blocks": case "blk": stats.Blocks = (int)value; break;
                case "turnovers": case "to": stats.Turnovers = (int)value; break;
                case "fouls": case "pf": stats.PersonalFouls = (int)value; break;
                case "plusminus": case "+/-": stats.PlusMinus = (int)value; break;
            }
        }

        public async Task<bool> SyncGameStatsForAllPlayersInGameAsync(int gameId)
        {
            try
            {
                var game = await _gameRepository.GetByIdAsync(gameId);
                if (game == null || string.IsNullOrEmpty(game.ExternalId)) return false;

                _logger.LogInformation("Syncing all player stats for game {GameId} ({ExternalId})", gameId, game.ExternalId);

                var boxscore = await _espnApiService.GetGameBoxscoreAsync(game.ExternalId);
                if (boxscore?.Players == null) return false;

                var statsToUpsert = new List<PlayerGameStats>();

                foreach (var teamGroup in boxscore.Players)
                {
                    if (teamGroup.Statistics == null) continue;

                    foreach (var statGroup in teamGroup.Statistics)
                    {
                        if (statGroup.Athletes == null || statGroup.Keys == null) continue;

                        foreach (var athleteEntry in statGroup.Athletes)
                        {
                            if (athleteEntry.Athlete == null || string.IsNullOrEmpty(athleteEntry.Athlete.Id)) continue;

                            var player = await _playerRepository.GetByEspnIdAsync(athleteEntry.Athlete.Id);
                            if (player == null) continue;

                            var gameStats = new PlayerGameStats
                            {
                                PlayerId = player.Id,
                                GameId = gameId,
                                TeamId = player.TeamId ?? 0
                            };

                            for (int i = 0; i < statGroup.Keys.Count && i < athleteEntry.Stats.Count; i++)
                            {
                                var key = statGroup.Keys[i];
                                var valStr = athleteEntry.Stats[i];
                                UpdateGameStatsFromKey(gameStats, key, valStr);
                            }

                            statsToUpsert.Add(gameStats);
                        }
                    }
                }

                if (statsToUpsert.Any())
                {
                    await _statsRepository.BulkUpsertGameStatsAsync(statsToUpsert);
                    _logger.LogInformation("Successfully synced {Count} players for game {GameId}", statsToUpsert.Count, gameId);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing all players for game {GameId}", gameId);
                return false;
            }
        }

        private void UpdateGameStatsFromKey(PlayerGameStats stats, string key, string valStr)
        {
            switch (key.ToLowerInvariant())
            {
                case "min":
                case "minutes":
                case "avgminutes":
                case "mpg":
                    if (valStr.Contains(":"))
                    {
                        var parts = valStr.Split(':');
                        stats.MinutesPlayed = _espnParser.SafeParseInt(parts[0]);
                        stats.SecondsPlayed = parts.Length > 1 ? _espnParser.SafeParseInt(parts[1]) : 0;
                    }
                    else
                    {
                        stats.MinutesPlayed = _espnParser.SafeParseInt(valStr);
                    }
                    break;

                case "pts":
                case "points":
                case "totalpoints":
                    stats.Points = _espnParser.SafeParseInt(valStr); break;

                case "fg":
                case "fgm-fga":
                case "fieldgoals":
                case "fieldgoalsmade-fieldgoalsattempted":
                    if (valStr.Contains("-") || valStr.Contains("/")) {
                        var fParts = valStr.Split('-', '/');
                        stats.FieldGoalsMade = _espnParser.SafeParseInt(fParts[0]);
                        stats.FieldGoalsAttempted = fParts.Length > 1 ? _espnParser.SafeParseInt(fParts[1]) : 0;
                    }
                    break;
                case "fgm":
                case "fieldgoalsmade": stats.FieldGoalsMade = _espnParser.SafeParseInt(valStr); break;
                case "fga":
                case "fieldgoalsattempted": stats.FieldGoalsAttempted = _espnParser.SafeParseInt(valStr); break;

                case "3pt":
                case "3pm-3pa":
                case "threepointers":
                case "3p":
                case "threepointfieldgoalsmade-threepointfieldgoalsattempted":
                    if (valStr.Contains("-") || valStr.Contains("/")) {
                        var tParts = valStr.Split('-', '/');
                        stats.ThreePointersMade = _espnParser.SafeParseInt(tParts[0]);
                        stats.ThreePointersAttempted = tParts.Length > 1 ? _espnParser.SafeParseInt(tParts[1]) : 0;
                    }
                    break;
                case "3pm":
                case "threepointersmade":
                case "threepointfieldgoalsmade": stats.ThreePointersMade = _espnParser.SafeParseInt(valStr); break;
                case "3pa":
                case "threepointersattempted":
                case "threepointfieldgoalsattempted": stats.ThreePointersAttempted = _espnParser.SafeParseInt(valStr); break;

                case "ft":
                case "ftm-fta":
                case "freethrows":
                case "freethrowsmade-freethrowsattempted":
                    if (valStr.Contains("-") || valStr.Contains("/")) {
                        var ftParts = valStr.Split('-', '/');
                        stats.FreeThrowsMade = _espnParser.SafeParseInt(ftParts[0]);
                        stats.FreeThrowsAttempted = ftParts.Length > 1 ? _espnParser.SafeParseInt(ftParts[1]) : 0;
                    }
                    break;
                case "ftm":
                case "freethrowsmade": stats.FreeThrowsMade = _espnParser.SafeParseInt(valStr); break;
                case "fta":
                case "freethrowsattempted": stats.FreeThrowsAttempted = _espnParser.SafeParseInt(valStr); break;

                case "oreb":
                case "offensiverebounds": stats.OffensiveRebounds = _espnParser.SafeParseInt(valStr); break;
                case "dreb":
                case "defensiverebounds": stats.DefensiveRebounds = _espnParser.SafeParseInt(valStr); break;
                case "reb":
                case "totalrebounds":
                case "rebounds": stats.TotalRebounds = _espnParser.SafeParseInt(valStr); break;

                case "ast":
                case "assists":
                case "totalassists": stats.Assists = _espnParser.SafeParseInt(valStr); break;

                case "stl":
                case "steals":
                case "totalsteals": stats.Steals = _espnParser.SafeParseInt(valStr); break;

                case "blk":
                case "blocks":
                case "totalblocks": stats.Blocks = _espnParser.SafeParseInt(valStr); break;

                case "to":
                case "tov":
                case "turnovers":
                case "totalturnovers": stats.Turnovers = _espnParser.SafeParseInt(valStr); break;

                case "pf":
                case "fouls":
                case "personalfouls":
                case "totalpersonalfouls": stats.PersonalFouls = _espnParser.SafeParseInt(valStr); break;

                case "+/-":
                case "pm":
                case "plusminus": stats.PlusMinus = _espnParser.SafeParseInt(valStr); break;

                default:
                    // Log nomes desconhecidos para facilitar o diagnóstico
                    _logger.LogDebug("Gamelog Sync: Coluna ESPN desconhecida '{Key}' = '{Value}' — ignorada", key, valStr);
                    break;
            }
        }
        public async Task<int> SyncPlayerCareerHistoryAsync(int playerId, int startYear, int endYear)
        {
            try
            {
                var player = await _playerRepository.GetByIdAsync(playerId);
                if (player == null || string.IsNullOrEmpty(player.EspnId)) return 0;

                _logger.LogInformation("Syncing career history for player {PlayerName} ({PlayerId})", player.FullName, playerId);

                var careerStats = await _espnApiService.GetPlayerCareerStatsAsync(player.EspnId);
                if (careerStats == null || !careerStats.Any()) return 0;

                var statsToUpsert = new List<PlayerSeasonStats>();
                foreach (var espnSeasonStats in careerStats)
                {
                    if (espnSeasonStats.Season == null) continue;
                    
                    var year = espnSeasonStats.Season.Year;
                    if (year < startYear || year > endYear) continue;

                    var seasonStats = _espnParser.ParseSeasonStats(espnSeasonStats, playerId);
                    statsToUpsert.Add(seasonStats);
                }

                if (statsToUpsert.Any())
                {
                    await _statsRepository.BulkUpsertSeasonStatsAsync(statsToUpsert);
                    _logger.LogInformation("Successfully synced {Count} seasons for player {PlayerId}", statsToUpsert.Count, playerId);
                }

                return statsToUpsert.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing career history for player {PlayerId}", playerId);
                return 0;
            }
        }

        public async Task<bool> SyncPlayerSeasonStatsAsync(int playerId, int season)
        {
            _logger.LogInformation("Syncing season stats for player {PlayerId} season {Season}", playerId, season);
            return await SyncPlayerCareerHistoryAsync(playerId, season, season) > 0;
        }

        public async Task<bool> SyncPlayerRecentGamesAsync(int playerId, int numberOfGames = 20)
        {
            try
            {
                var player = await _playerRepository.GetByIdAsync(playerId);
                if (player == null || string.IsNullOrEmpty(player.EspnId)) return false;

                _logger.LogInformation("Gamelog Sync: Iniciando para player {PlayerName} (id={PlayerId}, espnId={EspnId})", player.FullName, playerId, player.EspnId);
                var gamelog = await _espnApiService.GetPlayerGamelogAsync(player.EspnId);

                if (gamelog == null)
                {
                    _logger.LogWarning("Gamelog Sync: ESPN retornou null para player {PlayerId}", playerId);
                    return false;
                }

                // Build stat column names from gamelog root-level names/labels
                var statNames = gamelog.Names ?? gamelog.Labels ?? new System.Collections.Generic.List<string>();
                _logger.LogInformation("Gamelog Sync: {Count} colunas de estatísticas encontradas para player {PlayerId}: [{Names}]",
                    statNames.Count, playerId, string.Join(", ", statNames));

                // Collect all events from all season types
                var allEntries = new System.Collections.Generic.List<(string eventId, string? gameDate, System.Collections.Generic.List<string>? stats)>();

                // Format 1: root-level entries
                if (gamelog.Entries != null && gamelog.Entries.Any())
                {
                    foreach (var entry in gamelog.Entries)
                    {
                        var id = entry.EventId ?? entry.Id;
                        if (!string.IsNullOrEmpty(id))
                            allEntries.Add((id, entry.GameDate, entry.Stats));
                    }
                }

                // Format 2: seasonTypes[].events
                if (gamelog.SeasonTypes != null)
                {
                    foreach (var season in gamelog.SeasonTypes)
                    {
                        // Per-category stat names if different from root
                        var categoryNames = statNames;
                        if (season.Categories != null && season.Categories.Any())
                        {
                            // Use first category's names if root-level names are empty
                            var firstCat = season.Categories.FirstOrDefault(c => c.Events != null && c.Events.Any());
                            // categories don't carry names directly here; root names are authoritative
                        }

                        if (season.Events == null) continue;
                        foreach (var ev in season.Events)
                        {
                            var id = ev.EventId ?? ev.Id;
                            if (!string.IsNullOrEmpty(id) && !allEntries.Any(e => e.eventId == id))
                                allEntries.Add((id, ev.GameDate, ev.Stats));
                        }
                    }
                }

                _logger.LogInformation("Gamelog Sync: {Count} jogos encontrados para player {PlayerId}", allEntries.Count, playerId);
                if (!allEntries.Any()) return false;

                var statsToUpsert = new System.Collections.Generic.List<PlayerGameStats>();
                var recentEntries = allEntries.TakeLast(numberOfGames).ToList();

                foreach (var (eventId, gameDate, stats) in recentEntries)
                {
                    // Find the corresponding internal game
                    var game = await _gameRepository.GetByExternalIdAsync(eventId);
                    if (game == null)
                    {
                        _logger.LogDebug("Gamelog Sync: Jogo ESPN {EventId} não encontrado no banco local (data={Date}), pulando", eventId, gameDate);
                        continue;
                    }

                    var gameStats = new PlayerGameStats
                    {
                        PlayerId = playerId,
                        GameId = game.Id,
                        TeamId = player.TeamId ?? 0
                    };

                    if (stats != null && statNames.Any())
                    {
                        for (int i = 0; i < statNames.Count && i < stats.Count; i++)
                        {
                            UpdateGameStatsFromKey(gameStats, statNames[i], stats[i]);
                        }
                    }

                    statsToUpsert.Add(gameStats);
                }

                if (statsToUpsert.Any())
                {
                    await _statsRepository.BulkUpsertGameStatsAsync(statsToUpsert);
                    _logger.LogInformation("Gamelog Sync: {Count} jogos persistidos com sucesso para player {PlayerId}", statsToUpsert.Count, playerId);
                    return true;
                }

                _logger.LogWarning("Gamelog Sync: Nenhum jogo correspondente encontrado no banco local para player {PlayerId} (ESPN retornou {Count} entradas)", playerId, allEntries.Count);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gamelog Sync: Erro ao sincronizar jogos recentes para player {PlayerId}", playerId);
                return false;
            }
        }

        public async Task<bool> SyncAllPlayersSeasonStatsAsync(int season)
        {
            _logger.LogInformation("Syncing all players season stats for {Season}", season);
            return true; 
        }
    }
}
