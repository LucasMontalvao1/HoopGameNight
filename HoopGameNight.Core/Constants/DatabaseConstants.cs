namespace HoopGameNight.Core.Constants
{
    public static class DatabaseConstants
    {
        public const int DEFAULT_PAGE_SIZE = 25;
        public const int MAX_PAGE_SIZE = 100;
        public const int DEFAULT_COMMAND_TIMEOUT = 30;

        public static class Tables
        {
            public const string TEAMS = "teams";
            public const string PLAYERS = "players";
            public const string GAMES = "games";
        }

        public static class Columns
        {
            public static class Teams
            {
                public const string ID = "id";
                public const string EXTERNAL_ID = "external_id";
                public const string NAME = "name";
                public const string FULL_NAME = "full_name";
                public const string ABBREVIATION = "abbreviation";
                public const string CITY = "city";
                public const string CONFERENCE = "conference";
                public const string DIVISION = "division";
                public const string CREATED_AT = "created_at";
                public const string UPDATED_AT = "updated_at";
            }

            public static class Players
            {
                public const string ID = "id";
                public const string EXTERNAL_ID = "external_id";
                public const string FIRST_NAME = "first_name";
                public const string LAST_NAME = "last_name";
                public const string POSITION = "position";
                public const string HEIGHT_FEET = "height_feet";
                public const string HEIGHT_INCHES = "height_inches";
                public const string WEIGHT_POUNDS = "weight_pounds";
                public const string TEAM_ID = "team_id";
                public const string CREATED_AT = "created_at";
                public const string UPDATED_AT = "updated_at";
            }

            public static class Games
            {
                public const string ID = "id";
                public const string EXTERNAL_ID = "external_id";
                public const string DATE = "date";
                public const string DATETIME = "datetime";
                public const string HOME_TEAM_ID = "home_team_id";
                public const string VISITOR_TEAM_ID = "visitor_team_id";
                public const string HOME_TEAM_SCORE = "home_team_score";
                public const string VISITOR_TEAM_SCORE = "visitor_team_score";
                public const string STATUS = "status";
                public const string PERIOD = "period";
                public const string TIME_REMAINING = "time_remaining";
                public const string POSTSEASON = "postseason";
                public const string SEASON = "season";
                public const string CREATED_AT = "created_at";
                public const string UPDATED_AT = "updated_at";
            }
        }
    }
}