using HoopGameNight.Infrastructure.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HoopGameNight.Api.HealthChecks
{
    public class SyncHealthCheck : IHealthCheck
    {
        private readonly ISyncMetricsService _syncMetricsService;
        private readonly ILogger<SyncHealthCheck> _logger;

        public SyncHealthCheck(ISyncMetricsService syncMetricsService, ILogger<SyncHealthCheck> logger)
        {
            _syncMetricsService = syncMetricsService;
            _logger = logger;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var metrics = _syncMetricsService.GetMetrics();

                var data = new Dictionary<string, object>
                {
                    ["successRate"] = metrics.SuccessRate,
                    ["lastSync"] = metrics.LastSyncTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Nunca",
                    ["consecutiveFailures"] = metrics.ConsecutiveFailures,
                    ["totalSyncs"] = metrics.TotalSyncs
                };

                if (metrics.ConsecutiveFailures > 5)
                {
                    return Task.FromResult(HealthCheckResult.Unhealthy(
                        $"Muitas falhas consecutivas de sincronização: {metrics.ConsecutiveFailures}",
                        data: data));
                }

                if (metrics.SuccessRate < 50 && metrics.TotalSyncs > 10)
                {
                    return Task.FromResult(HealthCheckResult.Degraded(
                        $"Baixa taxa de sucesso de sincronização: {metrics.SuccessRate:F1}%",
                        data: data));
                }

                if (metrics.LastSuccessfulSync < DateTime.UtcNow.AddHours(-6))
                {
                    return Task.FromResult(HealthCheckResult.Degraded(
                        "Nenhuma sincronização bem-sucedida nas últimas 6 horas",
                        data: data));
                }

                return Task.FromResult(HealthCheckResult.Healthy(
                    $"Sincronização está saudável. Taxa de sucesso: {metrics.SuccessRate:F1}%",
                    data: data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar saúde da sincronização");
                return Task.FromResult(HealthCheckResult.Unhealthy("Verificação de saúde da sincronização falhou", ex));
            }
        }
    }
}