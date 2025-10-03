using HoopGameNight.Core.DTOs.External;
using HoopGameNight.Core.DTOs.External.ESPN;

namespace HoopGameNight.Core.Interfaces.Services
{
    public interface IEspnApiService
    {
        Task<List<EspnGameDto>> GetGamesByDateAsync(DateTime date);
        Task<List<EspnGameDto>> GetFutureGamesAsync(int days = 7);
        Task<List<EspnGameDto>> GetTeamScheduleAsync(int teamId, DateTime startDate, DateTime endDate);
        Task<bool> IsApiAvailableAsync();
        Task<List<EspnAthleteRefDto>> GetAllPlayersAsync();
        Task<EspnPlayerDetailsDto?> GetPlayerDetailsAsync(string playerId);
        Task<EspnPlayerStatsDto?> GetPlayerStatsAsync(string playerId);
        Task<EspnPlayerStatsDto?> GetPlayerSeasonStatsAsync(string playerId, int season);
        Task<List<EspnPlayerStatsDto>> GetPlayerCareerStatsAsync(string playerId);
        Task<EspnPlayerStatsDto?> GetPlayerGameStatsAsync(string playerId, string gameId);
    }
}