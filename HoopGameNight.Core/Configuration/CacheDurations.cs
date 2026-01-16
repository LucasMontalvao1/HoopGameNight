using System;

namespace HoopGameNight.Core.Configuration
{
    /// <summary>
    /// Durações de cache padronizadas para garantir consistência
    /// Todos os valores centralizados aqui para facilitar ajustes
    /// </summary>
    public static class CacheDurations
    {
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
        /// Cache para jogos passados (1 hora)
        /// TTL longo pois dados históricos não mudam
        /// </summary>
        public static TimeSpan PastGames => TimeSpan.FromHours(1);

        /// <summary>
        /// Cache para jogos futuros (30 minutos)
        /// TTL médio pois horários podem ser alterados
        /// </summary>
        public static TimeSpan FutureGames => TimeSpan.FromMinutes(30);

        /// <summary>
        /// Cache para jogos ao vivo (2 minutos)
        /// TTL muito curto para atualização frequente de placares
        /// </summary>
        public static TimeSpan LiveGames => TimeSpan.FromMinutes(2);

        // ===== TEAMS =====

        /// <summary>
        /// Cache para todos os times (24 horas)
        /// TTL longo pois lista de times muda raramente
        /// </summary>
        public static TimeSpan AllTeams => TimeSpan.FromHours(24);

        /// <summary>
        /// Cache para um time específico (2 horas)
        /// TTL longo pois dados de time mudam raramente
        /// </summary>
        public static TimeSpan SingleTeam => TimeSpan.FromHours(2);

        /// <summary>
        /// Cache para times por conferência (12 horas)
        /// TTL longo pois conferências não mudam durante temporada
        /// </summary>
        public static TimeSpan TeamsByConference => TimeSpan.FromHours(12);

        // ===== PLAYERS =====

        /// <summary>
        /// Cache para um jogador específico (1 hora)
        /// TTL médio pois dados podem ser atualizados (lesões, trocas)
        /// </summary>
        public static TimeSpan Player => TimeSpan.FromHours(1);

        /// <summary>
        /// Cache para jogadores de um time (30 minutos)
        /// TTL médio pois roster pode mudar (call-ups, trades)
        /// </summary>
        public static TimeSpan PlayersByTeam => TimeSpan.FromMinutes(30);

        /// <summary>
        /// Cache para busca de jogadores (15 minutos)
        /// TTL curto pois resultados dependem de dados frescos
        /// </summary>
        public static TimeSpan PlayersSearch => TimeSpan.FromMinutes(15);

        // ===== STATISTICS =====

        /// <summary>
        /// Cache para estatísticas de jogador (15 minutos)
        /// TTL curto pois stats mudam após cada jogo
        /// </summary>
        public static TimeSpan PlayerStats => TimeSpan.FromMinutes(15);

        /// <summary>
        /// Cache para estatísticas da temporada (1 hora)
        /// TTL médio pois stats agregadas mudam menos frequentemente
        /// </summary>
        public static TimeSpan SeasonStats => TimeSpan.FromHours(1);

        /// <summary>
        /// Cache para estatísticas de carreira (24 horas)
        /// TTL longo pois dados históricos são estáveis
        /// </summary>
        public static TimeSpan CareerStats => TimeSpan.FromHours(24);

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
