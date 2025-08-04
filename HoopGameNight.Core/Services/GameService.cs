using AutoMapper;
using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.DTOs.External; // Adicionar esta using
using HoopGameNight.Core.Enums;
using HoopGameNight.Core.Exceptions;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Core.Models.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Core.Services
{
    public class GameService : IGameService
    {
        private readonly IGameRepository _gameRepository;
        private readonly ITeamRepository _teamRepository;
        private readonly IBallDontLieService _ballDontLieService;
        private readonly IEspnApiService _espnService;
        private readonly IMapper _mapper;
        private readonly IMemoryCache _cache;
        private readonly ILogger<GameService> _logger;

        // Mapeamento de IDs de times (Sistema -> ESPN)
        private readonly Dictionary<int, string> _systemToEspnTeamMapping = new()
        {
            { 1, "1" },   // Hawks
            { 2, "2" },   // Celtics
            { 3, "17" },  // Nets
            { 4, "3" },   // Hornets
            { 5, "5" },   // Bulls
            { 6, "6" },   // Cavaliers
            { 7, "7" },   // Mavericks
            { 8, "8" },   // Nuggets
            { 9, "9" },   // Pistons
            { 10, "10" }, // Warriors
            { 11, "11" }, // Rockets
            { 12, "12" }, // Pacers
            { 13, "13" }, // Clippers
            { 14, "14" }, // Lakers
            { 15, "15" }, // Grizzlies
            { 16, "16" }, // Heat
            { 17, "18" }, // Bucks
            { 18, "19" }, // Timberwolves
            { 19, "20" }, // Pelicans
            { 20, "21" }, // Knicks
            { 21, "22" }, // Thunder
            { 22, "23" }, // Magic
            { 23, "24" }, // 76ers
            { 24, "25" }, // Suns
            { 25, "26" }, // Trail Blazers
            { 26, "27" }, // Kings
            { 27, "28" }, // Spurs
            { 28, "29" }, // Raptors
            { 29, "30" }, // Jazz
            { 30, "4" }   // Wizards
        };

        // Mapeamento reverso (ESPN -> Sistema)
        private readonly Dictionary<string, int> _espnToSystemTeamMapping;

        public GameService(
            IGameRepository gameRepository,
            ITeamRepository teamRepository,
            IBallDontLieService ballDontLieService,
            IEspnApiService espnService,
            IMapper mapper,
            IMemoryCache cache,
            ILogger<GameService> logger)
        {
            _gameRepository = gameRepository;
            _teamRepository = teamRepository;
            _ballDontLieService = ballDontLieService;
            _espnService = espnService;
            _mapper = mapper;
            _cache = cache;
            _logger = logger;

            // Criar mapeamento reverso
            _espnToSystemTeamMapping = _systemToEspnTeamMapping.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
        }

        #region Métodos Principais

        /// <summary>
        /// Busca jogos de hoje com cache
        /// </summary>
        public async Task<List<GameResponse>> GetTodayGamesAsync()
        {
            var cacheKey = $"games_today_{DateTime.Today:yyyy-MM-dd}";

            if (_cache.TryGetValue<List<GameResponse>>(cacheKey, out var cachedGames))
            {
                return cachedGames!;
            }

            var games = await _gameRepository.GetTodayGamesAsync();
            var response = _mapper.Map<List<GameResponse>>(games);

            _cache.Set(cacheKey, response, TimeSpan.FromMinutes(5));

            _logger.LogInformation("Retrieved {Count} games for today", response.Count);
            return response;
        }

        /// <summary>
        /// Busca jogos por data específica
        /// </summary>
        public async Task<List<GameResponse>> GetGamesByDateAsync(DateTime date)
        {
            var cacheKey = $"games_date_{date:yyyy-MM-dd}";

            if (_cache.TryGetValue<List<GameResponse>>(cacheKey, out var cachedGames))
            {
                return cachedGames!;
            }

            List<GameResponse> response;

            if (date > DateTime.Today)
            {
                // Data futura - buscar da ESPN
                var espnGames = await _espnService.GetGamesByDateAsync(date);
                response = ConvertEspnGamesToResponse(espnGames);
            }
            else
            {
                // Data passada ou hoje - buscar do banco
                var games = await _gameRepository.GetGamesByDateAsync(date);
                response = _mapper.Map<List<GameResponse>>(games);
            }

            _cache.Set(cacheKey, response, TimeSpan.FromMinutes(15));

            return response;
        }

        /// <summary>
        /// Busca jogos com filtros e paginação
        /// </summary>
        public async Task<(List<GameResponse> Games, int TotalCount)> GetGamesAsync(GetGamesRequest request)
        {
            var (games, totalCount) = await _gameRepository.GetGamesAsync(request);
            var response = _mapper.Map<List<GameResponse>>(games);
            return (response, totalCount);
        }

        /// <summary>
        /// Busca jogos de um time específico
        /// </summary>
        public async Task<(List<GameResponse> Games, int TotalCount)> GetGamesByTeamAsync(int teamId, int page, int pageSize)
        {
            var (games, totalCount) = await _gameRepository.GetGamesByTeamAsync(teamId, page, pageSize);
            var response = _mapper.Map<List<GameResponse>>(games);
            return (response, totalCount);
        }

        /// <summary>
        /// Busca um jogo específico por ID
        /// </summary>
        public async Task<GameResponse?> GetGameByIdAsync(int id)
        {
            var game = await _gameRepository.GetByIdAsync(id);
            return game != null ? _mapper.Map<GameResponse>(game) : null;
        }

        #endregion

        #region Método Principal - Múltiplos Times com Suporte a Jogos Futuros

        /// <summary>
        /// Busca jogos de múltiplos times com suporte completo para datas futuras
        /// </summary>
        public async Task<MultipleTeamsGamesResponse> GetGamesForMultipleTeamsAsync(GetMultipleTeamsGamesRequest request)
        {
            _logger.LogInformation(
                "Getting games for {TeamCount} teams from {Start} to {End}",
                request.TeamIds.Count,
                request.StartDate.ToShortDateString(),
                request.EndDate.ToShortDateString()
            );

            // Validar request
            if (!request.IsValid())
            {
                throw new BusinessException("Invalid request parameters");
            }

            // Criar response com informações de data
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

            // Coletar todos os jogos
            var allGames = new List<GameResponse>();

            // 1. Buscar jogos do passado/hoje do banco de dados
            if (response.DateRange.IncludesPastDates || response.DateRange.IncludesToday)
            {
                var dbEndDate = request.EndDate > DateTime.Today ? DateTime.Today : request.EndDate;
                var databaseGames = await GetGamesFromDatabaseAsync(request.TeamIds, request.StartDate, dbEndDate);
                allGames.AddRange(databaseGames);

                _logger.LogInformation("Found {Count} games from database", databaseGames.Count);
            }

            // 2. Buscar jogos futuros da ESPN API
            if (response.DateRange.IncludesFutureDates)
            {
                var futureStartDate = request.StartDate > DateTime.Today ? request.StartDate : DateTime.Today.AddDays(1);
                var futureGames = await GetFutureGamesFromEspnAsync(request.TeamIds, futureStartDate, request.EndDate);
                allGames.AddRange(futureGames);

                _logger.LogInformation("Found {Count} future games from ESPN", futureGames.Count);

                // Atualizar informações da API
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

            // 3. Processar e organizar resultados
            response.AllGames = allGames
                .GroupBy(g => new { Date = g.Date.Date, HomeTeamId = g.HomeTeam.Id, VisitorTeamId = g.VisitorTeam.Id })
                .Select(g => g.First())
                .OrderBy(g => g.Date)
                .ThenBy(g => g.HomeTeam.Name)
                .ToList();

            // 4. Agrupar por time se solicitado
            if (request.GroupByTeam)
            {
                response.GamesByTeam = GroupGamesByTeam(response.AllGames, request.TeamIds);
            }

            // 5. Calcular estatísticas se solicitado
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

        /// <summary>
        /// Busca próximos jogos de um time
        /// </summary>
        public async Task<List<GameResponse>> GetUpcomingGamesForTeamAsync(int teamId, int days = 7)
        {
            var request = new GetMultipleTeamsGamesRequest
            {
                TeamIds = new List<int> { teamId },
                StartDate = DateTime.Today.AddDays(1),
                EndDate = DateTime.Today.AddDays(days),
                GroupByTeam = false,
                IncludeStats = false
            };

            var result = await GetGamesForMultipleTeamsAsync(request);
            return result.AllGames.OrderBy(g => g.Date).ToList();
        }

        /// <summary>
        /// Busca jogos recentes de um time
        /// </summary>
        public async Task<List<GameResponse>> GetRecentGamesForTeamAsync(int teamId, int days = 7)
        {
            var request = new GetMultipleTeamsGamesRequest
            {
                TeamIds = new List<int> { teamId },
                StartDate = DateTime.Today.AddDays(-days),
                EndDate = DateTime.Today,
                GroupByTeam = false,
                IncludeStats = false
            };

            var result = await GetGamesForMultipleTeamsAsync(request);
            return result.AllGames.OrderByDescending(g => g.Date).ToList();
        }

        #endregion

        #region Sincronização

        /// <summary>
        /// Sincroniza jogos de hoje
        /// </summary>
        public async Task SyncTodayGamesAsync()
        {
            await SyncGamesByDateAsync(DateTime.Today);
        }

        /// <summary>
        /// Sincroniza jogos por data específica (apenas datas passadas/hoje)
        /// </summary>
        public async Task<int> SyncGamesByDateAsync(DateTime date)
        {
            if (date > DateTime.Today)
            {
                _logger.LogWarning("Cannot sync future games. Date: {Date}", date);
                return 0;
            }

            try
            {
                var externalGames = await _ballDontLieService.GetGamesByDateAsync(date);
                var syncCount = 0;

                foreach (var externalGame in externalGames)
                {
                    var existingGame = await _gameRepository.GetByExternalIdAsync(externalGame.Id);

                    if (existingGame == null)
                    {
                        var homeTeam = await _teamRepository.GetByExternalIdAsync(externalGame.HomeTeam.Id);
                        var visitorTeam = await _teamRepository.GetByExternalIdAsync(externalGame.VisitorTeam.Id);

                        if (homeTeam == null || visitorTeam == null)
                        {
                            _logger.LogWarning(
                                "Skipping game {GameId} - teams not found",
                                externalGame.Id
                            );
                            continue;
                        }

                        var game = new Game
                        {
                            ExternalId = externalGame.Id,
                            Date = DateTime.Parse(externalGame.Date),
                            DateTime = DateTime.Parse(externalGame.Date),
                            HomeTeamId = homeTeam.Id,
                            VisitorTeamId = visitorTeam.Id,
                            HomeTeamScore = externalGame.HomeTeamScore,
                            VisitorTeamScore = externalGame.VisitorTeamScore,
                            Status = MapGameStatus(externalGame.Status),
                            Period = externalGame.Period,
                            TimeRemaining = externalGame.Time,
                            PostSeason = externalGame.Postseason,
                            Season = externalGame.Season,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        await _gameRepository.InsertAsync(game);
                        syncCount++;
                    }
                    else
                    {
                        // Atualizar jogo existente
                        existingGame.HomeTeamScore = externalGame.HomeTeamScore;
                        existingGame.VisitorTeamScore = externalGame.VisitorTeamScore;
                        existingGame.Status = MapGameStatus(externalGame.Status);
                        existingGame.Period = externalGame.Period;
                        existingGame.TimeRemaining = externalGame.Time;
                        existingGame.UpdatedAt = DateTime.UtcNow;

                        await _gameRepository.UpdateAsync(existingGame);
                    }
                }

                // Limpar cache
                _cache.Remove($"games_today_{DateTime.Today:yyyy-MM-dd}");
                _cache.Remove($"games_date_{date:yyyy-MM-dd}");

                _logger.LogInformation("Synced {Count} new games for {Date}", syncCount, date.ToShortDateString());
                return syncCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing games for date: {Date}", date);
                throw new ExternalApiException("Ball Don't Lie", $"Failed to sync games for {date:yyyy-MM-dd}", ex);
            }
        }

        /// <summary>
        /// Sincroniza jogos de um período
        /// </summary>
        public async Task<int> SyncGamesForPeriodAsync(DateTime startDate, DateTime endDate)
        {
            if (endDate > DateTime.Today)
            {
                endDate = DateTime.Today;
            }

            var totalSynced = 0;
            var currentDate = startDate;

            while (currentDate <= endDate)
            {
                var synced = await SyncGamesByDateAsync(currentDate);
                totalSynced += synced;

                // Delay para evitar rate limit
                await Task.Delay(1000);

                currentDate = currentDate.AddDays(1);
            }

            return totalSynced;
        }

        #endregion

        #region Métodos Privados

        /// <summary>
        /// Busca jogos do banco de dados
        /// </summary>
        private async Task<List<GameResponse>> GetGamesFromDatabaseAsync(List<int> teamIds, DateTime startDate, DateTime endDate)
        {
            var allGames = new List<Game>();

            // Otimização: buscar todos os jogos do período de uma vez
            // CORREÇÃO: Usar método que existe no repositório
            var currentDate = startDate;
            while (currentDate <= endDate)
            {
                var dayGames = await _gameRepository.GetGamesByDateAsync(currentDate);
                allGames.AddRange(dayGames);
                currentDate = currentDate.AddDays(1);
            }

            // Filtrar jogos dos times solicitados
            allGames = allGames
                .Where(g => teamIds.Contains(g.HomeTeamId) || teamIds.Contains(g.VisitorTeamId))
                .ToList();

            return _mapper.Map<List<GameResponse>>(allGames);
        }

        /// <summary>
        /// Busca jogos futuros da ESPN
        /// </summary>
        private async Task<List<GameResponse>> GetFutureGamesFromEspnAsync(List<int> teamIds, DateTime startDate, DateTime endDate)
        {
            var allGames = new List<GameResponse>();

            // Buscar para cada time
            var tasks = teamIds.Select(async teamId =>
            {
                try
                {
                    var espnGames = await _espnService.GetTeamScheduleAsync(teamId, startDate, endDate);
                    return ConvertEspnGamesToResponse(espnGames);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching ESPN games for team {TeamId}", teamId);
                    return new List<GameResponse>();
                }
            });

            var results = await Task.WhenAll(tasks);
            allGames = results.SelectMany(r => r).ToList();

            // Remover duplicatas
            return allGames
                .GroupBy(g => new { Date = g.Date.Date, HomeTeamId = g.HomeTeam.Id, VisitorTeamId = g.VisitorTeam.Id })
                .Select(g => g.First())
                .ToList();
        }

        /// <summary>
        /// Converte jogos da ESPN para GameResponse
        /// </summary>
        private List<GameResponse> ConvertEspnGamesToResponse(List<EspnGameDto> espnGames)
        {
            return espnGames.Select(eg => new GameResponse
            {
                Id = -Math.Abs(eg.Id.GetHashCode()), // ID negativo para jogos futuros
                Date = eg.Date,
                DateTime = eg.Date,
                HomeTeam = new TeamSummaryResponse
                {
                    Id = MapEspnToSystemTeamId(eg.HomeTeamId),
                    Name = eg.HomeTeamName,
                    Abbreviation = eg.HomeTeamAbbreviation
                },
                VisitorTeam = new TeamSummaryResponse
                {
                    Id = MapEspnToSystemTeamId(eg.AwayTeamId),
                    Name = eg.AwayTeamName,
                    Abbreviation = eg.AwayTeamAbbreviation
                },
                HomeTeamScore = eg.HomeTeamScore,
                VisitorTeamScore = eg.AwayTeamScore,
                Status = "Scheduled",
                StatusDisplay = eg.Date.ToString("MMM dd, h:mm tt"),
                GameTitle = $"{eg.AwayTeamName} @ {eg.HomeTeamName}",
                Score = eg.HomeTeamScore.HasValue && eg.AwayTeamScore.HasValue
                    ? $"{eg.AwayTeamScore} - {eg.HomeTeamScore}"
                    : "-",
                IsLive = false,
                IsCompleted = false,
                IsFutureGame = true,
                DataSource = "ESPN"
            }).ToList();
        }

        /// <summary>
        /// Agrupa jogos por time
        /// </summary>
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

        /// <summary>
        /// Calcula estatísticas dos jogos
        /// </summary>
        private async Task<GamesStatsResponse> CalculateGamesStatsAsync(List<GameResponse> games, List<int> teamIds)
        {
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
                var team = await _teamRepository.GetByIdAsync(teamId);
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

        /// <summary>
        /// Mapeia status da API externa para enum
        /// </summary>
        private GameStatus MapGameStatus(string? externalStatus)
        {
            if (string.IsNullOrEmpty(externalStatus))
                return GameStatus.Scheduled;

            return externalStatus.ToLower() switch
            {
                "final" => GameStatus.Final,
                "completed" => GameStatus.Final,
                "in progress" => GameStatus.Live,
                "live" => GameStatus.Live,
                "scheduled" => GameStatus.Scheduled,
                "postponed" => GameStatus.Postponed,
                "cancelled" => GameStatus.Cancelled,
                _ => GameStatus.Scheduled
            };
        }

        /// <summary>
        /// Mapeia ID da ESPN para ID do sistema
        /// </summary>
        private int MapEspnToSystemTeamId(string espnTeamId)
        {
            return _espnToSystemTeamMapping.TryGetValue(espnTeamId, out var systemId) ? systemId : 0;
        }

        #endregion
    }
}