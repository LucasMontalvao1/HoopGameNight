namespace HoopGameNight.Core.Constants
{
    public static class ValidationMessages
    {
        public static class Games
        {
            public const string INVALID_DATE_RANGE = "A data de início não pode ser maior que a data de fim";
            public const string INVALID_SEASON = "A temporada deve ser maior que 2000";
            public const string TEAMS_CANNOT_BE_SAME = "O time da casa e o time visitante não podem ser iguais";
        }
        public static class Players
        {
            public const string SEARCH_TOO_SHORT = "O termo de busca deve ter pelo menos 2 caracteres";
            public const string SEARCH_CRITERIA_REQUIRED = "Pelo menos um critério de busca é obrigatório";
            public const string INVALID_POSITION = "Posição do jogador inválida";
        }
        public static class Pagination
        {
            public const string INVALID_PAGE = "A página deve ser maior que 0";
            public const string INVALID_PAGE_SIZE = "O tamanho da página deve estar entre 1 e 100";
        }
        public static class Teams
        {
            public const string NAME_REQUIRED = "O nome do time é obrigatório";
            public const string CITY_REQUIRED = "A cidade do time é obrigatória";
            public const string ABBREVIATION_REQUIRED = "A abreviação do time é obrigatória";
        }
    }
}