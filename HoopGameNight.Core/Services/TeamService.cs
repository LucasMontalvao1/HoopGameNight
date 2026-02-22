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
                var cachedTeams = await _cacheService.GetAsync<List<TeamResponse>>(CacheKeys.ALL_TEAMS);
                if (cachedTeams != null) return cachedTeams;

                var teams = await _teamRepository.GetAllAsync();
                var response = _mapper.Map<List<TeamResponse>>(teams);

                await _cacheService.SetAsync(CacheKeys.ALL_TEAMS, response, TimeSpan.FromHours(24));

                return response;
            }
            catch (Exception ex)
            {
                throw new BusinessException("Falha ao recuperar os times", ex);
            }
        }

        public async Task<TeamResponse?> GetTeamByIdAsync(int id)
        {
            try
            {
                var team = await _teamRepository.GetByIdAsync(id);

                if (team == null) return null;

                return _mapper.Map<TeamResponse>(team);
            }
            catch (Exception ex)
            {
                throw new BusinessException("Falha ao recuperar o time", ex);
            }
        }

        public async Task<TeamResponse?> GetTeamByAbbreviationAsync(string abbreviation)
        {
            var team = await _teamRepository.GetByAbbreviationAsync(abbreviation);
            return _mapper.Map<TeamResponse>(team);
        }

        public async Task SyncAllTeamsAsync()
        {
            try
            {
                _logger.LogInformation("Iniciando sincronização de todos os times da NBA...");
                var espnTeams = await _espnService.GetAllTeamsAsync();

                if (espnTeams == null || !espnTeams.Any())
                {
                    _logger.LogWarning("Nenhum time retornado pela API da ESPN.");
                    return;
                }

                var syncCount = 0;
                var updateCount = 0;

                foreach (var espnTeam in espnTeams)
                {
                    try
                    {
                        var externalId = int.TryParse(espnTeam.Id, out var id) ? id : 0;
                        var existingTeam = await _teamRepository.GetByExternalIdAsync(externalId);

                        if (existingTeam == null)
                        {
                            var newTeam = MapEspnTeamToEntity(espnTeam);
                            await _teamRepository.InsertAsync(newTeam);
                            syncCount++;
                        }
                        else
                        {
                            existingTeam.Name = espnTeam.Name;
                            existingTeam.FullName = espnTeam.DisplayName;
                            existingTeam.Abbreviation = espnTeam.Abbreviation;
                            existingTeam.City = espnTeam.Location;
                            existingTeam.UpdatedAt = DateTime.UtcNow;

                            await _teamRepository.UpdateAsync(existingTeam);
                            updateCount++;
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

        public async Task<int> MapEspnTeamToSystemIdAsync(string espnTeamId, string espnAbbreviation)
        {
            if (string.IsNullOrEmpty(espnTeamId) && string.IsNullOrEmpty(espnAbbreviation))
            {
                _logger.LogWarning("ESPN Team ID and Abbreviation are both null/empty.");
                return 0;
            }

            if (int.TryParse(espnTeamId, out var extId))
            {
                var teamById = await _teamRepository.GetByExternalIdAsync(extId);
                if (teamById != null) return teamById.Id;
            }

            if (!string.IsNullOrEmpty(espnAbbreviation))
            {
                var teamByAbbr = await _teamRepository.GetByAbbreviationAsync(espnAbbreviation);
                if (teamByAbbr != null) return teamByAbbr.Id;
            }

            _logger.LogInformation("Time {EspnId} ({Abbr}) não encontrado. Sincronizando times...", espnTeamId, espnAbbreviation);
            await SyncAllTeamsAsync();

            if (int.TryParse(espnTeamId, out var extIdRetry))
            {
                var teamById = await _teamRepository.GetByExternalIdAsync(extIdRetry);
                if (teamById != null) return teamById.Id;
            }

            if (!string.IsNullOrEmpty(espnAbbreviation))
            {
                var teamByAbbr = await _teamRepository.GetByAbbreviationAsync(espnAbbreviation);
                if (teamByAbbr != null) return teamByAbbr.Id;
            }

            _logger.LogWarning(
                "Team not found in database even after sync | ESPN Abbr: {Abbr}, ESPN ID: {EspnId}",
                espnAbbreviation, espnTeamId);
            return 0;
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
            var eastTeams = new[] { "BOS", "BKN", "NY", "PHI", "TOR", "CHI", "CLE", "DET", "IND", "MIL", "ATL", "CHA", "MIA", "ORL", "WSH" };
            return eastTeams.Contains(espnTeam.Abbreviation) ? Conference.East : Conference.West;
        }

        private string DetermineDivision(DTOs.External.ESPN.EspnTeamDto espnTeam)
        {
            var divisions = new Dictionary<string, string>
            {
                { "BOS", "Atlantic" }, { "BKN", "Atlantic" }, { "NY", "Atlantic" }, { "PHI", "Atlantic" }, { "TOR", "Atlantic" },
                { "CHI", "Central" }, { "CLE", "Central" }, { "DET", "Central" }, { "IND", "Central" }, { "MIL", "Central" },
                { "ATL", "Southeast" }, { "CHA", "Southeast" }, { "MIA", "Southeast" }, { "ORL", "Southeast" }, { "WSH", "Southeast" },
                { "DEN", "Northwest" }, { "MIN", "Northwest" }, { "OKC", "Northwest" }, { "POR", "Northwest" }, { "UTAH", "Northwest" },
                { "GS", "Pacific" }, { "LAC", "Pacific" }, { "LAL", "Pacific" }, { "PHX", "Pacific" }, { "SAC", "Pacific" },
                { "DAL", "Southwest" }, { "HOU", "Southwest" }, { "MEM", "Southwest" }, { "NO", "Southwest" }, { "SA", "Southwest" }
            };

            return divisions.TryGetValue(espnTeam.Abbreviation, out var division) ? division : "Unknown";
        }
    }
}