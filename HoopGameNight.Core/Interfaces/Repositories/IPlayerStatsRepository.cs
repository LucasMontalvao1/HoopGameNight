using HoopGameNight.Core.Models.Entities;

namespace HoopGameNight.Core.Interfaces.Repositories
{
    public interface IPlayerStatsRepository
    {
        Task<PlayerSeasonStats?> GetSeasonStatsAsync(int playerId, int season);
        Task<IEnumerable<PlayerSeasonStats>> GetAllSeasonStatsAsync(int playerId);
        Task UpsertSeasonStatsAsync(PlayerSeasonStats seasonStats);

        Task<PlayerCareerStats?> GetCareerStatsAsync(int playerId);
        Task<bool> UpsertCareerStatsAsync(PlayerCareerStats careerStats);

        Task<PlayerGameStats?> GetGameStatsAsync(int playerId, int gameId);
        Task<IEnumerable<PlayerGameStats>> GetRecentGamesAsync(int playerId, int limit);
        Task UpsertGameStatsAsync(PlayerGameStats gameStats);

        Task<IEnumerable<dynamic>> GetScoringLeadersAsync(int season, int minGames, int limit);
        Task<IEnumerable<dynamic>> GetReboundLeadersAsync(int season, int minGames, int limit);
        Task<IEnumerable<dynamic>> GetAssistLeadersAsync(int season, int minGames, int limit);
        Task<bool> BulkUpsertSeasonStatsAsync(IEnumerable<PlayerSeasonStats> seasonStats);
        Task<bool> BulkUpsertGameStatsAsync(IEnumerable<PlayerGameStats> gameStats);
        Task<DateTime?> GetLastSyncDateForPlayerAsync(int playerId);
        Task<bool> UpdateLastSyncDateAsync(int playerId, DateTime syncDate);
    }
}