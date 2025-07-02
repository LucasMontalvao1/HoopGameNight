using AutoMapper;
using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Enums;
using HoopGameNight.Core.Exceptions;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Core.Services
{
    public class GameService : IGameService
    {
        private readonly IGameRepository _gameRepository;
        private readonly ITeamRepository _teamRepository;
        private readonly IBallDontLieService _ballDontLieService;
        private readonly IMapper _mapper;
        private readonly IMemoryCache _cache;
        private readonly ILogger<GameService> _logger;

        public GameService(
            IGameRepository gameRepository,
            ITeamRepository teamRepository,
            IBallDontLieService ballDontLieService,
            IMapper mapper,
            IMemoryCache cache,
            ILogger<GameService> logger)
        {
            _gameRepository = gameRepository;
            _teamRepository = teamRepository;
            _ballDontLieService = ballDontLieService;
            _mapper = mapper;
            _cache = cache;
            _logger = logger;
        }

        public async Task<List<GameResponse>> GetTodayGamesAsync()
        {
            try
            {
                _logger.LogInformation("Getting today's games");

                var games = await _gameRepository.GetTodayGamesAsync();
                var response = _mapper.Map<List<GameResponse>>(games);

                _logger.LogInformation("Retrieved {GameCount} games for today", response.Count);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting today's games");
                throw new BusinessException("Failed to retrieve today's games", ex);
            }
        }

        public async Task<List<GameResponse>> GetGamesByDateAsync(DateTime date)
        {
            try
            {
                _logger.LogInformation("Getting games for date: {Date}", date.ToShortDateString());

                var games = await _gameRepository.GetGamesByDateAsync(date);
                var response = _mapper.Map<List<GameResponse>>(games);

                _logger.LogInformation("Retrieved {GameCount} games for {Date}", response.Count, date.ToShortDateString());
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting games for date: {Date}", date);
                throw new BusinessException($"Failed to retrieve games for {date:yyyy-MM-dd}", ex);
            }
        }

        public async Task<(List<GameResponse> Games, int TotalCount)> GetGamesAsync(GetGamesRequest request)
        {
            try
            {
                _logger.LogInformation("Getting games with filters: {@Request}", request);

                var (games, totalCount) = await _gameRepository.GetGamesAsync(request);
                var response = _mapper.Map<List<GameResponse>>(games);

                _logger.LogInformation("Retrieved {GameCount} games (total: {TotalCount})", response.Count, totalCount);
                return (response, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting games with filters: {@Request}", request);
                throw new BusinessException("Failed to retrieve games", ex);
            }
        }

        public async Task<(List<GameResponse> Games, int TotalCount)> GetGamesByTeamAsync(int teamId, int page, int pageSize)
        {
            try
            {
                _logger.LogInformation("Getting games for team: {TeamId}", teamId);

                var (games, totalCount) = await _gameRepository.GetGamesByTeamAsync(teamId, page, pageSize);
                var response = _mapper.Map<List<GameResponse>>(games);

                _logger.LogInformation("Retrieved {GameCount} games for team {TeamId}", response.Count, teamId);
                return (response, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting games for team: {TeamId}", teamId);
                throw new BusinessException($"Failed to retrieve games for team {teamId}", ex);
            }
        }

        public async Task<GameResponse?> GetGameByIdAsync(int id)
        {
            try
            {
                _logger.LogInformation("Getting game by ID: {GameId}", id);

                var game = await _gameRepository.GetByIdAsync(id);
                if (game == null)
                {
                    _logger.LogWarning("Game not found: {GameId}", id);
                    return null;
                }

                var response = _mapper.Map<GameResponse>(game);
                _logger.LogInformation("Retrieved game: {GameTitle}", response.GameTitle);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting game by ID: {GameId}", id);
                throw new BusinessException($"Failed to retrieve game {id}", ex);
            }
        }

        public async Task SyncTodayGamesAsync()
        {
            await SyncGamesByDateAsync(DateTime.Today);
        }

        /// <summary>
        /// ✅ NOVO: Método para sincronizar jogos por data específica
        /// </summary>
        public async Task<int> SyncGamesByDateAsync(DateTime date)
        {
            try
            {
                _logger.LogInformation("Starting sync of games for date: {Date}", date.ToShortDateString());

                var externalGames = await _ballDontLieService.GetGamesByDateAsync(date);
                _logger.LogInformation("Found {Count} games from external API for {Date}", externalGames.Count(), date.ToShortDateString());

                var syncCount = 0;

                foreach (var externalGame in externalGames)
                {
                    try
                    {
                        // Verificar se o jogo já existe
                        var existingGame = await _gameRepository.GetByExternalIdAsync(externalGame.Id);

                        if (existingGame == null)
                        {
                            // Verificar se os times existem no banco
                            var homeTeam = await _teamRepository.GetByExternalIdAsync(externalGame.HomeTeam.Id);
                            var visitorTeam = await _teamRepository.GetByExternalIdAsync(externalGame.VisitorTeam.Id);

                            if (homeTeam == null || visitorTeam == null)
                            {
                                _logger.LogWarning("Skipping game {GameId} - Teams not found. Home: {HomeId}, Visitor: {VisitorId}",
                                    externalGame.Id, externalGame.HomeTeam.Id, externalGame.VisitorTeam.Id);
                                continue;
                            }

                            // Criar nova entidade de jogo
                            var game = new Models.Entities.Game
                            {
                                ExternalId = externalGame.Id,
                                Date = DateTime.Parse(externalGame.Date), // ✅ Corrigido: Converter string para DateTime
                                DateTime = DateTime.Parse(externalGame.Date), // ✅ Mesmo valor para DateTime
                                HomeTeamId = homeTeam.Id,
                                VisitorTeamId = visitorTeam.Id,
                                HomeTeamScore = externalGame.HomeTeamScore,
                                VisitorTeamScore = externalGame.VisitorTeamScore,
                                Status = MapGameStatus(externalGame.Status), // ✅ Converter para enum
                                Period = externalGame.Period,
                                TimeRemaining = externalGame.Time,
                                PostSeason = externalGame.Postseason,
                                Season = externalGame.Season,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            };

                            await _gameRepository.InsertAsync(game); // ✅ InsertAsync é o método correto
                            syncCount++;

                            _logger.LogInformation("Synced game: {HomeTeam} vs {VisitorTeam} on {Date}",
                                homeTeam.Name, visitorTeam.Name, game.Date);
                        }
                        else
                        {
                            // Atualizar jogo existente
                            existingGame.HomeTeamScore = externalGame.HomeTeamScore;
                            existingGame.VisitorTeamScore = externalGame.VisitorTeamScore;
                            existingGame.Status = MapGameStatus(externalGame.Status); // ✅ Converter para enum
                            existingGame.Period = externalGame.Period;
                            existingGame.TimeRemaining = externalGame.Time;
                            existingGame.UpdatedAt = DateTime.UtcNow;

                            await _gameRepository.UpdateAsync(existingGame);

                            _logger.LogDebug("Updated existing game: {GameId}", existingGame.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error syncing game {GameId}", externalGame.Id);
                        // Continue com o próximo jogo
                    }
                }

                // Limpar cache relevante
                _cache.Remove(Constants.CacheKeys.TODAY_GAMES);
                _cache.Remove(string.Format(Constants.CacheKeys.GAMES_BY_DATE, date.ToString("yyyy-MM-dd")));

                _logger.LogInformation("Synced {SyncCount} new games for {Date}", syncCount, date.ToShortDateString());
                return syncCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing games for date: {Date}", date);
                throw new ExternalApiException("Ball Don't Lie", $"Failed to sync games for {date:yyyy-MM-dd}", null);
            }
        }

        /// <summary>
        /// ✅ NOVO: Sincronizar jogos de um período
        /// </summary>
        public async Task<int> SyncGamesForPeriodAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                _logger.LogInformation("Starting sync of games from {StartDate} to {EndDate}",
                    startDate.ToShortDateString(), endDate.ToShortDateString());

                var totalSynced = 0;
                var currentDate = startDate;

                while (currentDate <= endDate)
                {
                    var synced = await SyncGamesByDateAsync(currentDate);
                    totalSynced += synced;

                    // Aguardar um pouco entre as requisições para evitar rate limit
                    await Task.Delay(2000);

                    currentDate = currentDate.AddDays(1);
                }

                _logger.LogInformation("Synced {TotalCount} games for period {StartDate} to {EndDate}",
                    totalSynced, startDate.ToShortDateString(), endDate.ToShortDateString());

                return totalSynced;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing games for period");
                throw;
            }
        }

        /// <summary>
        /// Converter status da API externa para enum
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
    }
}