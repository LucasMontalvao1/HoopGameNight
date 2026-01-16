using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.Interfaces.Infrastructure;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Models.Entities;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Infrastructure.Repositories
{
    public class PlayerRepository : BaseRepository<Player>, IPlayerRepository
    {
        protected override string EntityName => "Players";

        public PlayerRepository(
            IDatabaseConnection connection,
            ISqlLoader sqlLoader,
            ILogger<PlayerRepository> logger) : base(connection, sqlLoader, logger)
        {
        }

        public async Task<(IEnumerable<Player> Players, int TotalCount)> SearchPlayersAsync(SearchPlayerRequest request)
        {
            Logger.LogDebug("Searching players with criteria: {@Request}", request);

            var sql = await LoadSqlAsync("SearchByName");
            var countSql = await LoadSqlAsync("SearchByNameCount");

            var parameters = new
            {
                Search = request.Search,
                TeamId = request.TeamId,
                Position = request.Position,
                Offset = request.Skip,
                PageSize = request.Take
            };

            var players = await ExecuteQueryWithTeamAsync(sql, parameters);
            var totalCount = await ExecuteScalarAsync<int>(countSql, parameters);

            Logger.LogDebug("Found {PlayerCount} players (total: {TotalCount})", players.Count(), totalCount);
            return (players, totalCount);
        }

        private async Task<IEnumerable<Player>> ExecuteQueryWithTeamAsync(string sql, object? parameters = null)
        {
            var players = await ExecuteQueryAsync<Player>(sql, parameters);
            var playersList = players.ToList();

            // Get unique team IDs
            var teamIds = playersList.Where(p => p.TeamId.HasValue).Select(p => p.TeamId!.Value).Distinct().ToList();

            if (teamIds.Any())
            {
                // Fetch teams
                var teamSql = "SELECT id, external_id, name, full_name, abbreviation, city, conference, division FROM teams WHERE id IN @TeamIds";
                var teams = await ExecuteQueryAsync<Team>(teamSql, new { TeamIds = teamIds });
                var teamDict = teams.ToDictionary(t => t.Id);

                // Map teams to players
                foreach (var player in playersList)
                {
                    if (player.TeamId.HasValue && teamDict.TryGetValue(player.TeamId.Value, out var team))
                    {
                        player.Team = team;
                    }
                }
            }

            return playersList;
        }

        public async Task<(IEnumerable<Player> Players, int TotalCount)> GetPlayersByTeamAsync(int teamId, int page, int pageSize)
        {
            Logger.LogDebug("Getting players for team: {TeamId}", teamId);

            var sql = await LoadSqlAsync("GetByTeamId");
            var countSql = await LoadSqlAsync("GetByTeamIdCount");

            var parameters = new
            {
                TeamId = teamId,
                Offset = (page - 1) * pageSize,
                PageSize = pageSize
            };

            var players = await ExecuteQueryWithTeamAsync(sql, parameters);
            var totalCount = await ExecuteScalarAsync<int>(countSql, new { TeamId = teamId });

            Logger.LogDebug("Found {PlayerCount} players for team {TeamId}", players.Count(), teamId);
            return (players, totalCount);
        }

        public async Task<(IEnumerable<Player> Players, int TotalCount)> GetAllPlayersAsync(int page, int pageSize)
        {
            Logger.LogDebug("Getting all players - Page: {Page}, PageSize: {PageSize}", page, pageSize);

            var sql = await LoadSqlAsync("GetAllPaginated");
            var countSql = await LoadSqlAsync("GetAllCount");

            var parameters = new
            {
                Offset = (page - 1) * pageSize,
                PageSize = pageSize
            };

            var players = await ExecuteQueryWithTeamAsync(sql, parameters);
            var totalCount = await ExecuteScalarAsync<int>(countSql);

            Logger.LogDebug("Found {PlayerCount} players (total: {TotalCount})", players.Count(), totalCount);
            return (players, totalCount);
        }

        public async Task<Player?> GetByIdAsync(int id)
        {
            Logger.LogDebug("Getting player by ID: {PlayerId}", id);

            var sql = await LoadSqlAsync("GetById");
            var players = await ExecuteQueryWithTeamAsync(sql, new { Id = id });
            var player = players.FirstOrDefault();

            Logger.LogDebug("Player {Found} with ID: {PlayerId}", player != null ? "found" : "not found", id);
            return player;
        }

        public async Task<Player?> GetByExternalIdAsync(int externalId)
        {
            Logger.LogDebug("Getting player by external ID: {ExternalId}", externalId);

            var sql = await LoadSqlAsync("GetByExternalId");
            var player = await ExecuteQuerySingleOrDefaultAsync<Player>(sql, new { ExternalId = externalId });

            Logger.LogDebug("Player {Found} with external ID: {ExternalId}", player != null ? "found" : "not found", externalId);
            return player;
        }

        public async Task<IEnumerable<Player>> GetAllAsync()
        {
            Logger.LogDebug("Getting all players");

            var sql = await LoadSqlAsync("GetAll");
            var players = await ExecuteQueryAsync<Player>(sql);

            Logger.LogDebug("Retrieved {PlayerCount} players", players.Count());
            return players;
        }

        public async Task<bool> ExistsAsync(int externalId)
        {
            Logger.LogDebug("Checking if player exists with external ID: {ExternalId}", externalId);

            var sql = await LoadSqlAsync("Exists");
            var count = await ExecuteScalarAsync<int>(sql, new { ExternalId = externalId });

            var exists = count > 0;
            Logger.LogDebug("Player {Exists} with external ID: {ExternalId}", exists ? "exists" : "does not exist", externalId);
            return exists;
        }

        public async Task<IEnumerable<Player>> GetByTeamIdAsync(int teamId)
        {
            Logger.LogDebug("Getting players by team ID: {TeamId}", teamId);

            var sql = await LoadSqlAsync("GetByTeamId");
            var players = await ExecuteQueryAsync<Player>(sql, new { TeamId = teamId });

            Logger.LogDebug("Found {PlayerCount} players for team {TeamId}", players.Count(), teamId);
            return players;
        }

        public async Task<IEnumerable<Player>> GetActivePlayersAsync()
        {
            Logger.LogDebug("Getting active players");

            var sql = await LoadSqlAsync("GetActivePlayers");
            var players = await ExecuteQueryAsync<Player>(sql);

            Logger.LogDebug("Found {PlayerCount} active players", players.Count());
            return players;
        }

        // NOVO: Implementação do método SearchAsync da interface
        public async Task<IEnumerable<Player>> SearchAsync(string searchTerm)
        {
            Logger.LogDebug("Searching players with term: {SearchTerm}", searchTerm);

            var sql = await LoadSqlAsync("Search");
            var players = await ExecuteQueryAsync<Player>(sql, new { SearchTerm = $"%{searchTerm}%" });

            Logger.LogDebug("Found {PlayerCount} players matching '{SearchTerm}'", players.Count(), searchTerm);
            return players;
        }

        public async Task<bool> UpdateTeamAsync(int playerId, int? teamId)
        {
            Logger.LogDebug("Updating team for player: {PlayerId} to team: {TeamId}", playerId, teamId);

            var sql = "UPDATE Players SET TeamId = @TeamId, UpdatedAt = NOW() WHERE Id = @PlayerId";
            var result = await ExecuteAsync(sql, new { PlayerId = playerId, TeamId = teamId });

            var updated = result > 0;
            Logger.LogDebug("Player team {Updated}: {PlayerId}", updated ? "updated" : "not updated", playerId);
            return updated;
        }

        public async Task<int> InsertAsync(Player player)
        {
            Logger.LogDebug("Inserting player: {PlayerName}", player.FullName);

            var sql = await LoadSqlAsync("Insert");
            var id = await ExecuteScalarAsync<int>(sql, new
            {
                player.ExternalId,
                player.FirstName,
                player.LastName,
                Position = player.Position?.ToString(),
                player.HeightFeet,
                player.HeightInches,
                player.WeightPounds,
                player.TeamId,
                player.NbaStatsId,
                player.EspnId
            });

            Logger.LogInformation("Player inserted with ID: {PlayerId}", id);
            return id;
        }

        public async Task<bool> UpdateAsync(Player player)
        {
            Logger.LogDebug("Updating player: {PlayerId}", player.Id);

            player.UpdateTimestamp();

            var sql = await LoadSqlAsync("Update");
            var rowsAffected = await ExecuteAsync(sql, new
            {
                player.Id,
                player.FirstName,
                player.LastName,
                Position = player.Position?.ToString(),
                player.HeightFeet,
                player.HeightInches,
                player.WeightPounds,
                player.TeamId,
                player.UpdatedAt
            });

            var updated = rowsAffected > 0;
            Logger.LogDebug("Player {Updated}: {PlayerId}", updated ? "updated" : "not updated", player.Id);
            return updated;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            Logger.LogDebug("Deleting player: {PlayerId}", id);

            var sql = await LoadSqlAsync("Delete");
            var rowsAffected = await ExecuteAsync(sql, new { Id = id });

            var deleted = rowsAffected > 0;
            Logger.LogDebug("Player {Deleted}: {PlayerId}", deleted ? "deleted" : "not deleted", id);
            return deleted;
        }

        // Métodos adicionais úteis 
        public async Task<IEnumerable<Player>> GetPlayersByPositionAsync(string position)
        {
            Logger.LogDebug("Getting players by position: {Position}", position);

            var sql = await LoadSqlAsync("GetByPosition");
            var players = await ExecuteQueryAsync<Player>(sql, new { Position = position });

            Logger.LogDebug("Found {PlayerCount} players with position {Position}", players.Count(), position);
            return players;
        }

        public async Task<IEnumerable<Player>> GetPlayersByHeightRangeAsync(int minHeightInches, int maxHeightInches)
        {
            Logger.LogDebug("Getting players by height range: {MinHeight}-{MaxHeight} inches", minHeightInches, maxHeightInches);

            var sql = await LoadSqlAsync("GetByHeightRange");
            var players = await ExecuteQueryAsync<Player>(sql, new
            {
                MinHeightInches = minHeightInches,
                MaxHeightInches = maxHeightInches
            });

            Logger.LogDebug("Found {PlayerCount} players in height range", players.Count());
            return players;
        }
    }
}