using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HoopGameNight.Core.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Infrastructure.HealthChecks
{
    /// <summary>
    /// Verifica a disponibilidade e integridade do serviço Ollama (IA Local).
    /// </summary>
    public class OllamaHealthCheck : IHealthCheck
    {
        private readonly OllamaClient _ollamaClient;
        private readonly ILogger<OllamaHealthCheck> _logger;

        public OllamaHealthCheck(
            OllamaClient ollamaClient,
            ILogger<OllamaHealthCheck> logger)
        {
            _ollamaClient = ollamaClient;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Verificando disponibilidade do Ollama...");

                var isAvailable = await _ollamaClient.IsAvailableAsync();

                if (isAvailable)
                {
                    _logger.LogDebug("Ollama disponível");

                    return HealthCheckResult.Healthy(
                        "Ollama está rodando e respondendo corretamente",
                        new Dictionary<string, object>
                        {
                            { "status", "running" },
                            { "endpoint", "http://localhost:11434" },
                            { "model", "llama3.2" }
                        }
                    );
                }

                _logger.LogWarning("Ollama não está disponível");

                return HealthCheckResult.Degraded(
                    "Ollama não está disponível. Verifique se está rodando.",
                    null,
                    new Dictionary<string, object>
                    {
                        { "status", "unavailable" },
                        { "solution", "Execute: ollama serve" }
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao verificar Ollama (reportando como Degraded)");

                return HealthCheckResult.Degraded(
                    "Erro ao verificar Ollama",
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