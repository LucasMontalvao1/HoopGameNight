using System;

namespace HoopGameNight.Core.Constants
{
    /// <summary>
    /// Chaves de cache padronizadas para garantir consistência
    /// </summary>
    public static class CacheKeys
    {
        // ===== GAMES =====

        /// <summary>
        /// Chave para jogos de hoje
        /// Formato: "games:today:2025-11-13"
        /// </summary>
        public static string TodayGames() => $"games:today:{DateTime.Today:yyyy-MM-dd}";

        /// <summary>
        /// Chave para jogos de uma data específica
        /// Formato: "games:date:2025-11-20"
        /// </summary>
        public static string GamesByDate(DateTime date) => $"games:date:{date:yyyy-MM-dd}";

        /// <summary>
        /// Chave para um jogo específico por ID
        /// Formato: "games:id:123"
        /// </summary>
        public static string GameById(int id) => $"games:id:{id}";

        /// <summary>
        /// Chave para líderes do jogo
        /// Formato: "game_leaders_123"
        /// </summary>
        public static string GameLeaders(int gameId) => $"game_leaders_{gameId}";

        /// <summary>
        /// Chave para boxscore do jogo
        /// Formato: "game_boxscore_123"
        /// </summary>
        public static string GameBoxscore(int gameId) => $"game_boxscore_{gameId}";

        /// <summary>
        /// Chave para jogos de um time
        /// Formato: "games:team:13:page:1"
        /// </summary>
        public static string GamesByTeam(int teamId, int page) => $"games:team:{teamId}:page:{page}";

        /// <summary>
        /// Chave para próximos jogos de um time
        /// Formato: "games:team:13:upcoming:7"
        /// </summary>
        public static string UpcomingGamesByTeam(int teamId, int days) => $"games:team:{teamId}:upcoming:{days}";

        /// <summary>
        /// Chave para jogos recentes de um time
        /// Formato: "games:team:13:recent:7"
        /// </summary>
        public static string RecentGamesByTeam(int teamId, int days) => $"games:team:{teamId}:recent:{days}";

        // ===== TEAMS =====

        /// <summary>
        /// Chave para todos os times
        /// Formato: "teams:all"
        /// </summary>
        public const string ALL_TEAMS = "teams:all";

        /// <summary>
        /// Chave para um time específico por ID
        /// Formato: "teams:id:13"
        /// </summary>
        public static string TeamById(int id) => $"teams:id:{id}";

        /// <summary>
        /// Chave para um time por abreviação
        /// Formato: "teams:abbr:LAL"
        /// </summary>
        public static string TeamByAbbreviation(string abbreviation) => $"teams:abbr:{abbreviation.ToUpperInvariant()}";

        /// <summary>
        /// Chave para times por conferência
        /// Formato: "teams:conference:WEST"
        /// </summary>
        public static string TeamsByConference(string conference) => $"teams:conference:{conference.ToUpperInvariant()}";

        // ===== PLAYERS =====

        /// <summary>
        /// Chave para um jogador específico por ID
        /// Formato: "players:id:456"
        /// </summary>
        public static string PlayerById(int id) => $"players:id:{id}";

        /// <summary>
        /// Chave para jogadores de um time
        /// Formato: "players:team:13:page:1"
        /// </summary>
        public static string PlayersByTeam(int teamId, int page) => $"players:team:v3:{teamId}:page:{page}";

        /// <summary>
        /// Chave para busca de jogadores
        /// Formato: "players:search:lebron:page:1"
        /// </summary>
        public static string PlayersSearch(string term, int page) => $"players:search:{term.ToLowerInvariant()}:page:{page}";

        /// <summary>
        /// Chave para estatísticas de jogador na temporada
        /// Formato: "player_season_stats_456" (modificado para bater com BackgroundSyncService)
        /// </summary>
        public static string PlayerSeasonStats(int playerId) => $"player_season_stats_{playerId}";

        /// <summary>
        /// Chave para estatísticas de carreira do jogador
        /// Formato: "player_career_456"
        /// </summary>
        public static string PlayerCareer(int playerId) => $"player_career_{playerId}";

        /// <summary>
        /// Chave para jogos recentes do jogador
        /// Formato: "player_recent_456"
        /// </summary>
        public static string PlayerRecentGames(int playerId) => $"player_recent_{playerId}";

        // ===== STATISTICS =====

        /// <summary>
        /// Chave para estatísticas da temporada atual
        /// Formato: "stats:season:2024"
        /// </summary>
        public static string SeasonStats(int season) => $"stats:season:{season}";

        /// <summary>
        /// Chave para estatísticas de um time na temporada
        /// Formato: "stats:team:13:season:2024"
        /// </summary>
        public static string TeamSeasonStats(int teamId, int season) => $"stats:team:{teamId}:season:{season}";

        // ===== SYNC STATUS =====

        /// <summary>
        /// Chave para status de sincronização
        /// Formato: "sync:status"
        /// </summary>
        public const string SYNC_STATUS = "sync:status";

        /// <summary>
        /// Chave para última sincronização
        /// Formato: "sync:last:games"
        /// </summary>
        public static string LastSync(string entity) => $"sync:last:{entity.ToLowerInvariant()}";

        // ===== HELPERS =====

        /// <summary>
        /// Remove todas as chaves de cache que começam com o prefixo
        /// Útil para invalidação em massa
        /// </summary>
        public static string GetPattern(string prefix) => $"{prefix}*";

        /// <summary>
        /// Padrão para invalidar todos os jogos
        /// </summary>
        public static string AllGamesPattern => "games:*";

        /// <summary>
        /// Padrão para invalidar todos os times
        /// </summary>
        public static string AllTeamsPattern => "teams:*";

        /// <summary>
        /// Padrão para invalidar todos os jogadores
        /// </summary>
        public static string AllPlayersPattern => "players:*";

        public static string GamesByDateRange(DateTime start, DateTime end) => $"games:range:{start:yyyyMMdd}:{end:yyyyMMdd}";
    }
}
