using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Models.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
        Task<IEnumerable<PlayerGameStats>> GetAllPlayerGamesAsync(int playerId);
        Task UpsertGameStatsAsync(PlayerGameStats gameStats);

        // ===== NOVO: Buscar season stats da VIEW calculada =====
        Task<PlayerSeasonStatsResponse?> GetSeasonStatsFromViewAsync(int playerId, int season);

        Task<IEnumerable<dynamic>> GetScoringLeadersAsync(int season, int minGames, int limit);
        Task<IEnumerable<dynamic>> GetReboundLeadersAsync(int season, int minGames, int limit);
        Task<IEnumerable<dynamic>> GetAssistLeadersAsync(int season, int minGames, int limit);
        Task<bool> BulkUpsertSeasonStatsAsync(IEnumerable<PlayerSeasonStats> seasonStats);
        Task<bool> BulkUpsertGameStatsAsync(IEnumerable<PlayerGameStats> gameStats);
        Task<DateTime?> GetLastSyncDateForPlayerAsync(int playerId);
        Task<bool> UpdateLastSyncDateAsync(int playerId, DateTime syncDate);

        // ===== NOVOS MÉTODOS: Estatísticas Detalhadas por Jogo (usando VIEW) =====
        Task<PlayerGameStatsDetailedResponse?> GetPlayerGameStatsDetailedAsync(int playerId, int gameId);
        Task<IEnumerable<PlayerGameStatsDetailedResponse>> GetPlayerRecentGamesDetailedAsync(int playerId, int limit);
        Task<IEnumerable<PlayerGameStatsDetailedResponse>> GetPlayerAllGamesDetailedAsync(int playerId, int page, int pageSize);
        Task<int> GetPlayerGamesCountAsync(int playerId);
        Task<IEnumerable<PlayerGameStatsDetailedResponse>> GetGamePlayerStatsDetailedAsync(int gameId);
        Task AggregateSeasonStatsAsync(int playerId, int season, int seasonTypeId);
    }
}