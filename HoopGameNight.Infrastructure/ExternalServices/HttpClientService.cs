using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace HoopGameNight.Infrastructure.ExternalServices
{
    public class HttpClientService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<HttpClientService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public HttpClientService(HttpClient httpClient, ILogger<HttpClientService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<T?> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Making GET request to: {Endpoint}", endpoint);

                var response = await _httpClient.GetAsync(endpoint, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("HTTP request failed. Endpoint: {Endpoint}, Status: {StatusCode}", endpoint, response.StatusCode);
                    return default;
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<T>(content, _jsonOptions);

                _logger.LogDebug("GET request successful. Endpoint: {Endpoint}", endpoint);
                return result;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request exception for endpoint: {Endpoint}", endpoint);
                throw;
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError(ex, "HTTP request timeout for endpoint: {Endpoint}", endpoint);
                throw;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON deserialization error for endpoint: {Endpoint}", endpoint);
                throw;
            }
        }

        public async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest data, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Making POST request to: {Endpoint}", endpoint);

                var json = JsonSerializer.Serialize(data, _jsonOptions);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("POST request failed. Endpoint: {Endpoint}, Status: {StatusCode}", endpoint, response.StatusCode);
                    return default;
                }

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<TResponse>(responseContent, _jsonOptions);

                _logger.LogDebug("POST request successful. Endpoint: {Endpoint}", endpoint);
                return result;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP POST request exception for endpoint: {Endpoint}", endpoint);
                throw;
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError(ex, "HTTP POST request timeout for endpoint: {Endpoint}", endpoint);
                throw;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON serialization/deserialization error for endpoint: {Endpoint}", endpoint);
                throw;
            }
        }
    }
}