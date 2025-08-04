using HoopGameNight.Core.DTOs.External;

namespace HoopGameNight.Core.Interfaces.Services
{
    public interface IEspnApiService
    {
        Task<List<EspnGameDto>> GetGamesByDateAsync(DateTime date);
        Task<List<EspnGameDto>> GetFutureGamesAsync(int days = 7);
        Task<List<EspnGameDto>> GetTeamScheduleAsync(int teamId, DateTime startDate, DateTime endDate);
        Task<bool> IsApiAvailableAsync();
    }
}