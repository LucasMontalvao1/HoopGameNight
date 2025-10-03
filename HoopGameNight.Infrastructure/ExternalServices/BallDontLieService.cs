using HoopGameNight.Core.DTOs.External.BallDontLie;
using HoopGameNight.Core.DTOs.External.ESPN;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HoopGameNight.Infrastructure.ExternalServices
{
    public class BallDontLieService : IBallDontLieService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<BallDontLieService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly string _apiKey;
        private readonly IEspnApiService _espnApiService;

        // RATE LIMITING - 60 requests per minute
        private static readonly SemaphoreSlim _rateLimitSemaphore = new(1, 1);
        private static DateTime _lastRequestTime = DateTime.MinValue;
        private const int MinDelayBetweenRequestsMs = 2000;
        private static DateTime _rateLimitResetTime = DateTime.MinValue;

        public BallDontLieService(
            HttpClient httpClient,
            ILogger<BallDontLieService> logger,
            IConfiguration configuration,
            IEspnApiService espnApiService)
        {
            _httpClient = httpClient;
            _logger = logger;
            _espnApiService = espnApiService;

            // CONFIGURAÇÃO DA API KEY
            var ballDontLieConfig = configuration.GetSection("ExternalApis:BallDontLie");
            _apiKey = ballDontLieConfig["ApiKey"] ?? throw new InvalidOperationException("BallDontLie ApiKey not configured");

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                PropertyNameCaseInsensitive = true
            };

            _logger.LogInformation("BallDontLieService initialized with API key configured: {HasApiKey}", !string.IsNullOrEmpty(_apiKey));
        }

        // PROPRIEDADES PÚBLICAS PARA CIRCUIT BREAKER
        public bool IsRateLimited => DateTime.UtcNow < _rateLimitResetTime;
        public TimeSpan TimeUntilReset => _rateLimitResetTime > DateTime.UtcNow
            ? _rateLimitResetTime - DateTime.UtcNow
            : TimeSpan.Zero;

        public async Task<IEnumerable<BallDontLieGameDto>> GetTodaysGamesAsync()
        {
            return await ExecuteWithRetry(async () =>
            {
                try
                {
                    var today = DateTime.Today.ToString("yyyy-MM-dd");
                    _logger.LogDebug("Fetching games for today: {Date}", today);

                    var response = await ExecuteWithRateLimit(async () =>
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/games?dates[]={today}");
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                        return await _httpClient.SendAsync(request);
                    });

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var apiResponse = JsonSerializer.Deserialize<BallDontLieApiResponse<BallDontLieGameDto>>(content, _jsonOptions);

                        _logger.LogInformation("Retrieved {GameCount} games from Ball Don't Lie API for today", apiResponse?.Data.Count ?? 0);
                        return apiResponse?.Data ?? Enumerable.Empty<BallDontLieGameDto>();
                    }

                    return await HandleErrorResponse<BallDontLieGameDto>(response, "fetch today's games");

                }
                catch (HttpRequestException ex) when (ex.Message.Contains("429"))
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching today's games from Ball Don't Lie API");
                    throw new Core.Exceptions.ExternalApiException("Ball Don't Lie", "Failed to fetch today's games", (int?)null);
                }
            });
        }

        public async Task<IEnumerable<BallDontLieGameDto>> GetGamesByDateAsync(DateTime date)
        {
            return await ExecuteWithRetry(async () =>
            {
                try
                {
                    var dateString = date.ToString("yyyy-MM-dd");
                    _logger.LogDebug("Fetching games for date: {Date}", dateString);

                    var response = await ExecuteWithRateLimit(async () =>
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/games?dates[]={dateString}");
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                        return await _httpClient.SendAsync(request);
                    });

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var apiResponse = JsonSerializer.Deserialize<BallDontLieApiResponse<BallDontLieGameDto>>(content, _jsonOptions);

                        _logger.LogInformation("Retrieved {GameCount} games from Ball Don't Lie API for {Date}", apiResponse?.Data.Count ?? 0, dateString);
                        return apiResponse?.Data ?? Enumerable.Empty<BallDontLieGameDto>();
                    }

                    return await HandleErrorResponse<BallDontLieGameDto>(response, $"fetch games for {dateString}");
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "HTTP error fetching games for date {Date} from Ball Don't Lie API", date);
                    var statusCode = ExtractStatusCodeFromException(ex);
                    throw new Core.Exceptions.ExternalApiException("Ball Don't Lie", $"Erro HTTP ao buscar jogos para {date:yyyy-MM-dd}", ex, statusCode);
                }
                catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
                {
                    _logger.LogWarning("Timeout fetching games for date {Date} from Ball Don't Lie API", date);
                    throw new Core.Exceptions.ExternalApiException("Ball Don't Lie", $"Tempo limite esgotado ao buscar jogos para {date:yyyy-MM-dd}", ex, 408);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error fetching games for date {Date} from Ball Don't Lie API", date);
                    throw new Core.Exceptions.ExternalApiException("Ball Don't Lie", $"Falha ao buscar jogos para {date:yyyy-MM-dd}", ex);
                }
            });
        }

        public async Task<IEnumerable<BallDontLieTeamDto>> GetAllTeamsAsync()
        {
            return await ExecuteWithRetry(async () =>
            {
                try
                {
                    _logger.LogDebug("Fetching all teams from Ball Don't Lie API");

                    var response = await ExecuteWithRateLimit(async () =>
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/teams");
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                        return await _httpClient.SendAsync(request);
                    });

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var apiResponse = JsonSerializer.Deserialize<BallDontLieApiResponse<BallDontLieTeamDto>>(content, _jsonOptions);

                        _logger.LogInformation("Retrieved {TeamCount} teams from Ball Don't Lie API", apiResponse?.Data.Count ?? 0);
                        return apiResponse?.Data ?? Enumerable.Empty<BallDontLieTeamDto>();
                    }

                    return await HandleErrorResponse<BallDontLieTeamDto>(response, "fetch teams");
                }
                catch (HttpRequestException ex) when (ex.Message.Contains("429"))
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching teams from Ball Don't Lie API");
                    throw new Core.Exceptions.ExternalApiException("Ball Don't Lie", "Failed to fetch teams", (int?)null);
                }
            });
        }

        public async Task<IEnumerable<BallDontLiePlayerDto>> SearchPlayersAsync(string search, int page = 1)
        {
            return await ExecuteWithRetry(async () =>
            {
                try
                {
                    _logger.LogDebug("Searching players with term: {SearchTerm}, page: {Page}", search, page);

                    var response = await ExecuteWithRateLimit(async () =>
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/players?search={Uri.EscapeDataString(search)}&page={page}");
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                        return await _httpClient.SendAsync(request);
                    });

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var apiResponse = JsonSerializer.Deserialize<BallDontLieApiResponse<BallDontLiePlayerDto>>(content, _jsonOptions);

                        _logger.LogInformation("Retrieved {PlayerCount} players from Ball Don't Lie API for search: {SearchTerm}", apiResponse?.Data.Count ?? 0, search);
                        return apiResponse?.Data ?? Enumerable.Empty<BallDontLiePlayerDto>();
                    }

                    return await HandleErrorResponse<BallDontLiePlayerDto>(response, $"search players for '{search}'");
                }
                catch (HttpRequestException ex) when (ex.Message.Contains("429"))
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error searching players with term: {SearchTerm} from Ball Don't Lie API", search);
                    throw new Core.Exceptions.ExternalApiException("Ball Don't Lie", $"Failed to search players for '{search}'", (int?)null);
                }
            });
        }

        public async Task<BallDontLiePlayerDto?> GetPlayerByIdAsync(int playerId)
        {
            return await ExecuteWithRetry(async () =>
            {
                try
                {
                    _logger.LogDebug("Fetching player with ID: {PlayerId}", playerId);

                    var response = await ExecuteWithRateLimit(async () =>
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/players/{playerId}");
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                        return await _httpClient.SendAsync(request);
                    });

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var player = JsonSerializer.Deserialize<BallDontLiePlayerDto>(content, _jsonOptions);

                        _logger.LogInformation("Retrieved player {PlayerId} from Ball Don't Lie API", playerId);
                        return player;
                    }

                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger.LogWarning("Player not found with ID: {PlayerId}", playerId);
                        return null;
                    }

                    await HandleErrorResponse<BallDontLiePlayerDto>(response, $"fetch player {playerId}");
                    return null;
                }
                catch (HttpRequestException ex) when (ex.Message.Contains("429"))
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching player {PlayerId} from Ball Don't Lie API", playerId);
                    throw new Core.Exceptions.ExternalApiException("Ball Don't Lie", $"Failed to fetch player {playerId}", (int?)null);
                }
            });
        }

        /// <summary>
        /// Método centralizado para handling de erros
        /// </summary>
        private async Task<IEnumerable<T>> HandleErrorResponse<T>(HttpResponseMessage response, string operation)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to {Operation}. Status: {StatusCode}, Content: {Content}",
                operation, response.StatusCode, errorContent);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _rateLimitResetTime = DateTime.UtcNow.AddMinutes(1).AddSeconds(10);
                _logger.LogWarning("Rate limit detected. Reset time set to: {ResetTime}", _rateLimitResetTime);
                throw new HttpRequestException("429 - Too Many Requests");
            }

            return Enumerable.Empty<T>();
        }

        /// <summary>
        /// Executa operação com rate limiting (máximo 60 requests/min)
        /// </summary>
        private async Task<HttpResponseMessage> ExecuteWithRateLimit(Func<Task<HttpResponseMessage>> operation)
        {
            await _rateLimitSemaphore.WaitAsync();
            try
            {
                if (DateTime.UtcNow < _rateLimitResetTime)
                {
                    var waitTime = _rateLimitResetTime - DateTime.UtcNow;
                    _logger.LogWarning("⏰ Waiting for rate limit reset: {WaitTime}s", waitTime.TotalSeconds);
                    await Task.Delay(waitTime);
                }

                var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
                if (timeSinceLastRequest.TotalMilliseconds < MinDelayBetweenRequestsMs)
                {
                    var delay = MinDelayBetweenRequestsMs - (int)timeSinceLastRequest.TotalMilliseconds;
                    _logger.LogDebug("Rate limiting: waiting {Delay}ms before next request", delay);
                    await Task.Delay(delay);
                }

                _lastRequestTime = DateTime.UtcNow;
                var response = await operation();

                _logger.LogDebug("API Request completed. Status: {StatusCode}, URL: {Url}",
                    response.StatusCode, response.RequestMessage?.RequestUri);

                return response;
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }

        /// <summary>
        /// Executa operação com retry em caso de rate limit (429)
        /// </summary>
        private async Task<T> ExecuteWithRetry<T>(Func<Task<T>> operation, int maxRetries = 2)
        {
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    return await operation();
                }
                catch (HttpRequestException ex) when (
                    ex.Message.Contains("429") ||
                    ex.Message.Contains("TooManyRequests"))
                {
                    if (attempt == maxRetries - 1)
                    {
                        _logger.LogError("Rate limit exceeded after {MaxRetries} retries. Service temporarily unavailable", maxRetries);

                        _rateLimitResetTime = DateTime.UtcNow.AddMinutes(2);

                        throw new InvalidOperationException("Rate limit exceeded - service temporarily unavailable", ex);
                    }

                    var delay = TimeSpan.FromSeconds(30 * (attempt + 1));
                    _logger.LogWarning("Rate limit hit, waiting {Delay}s before retry {Attempt}/{MaxRetries}",
                        delay.TotalSeconds, attempt + 1, maxRetries);

                    await Task.Delay(delay);
                }
            }

            throw new InvalidOperationException("Should not reach here");
        }

        public async Task<BallDontLiePlayerSeasonStatsDto?> GetPlayerSeasonStatsAsync(int playerId, int season)
        {
            try
            {
                _logger.LogInformation("Using ESPN API for player season stats - Player: {PlayerId}, Season: {Season}", playerId, season);

                // Map Ball Don't Lie player ID to ESPN player ID (for now we'll try to find a mapping)
                var espnPlayerId = await GetEspnPlayerIdAsync(playerId);
                if (string.IsNullOrEmpty(espnPlayerId))
                {
                    _logger.LogWarning("Could not find ESPN player ID for Ball Don't Lie player ID: {PlayerId}", playerId);
                    return null;
                }

                // Use the specific season stats endpoint
                var espnStats = await _espnApiService.GetPlayerSeasonStatsAsync(espnPlayerId, season);
                if (espnStats?.Splits?.Categories == null)
                {
                    _logger.LogWarning("No ESPN season stats found for player: {EspnPlayerId}, Season: {Season}", espnPlayerId, season);
                    return null;
                }

                return MapEspnToBallDontLieSeasonStats(espnStats, playerId, season);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching player season stats from ESPN for player {PlayerId}", playerId);
                return null;
            }
        }

        public async Task<IEnumerable<BallDontLiePlayerGameStatsDto>> GetPlayerRecentGamesAsync(int playerId, int limit)
        {
            try
            {
                _logger.LogInformation("Using ESPN API for player recent games - Player: {PlayerId}, Limit: {Limit}", playerId, limit);

                // For now, return empty since ESPN doesn't have a direct "recent games" endpoint
                // This would require fetching team schedule and filtering by player
                _logger.LogWarning("ESPN API does not have a direct recent games endpoint for players. Returning empty result.");
                return Enumerable.Empty<BallDontLiePlayerGameStatsDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching player recent games from ESPN for player {PlayerId}", playerId);
                return Enumerable.Empty<BallDontLiePlayerGameStatsDto>();
            }
        }

        public async Task<BallDontLiePlayerGameStatsDto?> GetPlayerGameStatsAsync(int playerId, int gameId)
        {
            try
            {
                _logger.LogInformation("Using ESPN API for player game stats - Player: {PlayerId}, Game: {GameId}", playerId, gameId);

                // Map Ball Don't Lie player ID to ESPN player ID
                var espnPlayerId = await GetEspnPlayerIdAsync(playerId);
                if (string.IsNullOrEmpty(espnPlayerId))
                {
                    _logger.LogWarning("Could not find ESPN player ID for Ball Don't Lie player ID: {PlayerId}", playerId);
                    return null;
                }

                // Map Ball Don't Lie game ID to ESPN game ID (for now use the game ID as-is)
                var espnGameId = gameId.ToString();

                var espnStats = await _espnApiService.GetPlayerGameStatsAsync(espnPlayerId, espnGameId);
                if (espnStats?.Splits?.Categories == null)
                {
                    _logger.LogWarning("No ESPN game stats found for player: {EspnPlayerId}, Game: {EspnGameId}", espnPlayerId, espnGameId);
                    return null;
                }

                return MapEspnToBallDontLieGameStats(espnStats, playerId, gameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching player game stats from ESPN for player {PlayerId}", playerId);
                return null;
            }
        }

        #region ESPN Mapping Methods

        private async Task<string?> GetEspnPlayerIdAsync(int ballDontLiePlayerId)
        {
            // For now, we'll use a simple mapping strategy
            // In a real implementation, you would store this mapping in the database
            // or create a more sophisticated mapping algorithm

            // As a temporary solution, we'll try to match by getting the first ESPN player
            // This is not ideal but will work for testing
            try
            {
                var athletes = await _espnApiService.GetAllPlayersAsync();
                if (athletes.Any())
                {
                    // Extract ESPN player ID from the first reference URL
                    var firstAthlete = athletes.First();
                    var match = Regex.Match(firstAthlete.Ref, @"/athletes/(\d+)");
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting ESPN player ID for Ball Don't Lie player {PlayerId}", ballDontLiePlayerId);
            }

            return null;
        }

        private BallDontLiePlayerSeasonStatsDto? MapEspnToBallDontLieSeasonStats(EspnPlayerStatsDto espnStats, int playerId, int season)
        {
            try
            {
                var offensiveStats = espnStats.Splits.Categories.FirstOrDefault(c => c.Name == "offensive");
                var generalStats = espnStats.Splits.Categories.FirstOrDefault(c => c.Name == "general");
                var defensiveStats = espnStats.Splits.Categories.FirstOrDefault(c => c.Name == "defensive");

                if (offensiveStats?.Stats == null && generalStats?.Stats == null)
                {
                    return null;
                }

                var result = new BallDontLiePlayerSeasonStatsDto
                {
                    PlayerId = playerId,
                    Season = season,
                };

                // Map offensive stats
                if (offensiveStats?.Stats != null)
                {
                    result.Pts = GetStatValue(offensiveStats.Stats, "avgPoints");
                    result.Ast = GetStatValue(offensiveStats.Stats, "avgAssists");
                    result.FgPct = GetStatValue(offensiveStats.Stats, "fieldGoalPct") / 100; // Convert percentage
                    result.Fg3Pct = GetStatValue(offensiveStats.Stats, "threePointPct") / 100; // Convert percentage
                    result.FtPct = GetStatValue(offensiveStats.Stats, "freeThrowPct") / 100; // Convert percentage
                    result.Turnover = GetStatValue(offensiveStats.Stats, "avgTurnovers");
                }

                // Map general stats
                if (generalStats?.Stats != null)
                {
                    result.GamesPlayed = (int)GetStatValue(generalStats.Stats, "gamesPlayed");
                    result.Min = GetStatValue(generalStats.Stats, "avgMinutes").ToString("F1"); // Convert to string
                    result.Reb = GetStatValue(generalStats.Stats, "avgRebounds");
                }

                // Map defensive stats
                if (defensiveStats?.Stats != null)
                {
                    result.Stl = GetStatValue(defensiveStats.Stats, "avgSteals");
                    result.Blk = GetStatValue(defensiveStats.Stats, "avgBlocks");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mapping ESPN stats to Ball Don't Lie format");
                return null;
            }
        }

        private BallDontLiePlayerGameStatsDto? MapEspnToBallDontLieGameStats(EspnPlayerStatsDto espnStats, int playerId, int gameId)
        {
            try
            {
                var offensiveStats = espnStats.Splits.Categories.FirstOrDefault(c => c.Name == "offensive");
                var generalStats = espnStats.Splits.Categories.FirstOrDefault(c => c.Name == "general");
                var defensiveStats = espnStats.Splits.Categories.FirstOrDefault(c => c.Name == "defensive");

                if (offensiveStats?.Stats == null && generalStats?.Stats == null)
                {
                    return null;
                }

                var result = new BallDontLiePlayerGameStatsDto
                {
                    Player = new BallDontLiePlayerDto { Id = playerId },
                    Game = new BallDontLieGameDto { Id = gameId },
                };

                // Map offensive stats (note: for game stats we use direct values, not averages)
                if (offensiveStats?.Stats != null)
                {
                    result.Pts = (int)GetStatValue(offensiveStats.Stats, "points");
                    result.Ast = (int)GetStatValue(offensiveStats.Stats, "assists");
                    result.FgPct = GetStatValue(offensiveStats.Stats, "fieldGoalPct") / 100; // Convert percentage
                    result.Fg3Pct = GetStatValue(offensiveStats.Stats, "threePointPct") / 100; // Convert percentage
                    result.FtPct = GetStatValue(offensiveStats.Stats, "freeThrowPct") / 100; // Convert percentage
                    result.Turnover = (int)GetStatValue(offensiveStats.Stats, "turnovers");
                }

                // Map general stats
                if (generalStats?.Stats != null)
                {
                    result.Min = GetStatValue(generalStats.Stats, "minutes").ToString("F1"); // Convert to string
                    result.Reb = (int)GetStatValue(generalStats.Stats, "rebounds");
                }

                // Map defensive stats
                if (defensiveStats?.Stats != null)
                {
                    result.Stl = (int)GetStatValue(defensiveStats.Stats, "steals");
                    result.Blk = (int)GetStatValue(defensiveStats.Stats, "blocks");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mapping ESPN game stats to Ball Don't Lie format");
                return null;
            }
        }

        private double GetStatValue(List<EspnStatDto> stats, string statName)
        {
            var stat = stats.FirstOrDefault(s => s.Name == statName);
            return stat?.Value ?? 0;
        }

        /// <summary>
        /// Extrai o código de status HTTP de uma HttpRequestException
        /// </summary>
        private static int? ExtractStatusCodeFromException(HttpRequestException ex)
        {
            // Tenta extrair o código de status da mensagem de erro
            if (ex.Data.Contains("StatusCode"))
            {
                return (int)ex.Data["StatusCode"];
            }

            // Pattern matching para códigos comuns na mensagem
            var message = ex.Message.ToLowerInvariant();

            if (message.Contains("429") || message.Contains("too many requests"))
                return 429;
            if (message.Contains("401") || message.Contains("unauthorized"))
                return 401;
            if (message.Contains("403") || message.Contains("forbidden"))
                return 403;
            if (message.Contains("404") || message.Contains("not found"))
                return 404;
            if (message.Contains("500") || message.Contains("internal server error"))
                return 500;
            if (message.Contains("502") || message.Contains("bad gateway"))
                return 502;
            if (message.Contains("503") || message.Contains("service unavailable"))
                return 503;
            if (message.Contains("504") || message.Contains("gateway timeout"))
                return 504;

            return null;
        }

        #endregion
    }
}