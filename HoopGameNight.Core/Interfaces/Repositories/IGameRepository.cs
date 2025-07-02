using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.Models.Entities;

namespace HoopGameNight.Core.Interfaces.Repositories
{
    public interface IGameRepository : IBaseRepository<Game>
    {
        Task<IEnumerable<Game>> GetTodayGamesAsync();
        Task<IEnumerable<Game>> GetGamesByDateAsync(DateTime date);
        Task<(IEnumerable<Game> Games, int TotalCount)> GetGamesAsync(GetGamesRequest request);
        Task<(IEnumerable<Game> Games, int TotalCount)> GetGamesByTeamAsync(int teamId, int page, int pageSize);
        Task<Game?> GetByExternalIdAsync(int externalId);
        Task<bool> ExistsAsync(int externalId);
    }
}