using Microsoft.Extensions.Diagnostics.HealthChecks;
using HoopGameNight.Core.Interfaces.Services;

namespace HoopGameNight.Api.HealthChecks
{
    public class BallDontLieHealthCheck : IHealthCheck
    {
        private readonly IBallDontLieService _ballDontLieService;
        private readonly ILogger<BallDontLieHealthCheck> _logger;

        public BallDontLieHealthCheck(
            IBallDontLieService ballDontLieService,
            ILogger<BallDontLieHealthCheck> logger)
        {
            _ballDontLieService = ballDontLieService;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Tenta buscar times (operação leve) com timeout curto
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var teams = await _ballDontLieService.GetAllTeamsAsync();

                if (teams == null || !teams.Any())
                {
                    return HealthCheckResult.Degraded(
                        "Ball Don't Lie API retornou dados vazios",
                        data: new Dictionary<string, object>
                        {
                            { "status", "degraded" },
                            { "hasData", false },
                            { "timestamp", DateTime.UtcNow }
                        });
                }

                return HealthCheckResult.Healthy(
                    "Ball Don't Lie API está funcionando",
                    data: new Dictionary<string, object>
                    {
                        { "status", "healthy" },
                        { "teamCount", teams.Count() },
                        { "timestamp", DateTime.UtcNow }
                    });
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Ball Don't Lie API em rate limit");

                return HealthCheckResult.Degraded(
                    "API em rate limit temporário",
                    exception: ex,
                    data: new Dictionary<string, object>
                    {
                        { "status", "degraded" },
                        { "rateLimited", true },
                        { "timestamp", DateTime.UtcNow }
                    });
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Timeout ao verificar Ball Don't Lie API");

                return HealthCheckResult.Degraded(
                    "API demorou muito para responder",
                    data: new Dictionary<string, object>
                    {
                        { "status", "degraded" },
                        { "timeout", true },
                        { "timestamp", DateTime.UtcNow }
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar saúde da Ball Don't Lie API");

                return HealthCheckResult.Unhealthy(
                    "Ball Don't Lie API não está acessível",
                    exception: ex,
                    data: new Dictionary<string, object>
                    {
                        { "status", "unhealthy" },
                        { "error", ex.Message },
                        { "timestamp", DateTime.UtcNow }
                    });
            }
        }
    }
}
