namespace HoopGameNight.Api.Constants
{
    public static class ApiConstants
    {
        public const string API_VERSION = "v1";
        public const string API_PREFIX = "api";
        public const string CORRELATION_ID_HEADER = "X-Correlation-ID";

        public static class Routes
        {
            public const string GAMES = $"{API_PREFIX}/{API_VERSION}/games";
            public const string TEAMS = $"{API_PREFIX}/{API_VERSION}/teams";
            public const string PLAYERS = $"{API_PREFIX}/{API_VERSION}/players";
        }

        public static class CacheKeys
        {
            public const string ALL_TEAMS = "teams:all";
            public const string TODAY_GAMES = "games:today";
            public const string TEAM_BY_ID = "team:id:{0}";
            public const string PLAYER_BY_ID = "player:id:{0}";
            public const string GAMES_BY_DATE = "games:date:{0}";
            public const string GAMES_BY_TEAM = "games:team:{0}";
            public const string PLAYERS_BY_TEAM = "players:team:{0}";
        }

        public static class Headers
        {
            public const string REQUEST_ID = "X-Request-ID";
            public const string API_VERSION_HEADER = "X-API-Version";
            public const string RATE_LIMIT_REMAINING = "X-RateLimit-Remaining";
            public const string RATE_LIMIT_RESET = "X-RateLimit-Reset";
        }
        public static class ApiDefaults
        {
            public const int DEFAULT_PAGE_SIZE = 25;
            public const int MAX_PAGE_SIZE = 100;
            public const int MIN_PAGE_SIZE = 1;
            public const int DEFAULT_CACHE_MINUTES = 15;
        }

        public static class ValidationMessages
        {
            public const string INVALID_PAGE_SIZE = "Page size must be between 1 and 100";
            public const string INVALID_PAGE_NUMBER = "Page number must be greater than 0";
            public const string INVALID_SEARCH_TERM = "Search term must be at least 2 characters";
            public const string INVALID_DATE_RANGE = "Start date cannot be greater than end date";
        }

        public static class ExternalApis
        {
            public const string BALL_DONT_LIE = "Ball Don't Lie";
        }

        public static class DateFormats
        {
            public const string API_DATE_FORMAT = "yyyy-MM-dd";
            public const string DISPLAY_DATE_FORMAT = "dd/MM/yyyy";
            public const string DATETIME_FORMAT = "yyyy-MM-dd HH:mm:ss";
        }
    }
}