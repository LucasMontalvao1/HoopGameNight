using System;

namespace HoopGameNight.Core.Configuration
{
    /// <summary>
    /// Durações de cache padronizadas para garantir consistência
    /// Todos os valores centralizados aqui para facilitar ajustes
    /// </summary>
    public static class CacheDurations
    {
        /// <summary>
        /// Indica que o cache não deve expirar automaticamente por tempo
        /// </summary>
        public static TimeSpan NoExpiration => System.Threading.Timeout.InfiniteTimeSpan;

        // ===== GAMES =====

        /// <summary>
        /// Cache para jogos de hoje (5 minutos)
        /// TTL curto pois dados mudam frequentemente durante jogos ao vivo
        /// </summary>
        public static TimeSpan TodayGames => TimeSpan.FromMinutes(5);

        /// <summary>
        /// Cache para jogos por data (15 minutos)
        /// TTL médio para balancear freshness e performance
        /// </summary>
        public static TimeSpan GamesByDate => TimeSpan.FromMinutes(15);

        /// <summary>
        /// Cache para jogos passados (Infinito - Imutável)
        /// </summary>
        public static TimeSpan PastGames => NoExpiration;

        /// <summary>
        /// Cache para jogos futuros (30 minutos)
        /// </summary>
        public static TimeSpan FutureGames => TimeSpan.FromMinutes(30);

        /// <summary>
        /// Cache para jogos ao vivo (30 segundos)
        /// TTL muito curto para atualização de polling
        /// </summary>
        public static TimeSpan LiveGames => TimeSpan.FromSeconds(30);

        // ===== TEAMS =====

        /// <summary>
        /// Cache para todos os times (Infinito - Imutável)
        /// Atualizado via background job
        /// </summary>
        public static TimeSpan AllTeams => NoExpiration;

        /// <summary>
        /// Cache para um time específico (Infinito - Imutável)
        /// </summary>
        public static TimeSpan SingleTeam => NoExpiration;

        /// <summary>
        /// Cache para times por conferência (Infinito)
        /// </summary>
        public static TimeSpan TeamsByConference => NoExpiration;

        // ===== PLAYERS =====

        /// <summary>
        /// Cache para um jogador específico (7 dias)
        /// Quase Estático
        /// </summary>
        public static TimeSpan Player => TimeSpan.FromDays(7);

        /// <summary>
        /// Cache para jogadores de um time (7 dias)
        /// Quase estático, invalidado sob detecção de trade
        /// </summary>
        public static TimeSpan PlayersByTeam => TimeSpan.FromDays(7);

        /// <summary>
        /// Cache para busca de jogadores (1 hora)
        /// </summary>
        public static TimeSpan PlayersSearch => TimeSpan.FromHours(1);

        // ===== STATISTICS =====

        /// <summary>
        /// Cache para estatísticas de jogador na Temporada (Infinito)
        /// Limpo pelo sync pós jogo
        /// </summary>
        public static TimeSpan PlayerStats => NoExpiration;

        /// <summary>
        /// Cache para estatísticas da temporada (Infinito)
        /// Limpo pelo Sync Noturno
        /// </summary>
        public static TimeSpan SeasonStats => NoExpiration;

        /// <summary>
        /// Cache para estatísticas de carreira (Infinito)
        /// Limpo pelo sync pós jogo
        /// </summary>
        public static TimeSpan CareerStats => NoExpiration;

        // ===== SYNC & METADATA =====

        /// <summary>
        /// Cache para status de sincronização (5 minutos)
        /// TTL curto para refletir estado atual
        /// </summary>
        public static TimeSpan SyncStatus => TimeSpan.FromMinutes(5);

        /// <summary>
        /// Cache para health check results (1 minuto)
        /// TTL muito curto para monitoramento em tempo real
        /// </summary>
        public static TimeSpan HealthCheck => TimeSpan.FromMinutes(1);

        // ===== FALLBACK =====

        /// <summary>
        /// TTL padrão quando não especificado (15 minutos)
        /// Valor conservador que funciona para maioria dos casos
        /// </summary>
        public static TimeSpan Default => TimeSpan.FromMinutes(15);

        // ===== HELPERS =====

        /// <summary>
        /// Retorna TTL dinâmico baseado na data do jogo
        /// Jogos passados = cache longo, jogos futuros/hoje = cache curto
        /// </summary>
        public static TimeSpan GetGameCacheDuration(DateTime gameDate)
        {
            if (gameDate.Date < DateTime.Today)
                return PastGames;
            else if (gameDate.Date == DateTime.Today)
                return TodayGames;
            else
                return FutureGames;
        }

        /// <summary>
        /// Retorna TTL baseado no status do jogo
        /// Live = muito curto, Scheduled = curto, Final = longo
        /// </summary>
        public static TimeSpan GetGameCacheDurationByStatus(string status)
        {
            return status.ToUpperInvariant() switch
            {
                "LIVE" => LiveGames,
                "SCHEDULED" => FutureGames,
                "FINAL" => PastGames,
                _ => Default
            };
        }
    }
}
