using AutoMapper;
using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Core.Models.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Core.Services
{
    public class PlayerStatsService : IPlayerStatsService
    {
        private readonly IPlayerStatsRepository _statsRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly ITeamRepository _teamRepository;
        private readonly IMapper _mapper;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PlayerStatsService> _logger;

        public PlayerStatsService(
            IPlayerStatsRepository statsRepository,
            IPlayerRepository playerRepository,
            ITeamRepository teamRepository,
            IMapper mapper,
            IMemoryCache cache,
            ILogger<PlayerStatsService> logger)
        {
            _statsRepository = statsRepository;
            _playerRepository = playerRepository;
            _teamRepository = teamRepository;
            _mapper = mapper;
            _cache = cache;
            _logger = logger;
        }

        public async Task<PlayerDetailedResponse?> GetPlayerDetailedStatsAsync(PlayerStatsRequest request)
        {
            try
            {
                var cacheKey = $"player_stats_{request.PlayerId}_{request.Season}_{request.IncludeCareer}_{request.IncludeCurrentSeason}_{request.LastGames}";

                if (_cache.TryGetValue(cacheKey, out PlayerDetailedResponse? cached))
                    return cached;

                var player = await _playerRepository.GetByIdAsync(request.PlayerId);
                if (player == null)
                {
                    _logger.LogWarning("Player not found: {PlayerId}", request.PlayerId);
                    return null;
                }

                var response = _mapper.Map<PlayerDetailedResponse>(player);

                if (request.IncludeCurrentSeason)
                {
                    try
                    {
                        var season = request.Season ?? DateTime.Now.Year;
                        response.CurrentSeasonStats = await GetPlayerSeasonStatsAsync(request.PlayerId, season);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error getting current season stats for player {PlayerId}", request.PlayerId);
                    }
                }

                if (request.IncludeCareer)
                {
                    try
                    {
                        response.CareerStats = await GetPlayerCareerStatsAsync(request.PlayerId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error getting career stats for player {PlayerId}", request.PlayerId);
                    }
                }

                if (request.LastGames > 0)
                {
                    try
                    {
                        response.RecentGames = await GetPlayerRecentGamesAsync(request.PlayerId, request.LastGames);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error getting recent games for player {PlayerId}", request.PlayerId);
                    }
                }

                _cache.Set(cacheKey, response, TimeSpan.FromMinutes(15));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetPlayerDetailedStatsAsync for player {PlayerId}", request.PlayerId);
                throw;
            }
        }

        public async Task<PlayerSeasonStatsResponse?> GetPlayerSeasonStatsAsync(int playerId, int season)
        {
            var stats = await _statsRepository.GetSeasonStatsAsync(playerId, season);

            // TODO: AUTO-SYNC desabilitado temporariamente - PlayerStatsSyncService será reimplementado
            // Estatísticas de jogadores devem ser sincronizadas via job em background no futuro
            if (stats == null)
            {
                _logger.LogWarning("Season stats not found for player {PlayerId}, season {Season}. Auto-sync not available yet.", playerId, season);
                return null;
            }

            /* CÓDIGO COMENTADO - Será reativado quando PlayerStatsSyncService for reimplementado
            if (stats == null)
            {
                _logger.LogInformation("Season stats not found for player {PlayerId}, season {Season}. Attempting auto-sync...", playerId, season);
                var syncSuccess = await _syncService.SyncPlayerSeasonStatsAsync(playerId, season);
                if (syncSuccess)
                {
                    _logger.LogInformation("Auto-sync successful for player {PlayerId}, season {Season}. Retrieving data...", playerId, season);
                    stats = await _statsRepository.GetSeasonStatsAsync(playerId, season);
                }
                else
                {
                    _logger.LogWarning("Auto-sync failed for player {PlayerId}, season {Season}", playerId, season);
                }
            }
            */

            if (stats == null)
                return null;

            var response = _mapper.Map<PlayerSeasonStatsResponse>(stats);

            if (stats.TeamId.HasValue)
            {
                var team = await _teamRepository.GetByIdAsync(stats.TeamId.Value);
                if (team != null)
                    response.TeamName = team.FullName;
            }

            return response;
        }

        public async Task<List<PlayerSeasonStatsResponse>> GetPlayerAllSeasonsAsync(int playerId)
        {
            var seasons = await _statsRepository.GetAllSeasonStatsAsync(playerId);
            var responses = _mapper.Map<List<PlayerSeasonStatsResponse>>(seasons);

            foreach (var response in responses)
            {
                var season = seasons.FirstOrDefault(s => s.Season == response.Season);
                if (season?.TeamId.HasValue == true)
                {
                    var team = await _teamRepository.GetByIdAsync(season.TeamId.Value);
                    if (team != null)
                        response.TeamName = team.FullName;
                }
            }

            return responses;
        }

        public async Task<PlayerCareerStatsResponse?> GetPlayerCareerStatsAsync(int playerId)
        {
            var stats = await _statsRepository.GetCareerStatsAsync(playerId);

            // AUTO-SYNC: Se não encontrar estatísticas de carreira, tenta sincronizar algumas temporadas e recalcular
            if (stats == null)
            {
                _logger.LogInformation("Career stats not found for player {PlayerId}. Attempting to sync recent seasons...", playerId);

                var currentYear = DateTime.Now.Year;
                // TODO: AUTO-SYNC desabilitado - PlayerStatsSyncService será reimplementado
                _logger.LogWarning("Career stats for player {PlayerId} incomplete. Auto-sync not available yet.", playerId);

                /* CÓDIGO COMENTADO - Será reativado quando PlayerStatsSyncService for reimplementado
                var seasonsToSync = new List<int> { currentYear, currentYear - 1, currentYear - 2 }; // Últimas 3 temporadas
                var syncedAny = false;
                foreach (var season in seasonsToSync)
                {
                    var seasonExists = await _statsRepository.GetSeasonStatsAsync(playerId, season);
                    if (seasonExists == null)
                    {
                        var syncSuccess = await _syncService.SyncPlayerSeasonStatsAsync(playerId, season);
                        if (syncSuccess)
                        {
                            syncedAny = true;
                            _logger.LogInformation("Auto-synced season {Season} for player {PlayerId}", season, playerId);
                        }
                    }
                }
                if (syncedAny)
                {
                    _logger.LogInformation("Updating career stats for player {PlayerId} after auto-sync", playerId);
                    await UpdatePlayerCareerStatsAsync(playerId);
                    stats = await _statsRepository.GetCareerStatsAsync(playerId);
                }
                */
            }

            return stats != null ? _mapper.Map<PlayerCareerStatsResponse>(stats) : null;
        }

        public async Task<List<PlayerRecentGameResponse>> GetPlayerRecentGamesAsync(int playerId, int limit)
        {
            var games = await _statsRepository.GetRecentGamesAsync(playerId, Math.Min(limit, 20));
            return _mapper.Map<List<PlayerRecentGameResponse>>(games);
        }

        public async Task<PlayerRecentGameResponse?> GetPlayerGameStatsAsync(int playerId, int gameId)
        {
            var stats = await _statsRepository.GetGameStatsAsync(playerId, gameId);

            // TODO: AUTO-SYNC desabilitado - PlayerStatsSyncService será reimplementado
            if (stats == null)
            {
                _logger.LogWarning("Game stats not found for player {PlayerId}, game {GameId}. Auto-sync not available yet.", playerId, gameId);
                return null;
            }

            /* CÓDIGO COMENTADO - Será reativado quando PlayerStatsSyncService for reimplementado
            if (stats == null)
            {
                _logger.LogInformation("Game stats not found for player {PlayerId}, game {GameId}. Attempting auto-sync...", playerId, gameId);
                var syncSuccess = await _syncService.SyncPlayerGameStatsAsync(playerId, gameId);
                if (syncSuccess)
                {
                    _logger.LogInformation("Auto-sync successful for player {PlayerId}, game {GameId}. Retrieving data...", playerId, gameId);
                    stats = await _statsRepository.GetGameStatsAsync(playerId, gameId);
                }
                else
                {
                    _logger.LogWarning("Auto-sync failed for player {PlayerId}, game {GameId}", playerId, gameId);
                }
            }
            */

            return stats != null ? _mapper.Map<PlayerRecentGameResponse>(stats) : null;
        }

        public async Task<PlayerComparisonResponse?> ComparePlayersAsync(int player1Id, int player2Id, int? season = null)
        {
            try
            {
                var request1 = new PlayerStatsRequest
                {
                    PlayerId = player1Id,
                    Season = season,
                    IncludeCareer = true,
                    IncludeCurrentSeason = true,
                    LastGames = 5
                };

                var request2 = new PlayerStatsRequest
                {
                    PlayerId = player2Id,
                    Season = season,
                    IncludeCareer = true,
                    IncludeCurrentSeason = true,
                    LastGames = 5
                };

                var player1Stats = await GetPlayerDetailedStatsAsync(request1);
                var player2Stats = await GetPlayerDetailedStatsAsync(request2);

                if (player1Stats == null || player2Stats == null)
                {
                    _logger.LogWarning("Cannot compare - Player1: {Player1Found}, Player2: {Player2Found}",
                        player1Stats != null, player2Stats != null);
                    return null;
                }

                return new PlayerComparisonResponse
                {
                    Player1 = player1Stats,
                    Player2 = player2Stats,
                    Comparison = CompareStats(player1Stats, player2Stats)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error comparing players {Player1Id} and {Player2Id}", player1Id, player2Id);
                throw;
            }
        }

        public async Task<StatLeadersResponse> GetStatLeadersAsync(int season, int minGames, int limit)
        {
            var cacheKey = $"stat_leaders_{season}_{minGames}_{limit}";

            if (_cache.TryGetValue(cacheKey, out StatLeadersResponse? cached))
                return cached!;

            var scoringLeaders = await _statsRepository.GetScoringLeadersAsync(season, minGames, limit);
            var reboundLeaders = await _statsRepository.GetReboundLeadersAsync(season, minGames, limit);
            var assistLeaders = await _statsRepository.GetAssistLeadersAsync(season, minGames, limit);

            var response = new StatLeadersResponse
            {
                ScoringLeaders = MapLeaders(scoringLeaders),
                ReboundLeaders = MapLeaders(reboundLeaders),
                AssistLeaders = MapLeaders(assistLeaders),
                LastUpdated = DateTime.UtcNow
            };

            _cache.Set(cacheKey, response, TimeSpan.FromMinutes(30));
            return response;
        }

        public async Task<bool> UpdatePlayerCareerStatsAsync(int playerId)
        {
            var seasons = await _statsRepository.GetAllSeasonStatsAsync(playerId);
            if (!seasons.Any())
                return false;

            var careerStats = new PlayerCareerStats
            {
                PlayerId = playerId,
                TotalSeasons = seasons.Count(),
                TotalGames = seasons.Sum(s => s.GamesPlayed),
                TotalGamesStarted = seasons.Sum(s => s.GamesStarted),
                TotalMinutes = seasons.Sum(s => s.MinutesPlayed),
                TotalPoints = seasons.Sum(s => s.Points),
                TotalRebounds = seasons.Sum(s => s.TotalRebounds),
                TotalAssists = seasons.Sum(s => s.Assists),
                TotalSteals = seasons.Sum(s => s.Steals),
                TotalBlocks = seasons.Sum(s => s.Blocks),
                TotalTurnovers = seasons.Sum(s => s.Turnovers),

                // Totais de arremessos (necessários para calcular porcentagens)
                TotalFieldGoalsMade = seasons.Sum(s => s.FieldGoalsMade),
                TotalFieldGoalsAttempted = seasons.Sum(s => s.FieldGoalsAttempted),
                TotalThreePointersMade = seasons.Sum(s => s.ThreePointersMade),
                TotalThreePointersAttempted = seasons.Sum(s => s.ThreePointersAttempted),
                TotalFreeThrowsMade = seasons.Sum(s => s.FreeThrowsMade),
                TotalFreeThrowsAttempted = seasons.Sum(s => s.FreeThrowsAttempted),

                LastGameDate = DateTime.Today
            };

            // Calcular médias por jogo
            if (careerStats.TotalGames > 0)
            {
                careerStats.CareerPPG = Math.Round((decimal)careerStats.TotalPoints / careerStats.TotalGames, 2);
                careerStats.CareerRPG = Math.Round((decimal)careerStats.TotalRebounds / careerStats.TotalGames, 2);
                careerStats.CareerAPG = Math.Round((decimal)careerStats.TotalAssists / careerStats.TotalGames, 2);
            }

            // Calcular porcentagens de carreira
            if (careerStats.TotalFieldGoalsAttempted > 0)
            {
                careerStats.CareerFgPercentage = Math.Round((decimal)careerStats.TotalFieldGoalsMade / careerStats.TotalFieldGoalsAttempted * 100, 1);
            }

            if (careerStats.TotalThreePointersAttempted > 0)
            {
                careerStats.Career3PtPercentage = Math.Round((decimal)careerStats.TotalThreePointersMade / careerStats.TotalThreePointersAttempted * 100, 1);
            }

            if (careerStats.TotalFreeThrowsAttempted > 0)
            {
                careerStats.CareerFtPercentage = Math.Round((decimal)careerStats.TotalFreeThrowsMade / careerStats.TotalFreeThrowsAttempted * 100, 1);
            }

            // Buscar estatísticas de jogos individuais para calcular career highs
            try
            {
                var allGames = await _statsRepository.GetAllPlayerGamesAsync(playerId);
                if (allGames.Any())
                {
                    careerStats.HighestPointsGame = allGames.Max(g => g.Points);
                    careerStats.HighestReboundsGame = allGames.Max(g => g.TotalRebounds);
                    careerStats.HighestAssistsGame = allGames.Max(g => g.Assists);
                }
                else
                {
                    // Se não há estatísticas de jogos individuais, usar máximos das médias de temporadas
                    _logger.LogWarning("No game stats found for player {PlayerId}, using season maxes for career highs", playerId);
                    careerStats.HighestPointsGame = (int)Math.Ceiling(seasons.Max(s => s.PPG));
                    careerStats.HighestReboundsGame = (int)Math.Ceiling(seasons.Max(s => s.RPG));
                    careerStats.HighestAssistsGame = (int)Math.Ceiling(seasons.Max(s => s.APG));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating career highs for player {PlayerId}", playerId);
                // Continua sem career highs se houver erro
            }

            // Clear cache
            var pattern = $"player_stats_{playerId}_*";
            _cache.Remove(pattern);

            return await _statsRepository.UpsertCareerStatsAsync(careerStats);
        }

        private ComparisonStats CompareStats(PlayerDetailedResponse p1, PlayerDetailedResponse p2)
        {
            var comparison = new ComparisonStats
            {
                StatsDifference = new Dictionary<string, decimal>()
            };

            if (p1.CurrentSeasonStats != null && p2.CurrentSeasonStats != null)
            {
                var s1 = p1.CurrentSeasonStats;
                var s2 = p2.CurrentSeasonStats;

                comparison.BetterScorer = s1.PPG > s2.PPG ? p1.FullName : p2.FullName;
                comparison.BetterRebounder = s1.RPG > s2.RPG ? p1.FullName : p2.FullName;
                comparison.BetterPasser = s1.APG > s2.APG ? p1.FullName : p2.FullName;

                comparison.StatsDifference["PPG"] = Math.Abs(s1.PPG - s2.PPG);
                comparison.StatsDifference["RPG"] = Math.Abs(s1.RPG - s2.RPG);
                comparison.StatsDifference["APG"] = Math.Abs(s1.APG - s2.APG);
            }

            return comparison;
        }

        private List<StatLeader> MapLeaders(IEnumerable<dynamic> leaders)
        {
            var result = new List<StatLeader>();
            int rank = 1;

            foreach (var leader in leaders)
            {
                result.Add(new StatLeader
                {
                    Rank = rank++,
                    PlayerId = leader.id,
                    PlayerName = $"{leader.first_name} {leader.last_name}",
                    Team = leader.team_abbreviation ?? "N/A",
                    Value = leader.value,
                    GamesPlayed = leader.games_played
                });
            }

            return result;
        }
    }
}