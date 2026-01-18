using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

namespace HoopGameNight.Infrastructure.ExternalServices.ESPN
{
    public abstract class BaseEspnClient
    {
        protected readonly HttpClient _httpClient;
        protected readonly ICacheService _cacheService;
        protected readonly ILogger _logger;
        protected readonly JsonSerializerOptions _serializerOptions;
        protected readonly IAsyncPolicy<HttpResponseMessage> _resiliencePolicy;

        protected BaseEspnClient(
            HttpClient httpClient,
            ICacheService cacheService,
            ILogger logger)
        {
            _httpClient = httpClient;
            _cacheService = cacheService;
            _logger = logger;
            _serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            // Definindo política de resiliência (Retry + Circuit Breaker)
            _resiliencePolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (outcome, timespan, retryAttempt, context) =>
                    {
                        _logger.LogWarning("Retry {RetryAttempt} after {TimeSpan} due to {StatusCode}", 
                            retryAttempt, timespan, outcome.Result?.StatusCode);
                    })
                .WrapAsync(HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));
        }

        protected async Task<T?> GetAsync<T>(string url, string cacheKey, TimeSpan? cacheDuration)
        {
            try
            {
                // 1. Tentar Cache
                var cached = await _cacheService.GetAsync<T>(cacheKey);
                if (cached != null)
                {
                    _logger.LogDebug("Cache Hit for Key: {CacheKey}", cacheKey);
                    return cached;
                }

                _logger.LogInformation("Fetching from ESPN: {Url}", url);

                // 2. Executar Request com Polly
                var response = await _resiliencePolicy.ExecuteAsync(() => _httpClient.GetAsync(url));

                // 3. Tratar Erros
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("ESPN Resource not found: {Url}", url);
                    return default;
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    _logger.LogError("ESPN Rate Limit exceeded for: {Url}", url);
                    return default;
                }

                response.EnsureSuccessStatusCode();

                // 4. Desserializar
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<T>(json, _serializerOptions);

                // 5. Cache do Resultado
                if (result != null && cacheDuration.HasValue)
                {
                    await _cacheService.SetAsync(cacheKey, result, cacheDuration.Value);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching from ESPN URL: {Url}", url);
                return default;
            }
        }
    }
}
