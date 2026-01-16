namespace HoopGameNight.Api.Constants
{
    public static class RouteConstants
    {
        public static class Games
        {
            public const string GET_TODAY = "today";
            public const string GET_BY_DATE = "date/{date:datetime}";
            public const string GET_BY_TEAM = "team/{teamId:int}";
            public const string GET_BY_ID = "{id:int}";

            public const string SYNC_TODAY = "sync/today";
            public const string SYNC_BY_DATE = "sync/date/{date:datetime}";
            public const string EXTERNAL_TODAY = "external/today";
            public const string SYNC_STATUS = "sync/status";
        }

        public static class GamesStats
        {
            public const string GET_GAMES_STATS = "GetGameStats";
        }

        public static class Teams
        {
            public const string GET_ALL = "";
            public const string GET_BY_ID = "{id:int}";
            public const string GET_BY_ABBREVIATION = "abbreviation/{abbreviation}";

            public const string SYNC = "sync";
            public const string EXTERNAL = "external";
            public const string SYNC_STATUS = "sync/status";
        }

        public static class Players
        {
            public const string SEARCH = "search";
            public const string GET_BY_ID = "{id:int}";
            public const string GET_BY_TEAM = "team/{teamId:int}";

            public const string SYNC = "sync";
            public const string EXTERNAL_SEARCH = "external/search";
            public const string SYNC_STATUS = "sync/status";
        }

        public static class PlayerStats
        {
            public const string GET_STATS = "{playerId:int}";
            public const string GET_SEASON = "{playerId:int}/season/{season:int}";
            public const string GET_ALL_SEASONS = "{playerId:int}/seasons";
            public const string GET_CAREER = "{playerId:int}/career";
            public const string GET_RECENT_GAMES = "{playerId:int}/recent-games";
            public const string GET_GAME_STATS = "{playerId:int}/game/{gameId:int}";
            public const string GET_ALL_GAMES = "{playerId:int}/games";
            public const string GET_GAME_PLAYERS = "game/{gameId:int}";
            public const string UPDATE_CAREER = "{playerId:int}/career/update";
        }

        public static class Sync
        {
            public const string ALL = "all";
            public const string ESSENTIAL = "essential";
            public const string STATUS = "status";
            public const string HEALTH = "health";
            public const string CLEAR_CACHE = "cache/clear";
        }
    }
}