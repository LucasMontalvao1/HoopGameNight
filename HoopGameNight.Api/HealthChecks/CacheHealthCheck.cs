using HoopGameNight.Core.Interfaces.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HoopGameNight.Api.HealthChecks
{
    public class CacheHealthCheck : IHealthCheck
    {
        private readonly ICacheService _cacheService;
        private readonly ILogger<CacheHealthCheck> _logger;

        public CacheHealthCheck(ICacheService cacheService, ILogger<CacheHealthCheck> logger)
        {
            _cacheService = cacheService;
            _logger = logger;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var stats = _cacheService.GetStatistics();

                var data = new Dictionary<string, object>
                {
                    ["hitRate"] = stats.HitRate,
                    ["totalRequests"] = stats.TotalRequests,
                    ["currentEntries"] = stats.CurrentEntries,
                    ["evictions"] = stats.Evictions
                };

                if (stats.HitRate < 0.1 && stats.TotalRequests > 100)
                {
                    return Task.FromResult(HealthCheckResult.Degraded(
                        $"Low cache hit rate: {stats.HitRate:P}",
                        data: data));
                }

                return Task.FromResult(HealthCheckResult.Healthy(
                    $"Cache is healthy. Hit rate: {stats.HitRate:P}",
                    data: data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking cache health");
                return Task.FromResult(HealthCheckResult.Unhealthy("Cache health check failed", ex));
            }
        }
    }
}