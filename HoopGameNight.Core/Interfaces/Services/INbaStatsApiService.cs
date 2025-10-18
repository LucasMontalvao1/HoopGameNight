using HoopGameNight.Core.DTOs.External.NBAStats;

namespace HoopGameNight.Core.Interfaces.Services
{
    /// <summary>
    /// Serviço para integração com a Ball Don't Lie API v2 (usa dados da NBA Stats)
    /// Documentação: https://docs.balldontlie.io
    /// </summary>
    public interface INbaStatsApiService
    {
        /// <summary>
        /// Buscar jogador por nome completo
        /// </summary>
        Task<NbaStatsPlayerDto?> SearchPlayerByNameAsync(string firstName, string lastName);

        /// <summary>
        /// Buscar estatísticas de temporada de um jogador
        /// </summary>
        Task<NbaStatsSeasonStatsDto?> GetPlayerSeasonStatsAsync(string playerId, int season);

        /// <summary>
        /// Verificar se a API está disponível
        /// </summary>
        Task<bool> IsApiAvailableAsync();
    }
}
