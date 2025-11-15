using Microsoft.Extensions.Diagnostics.HealthChecks;
using HoopGameNight.Core.Interfaces.Services;

namespace HoopGameNight.Api.HealthChecks
{
    public class EspnApiHealthCheck : IHealthCheck
    {
        private readonly IEspnApiService _espnApiService;
        private readonly ILogger<EspnApiHealthCheck> _logger;

        public EspnApiHealthCheck(IEspnApiService espnApiService,ILogger<EspnApiHealthCheck> logger)
        {
            _espnApiService = espnApiService;
            _logger = logger;
        }

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            if (_espnApiService == null)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("ESPN API service não está configurado"));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                "ESPN API service está configurado",
                data: new Dictionary<string, object>
                {
                    { "configured", true },
                    { "timestamp", DateTime.UtcNow }
                }));
        }
    }
}
