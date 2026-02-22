using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HoopGameNight.Core.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Infrastructure.HealthChecks
{
    /// <summary>
    /// Verifica a disponibilidade e integridade do serviço Groq (IA em Nuvem).
    /// </summary>
    public class GroqHealthCheck : IHealthCheck
    {
        private readonly GroqClient _groqClient;
        private readonly ILogger<GroqHealthCheck> _logger;

        public GroqHealthCheck(
            GroqClient groqClient,
            ILogger<GroqHealthCheck> logger)
        {
            _groqClient = groqClient;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Verificando disponibilidade do Groq...");

                var isAvailable = await _groqClient.IsAvailableAsync();

                if (isAvailable)
                {
                    return HealthCheckResult.Healthy(
                        "O serviço Groq está configurado e disponível.",
                        new Dictionary<string, object>
                        {
                            { "engine", "Groq" },
                            { "model", "llama-3.3-70b-versatile" }
                        }
                    );
                }

                _logger.LogWarning("Groq API Key não configurada no .env");

                return HealthCheckResult.Degraded(
                    "Groq API Key não encontrada. O assistente de IA não funcionará.",
                    null,
                    new Dictionary<string, object>
                    {
                        { "status", "missing_config" },
                        { "required_var", "GROQ_API_KEY" }
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao verificar Groq Health");

                return HealthCheckResult.Degraded(
                    "Erro ao verificar Groq",
                    ex,
                    new Dictionary<string, object>
                    {
                        { "status", "error" },
                        { "error", ex.Message }
                    }
                );
            }
        }
    }
}
