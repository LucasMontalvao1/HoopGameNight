using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.DTOs.Response;

namespace HoopGameNight.Core.Interfaces.Services
{
    public interface IPlayerStatsService
    {
        Task<PlayerDetailedResponse?> GetPlayerDetailedStatsAsync(PlayerStatsRequest request);
        Task<PlayerSeasonStatsResponse?> GetPlayerSeasonStatsAsync(int playerId, int season);
        Task<List<PlayerSeasonStatsResponse>> GetPlayerAllSeasonsAsync(int playerId);
        Task<PlayerCareerStatsResponse?> GetPlayerCareerStatsAsync(int playerId);
        Task<List<PlayerRecentGameResponse>> GetPlayerRecentGamesAsync(int playerId, int limit);
        Task<PlayerRecentGameResponse?> GetPlayerGameStatsAsync(int playerId, int gameId);
        Task<PlayerComparisonResponse?> ComparePlayersAsync(int player1Id, int player2Id, int? season = null);
        Task<StatLeadersResponse> GetStatLeadersAsync(int season, int minGames, int limit);
        Task<bool> UpdatePlayerCareerStatsAsync(int playerId);
    }
}