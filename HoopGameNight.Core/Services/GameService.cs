using AutoMapper;
using HoopGameNight.Core.Configuration;
using HoopGameNight.Core.Constants;
using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.DTOs.External;
using HoopGameNight.Core.DTOs.External.ESPN;
using HoopGameNight.Core.Enums;
using HoopGameNight.Core.Exceptions;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Core.Models.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using HoopGameNight.Core.Extensions;

namespace HoopGameNight.Core.Services
{
    public class GameService : IGameService
    {
        private readonly IGameRepository _gameRepository;
        private readonly ITeamRepository _teamRepository;
        private readonly IEspnApiService _espnService;
        private readonly IMapper _mapper;
        private readonly ICacheService _cacheService;
        private readonly IGameStatsService _gameStatsService;
        private readonly ITeamService _teamService;
        private readonly ILogger<GameService> _logger;

        public GameService(
            IGameRepository gameRepository,
            ITeamRepository teamRepository,
            IEspnApiService espnService,
            IMapper mapper,
            ICacheService cacheService,
            IGameStatsService gameStatsService,
            ITeamService teamService,
            ILogger<GameService> logger)
        {
            _gameRepository = gameRepository;
            _teamRepository = teamRepository;
            _espnService = espnService;
            _mapper = mapper;
            _cacheService = cacheService;
            _gameStatsService = gameStatsService;
            _teamService = teamService;
            _logger = logger;
        }

        #region Métodos Principais

        public async Task<List<GameResponse>> GetTodayGamesAsync()
        {
            try
            {
                var cacheKey = CacheKeys.TodayGames();

                var cachedData = await _cacheService.GetAsync<List<GameResponse>>(cacheKey);
                if (cachedData != null)
                {
                    _logger.LogInformation("CACHE HIT: Jogos de hoje ({Count} jogos)", cachedData.Count);
                    return cachedData;
                }

                _logger.LogInformation("Buscando jogos de hoje no BANCO...");
                var games = await _gameRepository.GetTodayGamesAsync();
                var response = _mapper.Map<List<GameResponse>>(games);

                var hasLiveGames = response.Any(g => g.IsLive);
                var cacheDuration = hasLiveGames ? TimeSpan.FromSeconds(30) : CacheDurations.TodayGames;

                await _cacheService.SetAsync(cacheKey, response, cacheDuration);
                _logger.LogInformation("Recuperados {Count} jogos de hoje do BANCO DE DADOS (TTL: {TTL}s)", response.Count, cacheDuration.TotalSeconds);

                return response;
            }
            catch (Exception ex)
            {
                throw new BusinessException("Falha ao recuperar jogos de hoje", ex);
            }
        }


        public async Task<List<GameResponse>> GetGamesByDateAsync(DateTime date)
        {
            var cacheKey = CacheKeys.GamesByDate(date);
            _logger.LogInformation("GET GAMES: Buscando jogos para {Date}", date.ToString("yyyy-MM-dd"));

            var cachedData = await _cacheService.GetAsync<List<GameResponse>>(cacheKey);
            if (cachedData != null)
            {
                _logger.LogInformation("CACHE HIT: {Date} ({Count} jogos)", date.ToString("yyyy-MM-dd"), cachedData.Count);
                return cachedData;
            }

            List<GameResponse> response;

            _logger.LogInformation("Consultando BANCO para {Date}...", date.ToString("yyyy-MM-dd"));
            var gamesFromDb = await _gameRepository.GetGamesByDateAsync(date);

            if (gamesFromDb.Any())
            {
                response = _mapper.Map<List<GameResponse>>(gamesFromDb);
                _logger.LogInformation("BANCO: Encontrou {Count} jogos para {Date}",
                    response.Count, date.ToString("yyyy-MM-dd"));

                var duration = response.Any(g => g.Status == GameStatus.Live.ToString()) 
                    ? CacheDurations.LiveGames 
                    : CacheDurations.GetGameCacheDuration(date);
                    
                await _cacheService.SetAsync(cacheKey, response, duration);
            }
            else if (date > DateTime.Today)
            {
                _logger.LogInformation("Buscando jogos futuros da ESPN para {Date}",
                    date.ToString("yyyy-MM-dd"));

                var espnGames = await _espnService.GetGamesByDateAsync(date);
                response = await ConvertEspnGamesToResponseAsync(espnGames);
                _logger.LogInformation("ESPN: {Count} jogos futuros", response.Count);

                await _cacheService.SetAsync(cacheKey, response, CacheDurations.FutureGames);
            }
            else
            {
                response = new List<GameResponse>();
                _logger.LogWarning("Nenhum jogo encontrado para {Date}", date.ToString("yyyy-MM-dd"));
                await _cacheService.SetAsync(cacheKey, response, CacheDurations.Default);
            }

            return response;
        }

