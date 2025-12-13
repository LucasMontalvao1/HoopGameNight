namespace HoopGameNight.Core.Helpers
{
    /// <summary>
    /// Helper para cálculos relacionados a temporadas da NBA.
    /// Esta versão foi revisada e ampliada para evitar ambiguidades,
    /// suportar temporadas passadas/futuras e garantir consistência.
    /// </summary>
    public static class NbaSeasonHelper
    {
        /// <summary>
        /// Determina a temporada "NBA-style".
        /// Regras da NBA:
        /// - Temporada 2025-2026 é representada externamente como "2025".
        /// - Começa oficialmente em OUTUBRO
        /// - Playoffs vão até JUNHO do ano seguinte
        /// </summary>
        public static int GetCurrentSeason()
        {
            var now = DateTime.UtcNow; // Utc para evitar divergências em deploys

            // Janeiro – Setembro → temporada pertence ao ano anterior
            // Outubro – Dezembro → temporada pertence ao ano atual
            return now.Month >= 10 ? now.Year : now.Year - 1;
        }

        /// <summary>
        /// Determina a temporada com base em uma data específica.
        /// Útil quando consultamos jogos ou boxscores com data histórica.
        /// </summary>
        public static int GetSeasonFromDate(DateTime date)
        {
            return date.Month >= 10 ? date.Year : date.Year - 1;
        }

        /// <summary>
        /// Validação simples de limites aceitáveis.
        /// </summary>
        public static bool IsValidSeason(int season)
        {
            int currentSeason = GetCurrentSeason();
            return season >= 2000 && season <= currentSeason + 1;
        }

        /// <summary>
        /// Retorna o nome completo da temporada (ex.: "2025-2026").
        /// </summary>
        public static string GetSeasonName(int season)
        {
            return $"{season}-{season + 1}";
        }

        /// <summary>
        /// Verifica se uma season é a season atual.
        /// </summary>
        public static bool IsCurrentSeason(int season)
        {
            return season == GetCurrentSeason();
        }

        /// <summary>
        /// Garante que uma temporada enviada pela API não quebre lógica
        /// e retorna a season final usada.
        /// </summary>
        public static int NormalizeSeason(int? season)
        {
            return season.HasValue && IsValidSeason(season.Value)
                ? season.Value
                : GetCurrentSeason();
        }

        /// <summary>
        /// Muitos serviços como ESPN usam season como "2025" para "2025-26".
        /// Esta função confirma que essa forma está coerente.
        /// </summary>
        public static int ToEspnSeasonFormat(int season)
        {
            return season; // ESPN já espera exatamente assim
        }
    }
}