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

        // Métodos básicos de consulta
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

        public async Task<IEnumerable<Game>> GetByDateAsync(DateTime date)
        {
            // Redireciona para o método principal
            return await GetGamesByDateAsync(date);
        }

        public async Task<IEnumerable<Game>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            Logger.LogDebug("Getting games between {StartDate} and {EndDate}",
                startDate.ToShortDateString(), endDate.ToShortDateString());

            var sql = await LoadSqlAsync("GetByDateRange");
            var games = await ExecuteQueryWithTeamsAsync(sql, new { StartDate = startDate, EndDate = endDate });

            Logger.LogDebug("Retrieved {GameCount} games between {StartDate} and {EndDate}",
                games.Count(), startDate.ToShortDateString(), endDate.ToShortDateString());
            return games;
        }

        public async Task<IEnumerable<Game>> GetLiveGamesAsync()
        {
            var sql = "SELECT * FROM Games WHERE Status = 'Live' ORDER BY DateTime";
            return await ExecuteQueryAsync<Game>(sql);
        }

        // Métodos de consulta por time
        public async Task<IEnumerable<Game>> GetByTeamAsync(int teamId, DateTime? startDate = null, DateTime? endDate = null)
        {
            Logger.LogDebug("Getting games for team: {TeamId}", teamId);

            var sql = await LoadSqlAsync("GetByTeam");
            var parameters = new
            {
                TeamId = teamId,
                StartDate = startDate,
                EndDate = endDate
            };

            var games = await ExecuteQueryWithTeamsAsync(sql, parameters);

            Logger.LogDebug("Retrieved {GameCount} games for team {TeamId}", games.Count(), teamId);
            return games;
        }

        public async Task<(IEnumerable<Game> Games, int TotalCount)> GetGamesByTeamAsync(int teamId, int page, int pageSize)
        {
            Logger.LogDebug("Getting games for team: {TeamId} with pagination", teamId);

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

        // Métodos de consulta com filtros
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

        // Métodos por ID externo
        public async Task<Game?> GetByExternalIdAsync(string externalId)
        {
            Logger.LogDebug("Getting game by external ID: {ExternalId}", externalId);

            var sql = await LoadSqlAsync("GetByExternalId");
            var games = await ExecuteQueryWithTeamsAsync(sql, new { ExternalId = externalId });
            var game = games.FirstOrDefault();

            Logger.LogDebug("Game {Found} with external ID: {ExternalId}", game != null ? "found" : "not found", externalId);
            return game;
        }

        public async Task<bool> ExistsByExternalIdAsync(string externalId)
        {
            Logger.LogDebug("Checking if game exists with external ID: {ExternalId}", externalId);

            var sql = await LoadSqlAsync("Exists");
            var count = await ExecuteScalarAsync<int>(sql, new { ExternalId = externalId });

            var exists = count > 0;
            Logger.LogDebug("Game {Exists} with external ID: {ExternalId}", exists ? "exists" : "does not exist", externalId);
            return exists;
        }

        // Implementação do IBaseRepository (verifica por ID do banco)
        public async Task<bool> ExistsAsync(int id)
        {
            var game = await GetByIdAsync(id);
            return game != null;
        }

        // Métodos de atualização específicos
        public async Task<bool> UpdateScoreAsync(int gameId, int homeScore, int visitorScore)
        {
            var sql = @"UPDATE Games 
                       SET HomeTeamScore = @HomeScore, 
                           VisitorTeamScore = @VisitorScore, 
                           UpdatedAt = NOW() 
                       WHERE Id = @GameId";

            var result = await ExecuteAsync(sql, new
            {
                GameId = gameId,
                HomeScore = homeScore,
                VisitorScore = visitorScore
            });

            return result > 0;
        }

        // Métodos herdados do BaseRepository
        public async Task<Game?> GetByIdAsync(int id)
        {
            Logger.LogDebug("Getting game by ID: {GameId}", id);

            var sql = await LoadSqlAsync("GetById");
            var games = await ExecuteQueryWithTeamsAsync(sql, new { Id = id });
            var game = games.FirstOrDefault();

            Logger.LogDebug("Game {Found} with ID: {GameId}", game != null ? "found" : "not found", id);
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

        private async Task<IEnumerable<Game>> ExecuteQueryWithTeamsAsync(string sql, object? parameters = null)
        {
            using var connection = _connection.CreateConnection();

            Logger.LogDebug("EXECUTING SQL: {Sql}", sql);
            Logger.LogDebug("PARAMETERS: {@Parameters}", parameters);

            try
            {
                var games = await connection.QueryAsync<Game, Team, Team, Game>(
                    sql,
                    (game, homeTeam, visitorTeam) =>
                    {
                        game.HomeTeam = homeTeam?.Id > 0 ? homeTeam : null;
                        game.VisitorTeam = visitorTeam?.Id > 0 ? visitorTeam : null;

                        return game;
                    },
                    parameters,
                    splitOn: "id,id"  
                );

                var gamesList = games.ToList();
                Logger.LogDebug("SUCCESSFULLY MAPPED {GameCount} games with Dapper multi-mapping", gamesList.Count);

                return gamesList;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "ERROR in ExecuteQueryWithTeamsAsync - SQL: {Sql}", sql);

                Logger.LogWarning("FALLING BACK to manual mapping due to error");
                return await ExecuteQueryWithTeamsManualAsync(sql, parameters);
            }
        }

        // Método de fallback com mapeamento manual 
        private async Task<IEnumerable<Game>> ExecuteQueryWithTeamsManualAsync(string sql, object? parameters = null)
        {
            using var connection = _connection.CreateConnection();

            try
            {
                var results = await connection.QueryAsync(sql, parameters);
                var games = new List<Game>();

                foreach (IDictionary<string, object> row in results)
                {
                    var game = new Game
                    {
                        Id = (int)row["id"],
                        ExternalId = row["external_id"]?.ToString() ?? string.Empty,
                        Date = row["date"] is DateOnly dateOnly ? dateOnly.ToDateTime(TimeOnly.MinValue) : (DateTime)row["date"],
                        DateTime = row.ContainsKey("datetime") && row["datetime"] != null ? (DateTime)row["datetime"] : System.DateTime.UtcNow,
                        HomeTeamId = (int)row["home_team_id"],
                        VisitorTeamId = (int)row["visitor_team_id"],
                        HomeTeamScore = row.ContainsKey("home_team_score") ? row["home_team_score"] as int? : null,
                        VisitorTeamScore = row.ContainsKey("visitor_team_score") ? row["visitor_team_score"] as int? : null,
                        Status = Enum.TryParse<GameStatus>(row["status"]?.ToString(), out GameStatus status) ? status : GameStatus.Scheduled,
                        Period = row.ContainsKey("period") ? row["period"] as int? : null,
                        TimeRemaining = row.ContainsKey("time_remaining") ? row["time_remaining"]?.ToString() : null,
                        PostSeason = row.ContainsKey("postseason") && row["postseason"] != null && (bool)row["postseason"],
                        Season = (int)row["season"],
                        CreatedAt = (DateTime)row["created_at"],
                        UpdatedAt = row.ContainsKey("updated_at") && row["updated_at"] != null ? (DateTime)row["updated_at"] : System.DateTime.UtcNow
                    };

                    if (row.ContainsKey("HomeTeam_Id") && row["HomeTeam_Id"] != null && (int)row["HomeTeam_Id"] > 0)
                    {
                        game.HomeTeam = new Team
                        {
                            Id = (int)row["HomeTeam_Id"],
                            ExternalId = row["HomeTeam_ExternalId"] as int? ?? 0,
                            Name = row["HomeTeam_Name"]?.ToString() ?? "",
                            FullName = row["HomeTeam_FullName"]?.ToString() ?? "",
                            Abbreviation = row["HomeTeam_Abbreviation"]?.ToString() ?? "",
                            City = row["HomeTeam_City"]?.ToString() ?? "",
                            Conference = Enum.TryParse<Conference>(row["HomeTeam_Conference"]?.ToString(), out Conference homeConf) ? homeConf : Conference.East,
                            Division = row["HomeTeam_Division"]?.ToString() ?? "",
                            CreatedAt = row.ContainsKey("HomeTeam_CreatedAt") && row["HomeTeam_CreatedAt"] != null ? (DateTime)row["HomeTeam_CreatedAt"] : System.DateTime.UtcNow,
                            UpdatedAt = row.ContainsKey("HomeTeam_UpdatedAt") && row["HomeTeam_UpdatedAt"] != null ? (DateTime)row["HomeTeam_UpdatedAt"] : System.DateTime.UtcNow
                        };
                    }
                    else
                    {
                        game.HomeTeam = await GetTeamByIdAsync(game.HomeTeamId);
                    }

                    if (row.ContainsKey("VisitorTeam_Id") && row["VisitorTeam_Id"] != null && (int)row["VisitorTeam_Id"] > 0)
                    {
                        game.VisitorTeam = new Team
                        {
                            Id = (int)row["VisitorTeam_Id"],
                            ExternalId = row["VisitorTeam_ExternalId"] as int? ?? 0,
                            Name = row["VisitorTeam_Name"]?.ToString() ?? "",
                            FullName = row["VisitorTeam_FullName"]?.ToString() ?? "",
                            Abbreviation = row["VisitorTeam_Abbreviation"]?.ToString() ?? "",
                            City = row["VisitorTeam_City"]?.ToString() ?? "",
                            Conference = Enum.TryParse<Conference>(row["VisitorTeam_Conference"]?.ToString(), out Conference visitorConf) ? visitorConf : Conference.East,
                            Division = row["VisitorTeam_Division"]?.ToString() ?? "",
                            CreatedAt = row.ContainsKey("VisitorTeam_CreatedAt") && row["VisitorTeam_CreatedAt"] != null ? (DateTime)row["VisitorTeam_CreatedAt"] : System.DateTime.UtcNow,
                            UpdatedAt = row.ContainsKey("VisitorTeam_UpdatedAt") && row["VisitorTeam_UpdatedAt"] != null ? (DateTime)row["VisitorTeam_UpdatedAt"] : System.DateTime.UtcNow
                        };
                    }
                    else
                    {
                        game.VisitorTeam = await GetTeamByIdAsync(game.VisitorTeamId);
                    }

                    games.Add(game);
                }

                Logger.LogDebug("FALLBACK MAPPING completed - {GameCount} games", games.Count);
                return games;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "CRITICAL ERROR in manual mapping fallback");
                throw;
            }
        }

        // Helper para buscar time por ID 
        private async Task<Team?> GetTeamByIdAsync(int teamId)
        {
            using var connection = _connection.CreateConnection();
            var sql = "SELECT * FROM teams WHERE id = @Id";
            return await connection.QueryFirstOrDefaultAsync<Team>(sql, new { Id = teamId });
        }
    }
}