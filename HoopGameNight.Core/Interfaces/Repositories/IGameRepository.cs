using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.Models.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HoopGameNight.Core.Interfaces.Repositories
{
    public interface IGameRepository : IBaseRepository<Game>
    {
        Task<IEnumerable<Game>> GetTodayGamesAsync();
        Task<IEnumerable<Game>> GetGamesByDateAsync(DateTime date);
        Task<IEnumerable<Game>> GetByDateAsync(DateTime date); 
        Task<IEnumerable<Game>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<IEnumerable<Game>> GetLiveGamesAsync();

        Task<IEnumerable<Game>> GetByTeamAsync(int teamId, DateTime? startDate = null, DateTime? endDate = null);
        Task<(IEnumerable<Game> Games, int TotalCount)> GetGamesByTeamAsync(int teamId, int page, int pageSize);

        Task<(IEnumerable<Game> Games, int TotalCount)> GetGamesAsync(GetGamesRequest request);

        Task<Game?> GetByExternalIdAsync(string externalId);
        Task<bool> ExistsByExternalIdAsync(string externalId);
        Task<bool> UpdateScoreAsync(int gameId, int homeScore, int visitorScore);
    }
}