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

            var players = await ExecuteQueryAsync<Player>(sql, parameters);
            var totalCount = await ExecuteScalarAsync<int>(countSql, parameters);

            Logger.LogDebug("Found {PlayerCount} players (total: {TotalCount})", players.Count(), totalCount);
            return (players, totalCount);
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

            var players = await ExecuteQueryAsync<Player>(sql, parameters);
            var totalCount = await ExecuteScalarAsync<int>(countSql, new { TeamId = teamId });

            Logger.LogDebug("Found {PlayerCount} players for team {TeamId}", players.Count(), teamId);
            return (players, totalCount);
        }

        public async Task<Player?> GetByIdAsync(int id)
        {
            Logger.LogDebug("Getting player by ID: {PlayerId}", id);

            var sql = await LoadSqlAsync("GetById");
            var player = await ExecuteQuerySingleOrDefaultAsync<Player>(sql, new { Id = id });

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
                player.TeamId
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

        public async Task<bool> ExistsAsync(int externalId)
        {
            Logger.LogDebug("Checking if player exists with external ID: {ExternalId}", externalId);

            var sql = await LoadSqlAsync("Exists");
            var count = await ExecuteScalarAsync<int>(sql, new { ExternalId = externalId });

            var exists = count > 0;
            Logger.LogDebug("Player {Exists} with external ID: {ExternalId}", exists ? "exists" : "does not exist", externalId);
            return exists;
        }

        // Método adicional para busca avançada
        public async Task<IEnumerable<Player>> GetPlayersByPositionAsync(string position)
        {
            Logger.LogDebug("Getting players by position: {Position}", position);

            var sql = await LoadSqlAsync("GetByPosition");
            var players = await ExecuteQueryAsync<Player>(sql, new { Position = position });

            Logger.LogDebug("Found {PlayerCount} players with position {Position}", players.Count(), position);
            return players;
        }

        // Método para buscar jogadores por altura
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