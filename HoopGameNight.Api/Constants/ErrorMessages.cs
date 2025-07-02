namespace HoopGameNight.Api.Constants
{
    public static class ErrorMessages
    {
        // Generic
        public const string INTERNAL_SERVER_ERROR = "An internal server error occurred";
        public const string INVALID_REQUEST = "Invalid request parameters";
        public const string RESOURCE_NOT_FOUND = "Resource not found";

        // Games
        public const string GAME_NOT_FOUND = "Game not found";
        public const string INVALID_GAME_DATE = "Invalid game date";

        // Teams
        public const string TEAM_NOT_FOUND = "Team not found";
        public const string INVALID_TEAM_ABBREVIATION = "Invalid team abbreviation";

        // Players
        public const string PLAYER_NOT_FOUND = "Player not found";
        public const string INVALID_SEARCH_CRITERIA = "Invalid search criteria";

        // External API
        public const string EXTERNAL_API_ERROR = "External service temporarily unavailable";
        public const string API_KEY_MISSING = "API key is missing or invalid";

        // Validation
        public const string INVALID_PAGINATION = "Invalid pagination parameters";
        public const string SEARCH_TOO_SHORT = "Search term must be at least 2 characters";
    }
}