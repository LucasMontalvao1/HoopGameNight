using Dapper;
using HoopGameNight.Core.Interfaces.Infrastructure;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Models.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HoopGameNight.Infrastructure.Repositories
{
    public class GamePlayRepository : BaseRepository<GamePlay>, IGamePlayRepository
    {
        protected override string EntityName => "Games"; // Using Games folder for SQL if needed, or ad-hoc

        public GamePlayRepository(
            IDatabaseConnection connection,
            ISqlLoader sqlLoader,
            ILogger<GamePlayRepository> logger) : base(connection, sqlLoader, logger)
        {
        }

        public async Task<IEnumerable<GamePlay>> GetByGameIdAsync(int gameId)
        {
            const string sql = "SELECT * FROM game_plays WHERE game_id = @GameId ORDER BY sequence ASC";
            return await ExecuteQueryAsync<GamePlay>(sql, new { GameId = gameId });
        }

        public async Task<bool> SavePlaysAsync(int gameId, IEnumerable<GamePlay> plays)
        {
            return await ExecuteInTransactionAsync(async (conn, trans) =>
            {
                // Delete existing plays for this game to avoid duplicates on resync
                const string deleteSql = "DELETE FROM game_plays WHERE game_id = @GameId";
                await conn.ExecuteAsync(deleteSql, new { GameId = gameId }, trans);

                // Insert new plays
                const string insertSql = @"
                    INSERT INTO game_plays (
                        game_id, external_id, sequence, period, clock, text, type, 
                        team_id, player_id, score_value, home_score, away_score
                    ) VALUES (
                        @GameId, @ExternalId, @Sequence, @Period, @Clock, @Text, @Type, 
                        @TeamId, @PlayerId, @ScoreValue, @HomeScore, @AwayScore
                    )";

                foreach (var play in plays)
                {
                    await conn.ExecuteAsync(insertSql, play, trans);
                }

                return true;
            });
        }

        public async Task<bool> DeleteByGameIdAsync(int gameId)
        {
            const string sql = "DELETE FROM game_plays WHERE game_id = @GameId";
            var result = await ExecuteAsync(sql, new { GameId = gameId });
            return result >= 0;
        }

        public Task<GamePlay?> GetByIdAsync(int id) => throw new NotImplementedException();
        public Task<IEnumerable<GamePlay>> GetAllAsync() => throw new NotImplementedException();
        public Task<int> InsertAsync(GamePlay entity) => throw new NotImplementedException();
        public Task<bool> UpdateAsync(GamePlay entity) => throw new NotImplementedException();
        public Task<bool> DeleteAsync(int id) => throw new NotImplementedException();
        public Task<bool> ExistsAsync(int id) => throw new NotImplementedException();
    }
}
