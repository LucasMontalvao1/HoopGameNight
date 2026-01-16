using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using HoopGameNight.Core.Configuration;
using Microsoft.Extensions.Options;

namespace HoopGameNight.Infrastructure.HealthChecks
{
    /// <summary>
    /// Health check para verificar se o Redis está disponível e funcionando corretamente
    /// </summary>
    public class RedisHealthCheck : IHealthCheck
    {
        private readonly IDistributedCache? _distributedCache;
        private readonly ILogger<RedisHealthCheck> _logger;
        private readonly RedisSettings _redisSettings;

        public RedisHealthCheck(
            IDistributedCache? distributedCache,
            ILogger<RedisHealthCheck> _logger,
            IOptions<RedisSettings> redisSettings)
        {
            _distributedCache = distributedCache;
            this._logger = _logger;
            _redisSettings = redisSettings.Value;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            // Se Redis está desabilitado nas configurações, retornar Degraded
            if (!_redisSettings.Enabled)
            {
                _logger.LogWarning("Redis está DESABILITADO nas configurações");
                return HealthCheckResult.Degraded(
                    "Redis is disabled in configuration",
                    data: new Dictionary<string, object>
                    {
                        { "enabled", false },
                        { "connectionString", _redisSettings.ConnectionString }
                    });
            }

            // Se IDistributedCache não foi injetado, retornar Unhealthy
            if (_distributedCache == null)
            {
                _logger.LogError("IDistributedCache é NULL - Redis não configurado corretamente!");
                return HealthCheckResult.Unhealthy(
                    "Redis is not configured (IDistributedCache is null)",
                    data: new Dictionary<string, object>
                    {
                        { "enabled", true },
                        { "injected", false }
                    });
            }

            try
            {
                // Tentar escrever e ler um valor de teste
                var testKey = "health_check_test";
                var testValue = $"test_{DateTime.UtcNow.Ticks}";

                _logger.LogDebug("Testing Redis connection with key: {TestKey}", testKey);

                // Write test
                await _distributedCache.SetStringAsync(
                    testKey,
                    testValue,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10)
                    },
                    cancellationToken);

                // Read test
                var retrievedValue = await _distributedCache.GetStringAsync(testKey, cancellationToken);

                // Cleanup
                await _distributedCache.RemoveAsync(testKey, cancellationToken);

                // Validar resultado
                if (retrievedValue == testValue)
                {
                    _logger.LogDebug("Redis health check PASSED");
                    return HealthCheckResult.Healthy(
                        "Redis is responsive",
                        data: new Dictionary<string, object>
                        {
                            { "connectionString", _redisSettings.ConnectionString },
                            { "instanceName", _redisSettings.InstanceName },
                            { "enabled", true },
                            { "readWrite", "success" }
                        });
                }
                else
                {
                    _logger.LogWarning("Redis returned incorrect value in health check");
                    return HealthCheckResult.Degraded(
                        "Redis read/write test failed",
                        data: new Dictionary<string, object>
                        {
                            { "expected", testValue },
                            { "actual", retrievedValue ?? "null" }
                        });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis health check FAILED: {ErrorMessage}", ex.Message);

                return HealthCheckResult.Unhealthy(
                    "Redis connection failed",
                    exception: ex,
                    data: new Dictionary<string, object>
                    {
                        { "connectionString", _redisSettings.ConnectionString },
                        { "error", ex.Message },
                        { "errorType", ex.GetType().Name }
                    });
            }
        }
    }
}
