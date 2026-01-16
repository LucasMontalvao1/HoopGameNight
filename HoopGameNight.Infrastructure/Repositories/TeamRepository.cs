using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HoopGameNight.Core.Enums;
using HoopGameNight.Core.Interfaces.Infrastructure;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Models.Entities;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Infrastructure.Repositories
{
    public class TeamRepository : BaseRepository<Team>, ITeamRepository
    {
        protected override string EntityName => "Teams";

        public TeamRepository(
            IDatabaseConnection connection,
            ISqlLoader sqlLoader,
            ILogger<TeamRepository> logger) : base(connection, sqlLoader, logger)
        {
        }

        public async Task<IEnumerable<Team>> GetAllAsync()
        {
            Logger.LogDebug("Getting all teams");

            var sql = await LoadSqlAsync("GetAll");
            var teams = await ExecuteQueryAsync<Team>(sql);

            Logger.LogDebug("Retrieved {TeamCount} teams", teams.Count());
            return teams;
        }

        public async Task<Team?> GetByIdAsync(int id)
        {
            Logger.LogDebug("Getting team by ID: {TeamId}", id);

            var sql = await LoadSqlAsync("GetById");
            var team = await ExecuteQuerySingleOrDefaultAsync<Team>(sql, new { Id = id });

            Logger.LogDebug("Team {Found} with ID: {TeamId}", team != null ? "found" : "not found", id);
            return team;
        }

        public async Task<Team?> GetByExternalIdAsync(int externalId)
        {
            Logger.LogDebug("Getting team by external ID: {ExternalId}", externalId);

            var sql = await LoadSqlAsync("GetByExternalId");
            var team = await ExecuteQuerySingleOrDefaultAsync<Team>(sql, new { ExternalId = externalId });

            Logger.LogDebug("Team {Found} with external ID: {ExternalId}", team != null ? "found" : "not found", externalId);
            return team;
        }

        public async Task<Team?> GetByAbbreviationAsync(string abbreviation)
        {
            Logger.LogDebug("Getting team by abbreviation: {Abbreviation}", abbreviation);

            var sql = await LoadSqlAsync("GetByAbbreviation");
            var team = await ExecuteQuerySingleOrDefaultAsync<Team>(sql, new { Abbreviation = abbreviation });

            Logger.LogDebug("Team {Found} with abbreviation: {Abbreviation}", team != null ? "found" : "not found", abbreviation);
            return team;
        }

        public async Task<IEnumerable<Team>> GetByConferenceAsync(Conference conference)
        {
            Logger.LogDebug("Getting teams by conference: {Conference}", conference);

            var sql = "SELECT * FROM Teams WHERE Conference = @Conference ORDER BY Name";
            var teams = await ExecuteQueryAsync<Team>(sql, new { Conference = conference.ToString() });

            Logger.LogDebug("Found {TeamCount} teams in conference {Conference}", teams.Count(), conference);
            return teams;
        }

        public async Task<IEnumerable<Team>> GetByDivisionAsync(string division)
        {
            Logger.LogDebug("Getting teams by division: {Division}", division);

            var sql = "SELECT * FROM Teams WHERE Division = @Division ORDER BY Name";
            var teams = await ExecuteQueryAsync<Team>(sql, new { Division = division });

            Logger.LogDebug("Found {TeamCount} teams in division {Division}", teams.Count(), division);
            return teams;
        }

        public async Task<int> InsertAsync(Team team)
        {
            Logger.LogDebug("Inserting team: {TeamName}", team.DisplayName);

            var sql = await LoadSqlAsync("Insert");
            var id = await ExecuteScalarAsync<int>(sql, new
            {
                team.ExternalId,
                team.Name,
                team.FullName,
                team.Abbreviation,
                team.City,
                Conference = team.Conference.ToString(),
                team.Division
            });

            Logger.LogInformation("Team inserted with ID: {TeamId}", id);
            return id;
        }

        public async Task<bool> UpdateAsync(Team team)
        {
            Logger.LogDebug("Updating team: {TeamId}", team.Id);

            team.UpdateTimestamp();

            var sql = await LoadSqlAsync("Update");
            var rowsAffected = await ExecuteAsync(sql, new
            {
                team.Id,
                team.Name,
                team.FullName,
                team.Abbreviation,
                team.City,
                Conference = team.Conference.ToString(),
                team.Division,
                team.UpdatedAt
            });

            var updated = rowsAffected > 0;
            Logger.LogDebug("Team {Updated}: {TeamId}", updated ? "updated" : "not updated", team.Id);
            return updated;
        }

        public async Task<bool> ExistsAsync(int externalId)
        {
            Logger.LogDebug("Checking if team exists with external ID: {ExternalId}", externalId);

            var sql = await LoadSqlAsync("Exists");
            var count = await ExecuteScalarAsync<int>(sql, new { ExternalId = externalId });

            var exists = count > 0;
            Logger.LogDebug("Team {Exists} with external ID: {ExternalId}", exists ? "exists" : "does not exist", externalId);
            return exists;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                Logger.LogDebug("Deleting team with ID: {TeamId}", id);

                var existingTeam = await GetByIdAsync(id);
                if (existingTeam == null)
                {
                    Logger.LogWarning("Cannot delete team - not found with ID: {TeamId}", id);
                    return false;
                }

                var sql = await LoadSqlAsync("Delete");
                var rowsAffected = await ExecuteAsync(sql, new { Id = id });

                var deleted = rowsAffected > 0;
                Logger.LogInformation("Team {Deleted} with ID: {TeamId}", deleted ? "deleted" : "not deleted", id);
                return deleted;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error deleting team with ID: {TeamId}", id);
                return false;
            }
        }
    }
}