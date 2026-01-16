using HoopGameNight.Core.Enums;
using HoopGameNight.Core.Models.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

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
        Task<IEnumerable<Team>> GetByConferenceAsync(Conference conference);
        Task<IEnumerable<Team>> GetByDivisionAsync(string division);
    }
}