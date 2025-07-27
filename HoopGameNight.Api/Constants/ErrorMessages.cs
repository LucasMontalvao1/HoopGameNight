namespace HoopGameNight.Api.Constants
{
    public static class ErrorMessages
    {
        // Genérico
        public const string INTERNAL_SERVER_ERROR = "Ocorreu um erro interno no servidor";
        public const string INVALID_REQUEST = "Parâmetros da requisição inválidos";
        public const string RESOURCE_NOT_FOUND = "Recurso não encontrado";

        // Jogos
        public const string GAME_NOT_FOUND = "Jogo não encontrado";
        public const string INVALID_GAME_DATE = "Data do jogo inválida";

        // Times
        public const string TEAM_NOT_FOUND = "Time não encontrado";
        public const string INVALID_TEAM_ABBREVIATION = "Abreviação do time inválida";

        // Jogadores
        public const string PLAYER_NOT_FOUND = "Jogador não encontrado";
        public const string INVALID_SEARCH_CRITERIA = "Critério de busca inválido";

        // API Externa
        public const string EXTERNAL_API_ERROR = "Serviço externo temporariamente indisponível";
        public const string API_KEY_MISSING = "Chave da API ausente ou inválida";

        // Validação
        public const string INVALID_PAGINATION = "Parâmetros de paginação inválidos";
        public const string SEARCH_TOO_SHORT = "O termo de busca deve ter pelo menos 2 caracteres";
    }
}
