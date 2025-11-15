using HoopGameNight.Core.Interfaces.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HoopGameNight.Api.HealthChecks
{
    public class ExternalApiHealthCheck : IHealthCheck
    {
        private readonly IEspnApiService _espnApiService;
        private readonly ILogger<ExternalApiHealthCheck> _logger;

        public ExternalApiHealthCheck(IEspnApiService espnApiService, ILogger<ExternalApiHealthCheck> logger)
        {
            _espnApiService = espnApiService;
            _logger = logger;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            if (_espnApiService == null)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("ESPN API service não está configurado"));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                "ESPN API service está configurado",
                data: new Dictionary<string, object>
                {
                    { "api", "ESPN" },
                    { "configured", true },
                    { "timestamp", DateTime.UtcNow }
                }));
        }
    }
}
