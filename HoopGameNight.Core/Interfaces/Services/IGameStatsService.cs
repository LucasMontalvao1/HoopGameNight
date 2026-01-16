using HoopGameNight.Core.DTOs.External.ESPN;
using HoopGameNight.Core.DTOs.Response;
using System.Threading.Tasks;

namespace HoopGameNight.Core.Interfaces.Services
{
    /// <summary>
    /// Interface para serviço de estatísticas de jogos
    /// </summary>
    public interface IGameStatsService
    {
        /// <summary>
        /// Busca estatísticas de todos os jogadores em um jogo
        /// </summary>
        Task<GamePlayerStatsResponse?> GetGamePlayerStatsAsync(int gameId);

        /// <summary>
        /// Busca líderes estatísticos de um jogo
        /// </summary>
        Task<GameLeadersResponse?> GetGameLeadersAsync(int gameId);

        /// <summary>
        /// Busca boxscore completo de um jogo
        /// </summary>
        Task<EspnBoxscoreDto?> GetGameBoxscoreAsync(int gameId);

        /// <summary>
        /// Sincroniza estatísticas de todos os jogadores de um jogo
        /// </summary>
        Task<bool> SyncGameStatsAsync(int gameId);
    }
}