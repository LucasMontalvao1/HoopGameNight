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
                var cached = await _cacheService.GetAsync<T>(cacheKey);
                if (cached != null)
                {
                    _logger.LogDebug("Cache Hit for Key: {CacheKey}", cacheKey);
                    return cached;
                }

                _logger.LogInformation("Fetching from ESPN: {Url}", url);

                var response = await _resiliencePolicy.ExecuteAsync(() => _httpClient.GetAsync(url));

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

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<T>(json, _serializerOptions);

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
