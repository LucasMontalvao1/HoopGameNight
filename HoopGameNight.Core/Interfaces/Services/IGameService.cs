using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.DTOs.Response;

namespace HoopGameNight.Core.Interfaces.Services
{
    public interface IGameService
    {
        Task<List<GameResponse>> GetTodayGamesAsync();
        Task<List<GameResponse>> GetGamesByDateAsync(DateTime date);
        Task<(List<GameResponse> Games, int TotalCount)> GetGamesAsync(GetGamesRequest request);
        Task<(List<GameResponse> Games, int TotalCount)> GetGamesByTeamAsync(int teamId, int page, int pageSize);
        Task<int> SyncGamesByDateAsync(DateTime date);
        Task<int> SyncGamesForPeriodAsync(DateTime startDate, DateTime endDate);
        Task<GameResponse?> GetGameByIdAsync(int id);
        Task SyncTodayGamesAsync();
    }
}