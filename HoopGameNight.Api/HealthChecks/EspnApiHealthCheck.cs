using Microsoft.Extensions.Diagnostics.HealthChecks;
using HoopGameNight.Core.Interfaces.Services;

namespace HoopGameNight.Api.HealthChecks
{
    public class EspnApiHealthCheck : IHealthCheck
    {
        private readonly IEspnApiService _espnApiService;
        private readonly ILogger<EspnApiHealthCheck> _logger;

        public EspnApiHealthCheck(
            IEspnApiService espnApiService,
            ILogger<EspnApiHealthCheck> logger)
        {
            _espnApiService = espnApiService;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Tenta buscar jogos de uma data futura (operação leve)
                var tomorrow = DateTime.Today.AddDays(1);
                var games = await _espnApiService.GetGamesByDateAsync(tomorrow);

                // ESPN pode retornar vazio se não houver jogos, isso é ok
                return HealthCheckResult.Healthy(
                    "ESPN API está funcionando",
                    data: new Dictionary<string, object>
                    {
                        { "upcomingGames", games?.Count() ?? 0 },
                        { "checkDate", tomorrow.ToString("yyyy-MM-dd") }
                    });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "ESPN API retornou erro HTTP");

                return HealthCheckResult.Degraded(
                    "ESPN API com problemas de conectividade",
                    exception: ex,
                    data: new Dictionary<string, object>
                    {
                        { "error", ex.Message },
                        { "statusCode", ex.StatusCode?.ToString() ?? "Unknown" }
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar saúde da ESPN API");

                return HealthCheckResult.Unhealthy(
                    "ESPN API não está acessível",
                    exception: ex,
                    data: new Dictionary<string, object>
                    {
                        { "error", ex.Message }
                    });
            }
        }
    }
}
