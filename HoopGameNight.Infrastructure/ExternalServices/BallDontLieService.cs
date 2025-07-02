using HoopGameNight.Core.DTOs.External.BallDontLie;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text.Json;

namespace HoopGameNight.Infrastructure.ExternalServices
{
    public class BallDontLieService : IBallDontLieService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<BallDontLieService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly string _apiKey;

        // ✅ RATE LIMITING - 60 requests per minute
        private static readonly SemaphoreSlim _rateLimitSemaphore = new(1, 1);
        private static DateTime _lastRequestTime = DateTime.MinValue;
        private const int MinDelayBetweenRequestsMs = 2000; // ✅ 2 segundos entre requests
        private static DateTime _rateLimitResetTime = DateTime.MinValue;

        public BallDontLieService(
            HttpClient httpClient,
            ILogger<BallDontLieService> logger,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;

            // ✅ CONFIGURAÇÃO DA API KEY
            var ballDontLieConfig = configuration.GetSection("ExternalApis:BallDontLie");
            _apiKey = ballDontLieConfig["ApiKey"] ?? throw new InvalidOperationException("BallDontLie ApiKey not configured");

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                PropertyNameCaseInsensitive = true // ✅ Adicionar para ser mais tolerante
            };

            _logger.LogInformation("BallDontLieService initialized with API key configured: {HasApiKey}", !string.IsNullOrEmpty(_apiKey));
        }

        // ✅ PROPRIEDADES PÚBLICAS PARA CIRCUIT BREAKER
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
                        // ✅ CORRIGIDO: Adicionar /v1 no path
                        var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/games?dates[]={today}");
                        // ✅ CORRIGIDO: Usar Bearer token
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
                    throw; // Re-throw para o retry handler
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
                        // ✅ CORRIGIDO: Adicionar /v1 no path
                        var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/games?dates[]={dateString}");
                        // ✅ CORRIGIDO: Usar Bearer token
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
                catch (HttpRequestException ex) when (ex.Message.Contains("429"))
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching games for date {Date} from Ball Don't Lie API", date);
                    throw new Core.Exceptions.ExternalApiException("Ball Don't Lie", $"Failed to fetch games for {date:yyyy-MM-dd}", (int?)null);
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
                        // ✅ CORRIGIDO: Adicionar /v1 no path
                        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/teams");
                        // ✅ CORRIGIDO: Usar Bearer token
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
                        // ✅ CORRIGIDO: Adicionar /v1 no path
                        var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/players?search={Uri.EscapeDataString(search)}&page={page}");
                        // ✅ CORRIGIDO: Usar Bearer token
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
                        // ✅ CORRIGIDO: Adicionar /v1 no path
                        var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/players/{playerId}");
                        // ✅ CORRIGIDO: Usar Bearer token
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

        // ✅ PRIVATE METHODS - RATE LIMITING E RETRY

        /// <summary>
        /// ✅ NOVO: Método centralizado para handling de erros
        /// </summary>
        private async Task<IEnumerable<T>> HandleErrorResponse<T>(HttpResponseMessage response, string operation)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to {Operation}. Status: {StatusCode}, Content: {Content}",
                operation, response.StatusCode, errorContent);

            // ✅ DETECTAR E TRATAR RATE LIMIT
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                // Rate limit por 1 minuto + margem de segurança
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
                // ✅ VERIFICAR SE ESTAMOS EM RATE LIMIT
                if (DateTime.UtcNow < _rateLimitResetTime)
                {
                    var waitTime = _rateLimitResetTime - DateTime.UtcNow;
                    _logger.LogWarning("⏰ Waiting for rate limit reset: {WaitTime}s", waitTime.TotalSeconds);
                    await Task.Delay(waitTime);
                }

                // Garantir delay mínimo entre requests
                var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
                if (timeSinceLastRequest.TotalMilliseconds < MinDelayBetweenRequestsMs)
                {
                    var delay = MinDelayBetweenRequestsMs - (int)timeSinceLastRequest.TotalMilliseconds;
                    _logger.LogDebug("Rate limiting: waiting {Delay}ms before next request", delay);
                    await Task.Delay(delay);
                }

                _lastRequestTime = DateTime.UtcNow;
                var response = await operation();

                // ✅ LOG DA REQUEST PARA DEBUG
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
        private async Task<T> ExecuteWithRetry<T>(Func<Task<T>> operation, int maxRetries = 2) // ✅ Reduzido para 2 retries
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

                        // ✅ DEFINIR RESET TIME MAIS LONGO
                        _rateLimitResetTime = DateTime.UtcNow.AddMinutes(2);

                        throw new InvalidOperationException("Rate limit exceeded - service temporarily unavailable", ex);
                    }

                    // ✅ Backoff mais longo: 30s, 60s
                    var delay = TimeSpan.FromSeconds(30 * (attempt + 1));
                    _logger.LogWarning("Rate limit hit, waiting {Delay}s before retry {Attempt}/{MaxRetries}",
                        delay.TotalSeconds, attempt + 1, maxRetries);

                    await Task.Delay(delay);
                }
            }

            throw new InvalidOperationException("Should not reach here");
        }
    }
}