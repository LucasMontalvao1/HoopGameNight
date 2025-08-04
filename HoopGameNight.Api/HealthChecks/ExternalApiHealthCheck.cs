using HoopGameNight.Core.Interfaces.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HoopGameNight.Api.HealthChecks
{
    public class ExternalApiHealthCheck : IHealthCheck
    {
        private readonly IBallDontLieService _ballDontLieService;
        private readonly ILogger<ExternalApiHealthCheck> _logger;

        public ExternalApiHealthCheck(IBallDontLieService ballDontLieService, ILogger<ExternalApiHealthCheck> logger)
        {
            _ballDontLieService = ballDontLieService;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                var teams = await _ballDontLieService.GetAllTeamsAsync();
                var responseTime = DateTime.UtcNow - startTime;

                var data = new Dictionary<string, object>
                {
                    ["responseTime"] = responseTime.TotalMilliseconds,
                    ["teamsFound"] = teams.Count()
                };

                if (!teams.Any())
                {
                    return HealthCheckResult.Unhealthy("API externa não retornou dados", data: data);
                }

                if (responseTime.TotalSeconds > 5)
                {
                    return HealthCheckResult.Degraded(
                        $"API externa está lenta: {responseTime.TotalSeconds:F2}s",
                        data: data);
                }

                return HealthCheckResult.Healthy(
                    $"API externa está saudável. Tempo de resposta: {responseTime.TotalMilliseconds:F0}ms",
                    data: data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar saúde da API externa");
                return HealthCheckResult.Unhealthy("Verificação de saúde da API externa falhou", ex);
            }
        }
    }
}