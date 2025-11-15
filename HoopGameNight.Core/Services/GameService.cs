using AutoMapper;
using HoopGameNight.Core.Configuration;
using HoopGameNight.Core.Constants;
using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.DTOs.External;
using HoopGameNight.Core.Enums;
using HoopGameNight.Core.Exceptions;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Core.Models.Entities;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Core.Services
{
    public class GameService : IGameService
    {
        private readonly IGameRepository _gameRepository;
        private readonly ITeamRepository _teamRepository;
        private readonly IEspnApiService _espnService;
        private readonly IMapper _mapper;
        private readonly ICacheService _cacheService;
        private readonly ILogger<GameService> _logger;

        public GameService(
            IGameRepository gameRepository,
            ITeamRepository teamRepository,
            IEspnApiService espnService,
            IMapper mapper,
            ICacheService cacheService,
            ILogger<GameService> logger)
        {
            _gameRepository = gameRepository;
            _teamRepository = teamRepository;
            _espnService = espnService;
            _mapper = mapper;
            _cacheService = cacheService;
            _logger = logger;
        }

        #region Métodos Principais

        /// <summary>
        /// Busca jogos de hoje com cache (Redis → Memory → Banco)
        /// </summary>
        public async Task<List<GameResponse>> GetTodayGamesAsync()
        {
            var cacheKey = CacheKeys.TodayGames();

            // 1. Tentar cache (Redis → Memory)
            var cachedData = await _cacheService.GetAsync<List<GameResponse>>(cacheKey);
            if (cachedData != null)
            {
                _logger.LogInformation("CACHE HIT: Jogos de hoje ({Count} jogos)", cachedData.Count);
                return cachedData;
            }

            // 2. Buscar do banco
            _logger.LogInformation("Buscando jogos de hoje no BANCO...");
            var games = await _gameRepository.GetTodayGamesAsync();
            var response = _mapper.Map<List<GameResponse>>(games);

            // 3. Salvar em cache
            await _cacheService.SetAsync(cacheKey, response, CacheDurations.TodayGames);
            _logger.LogInformation("Retrieved {Count} games for today from DATABASE", response.Count);

            return response;
        }

        /// <summary>
        /// Busca jogos por data específica (Redis → Banco → ESPN)
        /// </summary>
        public async Task<List<GameResponse>> GetGamesByDateAsync(DateTime date)
        {
            var cacheKey = CacheKeys.GamesByDate(date);
            _logger.LogInformation("GET GAMES: Buscando jogos para {Date}", date.ToString("yyyy-MM-dd"));

            // 1. Tentar cache (Redis → Memory)
            var cachedData = await _cacheService.GetAsync<List<GameResponse>>(cacheKey);
            if (cachedData != null)
            {
                _logger.LogInformation("CACHE HIT: {Date} ({Count} jogos)", date.ToString("yyyy-MM-dd"), cachedData.Count);
                return cachedData;
            }

            List<GameResponse> response;

            // 2. SEMPRE tentar buscar do banco primeiro
            _logger.LogInformation("Consultando BANCO para {Date}...", date.ToString("yyyy-MM-dd"));
            var gamesFromDb = await _gameRepository.GetGamesByDateAsync(date);

            if (gamesFromDb.Any())
            {
                // Encontrou no banco - usar esses dados
                response = _mapper.Map<List<GameResponse>>(gamesFromDb);
                _logger.LogInformation("BANCO: Encontrou {Count} jogos para {Date}",
                    response.Count, date.ToString("yyyy-MM-dd"));

                // Salvar em cache (TTL dinâmico baseado na data)
                await _cacheService.SetAsync(cacheKey, response, CacheDurations.GetGameCacheDuration(date));
            }
            else if (date > DateTime.Today)
            {
                // Data futura sem dados no banco - buscar da ESPN
                _logger.LogInformation("Buscando jogos futuros da ESPN para {Date}",
                    date.ToString("yyyy-MM-dd"));

                var espnGames = await _espnService.GetGamesByDateAsync(date);
                response = await ConvertEspnGamesToResponseAsync(espnGames);
                _logger.LogInformation("ESPN: {Count} jogos futuros", response.Count);

                // Salvar em cache
                await _cacheService.SetAsync(cacheKey, response, CacheDurations.FutureGames);
            }
            else
            {
                // Data passada sem dados (vazio)
                response = new List<GameResponse>();
                _logger.LogWarning("Nenhum jogo encontrado para {Date}", date.ToString("yyyy-MM-dd"));
                await _cacheService.SetAsync(cacheKey, response, CacheDurations.Default);
            }

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
        /// Busca próximos jogos de um time (com cache Redis)
        /// </summary>
        public async Task<List<GameResponse>> GetUpcomingGamesForTeamAsync(int teamId, int days = 7)
        {
            var cacheKey = CacheKeys.UpcomingGamesByTeam(teamId, days);

            // 1. Tentar buscar do cache Redis
            var cachedData = await _cacheService.GetAsync<List<GameResponse>>(cacheKey);
            if (cachedData != null)
            {
                _logger.LogInformation("REDIS CACHE HIT: Próximos jogos do time {TeamId} ({Count} jogos)", teamId, cachedData.Count);
                return cachedData;
            }

            // 2. Buscar do banco/ESPN
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

            // 3. Salvar em cache (10 minutos para jogos futuros)
            await _cacheService.SetAsync(cacheKey, games, TimeSpan.FromMinutes(10));
            _logger.LogInformation("Salvos {Count} próximos jogos do time {TeamId} no Redis", games.Count, teamId);

            return games;
        }

        /// <summary>
        /// Busca jogos recentes de um time (com cache Redis)
        /// </summary>
        public async Task<List<GameResponse>> GetRecentGamesForTeamAsync(int teamId, int days = 7)
        {
            var cacheKey = CacheKeys.RecentGamesByTeam(teamId, days);

            // 1. Tentar buscar do cache Redis
            var cachedData = await _cacheService.GetAsync<List<GameResponse>>(cacheKey);
            if (cachedData != null)
            {
                _logger.LogInformation("REDIS CACHE HIT: Jogos recentes do time {TeamId} ({Count} jogos)", teamId, cachedData.Count);
                return cachedData;
            }

            // 2. Buscar do banco
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

            // 3. Salvar em cache (5 minutos para jogos passados)
            await _cacheService.SetAsync(cacheKey, games, TimeSpan.FromMinutes(5));
            _logger.LogInformation("Salvos {Count} jogos recentes do time {TeamId} no Redis", games.Count, teamId);

            return games;
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
        /// Sincroniza jogos por data específica (SIMPLIFICADO - sem duplicatas)
        /// </summary>
        public async Task<int> SyncGamesByDateAsync(DateTime date)
        {
            var syncCount = 0;
            var updateCount = 0;

            try
            {
                _logger.LogInformation("Sincronizando jogos para {Date}", date.ToString("yyyy-MM-dd"));

                // 1. Buscar jogos da API apropriada
                List<EspnGameDto>? espnGames = null;

                // USAR APENAS ESPN (fonte única confiável)
                try
                {
                    espnGames = await _espnService.GetGamesByDateAsync(date);
                    _logger.LogInformation("ESPN retornou {Count} jogos para {Date}", espnGames.Count, date.ToString("yyyy-MM-dd"));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao buscar da ESPN para {Date}", date);
                    return 0;
                }

                // 2. Se não há jogos, retornar
                if (espnGames == null || !espnGames.Any())
                {
                    _logger.LogInformation("Nenhum jogo encontrado para {Date}", date.ToString("yyyy-MM-dd"));
                    return 0;
                }

                // 3. Validar times e processar jogos (INSERT ou UPDATE)
                foreach (var espnGame in espnGames)
                {
                    try
                    {
                        var homeTeamId = await MapEspnTeamToSystemIdAsync(espnGame.HomeTeamId, espnGame.HomeTeamAbbreviation);
                        var visitorTeamId = await MapEspnTeamToSystemIdAsync(espnGame.AwayTeamId, espnGame.AwayTeamAbbreviation);

                        if (homeTeamId == 0 || visitorTeamId == 0)
                        {
                            _logger.LogWarning(
                                "Jogo ignorado - mapeamento falhou | ESPN: {Away}({AwayId}) @ {Home}({HomeId})",
                                espnGame.AwayTeamAbbreviation, espnGame.AwayTeamId,
                                espnGame.HomeTeamAbbreviation, espnGame.HomeTeamId);
                            continue;
                        }

                        // Validar se times existem no banco
                        var homeTeam = await _teamRepository.GetByIdAsync(homeTeamId);
                        var visitorTeam = await _teamRepository.GetByIdAsync(visitorTeamId);

                        if (homeTeam == null || visitorTeam == null)
                        {
                            _logger.LogError(
                                "CRÍTICO: Mapeamento retornou IDs inexistentes | System IDs: Home={HomeId}, Away={VisitorId}",
                                homeTeamId, visitorTeamId);
                            continue;
                        }

                        // Log detalhado do mapeamento bem-sucedido
                        _logger.LogDebug(
                            "Mapeamento OK | ESPN: {AwayAbbr}({AwayEspnId})→{AwayId} @ {HomeAbbr}({HomeEspnId})→{HomeId} | Score: {AwayScore}-{HomeScore}",
                            espnGame.AwayTeamAbbreviation, espnGame.AwayTeamId, visitorTeamId,
                            espnGame.HomeTeamAbbreviation, espnGame.HomeTeamId, homeTeamId,
                            espnGame.AwayTeamScore ?? 0, espnGame.HomeTeamScore ?? 0);

                        var gameDate = espnGame.Date.Date;
                        var existingGames = await _gameRepository.GetGamesByDateAsync(gameDate);
                        var existingGame = existingGames.FirstOrDefault(g =>
                            g.HomeTeamId == homeTeamId &&
                            g.VisitorTeamId == visitorTeamId &&
                            g.Date.Date == gameDate);

                        if (existingGame == null)
                        {
                            var newGame = new Game
                            {
                                ExternalId = GenerateExternalId(espnGame, gameDate),
                                Date = espnGame.Date.Date,
                                DateTime = espnGame.Date,
                                HomeTeamId = homeTeamId,
                                VisitorTeamId = visitorTeamId,
                                HomeTeamScore = espnGame.HomeTeamScore,
                                VisitorTeamScore = espnGame.AwayTeamScore,
                                Status = DetermineGameStatus(espnGame),
                                Period = espnGame.Period, 
                                TimeRemaining = espnGame.TimeRemaining, 
                                PostSeason = DeterminePostseason(espnGame), 
                                Season = espnGame.Season ?? GetSeasonYear(espnGame.Date), 
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            };

                            await _gameRepository.InsertAsync(newGame);
                            syncCount++;
                            _logger.LogDebug("Inserido: {Away} @ {Home}", visitorTeam.Abbreviation, homeTeam.Abbreviation);
                        }
                        else
                        {
                            var hasChanges = false;

                            if (existingGame.HomeTeamScore != espnGame.HomeTeamScore)
                            {
                                existingGame.HomeTeamScore = espnGame.HomeTeamScore;
                                hasChanges = true;
                            }

                            if (existingGame.VisitorTeamScore != espnGame.AwayTeamScore)
                            {
                                existingGame.VisitorTeamScore = espnGame.AwayTeamScore;
                                hasChanges = true;
                            }

                            var newStatus = DetermineGameStatus(espnGame);
                            if (existingGame.Status != newStatus)
                            {
                                existingGame.Status = newStatus;
                                hasChanges = true;
                            }

                            if (existingGame.Period != espnGame.Period)
                            {
                                existingGame.Period = espnGame.Period;
                                hasChanges = true;
                            }

                            if (existingGame.TimeRemaining != espnGame.TimeRemaining)
                            {
                                existingGame.TimeRemaining = espnGame.TimeRemaining;
                                hasChanges = true;
                            }

                            var isPostseason = DeterminePostseason(espnGame);
                            if (existingGame.PostSeason != isPostseason)
                            {
                                existingGame.PostSeason = isPostseason;
                                hasChanges = true;
                            }

                            if (hasChanges)
                            {
                                existingGame.DateTime = espnGame.Date;
                                existingGame.UpdatedAt = DateTime.UtcNow;
                                await _gameRepository.UpdateAsync(existingGame);
                                updateCount++;
                                _logger.LogDebug("Atualizado: {Away} {AwayScore} @ {Home} {HomeScore} (Q{Period})",
                                    visitorTeam.Abbreviation, espnGame.AwayTeamScore ?? 0,
                                    homeTeam.Abbreviation, espnGame.HomeTeamScore ?? 0,
                                    espnGame.Period ?? 0);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao processar jogo ESPN ID {GameId}", espnGame.Id);
                    }
                }

                // 4. Invalidar cache
                await InvalidateCacheForDate(date);

                _logger.LogInformation("✅ Sincronização concluída para {Date}: {New} novos, {Updated} atualizados",
                    date.ToShortDateString(), syncCount, updateCount);

                return syncCount + updateCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro crítico ao sincronizar jogos para {Date}", date);
                return syncCount + updateCount; 
            }
        }


        /// <summary>
        /// Gera ExternalId baseado no ID da ESPN (agora STRING)
        /// </summary>
        private string GenerateExternalId(EspnGameDto espnGame, DateTime date)
        {
            if (!string.IsNullOrEmpty(espnGame.Id))
            {
                return espnGame.Id;
            }

            return $"ESPN_{espnGame.HomeTeamId}_{espnGame.AwayTeamId}_{date:yyyyMMdd}";
        }

        /// <summary>
        /// Determina status do jogo baseado nos dados da ESPN
        /// </summary>
        private GameStatus DetermineGameStatus(EspnGameDto espnGame)
        {
            if (espnGame.HomeTeamScore.HasValue && espnGame.AwayTeamScore.HasValue &&
                espnGame.Date < DateTime.Now.AddHours(-2))
            {
                _logger.LogDebug("🏁 Forcing Final status - game has scores ({Away}-{Home}) and date is in the past ({Date})",
                    espnGame.AwayTeamScore, espnGame.HomeTeamScore, espnGame.Date);
                return GameStatus.Final;
            }

            if (!string.IsNullOrEmpty(espnGame.Status))
            {
                return MapGameStatus(espnGame.Status);
            }

            return espnGame.Date > DateTime.Now ? GameStatus.Scheduled : GameStatus.Final;
        }

        /// <summary>
        /// Invalida cache para uma data específica
        /// </summary>
        private async Task InvalidateCacheForDate(DateTime date)
        {
            await _cacheService.RemoveAsync(CacheKeys.TodayGames());
            await _cacheService.RemoveAsync(CacheKeys.GamesByDate(date));
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

                await Task.Delay(1000);

                currentDate = currentDate.AddDays(1);
            }

            return totalSynced;
        }

        /// <summary>
        /// Sincroniza jogos FUTUROS da ESPN 
        /// </summary>
        public async Task<int> SyncFutureGamesAsync(int days = 10)
        {
            try
            {
                var startDate = DateTime.Today.AddDays(1);
                var endDate = DateTime.Today.AddDays(days);

                _logger.LogInformation(
                    "Syncing future games from {Start} to {End}",
                    startDate.ToShortDateString(),
                    endDate.ToShortDateString()
                );

                var totalSynced = 0;

                var currentDate = startDate;
                while (currentDate <= endDate)
                {
                    try
                    {
                        var synced = await SyncGamesByDateAsync(currentDate);
                        totalSynced += synced;

                        await Task.Delay(500);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error syncing future games for {Date}", currentDate);
                    }

                    currentDate = currentDate.AddDays(1);
                }

                _logger.LogInformation("Synced {Count} future games across {Days} days", totalSynced, days);
                return totalSynced;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing future games");
                return 0;
            }
        }

        /// <summary>
        /// Determina o ano da temporada baseado na data
        /// </summary>
        private int GetSeasonYear(DateTime date)
        {
            return date.Month >= 10 ? date.Year : date.Year - 1;
        }

        /// <summary>
        /// Determina se o jogo é de playoffs
        /// </summary>
        private bool DeterminePostseason(EspnGameDto espnGame)
        {
            if (espnGame.IsPostseason.HasValue)
            {
                return espnGame.IsPostseason.Value;
            }

            var month = espnGame.Date.Month;
            if (month >= 4 && month <= 6)
            {
                _logger.LogDebug("Jogo detectado como Playoff (mês {Month})", month);
                return true;
            }

            return false;
        }

        #endregion

        #region Métodos Privados

        /// <summary>
        /// Busca jogos do banco de dados
        /// </summary>
        private async Task<List<GameResponse>> GetGamesFromDatabaseAsync(List<int> teamIds, DateTime startDate, DateTime endDate)
        {
            var allGames = new List<Game>();

            var currentDate = startDate;
            while (currentDate <= endDate)
            {
                var dayGames = await _gameRepository.GetGamesByDateAsync(currentDate);
                allGames.AddRange(dayGames);
                currentDate = currentDate.AddDays(1);
            }

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

            var tasks = teamIds.Select(async teamId =>
            {
                try
                {
                    var espnGames = await _espnService.GetTeamScheduleAsync(teamId, startDate, endDate);
                    return await ConvertEspnGamesToResponseAsync(espnGames);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching ESPN games for team {TeamId}", teamId);
                    return new List<GameResponse>();
                }
            });

            var results = await Task.WhenAll(tasks);
            allGames = results.SelectMany(r => r).ToList();

            return allGames
                .GroupBy(g => new { Date = g.Date.Date, HomeTeamId = g.HomeTeam.Id, VisitorTeamId = g.VisitorTeam.Id })
                .Select(g => g.First())
                .ToList();
        }

        /// <summary>
        /// Converte jogos da ESPN para GameResponse (ASYNC para buscar times do banco)
        /// </summary>
        private async Task<List<GameResponse>> ConvertEspnGamesToResponseAsync(List<EspnGameDto> espnGames)
        {
            var responses = new List<GameResponse>();

            foreach (var eg in espnGames)
            {
                var homeTeamId = await MapEspnTeamToSystemIdAsync(eg.HomeTeamId, eg.HomeTeamAbbreviation);
                var awayTeamId = await MapEspnTeamToSystemIdAsync(eg.AwayTeamId, eg.AwayTeamAbbreviation);

                if (homeTeamId == 0 || awayTeamId == 0)
                {
                    _logger.LogWarning(
                        "Skipping future game - mapping failed | {Away}({AwayId}) @ {Home}({HomeId})",
                        eg.AwayTeamAbbreviation, eg.AwayTeamId,
                        eg.HomeTeamAbbreviation, eg.HomeTeamId);
                    continue;
                }

                var homeTeam = await _teamRepository.GetByIdAsync(homeTeamId);
                var awayTeam = await _teamRepository.GetByIdAsync(awayTeamId);

                if (homeTeam == null || awayTeam == null)
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
                        Abbreviation = homeTeam.Abbreviation 
                    },
                    VisitorTeam = new TeamSummaryResponse
                    {
                        Id = awayTeam.Id,
                        Name = awayTeam.Name,
                        Abbreviation = awayTeam.Abbreviation 
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

            var normalized = externalStatus.Trim().ToLowerInvariant();

            var mapped = normalized switch
            {
                "final" or "completed" => GameStatus.Final,
                "in progress" or "live" or "in_progress" => GameStatus.Live,
                "scheduled" or "pre" or "pregame" => GameStatus.Scheduled,
                "postponed" or "delayed" => GameStatus.Postponed,
                "cancelled" or "canceled" => GameStatus.Cancelled,
                _ => (GameStatus?)null
            };

            if (mapped == null)
            {
                _logger.LogWarning("Status de jogo desconhecido: '{Status}' - usando Scheduled como padrão", externalStatus);
                return GameStatus.Scheduled;
            }

            return mapped.Value;
        }

        /// <summary>
        /// Mapeia time da ESPN para ID do sistema usando ABREVIAÇÃO
        /// </summary>
        private async Task<int> MapEspnTeamToSystemIdAsync(string espnTeamId, string espnAbbreviation)
        {
            if (string.IsNullOrEmpty(espnAbbreviation))
            {
                _logger.LogWarning("ESPN Abbreviation is null/empty for team ID: {EspnTeamId}", espnTeamId);
                return 0;
            }

            var team = await _teamRepository.GetByAbbreviationAsync(espnAbbreviation);

            if (team != null)
            {
                _logger.LogTrace("Mapped {Abbr} → System ID {Id}", espnAbbreviation, team.Id);
                return team.Id;
            }

            _logger.LogWarning(
                "Team not found in database | ESPN Abbr: {Abbr}, ESPN ID: {EspnId}",
                espnAbbreviation, espnTeamId);
            return 0;
        }


        #endregion
    }
}