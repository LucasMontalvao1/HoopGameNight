using HoopGameNight.Api.Constants;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Api.Controllers.V1
{
    [Route(ApiConstants.Routes.TEAMS)]
    public class TeamsController : BaseApiController
    {
        private readonly ITeamService _teamService;
        private readonly IBallDontLieService _ballDontLieService;
        private readonly IMemoryCache _cache;

        public TeamsController(
            ITeamService teamService,
            IBallDontLieService ballDontLieService,
            IMemoryCache cache,
            ILogger<TeamsController> logger) : base(logger)
        {
            _teamService = teamService;
            _ballDontLieService = ballDontLieService;
            _cache = cache;
        }

        /// <summary>
        /// Buscar todos os times
        /// </summary>
        /// <returns>Lista de todos os times da NBA</returns>
        [HttpGet(RouteConstants.Teams.GET_ALL)]
        [ProducesResponseType(typeof(ApiResponse<List<TeamResponse>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<List<TeamResponse>>>> GetAllTeams()
        {
            try
            {
                Logger.LogInformation("Fetching all teams");

                var cacheKey = ApiConstants.CacheKeys.ALL_TEAMS;

                if (_cache.TryGetValue(cacheKey, out List<TeamResponse>? cachedTeams))
                {
                    Logger.LogDebug("Returning cached teams");
                    return Ok(cachedTeams!, "Teams retrieved successfully (cached)");
                }

                var teams = await _teamService.GetAllTeamsAsync();

                _cache.Set(cacheKey, teams, TimeSpan.FromHours(24));

                Logger.LogInformation("Found {TeamCount} teams", teams.Count);
                return Ok(teams, "Teams retrieved successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error fetching all teams");
                throw;
            }
        }

        /// <summary>
        /// Buscar time por ID
        /// </summary>
        /// <param name="id">ID do time</param>
        /// <returns>Detalhes do time</returns>
        [HttpGet(RouteConstants.Teams.GET_BY_ID)]
        [ProducesResponseType(typeof(ApiResponse<TeamResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<TeamResponse>>> GetTeamById(int id)
        {
            try
            {
                Logger.LogInformation("Fetching team with ID: {TeamId}", id);

                var cacheKey = string.Format(ApiConstants.CacheKeys.TEAM_BY_ID, id);

                if (_cache.TryGetValue(cacheKey, out TeamResponse? cachedTeam))
                {
                    Logger.LogDebug("Returning cached team: {TeamId}", id);
                    return Ok(cachedTeam!, "Team retrieved successfully (cached)");
                }

                var team = await _teamService.GetTeamByIdAsync(id);

                if (team == null)
                {
                    Logger.LogWarning("Team not found with ID: {TeamId}", id);
                    return NotFound<TeamResponse>($"Team with ID {id} not found");
                }

                _cache.Set(cacheKey, team, TimeSpan.FromHours(24));

                Logger.LogInformation("Team found: {TeamName}", team.DisplayName);
                return Ok(team, "Team retrieved successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error fetching team with ID: {TeamId}", id);
                throw;
            }
        }

        /// <summary>
        /// Buscar time por abreviação
        /// </summary>
        /// <param name="abbreviation">Abreviação do time (ex: LAL, GSW)</param>
        /// <returns>Detalhes do time</returns>
        [HttpGet(RouteConstants.Teams.GET_BY_ABBREVIATION)]
        [ProducesResponseType(typeof(ApiResponse<TeamResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<TeamResponse>>> GetTeamByAbbreviation(string abbreviation)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(abbreviation) || abbreviation.Length > 5)
                {
                    return BadRequest<TeamResponse>("Invalid team abbreviation");
                }

                Logger.LogInformation("Fetching team with abbreviation: {Abbreviation}", abbreviation);

                var team = await _teamService.GetTeamByAbbreviationAsync(abbreviation.ToUpper());

                if (team == null)
                {
                    Logger.LogWarning("Team not found with abbreviation: {Abbreviation}", abbreviation);
                    return NotFound<TeamResponse>($"Team with abbreviation '{abbreviation}' not found");
                }

                Logger.LogInformation("Team found: {TeamName}", team.DisplayName);
                return Ok(team, "Team retrieved successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error fetching team with abbreviation: {Abbreviation}", abbreviation);
                throw;
            }
        }

        // ============================================
        // NOVOS ENDPOINTS DE SYNC
        // ============================================

        /// <summary>
        /// Sincronizar todos os times da API externa
        /// </summary>
        /// <returns>Resultado da sincronização</returns>
        [HttpPost("sync")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<object>>> SyncTeams()
        {
            try
            {
                Logger.LogInformation("Starting manual sync of teams");

                await _teamService.SyncAllTeamsAsync();
                _cache.Remove(ApiConstants.CacheKeys.ALL_TEAMS);
                var teams = await _teamService.GetAllTeamsAsync();

                var result = (object)new
                {
                    message = "Teams synced successfully",
                    teamCount = teams.Count,
                    timestamp = DateTime.UtcNow
                };

                Logger.LogInformation("Manual sync completed - {TeamCount} teams", teams.Count);
                return Ok(result, "Teams synced successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during manual sync of teams");
                throw;
            }
        }


        /// <summary>
        /// Buscar times diretamente da API externa
        /// </summary>
        /// <returns>Times direto da Ball Don't Lie API</returns>
        [HttpGet("external")]
        [ProducesResponseType(typeof(ApiResponse<List<object>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<object>>>> GetTeamsFromExternal()
        {
            try
            {
                Logger.LogInformation("Fetching teams directly from external API");

                var externalTeams = await _ballDontLieService.GetAllTeamsAsync();
                var teamsList = externalTeams.Select(t => new
                {
                    id = t.Id,
                    name = t.Name,
                    fullName = t.FullName,
                    abbreviation = t.Abbreviation,
                    city = t.City,
                    conference = t.Conference,
                    division = t.Division
                }).ToList<object>();

                Logger.LogInformation("Retrieved {TeamCount} teams from external API", teamsList.Count);
                return Ok(teamsList, "Teams from external API");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error fetching teams from external API");
                throw;
            }
        }

        /// <summary>
        /// Verificar status da sincronização de times
        /// </summary>
        /// <returns>Status da sincronização</returns>
        [HttpGet("sync/status")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<object>>> GetSyncStatus()
        {
            try
            {
                var localTeamsCount = (await _teamService.GetAllTeamsAsync()).Count;
                var externalTeamsCount = (await _ballDontLieService.GetAllTeamsAsync()).Count();

                var status = (object)new
                {
                    localTeams = localTeamsCount,
                    externalTeams = externalTeamsCount,
                    needsSync = localTeamsCount != externalTeamsCount,
                    lastCheck = DateTime.UtcNow,
                    recommendation = localTeamsCount < 30 ?
                        "Initial sync required - less than 30 teams found" :
                        "Teams data looks complete"
                };

                return Ok(status, "Sync status retrieved");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error checking sync status");
                throw;
            }
        }
    }
}