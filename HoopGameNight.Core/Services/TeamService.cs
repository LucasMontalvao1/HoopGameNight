using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using HoopGameNight.Core.Configuration;
using HoopGameNight.Core.Constants;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Enums;
using HoopGameNight.Core.Exceptions;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Core.Services
{
    public class TeamService : ITeamService
    {
        private readonly ITeamRepository _teamRepository;
        private readonly IEspnApiService _espnService;
        private readonly IMapper _mapper;
        private readonly ICacheService _cacheService;
        private readonly ILogger<TeamService> _logger;

        public TeamService(
            ITeamRepository teamRepository,
            IEspnApiService espnService,
            IMapper mapper,
            ICacheService cacheService,
            ILogger<TeamService> logger)
        {
            _teamRepository = teamRepository;
            _espnService = espnService;
            _mapper = mapper;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<List<TeamResponse>> GetAllTeamsAsync()
        {
            try
            {
                _logger.LogInformation("Buscando todos os times");

                var cachedTeams = await _cacheService.GetAsync<List<TeamResponse>>(CacheKeys.ALL_TEAMS);
                if (cachedTeams != null && cachedTeams.Count >= 30)
                {
                    _logger.LogInformation("Retornando {TeamCount} times do cache", cachedTeams.Count);
                    return cachedTeams;
                }

                var teams = await _teamRepository.GetAllAsync();

                if (!teams.Any() || teams.Count() < 30)
                {
                    _logger.LogWarning("Banco com {Count} times (esperado: 30). Sincronizando automaticamente...", teams.Count());

                    try
                    {
                        await SyncAllTeamsAsync();
                        teams = await _teamRepository.GetAllAsync();
                        _logger.LogInformation("Auto-sync concluído. {Count} times agora no banco", teams.Count());
                    }
                    catch (Exception syncEx)
                    {
                        _logger.LogError(syncEx, "Falha no auto-sync de times");
                    }
                }

                var response = _mapper.Map<List<TeamResponse>>(teams);

                await _cacheService.SetAsync(CacheKeys.ALL_TEAMS, response, CacheDurations.AllTeams);

                _logger.LogInformation("Recuperados {TeamCount} times do banco de dados e armazenados em cache", response.Count);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar todos os times");
                throw new BusinessException("Falha ao recuperar os times", ex);
            }
        }

        public async Task<TeamResponse?> GetTeamByIdAsync(int id)
        {
            try
            {
                _logger.LogInformation("Buscando time por ID: {TeamId}", id);

                var cacheKey = CacheKeys.TeamById(id);

                var cachedTeam = await _cacheService.GetAsync<TeamResponse>(cacheKey);
                if (cachedTeam != null)
                {
                    _logger.LogInformation("Retornando time {TeamId} do cache", id);
                    return cachedTeam;
                }

                var team = await _teamRepository.GetByIdAsync(id);
                if (team == null)
                {
                    _logger.LogWarning("Time não encontrado: {TeamId}", id);
                    return null;
                }

                var response = _mapper.Map<TeamResponse>(team);

                await _cacheService.SetAsync(cacheKey, response, CacheDurations.SingleTeam);

                _logger.LogInformation("Time recuperado: {TeamName} e armazenado em cache", response.DisplayName);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar time por ID: {TeamId}", id);
                throw new BusinessException($"Falha ao recuperar o time {id}", ex);
            }
        }

        public async Task<TeamResponse?> GetTeamByAbbreviationAsync(string abbreviation)
        {
            try
            {
                _logger.LogInformation("Buscando time por abreviação: {Abbreviation}", abbreviation);

                var team = await _teamRepository.GetByAbbreviationAsync(abbreviation);
                if (team == null)
                {
                    _logger.LogWarning("Time não encontrado com a abreviação: {Abbreviation}", abbreviation);
                    return null;
                }

                var response = _mapper.Map<TeamResponse>(team);
                _logger.LogInformation("Time recuperado: {TeamName}", response.DisplayName);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar time por abreviação: {Abbreviation}", abbreviation);
                throw new BusinessException($"Falha ao recuperar o time com a abreviação {abbreviation}", ex);
            }
        }

        public async Task SyncAllTeamsAsync()
        {
            try
            {
                _logger.LogInformation("Iniciando sincronização de todos os times via ESPN API");

                var espnTeams = await _espnService.GetAllTeamsAsync();

                if (espnTeams == null || !espnTeams.Any())
                {
                    _logger.LogWarning("Nenhum time retornado pela ESPN API");
                    return;
                }

                _logger.LogInformation("ESPN retornou {Count} times", espnTeams.Count);

                var syncCount = 0;
                var updateCount = 0;

                foreach (var espnTeam in espnTeams)
                {
                    try
                    {
                        if (string.IsNullOrEmpty(espnTeam.Id) ||
                            string.IsNullOrEmpty(espnTeam.Abbreviation) ||
                            string.IsNullOrEmpty(espnTeam.Name))
                        {
                            _logger.LogWarning("Time da ESPN com dados incompletos, ignorando: {DisplayName}", espnTeam.DisplayName);
                            continue;
                        }

                        var existingTeam = await _teamRepository.GetByAbbreviationAsync(espnTeam.Abbreviation);

                        if (existingTeam == null)
                        {
                            var newTeam = MapEspnTeamToEntity(espnTeam);
                            await _teamRepository.InsertAsync(newTeam);
                            syncCount++;
                            _logger.LogInformation("Time inserido: {Abbreviation} - {DisplayName}", espnTeam.Abbreviation, espnTeam.DisplayName);
                        }
                        else
                        {
                            existingTeam.Name = espnTeam.Name;
                            existingTeam.FullName = espnTeam.DisplayName;
                            existingTeam.City = espnTeam.Location;
                            existingTeam.EspnId = espnTeam.Id;
                            existingTeam.Conference = DetermineConference(espnTeam);
                            existingTeam.Division = DetermineDivision(espnTeam);

                            await _teamRepository.UpdateAsync(existingTeam);
                            updateCount++;
                            _logger.LogDebug("Time atualizado: {Abbreviation}", espnTeam.Abbreviation);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao processar time: {DisplayName}", espnTeam.DisplayName);
                    }
                }

                await _cacheService.RemoveAsync(CacheKeys.ALL_TEAMS);

                _logger.LogInformation("Sincronização concluída: {New} novos, {Updated} atualizados", syncCount, updateCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao sincronizar times da ESPN");
                throw new ExternalApiException("ESPN API", "Falha ao sincronizar times da API externa", null);
            }
        }

        private Models.Entities.Team MapEspnTeamToEntity(DTOs.External.ESPN.EspnTeamDto espnTeam)
        {
            var externalId = int.TryParse(espnTeam.Id, out var id) ? id : 0;

            return new Models.Entities.Team
            {
                ExternalId = externalId,
                EspnId = espnTeam.Id,
                Name = espnTeam.Name,
                FullName = espnTeam.DisplayName,
                Abbreviation = espnTeam.Abbreviation,
                City = espnTeam.Location,
                Conference = DetermineConference(espnTeam),
                Division = DetermineDivision(espnTeam),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        private Conference DetermineConference(DTOs.External.ESPN.EspnTeamDto espnTeam)
        {
            // Mapear baseado no nome ou slug do time
            // Times do Leste
            var eastTeams = new[] { "BOS", "BKN", "NY", "PHI", "TOR", "CHI", "CLE", "DET", "IND", "MIL", "ATL", "CHA", "MIA", "ORL", "WSH" };

            return eastTeams.Contains(espnTeam.Abbreviation) ? Conference.East : Conference.West;
        }

        private string DetermineDivision(DTOs.External.ESPN.EspnTeamDto espnTeam)
        {
            // Mapear divisões baseado no time
            var divisions = new Dictionary<string, string>
            {
                // Atlantic
                { "BOS", "Atlantic" }, { "BKN", "Atlantic" }, { "NY", "Atlantic" }, { "PHI", "Atlantic" }, { "TOR", "Atlantic" },
                // Central
                { "CHI", "Central" }, { "CLE", "Central" }, { "DET", "Central" }, { "IND", "Central" }, { "MIL", "Central" },
                // Southeast
                { "ATL", "Southeast" }, { "CHA", "Southeast" }, { "MIA", "Southeast" }, { "ORL", "Southeast" }, { "WSH", "Southeast" },
                // Northwest
                { "DEN", "Northwest" }, { "MIN", "Northwest" }, { "OKC", "Northwest" }, { "POR", "Northwest" }, { "UTAH", "Northwest" },
                // Pacific
                { "GS", "Pacific" }, { "LAC", "Pacific" }, { "LAL", "Pacific" }, { "PHX", "Pacific" }, { "SAC", "Pacific" },
                // Southwest
                { "DAL", "Southwest" }, { "HOU", "Southwest" }, { "MEM", "Southwest" }, { "NO", "Southwest" }, { "SA", "Southwest" }
            };

            return divisions.TryGetValue(espnTeam.Abbreviation, out var division) ? division : "Unknown";
        }
    }
}