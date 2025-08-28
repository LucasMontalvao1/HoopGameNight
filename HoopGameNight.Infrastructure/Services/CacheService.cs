using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace HoopGameNight.Infrastructure.Services
{
    /// <summary>
    /// Serviço de cache centralizado
    /// </summary>
    public class CacheService : ICacheService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<CacheService> _logger;
        private readonly ConcurrentDictionary<string, DateTime> _cacheKeys;
        private long _totalRequests;
        private long _hits;
        private long _misses;
        private long _evictions;

        public CacheService(IMemoryCache cache, ILogger<CacheService> logger)
        {
            _cache = cache;
            _logger = logger;
            _cacheKeys = new ConcurrentDictionary<string, DateTime>();
        }

        public T? Get<T>(string key)
        {
            Interlocked.Increment(ref _totalRequests);

            if (_cache.TryGetValue(key, out T? value))
            {
                Interlocked.Increment(ref _hits);
                _logger.LogDebug("Cache hit for key: {Key}", key);
                return value;
            }

            Interlocked.Increment(ref _misses);
            _logger.LogDebug("Cache miss for key: {Key}", key);
            return default;
        }

        public Task<T?> GetAsync<T>(string key) => Task.FromResult(Get<T>(key));

        public void Set<T>(string key, T value, TimeSpan? expiration = null)
        {
            var options = new MemoryCacheEntryOptions
            {
                SlidingExpiration = expiration ?? TimeSpan.FromMinutes(15)
            };

            options.RegisterPostEvictionCallback((evictedKey, evictedValue, reason, state) =>
            {
                _cacheKeys.TryRemove(evictedKey.ToString()!, out _);
                Interlocked.Increment(ref _evictions);
                _logger.LogDebug("Cache evicted: {Key}, Reason: {Reason}", evictedKey, reason);
            });

            _cache.Set(key, value, options);
            _cacheKeys[key] = DateTime.UtcNow;

            _logger.LogDebug("Cache set: {Key}, Expiration: {Expiration}", key, expiration);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
        {
            Set(key, value, expiration);
            return Task.CompletedTask;
        }

        public void Remove(string key)
        {
            _cache.Remove(key);
            _cacheKeys.TryRemove(key, out _);
            _logger.LogDebug("Cache removed: {Key}", key);
        }

        public Task RemoveAsync(string key)
        {
            Remove(key);
            return Task.CompletedTask;
        }

        public bool Exists(string key) => _cache.TryGetValue(key, out _);

        public void Clear()
        {
            var keys = _cacheKeys.Keys.ToList();
            foreach (var key in keys)
            {
                _cache.Remove(key);
            }
            _cacheKeys.Clear();
            _logger.LogInformation("Cache cleared - {Count} entries removed", keys.Count);
        }

        public void InvalidatePattern(string pattern)
        {
            var keysToRemove = _cacheKeys.Keys
                .Where(k => k.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var key in keysToRemove)
            {
                Remove(key);
            }

            _logger.LogInformation("Invalidated {Count} cache entries matching pattern: {Pattern}",
                keysToRemove.Count, pattern);
        }

        public CacheStatistics GetStatistics()
        {
            return new CacheStatistics
            {
                TotalRequests = Interlocked.Read(ref _totalRequests),
                Hits = Interlocked.Read(ref _hits),
                Misses = Interlocked.Read(ref _misses),
                CurrentEntries = _cacheKeys.Count,
                Evictions = Interlocked.Read(ref _evictions)
            };
        }
    }
}