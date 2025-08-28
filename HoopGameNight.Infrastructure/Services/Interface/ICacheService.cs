namespace HoopGameNight.Infrastructure.Services
{
    /// <summary>
    /// Interface para serviço de cache
    /// </summary>
    public interface ICacheService
    {
        T? Get<T>(string key);
        Task<T?> GetAsync<T>(string key);
        void Set<T>(string key, T value, TimeSpan? expiration = null);
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);
        void Remove(string key);
        Task RemoveAsync(string key);
        bool Exists(string key);
        void Clear();
        void InvalidatePattern(string pattern);
        CacheStatistics GetStatistics();
    }

    /// <summary>
    /// Estatísticas do cache
    /// </summary>
    public class CacheStatistics
    {
        public long TotalRequests { get; set; }
        public long Hits { get; set; }
        public long Misses { get; set; }
        public double HitRate => TotalRequests > 0 ? (double)Hits / TotalRequests : 0;
        public int CurrentEntries { get; set; }
        public long Evictions { get; set; }
    }
}