        public async Task<List<GameResponse>> GetGamesByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var cacheKey = CacheKeys.GamesByDateRange(startDate, endDate);

                _logger.LogInformation("GET GAMES RANGE: Buscando jogos entre {Start} e {End}",
                    startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));

                var cachedData = await _cacheService.GetAsync<List<GameResponse>>(cacheKey);
                if (cachedData != null)
                {
                    _logger.LogInformation("CACHE HIT: Range {Start} a {End} ({Count} jogos)",
                        startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"), cachedData.Count);
                    return cachedData;
                }

                var gamesFromDb = await _gameRepository.GetByDateRangeAsync(startDate, endDate);
                var response = _mapper.Map<List<GameResponse>>(gamesFromDb.ToList());

                await _cacheService.SetAsync(cacheKey, response, CacheDurations.GetGameCacheDuration(endDate));
                _logger.LogInformation("BANCO RANGE: Encontrou {Count} jogos no intervalo", response.Count);

                return response;
            }
            catch (Exception ex)
            {
                throw new BusinessException("Falha ao recuperar jogos no intervalo de datas", ex);
            }
        }

        public async Task<(List<GameResponse> Games, int TotalCount)> GetGamesAsync(GetGamesRequest request)
        {
            var cacheKey = $"games_paged_{request.Page}_{request.PageSize}_{request.Status}_{request.Date:yyyyMMdd}";

            var cachedData = await _cacheService.GetAsync<(List<GameResponse>, int)>(cacheKey);
            if (cachedData != default)
                return cachedData;

            var (games, totalCount) = await _gameRepository.GetGamesAsync(request);
            var response = _mapper.Map<List<GameResponse>>(games);

            await _cacheService.SetAsync(cacheKey, (response, totalCount), CacheDurations.Default);

            return (response, totalCount);
        }

        public async Task<(List<GameResponse> Games, int TotalCount)> GetGamesByTeamAsync(int teamId, int page, int pageSize)
        {
            var cacheKey = $"games_team_{teamId}_page_{page}_size_{pageSize}";

            var cachedData = await _cacheService.GetAsync<(List<GameResponse>, int)>(cacheKey);
            if (cachedData != default)
                return cachedData;

            var (games, totalCount) = await _gameRepository.GetGamesByTeamAsync(teamId, page, pageSize);
            var response = _mapper.Map<List<GameResponse>>(games);

            await _cacheService.SetAsync(cacheKey, (response, totalCount), CacheDurations.Default);

            return (response, totalCount);
        }

        public async Task<GameResponse?> GetGameByIdAsync(int id)
        {
            try
            {
                var game = await _gameRepository.GetByIdAsync(id);
                return game != null ? _mapper.Map<GameResponse>(game) : null;
            }
            catch (Exception ex)
            {
                throw new BusinessException($"Falha ao recuperar jogo {id}", ex);
            }
        }

        public async Task<EspnBoxscoreDto?> GetGameBoxscoreAsync(int gameId)
        {
            var game = await _gameRepository.GetByIdAsync(gameId);
            if (string.IsNullOrEmpty(game?.ExternalId)) return null;

            var cacheKey = $"boxscore_{gameId}";
            var cached = await _cacheService.GetAsync<EspnBoxscoreDto>(cacheKey);
            if (cached != null) return cached;

            var boxscore = await _espnService.GetGameBoxscoreAsync(game.ExternalId!);
            if (boxscore != null)
            {
                await _cacheService.SetAsync(cacheKey, boxscore, TimeSpan.FromMinutes(5));
            }
            return boxscore;
        }

        public async Task<GameLeadersResponse?> GetGameLeadersAsync(int gameId)
        {
            return await _gameStatsService.GetGameLeadersAsync(gameId);
        }

        public async Task<TeamSeasonLeadersResponse?> GetTeamLeadersAsync(int teamId)
        {
            try
            {
                var team = await _teamRepository.GetByIdAsync(teamId);
                if (team == null)
                {
                    _logger.LogWarning("Team {TeamId} not found", teamId);
                    return null;
                }

                var cacheKey = $"team_leaders_{teamId}";
                var cached = await _cacheService.GetAsync<TeamSeasonLeadersResponse>(cacheKey);
                if (cached != null) return cached;

                var espnData = await _espnService.GetTeamLeadersAsync(team.ExternalId.ToString());
                if (espnData == null) return null;

                var response = MapEspnTeamLeadersToResponse(espnData, teamId, team.Name);
                if (response != null)
                {
                    await _cacheService.SetAsync(cacheKey, response, TimeSpan.FromMinutes(60));
                }
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting team leaders for team {TeamId}", teamId);
                return null;
            }
        }

        #endregion

        #region Método Principal - Múltiplos Times com Suporte a Jogos Futuros

        public async Task<MultipleTeamsGamesResponse> GetGamesForMultipleTeamsAsync(GetMultipleTeamsGamesRequest request)
        {
            _logger.LogInformation(
                "Getting games for {TeamCount} teams from {Start} to {End}",
                request.TeamIds.Count,
                request.StartDate.ToShortDateString(),
                request.EndDate.ToShortDateString()
            );

            if (!request.IsValid())
            {
                throw new BusinessException("Invalid request parameters");
            }

            var response = new MultipleTeamsGamesResponse
            {
                DateRange = new DateRangeInfo
                {
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    TotalDays = (int)(request.EndDate - request.StartDate).TotalDays + 1,
                    IncludesFutureDates = request.EndDate > DateTime.Today,
                    IncludesPastDates = request.StartDate <= DateTime.Today,
                    IncludesToday = request.StartDate <= DateTime.Today && request.EndDate >= DateTime.Today
                }
            };

            var allGames = new List<GameResponse>();

            if (response.DateRange.IncludesPastDates || response.DateRange.IncludesToday)
            {
                var dbEndDate = request.EndDate > DateTime.Today ? DateTime.Today : request.EndDate;
                var databaseGames = await GetGamesFromDatabaseAsync(request.TeamIds, request.StartDate, dbEndDate);
                allGames.AddRange(databaseGames);

                _logger.LogInformation("Found {Count} games from database", databaseGames.Count);
            }

            if (response.DateRange.IncludesFutureDates)
            {
                var futureStartDate = request.StartDate > DateTime.Today ? request.StartDate : DateTime.Today.AddDays(1);
                var futureGames = await GetFutureGamesFromEspnAsync(request.TeamIds, futureStartDate, request.EndDate);
                allGames.AddRange(futureGames);

                _logger.LogInformation("Found {Count} future games from ESPN", futureGames.Count);

                response.ApiLimitations = new ApiLimitationInfo
                {
                    HasLimitations = false,
                    DataSource = "Hybrid (Database + ESPN)",
                    Limitations = new List<string>(),
                    Recommendation = "Full schedule available including future games"
                };
            }
            else
            {
                response.ApiLimitations = new ApiLimitationInfo
                {
                    HasLimitations = false,
                    DataSource = "Database",
                    Limitations = new List<string>(),
                    Recommendation = "Historical data from database"
                };
            }

            response.AllGames = allGames
                .GroupBy(g => new { Date = g.Date.Date, HomeTeamId = g.HomeTeam.Id, VisitorTeamId = g.VisitorTeam.Id })
                .Select(g => g.First())
                .OrderBy(g => g.Date)
                .ThenBy(g => g.HomeTeam.Name)
                .ToList();

            if (request.GroupByTeam)
            {
                response.GamesByTeam = GroupGamesByTeam(response.AllGames, request.TeamIds);
            }

            if (request.IncludeStats)
            {
                response.Stats = await CalculateGamesStatsAsync(response.AllGames, request.TeamIds);
            }

            _logger.LogInformation(
                "Total games found: {Total} (Past: {Past}, Future: {Future})",
                response.AllGames.Count,
                response.AllGames.Count(g => g.Date <= DateTime.Today),
                response.AllGames.Count(g => g.Date > DateTime.Today)
            );

            return response;
        }

        #endregion

        #region Métodos de Conveniência

        public async Task<List<GameResponse>> GetUpcomingGamesForTeamAsync(int teamId, int days = 7)
        {
            var cacheKey = CacheKeys.UpcomingGamesByTeam(teamId, days);

            var cachedData = await _cacheService.GetAsync<List<GameResponse>>(cacheKey);
            if (cachedData != null)
            {
                _logger.LogInformation("REDIS CACHE HIT: Próximos jogos do time {TeamId} ({Count} jogos)", teamId, cachedData.Count);
                return cachedData;
            }

            _logger.LogInformation("CACHE MISS: Buscando próximos jogos do time {TeamId}", teamId);
            var request = new GetMultipleTeamsGamesRequest
            {
                TeamIds = new List<int> { teamId },
                StartDate = DateTime.Today.AddDays(1),
                EndDate = DateTime.Today.AddDays(days),
                GroupByTeam = false,
                IncludeStats = false
            };

            var result = await GetGamesForMultipleTeamsAsync(request);
            var games = result.AllGames.OrderBy(g => g.Date).ToList();

            var hasActiveGames = games.Any(g => g.IsLive || g.Date.Date == DateTime.Today);
            var cacheDuration = hasActiveGames ? TimeSpan.FromMinutes(2) : TimeSpan.FromHours(12);

            await _cacheService.SetAsync(cacheKey, games, cacheDuration);
            _logger.LogInformation("Salvos {Count} próximos jogos do time {TeamId} no Redis com TTL de {TTL} min", games.Count, teamId, cacheDuration.TotalMinutes);

            return games;
        }

        public async Task<List<GameResponse>> GetRecentGamesForTeamAsync(int teamId, int days = 7)
        {
            var cacheKey = CacheKeys.RecentGamesByTeam(teamId, days);

            var cachedData = await _cacheService.GetAsync<List<GameResponse>>(cacheKey);
            if (cachedData != null)
            {
                _logger.LogInformation("REDIS CACHE HIT: Jogos recentes do time {TeamId} ({Count} jogos)", teamId, cachedData.Count);
                return cachedData;
            }

            _logger.LogInformation("CACHE MISS: Buscando jogos recentes do time {TeamId}", teamId);
            var request = new GetMultipleTeamsGamesRequest
            {
                TeamIds = new List<int> { teamId },
                StartDate = DateTime.Today.AddDays(-days),
                EndDate = DateTime.Today,
                GroupByTeam = false,
                IncludeStats = false
            };

            var result = await GetGamesForMultipleTeamsAsync(request);
            var games = result.AllGames.OrderByDescending(g => g.Date).ToList();

            var hasActiveOrTodayGames = games.Any(g => g.IsLive || (!g.IsCompleted && g.Date >= DateTime.Today));
            var cacheDuration = hasActiveOrTodayGames ? TimeSpan.FromMinutes(2) : TimeSpan.FromHours(24);

            await _cacheService.SetAsync(cacheKey, games, cacheDuration);
            _logger.LogInformation("Salvos {Count} jogos recentes do time {TeamId} no Redis com TTL de {TTL} min", games.Count, teamId, cacheDuration.TotalMinutes);

            return games;
        }

        #endregion

        #region Métodos Privados

        private async Task<List<GameResponse>> GetGamesFromDatabaseAsync(List<int> teamIds, DateTime startDate, DateTime endDate)
        {
            _logger.LogInformation("FETCHING GAMES: Database query for {TeamCount} teams between {Start} and {End}",
                teamIds.Count, startDate.ToShortDateString(), endDate.ToShortDateString());

            var games = await _gameRepository.GetByDateRangeAndTeamsAsync(startDate, endDate, teamIds);
            
            _logger.LogInformation("DATABASE RESULT: Found {Count} games", games.Count());
            
            return _mapper.Map<List<GameResponse>>(games.ToList());
        }

        private async Task<List<GameResponse>> GetFutureGamesFromEspnAsync(List<int> teamIds, DateTime startDate, DateTime endDate)
        {
            var allTeams = await _teamRepository.GetAllAsync();
            var targetTeams = allTeams.Where(t => teamIds.Contains(t.Id)).ToList();
            var targetEspnIds = targetTeams.Select(t => t.ExternalId.ToString()).ToHashSet();
            var targetAbbrs = targetTeams.Select(t => t.Abbreviation).ToHashSet();

            var allEspnGames = new List<EspnGameDto>();
            
            // Parallelize ESPN requests for each date in the range
            var dateRange = Enumerable.Range(0, (int)(endDate - startDate).TotalDays + 1)
                                      .Select(offset => startDate.AddDays(offset))
                                      .ToList();

            _logger.LogInformation("PARALLEL ESPN FETCH: Requesting {Days} days of games in parallel", dateRange.Count);

            var tasks = dateRange.Select(async d =>
            {
                try
                {
                    var gamesForDate = await _espnService.GetGamesByDateAsync(d);
                    if (gamesForDate != null)
                    {
                        var filteredGames = gamesForDate.Where(g =>
                            targetEspnIds.Contains(g.HomeTeamId) || targetEspnIds.Contains(g.AwayTeamId) ||
                            targetAbbrs.Contains(g.HomeTeamAbbreviation) || targetAbbrs.Contains(g.AwayTeamAbbreviation)
                        ).ToList();
                        
                        return filteredGames;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching ESPN future games for Date {Date}", d.ToString("yyyy-MM-dd"));
                }
                return new List<EspnGameDto>();
            });

            var results = await Task.WhenAll(tasks);
            foreach (var gamesList in results)
            {
                allEspnGames.AddRange(gamesList);
            }

            var mappedGames = await ConvertEspnGamesToResponseAsync(allEspnGames);

            return mappedGames
                .GroupBy(g => new { Date = g.Date.Date, HomeTeamId = g.HomeTeam.Id, VisitorTeamId = g.VisitorTeam.Id })
                .Select(g => g.First())
                .ToList();
        }

        private async Task<List<GameResponse>> ConvertEspnGamesToResponseAsync(List<EspnGameDto> espnGames)
        {
            var responses = new List<GameResponse>();

            var allTeams = await _teamRepository.GetAllAsync();
            var teamsDict = allTeams.ToDictionary(t => t.Id);

            foreach (var eg in espnGames)
            {
                var homeTeamId = await _teamService.MapEspnTeamToSystemIdAsync(eg.HomeTeamId, eg.HomeTeamAbbreviation);
                var awayTeamId = await _teamService.MapEspnTeamToSystemIdAsync(eg.AwayTeamId, eg.AwayTeamAbbreviation);

                if (homeTeamId == 0 || awayTeamId == 0)
                {
                    _logger.LogWarning(
                        "Skipping future game - mapping failed | {Away}({AwayId}) @ {Home}({HomeId})",
                        eg.AwayTeamAbbreviation, eg.AwayTeamId,
                        eg.HomeTeamAbbreviation, eg.HomeTeamId);
                    continue;
                }

                if (!teamsDict.TryGetValue(homeTeamId, out var homeTeam) ||
                    !teamsDict.TryGetValue(awayTeamId, out var awayTeam))
                {
                    _logger.LogError(
                        "CRÍTICO: Teams not found after mapping | System IDs: Home={HomeId}, Away={AwayId}",
                        homeTeamId, awayTeamId);
                    continue;
                }

                responses.Add(new GameResponse
                {
                    Id = 0,
                    Date = eg.Date,
                    DateTime = eg.Date,
                    HomeTeam = new TeamSummaryResponse
                    {
                        Id = homeTeam.Id,
                        Name = homeTeam.Name,
                        Abbreviation = homeTeam.Abbreviation,
                        LogoUrl = TeamExtensions.GetTeamLogoUrl(homeTeam.Abbreviation)
                    },
                    VisitorTeam = new TeamSummaryResponse
                    {
                        Id = awayTeam.Id,
                        Name = awayTeam.Name,
                        Abbreviation = awayTeam.Abbreviation,
                        LogoUrl = TeamExtensions.GetTeamLogoUrl(awayTeam.Abbreviation)
                    },
                    HomeTeamScore = eg.HomeTeamScore,
                    VisitorTeamScore = eg.AwayTeamScore,
                    Status = "Scheduled",
                    StatusDisplay = eg.Date.ToString("MMM dd, h:mm tt"),
                    GameTitle = $"{awayTeam.Name} @ {homeTeam.Name}",
                    Score = eg.HomeTeamScore.HasValue && eg.AwayTeamScore.HasValue
                        ? $"{eg.AwayTeamScore} - {eg.HomeTeamScore}"
                        : "-",
                    IsLive = false,
                    IsCompleted = false,
                    IsFutureGame = true,
                    DataSource = "ESPN"
                });
            }

            return responses;
        }

        private Dictionary<int, List<GameResponse>> GroupGamesByTeam(List<GameResponse> games, List<int> teamIds)
        {
            var grouped = new Dictionary<int, List<GameResponse>>();

            foreach (var teamId in teamIds)
            {
                grouped[teamId] = games
                    .Where(g => g.HomeTeam.Id == teamId || g.VisitorTeam.Id == teamId)
                    .OrderBy(g => g.Date)
                    .ToList();
            }

            return grouped;
        }

        private async Task<GamesStatsResponse> CalculateGamesStatsAsync(List<GameResponse> games, List<int> teamIds)
        {
            var allTeams = await _teamRepository.GetAllAsync();
            var teamsDict = allTeams.ToDictionary(t => t.Id);

            var stats = new GamesStatsResponse
            {
                TotalTeams = teamIds.Count,
                TotalGames = games.Count,
                LiveGames = games.Count(g => g.IsLive),
                CompletedGames = games.Count(g => g.IsCompleted),
                ScheduledGames = games.Count(g => !g.IsLive && !g.IsCompleted),
                TeamStats = new Dictionary<int, TeamGamesStats>()
            };

            foreach (var teamId in teamIds)
            {
                teamsDict.TryGetValue(teamId, out var team);
                var teamGames = games.Where(g => g.HomeTeam.Id == teamId || g.VisitorTeam.Id == teamId).ToList();

                stats.TeamStats[teamId] = new TeamGamesStats
                {
                    TeamId = teamId,
                    TeamName = team?.FullName ?? $"Team {teamId}",
                    TeamAbbreviation = team?.Abbreviation ?? "",
                    TotalGames = teamGames.Count,
                    HomeGames = teamGames.Count(g => g.HomeTeam.Id == teamId),
                    AwayGames = teamGames.Count(g => g.VisitorTeam.Id == teamId),
                    Wins = teamGames.Count(g => g.WinningTeam?.Id == teamId),
                    Losses = teamGames.Count(g => g.IsCompleted && g.WinningTeam?.Id != teamId),
                    NextGame = teamGames
                        .Where(g => !g.IsCompleted && g.Date >= DateTime.Now)
                        .OrderBy(g => g.Date)
                        .FirstOrDefault(),
                    LastGame = teamGames
                        .Where(g => g.IsCompleted)
                        .OrderByDescending(g => g.Date)
                        .FirstOrDefault()
                };
            }

            return stats;
        }

        private TeamSeasonLeadersResponse? MapEspnTeamLeadersToResponse(EspnTeamLeadersDto espnData, int teamId, string teamName)
        {
            try
            {
                var response = new TeamSeasonLeadersResponse
                {
                    TeamId = teamId,
                    TeamName = teamName,
                    Season = DateTime.Now.Year
                };

                if (espnData?.Categories == null) return response;

                foreach (var category in espnData.Categories)
                {
                    if (category?.Leaders == null || !category.Leaders.Any()) continue;

                    var categoryName = category.Name?.ToLowerInvariant() ?? category.DisplayName?.ToLowerInvariant() ?? "";
                    var topLeader = category.Leaders.FirstOrDefault();
                    if (topLeader?.Athlete == null) continue;

                    try
                    {
                        var espnPlayerId = topLeader.Athlete.Ref?.Split('/').LastOrDefault();
                        if (string.IsNullOrEmpty(espnPlayerId)) continue;

                        var statLeader = new StatLeader
                        {
                            PlayerId = 0,
                            PlayerName = topLeader.Athlete.DisplayName ?? "Unknown",
                            Team = teamName,
                            Value = decimal.TryParse(topLeader.DisplayValue, out var val) ? val : 0,
                            GamesPlayed = 0
                        };

                        if (categoryName.Contains("point") || categoryName.Contains("scoring"))
                            response.PointsLeader = statLeader;
                        else if (categoryName.Contains("rebound"))
                            response.ReboundsLeader = statLeader;
                        else if (categoryName.Contains("assist"))
                            response.AssistsLeader = statLeader;
                        else if (categoryName.Contains("steal"))
                            response.StealsLeader = statLeader;
                        else if (categoryName.Contains("block"))
                            response.BlocksLeader = statLeader;
                        else if (categoryName.Contains("field goal") && categoryName.Contains("percent"))
                            response.FGPercentageLeader = statLeader;
                        else if (categoryName.Contains("three point") && categoryName.Contains("percent"))
                            response.ThreePointPercentageLeader = statLeader;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error parsing team leader for team {TeamId}", teamId);
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mapping team leaders for team {TeamId}", teamId);
                return null;
            }
        }

        #endregion
    }
}