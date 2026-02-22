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
        private readonly ILogger<PlayerStatsSyncService> _logger;

        public PlayerStatsSyncService(
            IPlayerStatsRepository statsRepository,
            IPlayerRepository playerRepository,
            IGameRepository gameRepository,
            IEspnApiService espnApiService,
            IEspnParser espnParser,
            ILogger<PlayerStatsSyncService> logger)
        {
            _statsRepository = statsRepository;
            _playerRepository = playerRepository;
            _gameRepository = gameRepository;
            _espnApiService = espnApiService;
            _espnParser = espnParser;
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

                case "points": case "pts": stats.Points = (int)value; break;
                case "fieldgoalsmade": case "fgm": stats.FieldGoalsMade = (int)value; break;
                case "fieldgoalsattempted": case "fga": stats.FieldGoalsAttempted = (int)value; break;
                case "threepointfieldgoalsmade": case "3pm": stats.ThreePointersMade = (int)value; break;
                case "threepointfieldgoalsattempted": case "3pa": stats.ThreePointersAttempted = (int)value; break;
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

        public async Task<bool> SyncPlayerSeasonStatsAsync(int playerId, int season) => await Task.FromResult(true);
        public async Task<bool> SyncPlayerRecentGamesAsync(int playerId, int numberOfGames = 10) => await Task.FromResult(true);
        public async Task<bool> SyncAllPlayersSeasonStatsAsync(int season) => await Task.FromResult(true);
        public async Task<bool> SyncGameStatsForAllPlayersInGameAsync(int gameId) => await Task.FromResult(true);
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
    }
}
