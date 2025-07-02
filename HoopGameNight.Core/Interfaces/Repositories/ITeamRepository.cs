using HoopGameNight.Core.Models.Entities;

namespace HoopGameNight.Core.Interfaces.Repositories
{
    public interface ITeamRepository : IBaseRepository<Team>
    {
        Task<IEnumerable<Team>> GetAllAsync();
        Task<Team?> GetByIdAsync(int id);
        Task<Team?> GetByExternalIdAsync(int externalId);
        Task<Team?> GetByAbbreviationAsync(string abbreviation);
        Task<int> InsertAsync(Team team);
        Task<bool> UpdateAsync(Team team);
        Task<bool> ExistsAsync(int externalId);
    }
}