namespace HoopGameNight.Core.Constants
{
    public static class ValidationMessages
    {
        public static class Games
        {
            public const string INVALID_DATE_RANGE = "Start date cannot be greater than end date";
            public const string INVALID_SEASON = "Season must be greater than 2000";
            public const string TEAMS_CANNOT_BE_SAME = "Home team and visitor team cannot be the same";
        }

        public static class Players
        {
            public const string SEARCH_TOO_SHORT = "Search term must be at least 2 characters";
            public const string SEARCH_CRITERIA_REQUIRED = "At least one search criteria is required";
            public const string INVALID_POSITION = "Invalid player position";
        }

        public static class Pagination
        {
            public const string INVALID_PAGE = "Page must be greater than 0";
            public const string INVALID_PAGE_SIZE = "Page size must be between 1 and 100";
        }

        public static class Teams
        {
            public const string NAME_REQUIRED = "Team name is required";
            public const string CITY_REQUIRED = "Team city is required";
            public const string ABBREVIATION_REQUIRED = "Team abbreviation is required";
        }
    }
}