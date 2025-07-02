using HoopGameNight.Core.DTOs.External.BallDontLie;

namespace HoopGameNight.Core.Interfaces.Services
{
    public interface IBallDontLieService
    {
        Task<IEnumerable<BallDontLieGameDto>> GetTodaysGamesAsync();
        Task<IEnumerable<BallDontLieGameDto>> GetGamesByDateAsync(DateTime date);
        Task<IEnumerable<BallDontLieTeamDto>> GetAllTeamsAsync();
        Task<IEnumerable<BallDontLiePlayerDto>> SearchPlayersAsync(string search, int page = 1);
        Task<BallDontLiePlayerDto?> GetPlayerByIdAsync(int playerId);
    }
}