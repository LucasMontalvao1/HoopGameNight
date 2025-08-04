using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections;
using System.Collections.Concurrent;

namespace HoopGameNight.Infrastructure.Services
{
    public interface ICacheService
    {
        Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, CacheOptions? options = null);
        T? GetOrSet<T>(string key, Func<T> factory, CacheOptions? options = null);
        void Remove(string key);
        void RemoveByPattern(string pattern);
        void Clear();
        CacheStatistics GetStatistics();
    }

    public class CacheService : ICacheService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<CacheService> _logger;
        private readonly ConcurrentDictionary<string, CacheEntryInfo> _cacheRegistry = new();
        private readonly CacheStatistics _statistics = new();

        public CacheService(IMemoryCache cache, ILogger<CacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, CacheOptions? options = null)
        {
            _statistics.TotalRequests++;

            if (_cache.TryGetValue<T>(key, out var cachedValue))
            {
                _statistics.Hits++;
                _logger.LogDebug("Cache hit for key: {Key} (Hit rate: {Rate:P})",
                    key, _statistics.HitRate);
                return cachedValue;
            }

            _statistics.Misses++;
            _logger.LogDebug("Cache miss for key: {Key}", key);

            var value = await factory();
            if (value != null)
            {
                Set(key, value, options ?? CacheOptions.Default);
            }

            return value;
        }

        public T? GetOrSet<T>(string key, Func<T> factory, CacheOptions? options = null)
        {
            _statistics.TotalRequests++;

            if (_cache.TryGetValue<T>(key, out var cachedValue))
            {
                _statistics.Hits++;
                _logger.LogDebug("Cache hit for key: {Key}", key);
                return cachedValue;
            }

            _statistics.Misses++;
            _logger.LogDebug("Cache miss for key: {Key}", key);

            var value = factory();
            if (value != null)
            {
                Set(key, value, options ?? CacheOptions.Default);
            }

            return value;
        }

        public void Remove(string key)
        {
            _cache.Remove(key);
            _cacheRegistry.TryRemove(key, out _);
            _statistics.Evictions++;
            _logger.LogDebug("Removed cache key: {Key}", key);
        }

        public void RemoveByPattern(string pattern)
        {
            var keysToRemove = _cacheRegistry.Keys
                .Where(k => k.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var key in keysToRemove)
            {
                Remove(key);
            }

            _logger.LogInformation("Removed {Count} cache keys matching pattern: {Pattern}",
                keysToRemove.Count, pattern);
        }

        public void Clear()
        {
            var count = _cacheRegistry.Count;
            foreach (var key in _cacheRegistry.Keys.ToList())
            {
                _cache.Remove(key);
            }
            _cacheRegistry.Clear();
            _statistics.Evictions += count;

            _logger.LogInformation("Cleared all {Count} cache entries", count);
        }

        public CacheStatistics GetStatistics()
        {
            _statistics.CurrentEntries = _cacheRegistry.Count;
            _statistics.EntriesByCategory = _cacheRegistry.Values
                .GroupBy(e => e.Category)
                .ToDictionary(g => g.Key, g => g.Count());

            return _statistics;
        }

        private void Set<T>(string key, T value, CacheOptions options)
        {
            var memoryCacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = options.AbsoluteExpiration,
                SlidingExpiration = options.SlidingExpiration,
                Priority = options.Priority
            };

            memoryCacheOptions.RegisterPostEvictionCallback((evictedKey, evictedValue, reason, state) =>
            {
                _cacheRegistry.TryRemove(evictedKey.ToString()!, out _);
                _statistics.Evictions++;
                _logger.LogDebug("Cache entry evicted: {Key}, Reason: {Reason}", evictedKey, reason);
            });

            _cache.Set(key, value, memoryCacheOptions);

            _cacheRegistry[key] = new CacheEntryInfo
            {
                Key = key,
                Category = DetermineCategory(key),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow + (options.AbsoluteExpiration ?? TimeSpan.FromHours(1)),
                Size = EstimateSize(value)
            };

            _logger.LogDebug("Cached value for key: {Key} with expiration: {Expiration}",
                key, options.AbsoluteExpiration);
        }

        private string DetermineCategory(string key)
        {
            if (key.Contains("game", StringComparison.OrdinalIgnoreCase))
                return "Games";
            if (key.Contains("team", StringComparison.OrdinalIgnoreCase))
                return "Teams";
            if (key.Contains("player", StringComparison.OrdinalIgnoreCase))
                return "Players";
            return "Other";
        }

        private long EstimateSize<T>(T value)
        {
            // Estimativa simples baseada no tipo
            return value switch
            {
                string s => s.Length * 2, // chars são 2 bytes
                ICollection collection => collection.Count * 100, // estimativa
                _ => 100 // default
            };
        }
    }

    public class CacheOptions
    {
        public TimeSpan? AbsoluteExpiration { get; set; }
        public TimeSpan? SlidingExpiration { get; set; }
        public CacheItemPriority Priority { get; set; } = CacheItemPriority.Normal;

        public static CacheOptions Default => new()
        {
            AbsoluteExpiration = TimeSpan.FromMinutes(5),
            Priority = CacheItemPriority.Normal
        };

        public static CacheOptions Short => new()
        {
            AbsoluteExpiration = TimeSpan.FromMinutes(1),
            Priority = CacheItemPriority.Low
        };

        public static CacheOptions Long => new()
        {
            AbsoluteExpiration = TimeSpan.FromHours(1),
            Priority = CacheItemPriority.High
        };

        public static CacheOptions Sliding => new()
        {
            SlidingExpiration = TimeSpan.FromMinutes(15),
            AbsoluteExpiration = TimeSpan.FromHours(1),
            Priority = CacheItemPriority.Normal
        };

        public static CacheOptions Games => new()
        {
            AbsoluteExpiration = TimeSpan.FromMinutes(5),
            Priority = CacheItemPriority.High
        };

        public static CacheOptions Teams => new()
        {
            AbsoluteExpiration = TimeSpan.FromHours(24),
            Priority = CacheItemPriority.High
        };
    }

    public class CacheEntryInfo
    {
        public string Key { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public long Size { get; set; }
    }

    public class CacheStatistics
    {
        public int TotalRequests { get; set; }
        public int Hits { get; set; }
        public int Misses { get; set; }
        public int Evictions { get; set; }
        public int CurrentEntries { get; set; }
        public Dictionary<string, int> EntriesByCategory { get; set; } = new();

        public double HitRate => TotalRequests > 0 ? (double)Hits / TotalRequests : 0;
        public double MissRate => TotalRequests > 0 ? (double)Misses / TotalRequests : 0;
    }
}