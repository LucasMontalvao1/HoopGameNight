using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.Models.Entities;

namespace HoopGameNight.Core.Interfaces.Repositories
{
    public interface IGameRepository : IBaseRepository<Game>
    {
        // Métodos básicos de consulta
        Task<IEnumerable<Game>> GetTodayGamesAsync();
        Task<IEnumerable<Game>> GetGamesByDateAsync(DateTime date);
        Task<IEnumerable<Game>> GetByDateAsync(DateTime date); 
        Task<IEnumerable<Game>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<IEnumerable<Game>> GetLiveGamesAsync();

        // Métodos de consulta por time
        Task<IEnumerable<Game>> GetByTeamAsync(int teamId, DateTime? startDate = null, DateTime? endDate = null);
        Task<(IEnumerable<Game> Games, int TotalCount)> GetGamesByTeamAsync(int teamId, int page, int pageSize);

        // Métodos de consulta com filtros
        Task<(IEnumerable<Game> Games, int TotalCount)> GetGamesAsync(GetGamesRequest request);

        // Métodos por ID externo
        Task<Game?> GetByExternalIdAsync(int externalId);
        Task<bool> ExistsAsync(int externalId);

        // Métodos de atualização específicos
        Task<bool> UpdateScoreAsync(int gameId, int homeScore, int visitorScore);
    }
}