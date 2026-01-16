using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using HoopGameNight.Core.Interfaces.Infrastructure;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Models.Entities;
using Microsoft.Extensions.Logging;
using System.Data;

namespace HoopGameNight.Infrastructure.Repositories
{
    public class PlayerStatsRepository : BaseRepository<PlayerSeasonStats>, IPlayerStatsRepository
    {
        protected override string EntityName => "PlayerStats";

        private readonly IDatabaseConnection _connection;
        private readonly ILogger<PlayerStatsRepository> _logger;

        public PlayerStatsRepository(
            IDatabaseConnection connection,
            ISqlLoader sqlLoader,
            ILogger<PlayerStatsRepository> logger) : base(connection, sqlLoader, logger)
        {
            _connection = connection;
            _logger = logger;
        }

        private IDbConnection GetConnection() => _connection.CreateConnection();

        public async Task<PlayerSeasonStats?> GetSeasonStatsAsync(int playerId, int season)
        {
            var sql = await LoadSqlAsync("GetSeasonStats");
            return await ExecuteQuerySingleOrDefaultAsync<PlayerSeasonStats>(sql, new { PlayerId = playerId, Season = season });
        }

        // ===== NOVO: Buscar season stats da VIEW calculada =====
        public async Task<Core.DTOs.Response.PlayerSeasonStatsResponse?> GetSeasonStatsFromViewAsync(int playerId, int season)
        {
            var sql = await LoadSqlAsync("GetSeasonStatsFromView");
            return await ExecuteQuerySingleOrDefaultAsync<Core.DTOs.Response.PlayerSeasonStatsResponse>(sql, new { PlayerId = playerId, Season = season });
        }

        public async Task<IEnumerable<PlayerSeasonStats>> GetAllSeasonStatsAsync(int playerId)
        {
            var sql = await LoadSqlAsync("GetAllSeasonStats");
            return await ExecuteQueryAsync<PlayerSeasonStats>(sql, new { PlayerId = playerId });
        }

        public async Task UpsertSeasonStatsAsync(PlayerSeasonStats seasonStats)
        {
            var sql = await LoadSqlAsync("UpsertSeasonStats");
            await ExecuteAsync(sql, seasonStats);
        }

        public async Task<PlayerCareerStats?> GetCareerStatsAsync(int playerId)
        {
            var sql = await LoadSqlAsync("GetCareerStats");
            return await ExecuteQuerySingleOrDefaultAsync<PlayerCareerStats>(sql, new { PlayerId = playerId });
        }

        public async Task<bool> UpsertCareerStatsAsync(PlayerCareerStats careerStats)
        {
            var sql = await LoadSqlAsync("UpsertCareerStats");
            var result = await ExecuteScalarAsync<int>(sql, careerStats);
            return result > 0;
        }

        public async Task<PlayerGameStats?> GetGameStatsAsync(int playerId, int gameId)
        {
            var sql = await LoadSqlAsync("GetGameStats");
            return await ExecuteQuerySingleOrDefaultAsync<PlayerGameStats>(sql, new { PlayerId = playerId, GameId = gameId });
        }

        public async Task<IEnumerable<PlayerGameStats>> GetRecentGamesAsync(int playerId, int limit)
        {
            var sql = (await LoadSqlAsync("GetRecentGames")).Replace("@Limit", limit.ToString());
            return await ExecuteQueryAsync<PlayerGameStats>(sql, new { PlayerId = playerId });
        }

        public async Task<IEnumerable<PlayerGameStats>> GetAllPlayerGamesAsync(int playerId)
        {
            var sql = await LoadSqlAsync("GetAllPlayerGames");
            return await ExecuteQueryAsync<PlayerGameStats>(sql, new { PlayerId = playerId });
        }

        public async Task UpsertGameStatsAsync(PlayerGameStats gameStats)
        {
            var sql = await LoadSqlAsync("UpsertGameStats");
            await ExecuteAsync(sql, gameStats);
        }

        public async Task<IEnumerable<dynamic>> GetScoringLeadersAsync(int season, int minGames, int limit)
        {
            var sql = (await LoadSqlAsync("GetScoringLeaders")).Replace("@Limit", limit.ToString());
            return await ExecuteQueryAsync<dynamic>(sql, new { Season = season, MinGames = minGames });
        }

        public async Task<IEnumerable<dynamic>> GetReboundLeadersAsync(int season, int minGames, int limit)
        {
            var sql = (await LoadSqlAsync("GetReboundLeaders")).Replace("@Limit", limit.ToString());
            return await ExecuteQueryAsync<dynamic>(sql, new { Season = season, MinGames = minGames });
        }

