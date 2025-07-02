namespace HoopGameNight.Core.Constants
{
    public static class CacheKeys
    {
        public const string ALL_TEAMS = "teams:all";
        public const string TODAY_GAMES = "games:today";
        public const string TEAM_BY_ID = "team:id:{0}";
        public const string PLAYER_BY_ID = "player:id:{0}";
        public const string GAMES_BY_DATE = "games:date:{0}";
        public const string GAMES_BY_TEAM = "games:team:{0}";
        public const string PLAYERS_BY_TEAM = "players:team:{0}";

        public static string GetTeamById(int id) => string.Format(TEAM_BY_ID, id);
        public static string GetPlayerById(int id) => string.Format(PLAYER_BY_ID, id);
        public static string GetGamesByDate(string date) => string.Format(GAMES_BY_DATE, date);
        public static string GetGamesByTeam(int teamId) => string.Format(GAMES_BY_TEAM, teamId);
        public static string GetPlayersByTeam(int teamId) => string.Format(PLAYERS_BY_TEAM, teamId);
    }
}