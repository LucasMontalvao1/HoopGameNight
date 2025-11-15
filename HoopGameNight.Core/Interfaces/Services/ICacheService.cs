namespace HoopGameNight.Core.Interfaces.Services
{
    /// <summary>
    /// Serviço centralizado de cache com suporte a Redis e Memory Cache
    /// </summary>
    public interface ICacheService
    {
        /// <summary>
        /// Busca um valor do cache de forma síncrona (apenas Memory Cache)
        /// </summary>
        T? Get<T>(string key);

        /// <summary>
        /// Busca um valor do cache (Redis → Memory → null)
        /// </summary>
        Task<T?> GetAsync<T>(string key);

        /// <summary>
        /// Armazena um valor no cache de forma síncrona (apenas Memory Cache)
        /// </summary>
        void Set<T>(string key, T value, TimeSpan? expiration = null);

        /// <summary>
        /// Armazena um valor no cache (Redis + Memory)
        /// </summary>
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);

        /// <summary>
        /// Remove um valor do cache de forma síncrona
        /// </summary>
        void Remove(string key);

        /// <summary>
        /// Remove um valor específico do cache
        /// </summary>
        Task RemoveAsync(string key);

        /// <summary>
        /// Verifica se uma chave existe no cache
        /// </summary>
        bool Exists(string key);

        /// <summary>
        /// Verifica se uma chave existe no cache (async)
        /// </summary>
        Task<bool> ExistsAsync(string key);

        /// <summary>
        /// Limpa todo o cache (apenas Memory Cache)
        /// </summary>
        void Clear();

        /// <summary>
        /// Invalida chaves por padrão (ex: "games:*")
        /// </summary>
        void InvalidatePattern(string pattern);

        /// <summary>
        /// Remove todas as chaves que começam com o padrão especificado
        /// </summary>
        Task RemoveByPatternAsync(string pattern);

        /// <summary>
        /// Obtém estatísticas do cache
        /// </summary>
        CacheStatistics GetStatistics();

        /// <summary>
        /// Obtém estatísticas do cache (async)
        /// </summary>
        Task<CacheStatistics> GetStatisticsAsync();
    }

    /// <summary>
    /// Estatísticas do cache
    /// </summary>
    public class CacheStatistics
    {
        // Estatísticas detalhadas por camada
        public long RedisHits { get; set; }
        public long RedisMisses { get; set; }
        public long MemoryHits { get; set; }
        public long MemoryMisses { get; set; }

        // Totais agregados
        public long TotalHits => RedisHits + MemoryHits;
        public long TotalMisses => RedisMisses + MemoryMisses;
        public long TotalRequests => TotalHits + TotalMisses;

        // Estatísticas compatíveis com versão antiga
        public long Hits => TotalHits;
        public long Misses => TotalMisses;

        // Hit rate
        public double HitRate => TotalRequests > 0 ? (double)TotalHits / TotalRequests * 100 : 0;

        // Contadores
        public int MemoryCacheCount { get; set; }
        public int CurrentEntries => MemoryCacheCount;
        public long Evictions { get; set; }

        // Metadados
        public string LastResetTime { get; set; } = DateTime.UtcNow.ToString("O");
    }
}
