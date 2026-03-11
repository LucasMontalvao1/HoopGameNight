using HoopGameNight.Core.DTOs.External.ESPN;
using HoopGameNight.Core.DTOs.Response;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HoopGameNight.Core.Interfaces.Services
{
    public interface IPlayerStatsService
    {
        Task<PlayerSeasonStatsResponse?> GetPlayerSeasonStatsAsync(int playerId, int season);
        Task<PlayerGameStatsDetailedResponse?> GetPlayerGameStatsAsync(int playerId, int gameId);
        Task<PlayerGamelogResponse?> GetPlayerRecentGamesAsync(int playerId, int limit = 5);
        Task<PlayerGamelogResponse?> GetPlayerGamelogFromEspnAsync(int playerId); 
        Task<PlayerSplitsResponse?> GetPlayerSplitsFromEspnAsync(int playerId);
        Task<PlayerCareerResponse?> GetPlayerCareerStatsFromEspnAsync(int playerId);
        Task<bool> SyncPlayerSeasonStatsAsync(int playerId, int season);
        Task<bool> SyncPlayerGameStatsAsync(int playerId, int gameId);   
        Task<object?> GetPlayerGameStatsDirectAsync(int playerId, int gameId);
        Task<bool> UpdatePlayerCareerStatsAsync(int playerId);
        Task<StatLeadersResponse> GetLeagueLeadersAsync(int season);
    }
}