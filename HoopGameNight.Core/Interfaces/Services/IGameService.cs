using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.DTOs.Response;

namespace HoopGameNight.Core.Interfaces.Services
{
    public interface IGameService
    {
        /// <summary>
        /// Busca jogos do dia atual
        /// </summary>
        Task<List<GameResponse>> GetTodayGamesAsync();

        /// <summary>
        /// Busca jogos por data específica
        /// </summary>
        Task<List<GameResponse>> GetGamesByDateAsync(DateTime date);

        /// <summary>
        /// Busca jogos com paginação e filtros
        /// </summary>
        Task<(List<GameResponse> Games, int TotalCount)> GetGamesAsync(GetGamesRequest request);

        /// <summary>
        /// Busca jogos de um time específico com paginação
        /// </summary>
        Task<(List<GameResponse> Games, int TotalCount)> GetGamesByTeamAsync(int teamId, int page, int pageSize);

        /// <summary>
        /// Busca um jogo por ID
        /// </summary>
        Task<GameResponse?> GetGameByIdAsync(int id);

        /// <summary>
        /// Busca jogos de múltiplos times em um período
        /// </summary>
        Task<MultipleTeamsGamesResponse> GetGamesForMultipleTeamsAsync(GetMultipleTeamsGamesRequest request);

        /// <summary>
        /// Busca próximos jogos de um time
        /// </summary>
        Task<List<GameResponse>> GetUpcomingGamesForTeamAsync(int teamId, int days = 7);

        /// <summary>
        /// Busca jogos recentes de um time
        /// </summary>
        Task<List<GameResponse>> GetRecentGamesForTeamAsync(int teamId, int days = 7);

        /// <summary>
        /// Sincroniza jogos por data específica
        /// </summary>
        Task<int> SyncGamesByDateAsync(DateTime date);

        /// <summary>
        /// Sincroniza jogos para um período específico
        /// </summary>
        Task<int> SyncGamesForPeriodAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// Sincroniza jogos do dia atual
        /// </summary>
        Task SyncTodayGamesAsync();

        /// <summary>
        /// Sincroniza jogos futuros da ESPN e salva no banco
        /// </summary>
        Task<int> SyncFutureGamesAsync(int days = 30);
    }
}