using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.Models.Entities;

namespace HoopGameNight.Core.Interfaces.Repositories
{
    public interface IPlayerRepository : IBaseRepository<Player>
    {
        Task<(IEnumerable<Player> Players, int TotalCount)> SearchPlayersAsync(SearchPlayerRequest request);
        Task<(IEnumerable<Player> Players, int TotalCount)> GetPlayersByTeamAsync(int teamId, int page, int pageSize);
        Task<Player?> GetByExternalIdAsync(int externalId);
        Task<bool> ExistsAsync(int externalId);
        Task<IEnumerable<Player>> GetByTeamIdAsync(int teamId);
        Task<IEnumerable<Player>> GetActivePlayersAsync();
        Task<IEnumerable<Player>> SearchAsync(string searchTerm);
        Task<bool> UpdateTeamAsync(int playerId, int? teamId);
    }
}