using HoopGameNight.Core.Configuration;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Threading;

namespace HoopGameNight.Infrastructure.Services
{
    /// <summary>
    /// Implementação de cache em camadas (Redis → Memory → Database)
    /// Centraliza toda a lógica de cache do sistema
    /// </summary>
    public class CacheService : ICacheService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly IDistributedCache? _distributedCache;
        private readonly ILogger<CacheService> _logger;

        // Estatísticas do cache
        private long _redisHits = 0;
        private long _redisMisses = 0;
        private long _memoryHits = 0;
        private long _memoryMisses = 0;
        private readonly DateTime _startTime = DateTime.UtcNow;

        public CacheService(
            IMemoryCache memoryCache,
            ILogger<CacheService> logger,
            IDistributedCache? distributedCache = null)
        {
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _distributedCache = distributedCache;

            if (_distributedCache == null)
            {
                _logger.LogWarning("Redis (IDistributedCache) não está disponível. Usando apenas Memory Cache.");
            }
            else
            {
                _logger.LogInformation("CacheService inicializado com Redis + Memory Cache");
            }
        }

        /// <summary>
        /// Busca valor do cache de forma síncrona (apenas Memory Cache)
        /// </summary>
        public T? Get<T>(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return default;

            try
            {
                if (_memoryCache.TryGetValue(key, out T? value))
                {
                    Interlocked.Increment(ref _memoryHits);
                    _logger.LogDebug("MEMORY HIT (sync): {Key}", key);
                    return value;
                }

                Interlocked.Increment(ref _memoryMisses);
                return default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar do cache (sync): {Key}", key);
                return default;
            }
        }

        /// <summary>
        /// Busca valor do cache (Redis → Memory → null)
        /// </summary>
        public async Task<T?> GetAsync<T>(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            try
            {
                // 1️⃣ Tentar Redis primeiro (se disponível)
                if (_distributedCache != null)
                {
                    var redisData = await _distributedCache.GetStringAsync(key);
                    if (redisData != null)
                    {
                        Interlocked.Increment(ref _redisHits);
                        _logger.LogDebug("REDIS HIT: {Key}", key);

                        var deserializedData = JsonSerializer.Deserialize<T>(redisData);

                        // Salvar também no memory cache para próximas buscas
                        if (deserializedData != null)
                        {
                            _memoryCache.Set(key, deserializedData, TimeSpan.FromMinutes(5));
                        }

                        return deserializedData;
                    }

                    Interlocked.Increment(ref _redisMisses);
                    _logger.LogDebug("REDIS MISS: {Key}", key);
                }

                // 2️⃣ Fallback: Memory Cache
                if (_memoryCache.TryGetValue(key, out T? memCached))
                {
                    Interlocked.Increment(ref _memoryHits);
                    _logger.LogDebug("MEMORY HIT: {Key}", key);
                    return memCached;
                }

                Interlocked.Increment(ref _memoryMisses);
                _logger.LogDebug("CACHE MISS (total): {Key}", key);
                return default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar do cache: {Key}", key);
                return default; 
            }
        }

        /// <summary>
        /// Armazena valor no cache de forma síncrona (apenas Memory Cache)
        /// </summary>
        public void Set<T>(string key, T value, TimeSpan? expiration = null)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            var ttl = expiration ?? CacheDurations.Default;

            try
            {
                _memoryCache.Set(key, value, ttl);
                _logger.LogDebug("📦 MEMORY SET (sync): {Key} (TTL: {TTL})", key, ttl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar no cache (sync): {Key}", key);
            }
        }

        /// <summary>
        /// Armazena valor no cache (Redis + Memory)
        /// </summary>
        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            var ttl = expiration ?? CacheDurations.Default;

            try
            {
                // 1️⃣ Salvar no Redis (se disponível)
                if (_distributedCache != null)
                {
                    var json = JsonSerializer.Serialize(value);
                    var options = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = ttl
                    };

                    await _distributedCache.SetStringAsync(key, json, options);
                    _logger.LogDebug("REDIS SET: {Key} (TTL: {TTL})", key, ttl);
                }

