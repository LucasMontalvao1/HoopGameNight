using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public async Task<IEnumerable<Game>> GetByDateAsync(DateTime date)
        {
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

        public async Task<IEnumerable<Game>> GetByDateRangeAndTeamsAsync(DateTime startDate, DateTime endDate, List<int> teamIds)
        {
            Logger.LogDebug("Getting games between {StartDate} and {EndDate} for {TeamIdsCount} teams",
                startDate.ToShortDateString(), endDate.ToShortDateString(), teamIds.Count);

            if (teamIds == null || !teamIds.Any())
            {
                return await GetByDateRangeAsync(startDate, endDate);
            }

            var sql = await LoadSqlAsync("GetByDateRangeAndTeams");
            var games = await ExecuteQueryWithTeamsAsync(sql, new { StartDate = startDate, EndDate = endDate, TeamIds = teamIds });

            Logger.LogDebug("Retrieved {GameCount} games for specific teams", games.Count());
            return games;
        }

        public async Task<IEnumerable<Game>> GetLiveGamesAsync()
        {
            var sql = "SELECT * FROM Games WHERE Status = 'Live' ORDER BY DateTime";
            return await ExecuteQueryAsync<Game>(sql);
        }

        public async Task<IEnumerable<Game>> GetByTeamAsync(int teamId, DateTime? startDate = null, DateTime? endDate = null)
        {
            Logger.LogDebug("Getting games for team: {TeamId}", teamId);

            var sql = await LoadSqlAsync("GetByTeam");
            var parameters = new
            {
                TeamId = teamId,
                StartDate = startDate,
                EndDate = endDate,
                Offset = 0,
                PageSize = 1000 
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

        public async Task<bool> ExistsAsync(int id)
        {
            var game = await GetByIdAsync(id);
            return game != null;
        }

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
                game.Season,
                game.AiSummary,
                game.AiHighlights,
                game.LineScoreJson,
                game.GameLeadersJson
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
                game.AiSummary,
                game.AiHighlights,
                game.LineScoreJson,
                game.GameLeadersJson,
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
 
        private async Task<IEnumerable<Game>> ExecuteQueryWithTeamsManualAsync(string sql, object? parameters = null)
        {
            using var connection = _connection.CreateConnection();

            try
            {
                var results = await connection.QueryAsync(sql, parameters);
                var games = new List<Game>();
                
                var teamCache = new Dictionary<int, Team>();

                foreach (IDictionary<string, object> row in results)
                {
                    var game = new Game
                    {
                        Id = (int)row["id"],
                        ExternalId = row["external_id"]?.ToString() ?? string.Empty,
                        Date = row["date"] is DateTime dt ? dt : (row["date"] is DateOnly dOnly ? dOnly.ToDateTime(TimeOnly.MinValue) : DateTime.MinValue),
                        DateTime = row.ContainsKey("datetime") && row["datetime"] is DateTime dttm ? dttm : (row.ContainsKey("datetime") && row["datetime"] != null ? Convert.ToDateTime(row["datetime"]) : DateTime.MinValue),
                        HomeTeamId = (int)row["home_team_id"],
                        VisitorTeamId = (int)row["visitor_team_id"],
                        HomeTeamScore = row.ContainsKey("home_team_score") ? row["home_team_score"] as int? : null,
                        VisitorTeamScore = row.ContainsKey("visitor_team_score") ? row["visitor_team_score"] as int? : null,
                        Status = Enum.TryParse<GameStatus>(row["status"]?.ToString(), out GameStatus status) ? status : GameStatus.Scheduled,
                        Period = row.ContainsKey("period") ? row["period"] as int? : null,
                        TimeRemaining = row.ContainsKey("time_remaining") ? row["time_remaining"]?.ToString() : null,
                        PostSeason = row.ContainsKey("postseason") && row["postseason"] != null && (bool)row["postseason"],
                        Season = (int)row["season"],
                        AiSummary = row.ContainsKey("ai_summary") ? row["ai_summary"]?.ToString() : null,
                        AiHighlights = row.ContainsKey("ai_highlights") ? row["ai_highlights"]?.ToString() : null,
                        LineScoreJson = row.ContainsKey("line_score_json") ? row["line_score_json"]?.ToString() : null,
                        GameLeadersJson = row.ContainsKey("game_leaders_json") ? row["game_leaders_json"]?.ToString() : null,
                        CreatedAt = (DateTime)row["created_at"],
                        UpdatedAt = row.ContainsKey("updated_at") && row["updated_at"] != null ? (DateTime)row["updated_at"] : System.DateTime.UtcNow
                    };

                    // Home Team Mapping
                    if (row.ContainsKey("HomeTeam_Id") || row.ContainsKey("id1"))
                    {
                        var homePrefix = row.ContainsKey("HomeTeam_Id") ? "HomeTeam_" : "";
                        var idKey = homePrefix == "" ? "id1" : "HomeTeam_Id";
                        
                        game.HomeTeam = new Team
                        {
                            Id = Convert.ToInt32(row[idKey]),
                            ExternalId = GetValueSafe<int?>(row, homePrefix + "ExternalId", homePrefix + "external_id") ?? 0,
                            Name = GetValueSafe<string>(row, homePrefix + "Name", homePrefix + "name") ?? "",
                            FullName = GetValueSafe<string>(row, homePrefix + "FullName", homePrefix + "full_name") ?? "",
                            Abbreviation = GetValueSafe<string>(row, homePrefix + "Abbreviation", homePrefix + "abbreviation") ?? "",
                            City = GetValueSafe<string>(row, homePrefix + "City", homePrefix + "city") ?? "",
                            Conference = Enum.TryParse<Conference>(GetValueSafe<string>(row, homePrefix + "Conference", homePrefix + "conference"), out Conference homeConf) ? homeConf : Conference.East,
                            Division = GetValueSafe<string>(row, homePrefix + "Division", homePrefix + "division") ?? "",
                            Wins = GetValueSafe<int?>(row, homePrefix + "Wins", homePrefix + "wins", "wins1"),
                            Losses = GetValueSafe<int?>(row, homePrefix + "Losses", homePrefix + "losses", "losses1")
                        };
                    }
                    else
                    {
                        if (!teamCache.TryGetValue(game.HomeTeamId, out var cachedTeam))
                        {
                            cachedTeam = await GetTeamByIdAsync(game.HomeTeamId);
                            if (cachedTeam != null) teamCache[game.HomeTeamId] = cachedTeam;
                        }
                        game.HomeTeam = cachedTeam;
                    }

                    // Visitor Team Mapping
                    if (row.ContainsKey("VisitorTeam_Id") || row.ContainsKey("id2"))
                    {
                        var visitorPrefix = row.ContainsKey("VisitorTeam_Id") ? "VisitorTeam_" : "";
                        var idKey = visitorPrefix == "" ? "id2" : "VisitorTeam_Id";

                        game.VisitorTeam = new Team
                        {
                            Id = Convert.ToInt32(row[idKey]),
                            ExternalId = GetValueSafe<int?>(row, visitorPrefix + "ExternalId", visitorPrefix + "external_id") ?? 0,
                            Name = GetValueSafe<string>(row, visitorPrefix + "Name", visitorPrefix + "name") ?? "",
                            FullName = GetValueSafe<string>(row, visitorPrefix + "FullName", visitorPrefix + "full_name") ?? "",
                            Abbreviation = GetValueSafe<string>(row, visitorPrefix + "Abbreviation", visitorPrefix + "abbreviation") ?? "",
                            City = GetValueSafe<string>(row, visitorPrefix + "City", visitorPrefix + "city") ?? "",
                            Conference = Enum.TryParse<Conference>(GetValueSafe<string>(row, visitorPrefix + "Conference", visitorPrefix + "conference"), out Conference visitorConf) ? visitorConf : Conference.East,
                            Division = GetValueSafe<string>(row, visitorPrefix + "Division", visitorPrefix + "division") ?? "",
                            Wins = GetValueSafe<int?>(row, visitorPrefix + "Wins", visitorPrefix + "wins", "wins1", "wins2"),
                            Losses = GetValueSafe<int?>(row, visitorPrefix + "Losses", visitorPrefix + "losses", "losses1", "losses2")
                        };
                    }
                    else
                    {
                        if (!teamCache.TryGetValue(game.VisitorTeamId, out var cachedTeam))
                        {
                            cachedTeam = await GetTeamByIdAsync(game.VisitorTeamId);
                            if (cachedTeam != null) teamCache[game.VisitorTeamId] = cachedTeam;
                        }
                        game.VisitorTeam = cachedTeam;
                    }

                    games.Add(game);
                }

                Logger.LogDebug("FALLBACK MAPPING completed - {GameCount} games (Cached {TeamCount} teams)", games.Count, teamCache.Count);
                return games;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "CRITICAL ERROR in manual mapping fallback");
                throw;
            }
        }

        private T? GetValueSafe<T>(IDictionary<string, object> row, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (row.ContainsKey(key) && row[key] != null && row[key] != DBNull.Value)
                {
                    try
                    {
                        var value = row[key];
                        var targetType = typeof(T);

                        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
                        {
                            targetType = Nullable.GetUnderlyingType(targetType);
                        }

                        if (targetType == null) return default;

                        return (T)Convert.ChangeType(value, targetType);
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            return default;
        }

        public async Task<IEnumerable<Game>> GetGamesMissingStatsAsync()
        {
            Logger.LogInformation("Identifying games with status 'Final' but missing player statistics");

            var sql = @"
                SELECT g.*, 
                       h.id as id, h.external_id as external_id, h.name as name, h.full_name as full_name, h.abbreviation as abbreviation, h.city as city, h.conference as conference, h.division as division, h.created_at as created_at, h.updated_at as updated_at,
                       v.id as id, v.external_id as external_id, v.name as name, v.full_name as full_name, v.abbreviation as abbreviation, v.city as city, v.conference as conference, v.division as division, v.created_at as created_at, v.updated_at as updated_at
                FROM Games g
                LEFT JOIN Teams h ON g.home_team_id = h.id
                LEFT JOIN Teams v ON g.visitor_team_id = v.id
                LEFT JOIN player_game_stats pgs ON g.id = pgs.game_id
                WHERE g.Status = 'Final' AND pgs.game_id IS NULL
                ORDER BY g.Date DESC";

            var games = await ExecuteQueryWithTeamsAsync(sql);
            
            Logger.LogInformation("Found {Count} games missing statistics", games.Count());
            return games;
        }

        private async Task<Team?> GetTeamByIdAsync(int id)
        {
            var sql = "SELECT * FROM Teams WHERE Id = @Id";
            return await ExecuteQuerySingleOrDefaultAsync<Team>(sql, new { Id = id });
        }
    }
}