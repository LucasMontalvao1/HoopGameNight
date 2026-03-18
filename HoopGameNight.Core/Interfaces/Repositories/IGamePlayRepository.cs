using HoopGameNight.Core.Models.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HoopGameNight.Core.Interfaces.Repositories
{
    public interface IGamePlayRepository : IBaseRepository<GamePlay>
    {
        Task<IEnumerable<GamePlay>> GetByGameIdAsync(int gameId);
        Task<bool> SavePlaysAsync(int gameId, IEnumerable<GamePlay> plays);
        Task<bool> DeleteByGameIdAsync(int gameId);
    }
}
