using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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
        private readonly IMemoryCache _cache;

        public TeamsController(
            ITeamService teamService,
            IMemoryCache cache,
            ILogger<TeamsController> logger) : base(logger)
        {
            _teamService = teamService;
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
                Logger.LogInformation("Buscando todas as equipes");

                var cacheKey = ApiConstants.CacheKeys.ALL_TEAMS;

                if (_cache.TryGetValue(cacheKey, out List<TeamResponse>? cachedTeams))
                {
                    Logger.LogDebug("Retornando equipes em cache");
                    return Ok(cachedTeams!, "Equipes recuperadas com sucesso (armazenadas em cache)");
                }

                var teams = await _teamService.GetAllTeamsAsync();

                _cache.Set(cacheKey, teams, TimeSpan.FromHours(24));

                Logger.LogInformation("Foram encontrados {TeamCount} times", teams.Count);
                return Ok(teams, "Equipes recuperadas com sucesso");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao buscar todas as equipes");
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
                Logger.LogInformation("Buscando equipe com ID: {TeamId}", id);

                var cacheKey = string.Format(ApiConstants.CacheKeys.TEAM_BY_ID, id);

                if (_cache.TryGetValue(cacheKey, out TeamResponse? cachedTeam))
                {
                    Logger.LogDebug("Retornando equipe em cache: {TeamId}", id);
                    return Ok(cachedTeam!, "Equipe recuperada com sucesso (armazenada em cache)");
                }

                var team = await _teamService.GetTeamByIdAsync(id);

                if (team == null)
                {
                    Logger.LogWarning("Equipe não encontrada com ID: {TeamId}", id);
                    return NotFound<TeamResponse>($"Equipe com ID {id} não encontrada");
                }

                _cache.Set(cacheKey, team, TimeSpan.FromHours(24));

                Logger.LogInformation("Equipe encontrada: {TeamName}", team.DisplayName);
                return Ok(team, "Equipe recuperada com sucesso");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao buscar equipe com ID: {TeamId}", id);
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
                    Logger.LogWarning("Abreviação inválida fornecida: {Abbreviation}", abbreviation);
                    return BadRequest<TeamResponse>("Abreviação de equipe inválida");
                }

                Logger.LogInformation("Buscando equipe com abreviação: {Abbreviation}", abbreviation);

                var team = await _teamService.GetTeamByAbbreviationAsync(abbreviation.Trim().ToUpper());

                if (team == null)
                {
                    Logger.LogWarning("Equipe não encontrada com abreviação: {Abbreviation}", abbreviation);
                    return NotFound<TeamResponse>($"Equipe com abreviação '{abbreviation}' não encontrada");
                }

                Logger.LogInformation("Equipe encontrada: {TeamName} (ID: {TeamId})", team.DisplayName, team.Id);
                return Ok(team, "Equipe recuperada com sucesso");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao buscar equipe com abreviação: {Abbreviation}", abbreviation);
                throw;
            }
        }

        /// <summary>
        /// Sincronizar todos os times da API externa
        /// </summary>
        /// <returns>Resultado da sincronização</returns>
        [HttpPost("sync")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<object>>> SyncTeams()
        {
            try
            {
                Logger.LogInformation("Iniciando a sincronização manual das equipes");

                await _teamService.SyncAllTeamsAsync();
                _cache.Remove(ApiConstants.CacheKeys.ALL_TEAMS);
                var teams = await _teamService.GetAllTeamsAsync();

                var result = (object)new
                {
                    message = "Equipes sincronizadas com sucesso",
                    teamCount = teams.Count,
                    timestamp = DateTime.UtcNow,
                    resourcesCreated = teams.Count
                };

                Logger.LogInformation("Sincronização manual concluída - {TeamCount} equipes", teams.Count);

                return StatusCode(StatusCodes.Status201Created, ApiResponse<object>.SuccessResult(result, "Equipes sincronizadas com sucesso"));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro durante sincronização manual de equipes");
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
                Logger.LogInformation("Buscando equipes diretamente da API externa");

                var teams = await _teamService.GetAllTeamsAsync();
                var teamsList = teams.Select(t => new
                {
                    id = t.Id,
                    name = t.Name,
                    fullName = t.DisplayName,
                    abbreviation = t.Abbreviation,
                    city = t.City,
                    conference = t.Conference,
                    division = t.Division
                }).ToList<object>();

                Logger.LogInformation("{TeamCount} equipes recuperadas", teamsList.Count);
                return Ok(teamsList, "Equipes recuperadas do banco de dados");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao buscar equipes");
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

                var expectedTeamsCount = 30; 

                var status = (object)new
                {
                    localTeams = localTeamsCount,
                    expectedTeams = expectedTeamsCount,
                    needsSync = localTeamsCount != expectedTeamsCount,
                    lastCheck = DateTime.UtcNow,
                    recommendation = localTeamsCount < 30 ?
                        "Sincronização inicial necessária - menos de 30 equipes encontradas" :
                        "Os dados das equipes parecem completos"
                };

                return Ok(status, "Status de sincronização recuperado");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao verificar o status de sincronização");
                throw;
            }
        }
    }
}