using Dapper;
using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.Enums;
using HoopGameNight.Core.Interfaces.Infrastructure;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Models.Entities;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Infrastructure.Repositories
{
    public class GameRepository : BaseRepository<Game>, IGameRepository
    {
        protected override string EntityName => "Games";

        public GameRepository(
            IDatabaseConnection connection,
            ISqlLoader sqlLoader,
            ILogger<GameRepository> logger) : base(connection, sqlLoader, logger)
        {
        }

        public async Task<IEnumerable<Game>> GetTodayGamesAsync()
        {
            Logger.LogDebug("Getting today's games");

            var sql = await LoadSqlAsync("GetTodayGames");
            var games = await ExecuteQueryWithTeamsAsync(sql);

            Logger.LogDebug("Retrieved {GameCount} games for today", games.Count());
            return games;
        }

        public async Task<IEnumerable<Game>> GetGamesByDateAsync(DateTime date)
        {
            Logger.LogDebug("Getting games for date: {Date}", date.ToShortDateString());

            var sql = await LoadSqlAsync("GetByDate");
            var games = await ExecuteQueryWithTeamsAsync(sql, new { Date = date });

            Logger.LogDebug("Retrieved {GameCount} games for {Date}", games.Count(), date.ToShortDateString());
            return games;
        }

        public async Task<(IEnumerable<Game> Games, int TotalCount)> GetGamesAsync(GetGamesRequest request)
        {
            Logger.LogDebug("Getting games with filters: {@Request}", request);

            var sql = await LoadSqlAsync("GetFiltered");
            var countSql = await LoadSqlAsync("GetFilteredCount");

            var parameters = new
            {
                Date = request.Date,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                TeamId = request.TeamId,
                Status = request.Status?.ToString(),
                PostSeason = request.PostSeason,
                Season = request.Season,
                Offset = request.Skip,
                PageSize = request.Take
            };

            var games = await ExecuteQueryAsync<Game>(sql, parameters);
            var totalCount = await ExecuteScalarAsync<int>(countSql, parameters);

            Logger.LogDebug("Retrieved {GameCount} games (total: {TotalCount})", games.Count(), totalCount);
            return (games, totalCount);
        }

        public async Task<(IEnumerable<Game> Games, int TotalCount)> GetGamesByTeamAsync(int teamId, int page, int pageSize)
        {
            Logger.LogDebug("Getting games for team: {TeamId}", teamId);

            var sql = await LoadSqlAsync("GetByTeam");
            var countSql = await LoadSqlAsync("GetByTeamCount");

            var parameters = new
            {
                TeamId = teamId,
                Offset = (page - 1) * pageSize,
                PageSize = pageSize
            };

            var games = await ExecuteQueryWithTeamsAsync(sql, parameters);
            var totalCount = await ExecuteScalarAsync<int>(countSql, new { TeamId = teamId });

            Logger.LogDebug("Retrieved {GameCount} games for team {TeamId}", games.Count(), teamId);
            return (games, totalCount);
        }

        public async Task<Game?> GetByIdAsync(int id)
        {
            Logger.LogDebug("Getting game by ID: {GameId}", id);

            var sql = await LoadSqlAsync("GetById");
            var games = await ExecuteQueryWithTeamsAsync(sql, new { Id = id });
            var game = games.FirstOrDefault();

            Logger.LogDebug("Game {Found} with ID: {GameId}", game != null ? "found" : "not found", id);
            return game;
        }

        public async Task<Game?> GetByExternalIdAsync(int externalId)
        {
            Logger.LogDebug("Getting game by external ID: {ExternalId}", externalId);

            var sql = await LoadSqlAsync("GetByExternalId");
            var game = await ExecuteQuerySingleOrDefaultAsync<Game>(sql, new { ExternalId = externalId });

            Logger.LogDebug("Game {Found} with external ID: {ExternalId}", game != null ? "found" : "not found", externalId);
            return game;
        }

        public async Task<IEnumerable<Game>> GetAllAsync()
        {
            Logger.LogDebug("Getting all games");

            var sql = await LoadSqlAsync("GetAll");
            var games = await ExecuteQueryWithTeamsAsync(sql);

            Logger.LogDebug("Retrieved {GameCount} games", games.Count());
            return games;
        }

        // Método helper para executar queries com mapeamento de times
        private async Task<IEnumerable<Game>> ExecuteQueryWithTeamsAsync(string sql, object? parameters = null)
        {
            using var connection = _connection.CreateConnection();

            Logger.LogInformation("🔍 EXECUTING SQL: {Sql}", sql);
            Logger.LogInformation("🔍 PARAMETERS: {@Parameters}", parameters);

            try
            {
                var results = await connection.QueryAsync(sql, parameters);
                var games = new List<Game>();

                foreach (dynamic row in results)
                {
                    var game = new Game
                    {
                        Id = row.id,
                        ExternalId = row.external_id,
                        Date = row.date,
                        DateTime = row.datetime,
                        HomeTeamId = row.home_team_id,
                        VisitorTeamId = row.visitor_team_id,
                        HomeTeamScore = row.home_team_score,
                        VisitorTeamScore = row.visitor_team_score,
                        Status = Enum.Parse<GameStatus>(row.status ?? "Scheduled"),
                        Period = row.period,
                        TimeRemaining = row.time_remaining,
                        PostSeason = row.postseason,
                        Season = row.season,
                        CreatedAt = row.created_at,
                        UpdatedAt = row.updated_at
                    };

                    if (row.HomeTeam_Id != null && row.HomeTeam_Id > 0)
                    {
                        game.HomeTeam = new Team
                        {
                            Id = row.HomeTeam_Id,
                            ExternalId = row.HomeTeam_ExternalId ?? 0,
                            Name = row.HomeTeam_Name ?? "",
                            FullName = row.HomeTeam_FullName ?? "",
                            Abbreviation = row.HomeTeam_Abbreviation ?? "",
                            City = row.HomeTeam_City ?? "",
                            Conference = Enum.Parse<Conference>(row.HomeTeam_Conference ?? "East"),
                            Division = row.HomeTeam_Division ?? "",
                            CreatedAt = row.HomeTeam_CreatedAt,
                            UpdatedAt = row.HomeTeam_UpdatedAt
                        };
                    }

                    if (row.VisitorTeam_Id != null && row.VisitorTeam_Id > 0)
                    {
                        game.VisitorTeam = new Team
                        {
                            Id = row.VisitorTeam_Id,
                            ExternalId = row.VisitorTeam_ExternalId ?? 0,
                            Name = row.VisitorTeam_Name ?? "",
                            FullName = row.VisitorTeam_FullName ?? "",
                            Abbreviation = row.VisitorTeam_Abbreviation ?? "",
                            City = row.VisitorTeam_City ?? "",
                            Conference = Enum.Parse<Conference>(row.VisitorTeam_Conference ?? "East"),
                            Division = row.VisitorTeam_Division ?? "",
                            CreatedAt = row.VisitorTeam_CreatedAt,
                            UpdatedAt = row.VisitorTeam_UpdatedAt
                        };
                    }

                    Logger.LogInformation("🏀 MANUAL MAPPING -> Game: {GameId} | HomeTeam: {HomeTeamId}-{HomeTeamName} | VisitorTeam: {VisitorTeamId}-{VisitorTeamName}",
                        game.Id, game.HomeTeam?.Id, game.HomeTeam?.Name, game.VisitorTeam?.Id, game.VisitorTeam?.Name);

                    games.Add(game);
                }

                Logger.LogInformation("✅ SUCCESSFULLY MAPPED {GameCount} games with manual mapping", games.Count);
                return games;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "❌ ERROR in ExecuteQueryWithTeamsAsync");
                throw;
            }
        }

        public async Task<int> InsertAsync(Game game)
        {
            Logger.LogDebug("Inserting game: {GameTitle}", game.GameTitle);

            var sql = await LoadSqlAsync("Insert");
            var id = await ExecuteScalarAsync<int>(sql, new
            {
                game.ExternalId,
                game.Date,
                DateTime = game.DateTime,
                game.HomeTeamId,
                game.VisitorTeamId,
                game.HomeTeamScore,
                game.VisitorTeamScore,
                Status = game.Status.ToString(),
                game.Period,
                game.TimeRemaining,
                game.PostSeason,
                game.Season
            });

            game.Id = id;
            Logger.LogInformation("Game inserted with ID: {GameId}", id);
            return id;
        }

        public async Task<bool> UpdateAsync(Game game)
        {
            Logger.LogDebug("Updating game: {GameId}", game.Id);

            game.UpdateTimestamp();

            var sql = await LoadSqlAsync("Update");
            var rowsAffected = await ExecuteAsync(sql, new
            {
                game.Id,
                game.HomeTeamScore,
                game.VisitorTeamScore,
                Status = game.Status.ToString(),
                game.Period,
                game.TimeRemaining,
                game.UpdatedAt
            });

            var updated = rowsAffected > 0;
            Logger.LogDebug("Game {Updated}: {GameId}", updated ? "updated" : "not updated", game.Id);
            return updated;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            Logger.LogDebug("Deleting game: {GameId}", id);

            var sql = await LoadSqlAsync("Delete");
            var rowsAffected = await ExecuteAsync(sql, new { Id = id });

            var deleted = rowsAffected > 0;
            Logger.LogDebug("Game {Deleted}: {GameId}", deleted ? "deleted" : "not deleted", id);
            return deleted;
        }

        public async Task<bool> ExistsAsync(int externalId)
        {
            Logger.LogDebug("Checking if game exists with external ID: {ExternalId}", externalId);

            var sql = await LoadSqlAsync("Exists");
            var count = await ExecuteScalarAsync<int>(sql, new { ExternalId = externalId });

            var exists = count > 0;
            Logger.LogDebug("Game {Exists} with external ID: {ExternalId}", exists ? "exists" : "does not exist", externalId);
            return exists;
        }
    }
}