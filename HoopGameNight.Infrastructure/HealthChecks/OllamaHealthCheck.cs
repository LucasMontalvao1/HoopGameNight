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

                return HealthCheckResult.Unhealthy(
                    "Ollama não está disponível. Verifique se está rodando.",
                    null,
                    new Dictionary<string, object>
                    {
                        { "status", "unavailable" },
                        { "solution", "Execute: ollama serve" }
                    }
                );
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Erro de conexão com Ollama");

                return HealthCheckResult.Unhealthy(
                    "Não foi possível conectar ao Ollama",
                    ex,
                    new Dictionary<string, object>
                    {
                        { "status", "connection_failed" },
                        { "error", ex.Message },
                        { "solution", "Verifique se Ollama está rodando em http://localhost:11434" }
                    }
                );
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout ao verificar Ollama");

                return HealthCheckResult.Unhealthy(
                    "⏱Timeout ao conectar com Ollama",
                    ex,
                    new Dictionary<string, object>
                    {
                        { "status", "timeout" },
                        { "error", "Ollama não respondeu a tempo" }
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao verificar Ollama");

                return HealthCheckResult.Unhealthy(
                    "Erro inesperado ao verificar Ollama",
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