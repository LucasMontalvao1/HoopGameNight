using HoopGameNight.Core.Models.Entities;
using System.Threading.Tasks;

namespace HoopGameNight.Core.Interfaces.Services
{
    public interface IPlayerStatsSyncService
    {
        Task<bool> SyncPlayerSeasonStatsAsync(int playerId, int season);
        Task<bool> SyncPlayerGameStatsAsync(int playerId, int gameId);
        Task<bool> SyncPlayerRecentGamesAsync(int playerId, int numberOfGames = 10);
        Task<bool> SyncAllPlayersSeasonStatsAsync(int season);
        Task<bool> SyncGameStatsForAllPlayersInGameAsync(int gameId);
        Task<int> SyncPlayerCareerHistoryAsync(int playerId, int startYear, int endYear);
    }
}