        public async Task<IEnumerable<dynamic>> GetAssistLeadersAsync(int season, int minGames, int limit)
        {
            var sql = (await LoadSqlAsync("GetAssistLeaders")).Replace("@Limit", limit.ToString());
            return await ExecuteQueryAsync<dynamic>(sql, new { Season = season, MinGames = minGames });
        }

        public async Task<bool> BulkUpsertSeasonStatsAsync(IEnumerable<PlayerSeasonStats> seasonStats)
        {
            if (!seasonStats.Any())
                return false;

            using var connection = GetConnection();
            using var transaction = connection.BeginTransaction();

            try
            {
                foreach (var stats in seasonStats)
                {
                    var sql = await LoadSqlAsync("UpsertSeasonStats");
                    await connection.ExecuteAsync(sql, stats, transaction);
                }

                transaction.Commit();
                return true;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Error during bulk upsert of season stats");
                return false;
            }
        }

        public async Task<bool> BulkUpsertGameStatsAsync(IEnumerable<PlayerGameStats> gameStats)
        {
            if (!gameStats.Any())
                return false;

            using var connection = GetConnection();
            using var transaction = connection.BeginTransaction();

            try
            {
                foreach (var stats in gameStats)
                {
                    var sql = await LoadSqlAsync("UpsertGameStats");
                    await connection.ExecuteAsync(sql, stats, transaction);
                }

                transaction.Commit();
                return true;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Error during bulk upsert of game stats");
                return false;
            }
        }

        public async Task<DateTime?> GetLastSyncDateForPlayerAsync(int playerId)
        {
            var sql = await LoadSqlAsync("GetLastSyncDateForPlayer");
            return await ExecuteScalarAsync<DateTime?>(sql, new { PlayerId = playerId });
        }

        public async Task<bool> UpdateLastSyncDateAsync(int playerId, DateTime syncDate)
        {
            var sql = await LoadSqlAsync("UpdateLastSyncDate");
            var result = await ExecuteAsync(sql, new { PlayerId = playerId, SyncDate = syncDate });
            return result > 0;
        }

        // ===== NOVOS MÉTODOS: Estatísticas Detalhadas por Jogo (usando VIEW) =====

        public async Task<Core.DTOs.Response.PlayerGameStatsDetailedResponse?> GetPlayerGameStatsDetailedAsync(int playerId, int gameId)
        {
            var sql = await LoadSqlAsync("GetPlayerGameStatsDetailed");
            return await ExecuteQuerySingleOrDefaultAsync<Core.DTOs.Response.PlayerGameStatsDetailedResponse>(sql, new { PlayerId = playerId, GameId = gameId });
        }

        public async Task<IEnumerable<Core.DTOs.Response.PlayerGameStatsDetailedResponse>> GetPlayerRecentGamesDetailedAsync(int playerId, int limit)
        {
            var sql = await LoadSqlAsync("GetPlayerRecentGamesDetailed");
            return await ExecuteQueryAsync<Core.DTOs.Response.PlayerGameStatsDetailedResponse>(sql, new { PlayerId = playerId, Limit = limit });
        }

        public async Task<IEnumerable<Core.DTOs.Response.PlayerGameStatsDetailedResponse>> GetPlayerAllGamesDetailedAsync(int playerId, int page, int pageSize)
        {
            var offset = (page - 1) * pageSize;
            var sql = await LoadSqlAsync("GetPlayerAllGamesDetailed");
            return await ExecuteQueryAsync<Core.DTOs.Response.PlayerGameStatsDetailedResponse>(sql, new { PlayerId = playerId, PageSize = pageSize, Offset = offset });
        }

        public async Task<int> GetPlayerGamesCountAsync(int playerId)
        {
            var sql = await LoadSqlAsync("GetPlayerAllGamesCount");
            return await ExecuteScalarAsync<int>(sql, new { PlayerId = playerId });
        }

        public async Task<IEnumerable<Core.DTOs.Response.PlayerGameStatsDetailedResponse>> GetGamePlayerStatsDetailedAsync(int gameId)
        {
            var sql = await LoadSqlAsync("GetGamePlayerStatsDetailed");
            return await ExecuteQueryAsync<Core.DTOs.Response.PlayerGameStatsDetailedResponse>(sql, new { GameId = gameId });
        }

        public async Task AggregateSeasonStatsAsync(int playerId, int season, int seasonTypeId)
        {
            var sql = await LoadSqlAsync("AggregateSeasonStats");
            await ExecuteAsync(sql, new { PlayerId = playerId, Season = season, SeasonTypeId = seasonTypeId });
        }
    }
}