                // 2️⃣ Salvar no Memory Cache (sempre)
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl
                };

                _memoryCache.Set(key, value, cacheOptions);
                _logger.LogDebug("MEMORY SET: {Key} (TTL: {TTL})", key, ttl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar no cache: {Key}", key);
                // Não lança exceção - graceful degradation
            }
        }

        /// <summary>
        /// Remove valor do cache de forma síncrona
        /// </summary>
        public void Remove(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            try
            {
                _memoryCache.Remove(key);
                _logger.LogDebug("🗑MEMORY REMOVE (sync): {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao remover do cache (sync): {Key}", key);
            }
        }

        /// <summary>
        /// Remove valor do cache
        /// </summary>
        public async Task RemoveAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            try
            {
                // Remover do Redis
                if (_distributedCache != null)
                {
                    await _distributedCache.RemoveAsync(key);
                    _logger.LogDebug("REDIS REMOVE: {Key}", key);
                }

                // Remover do Memory
                _memoryCache.Remove(key);
                _logger.LogDebug("MEMORY REMOVE: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao remover do cache: {Key}", key);
            }
        }

        /// <summary>
        /// Remove chaves por padrão (ex: "games:*")
        /// NOTA: Funcionalidade limitada sem Redis Server commands
        /// </summary>
        public async Task RemoveByPatternAsync(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                throw new ArgumentException("Pattern cannot be null or empty", nameof(pattern));

            _logger.LogWarning("⚠RemoveByPatternAsync('{Pattern}') requer Redis Server commands (KEYS/SCAN). " +
                               "Funcionalidade limitada com IDistributedCache. " +
                               "Considere implementar usando StackExchange.Redis diretamente.", pattern);


            await Task.CompletedTask;
        }

        /// <summary>
        /// Verifica se chave existe no cache (síncrono)
        /// </summary>
        public bool Exists(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            try
            {
                return _memoryCache.TryGetValue(key, out _);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar existência no cache (sync): {Key}", key);
                return false;
            }
        }

        /// <summary>
        /// Verifica se chave existe no cache
        /// </summary>
        public async Task<bool> ExistsAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            try
            {
                // Verificar Redis
                if (_distributedCache != null)
                {
                    var redisData = await _distributedCache.GetStringAsync(key);
                    if (redisData != null)
                        return true;
                }

                // Verificar Memory
                return _memoryCache.TryGetValue(key, out _);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar existência no cache: {Key}", key);
                return false;
            }
        }

        /// <summary>
        /// Limpa todo o cache (apenas Memory Cache)
        /// </summary>
        public void Clear()
        {
            try
            {
                _logger.LogWarning("⚠️ Clear() chamado mas IMemoryCache não suporta limpeza total. " +
                                   "Use RemoveByPatternAsync para invalidações específicas.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao limpar cache");
            }
        }

        /// <summary>
        /// Invalida chaves por padrão (síncrono)
        /// </summary>
        public void InvalidatePattern(string pattern)
        {
            _logger.LogWarning("⚠️ InvalidatePattern('{Pattern}') requer Redis Server commands. " +
                               "Use RemoveByPatternAsync para funcionalidade assíncrona.", pattern);
        }

        /// <summary>
        /// Obtém estatísticas do cache (síncrono)
        /// </summary>
        public CacheStatistics GetStatistics()
        {
            var stats = new CacheStatistics
            {
                RedisHits = _redisHits,
                RedisMisses = _redisMisses,
                MemoryHits = _memoryHits,
                MemoryMisses = _memoryMisses,
                MemoryCacheCount = 0, 
                Evictions = 0, 
                LastResetTime = _startTime.ToString("O")
            };

            return stats;
        }

        /// <summary>
        /// Obtém estatísticas do cache
        /// </summary>
        public Task<CacheStatistics> GetStatisticsAsync()
        {
            var stats = GetStatistics();

            _logger.LogInformation("Cache Statistics | Hits: {Hits} | Misses: {Misses} | Hit Rate: {HitRate:F2}%",
                stats.TotalHits, stats.TotalMisses, stats.HitRate);

            return Task.FromResult(stats);
        }
    }
}
