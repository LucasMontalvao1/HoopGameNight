using AutoMapper;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Exceptions;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Core.Services
{
    public class TeamService : ITeamService
    {
        private readonly ITeamRepository _teamRepository;
        private readonly IBallDontLieService _ballDontLieService;
        private readonly IMapper _mapper;
        private readonly IMemoryCache _cache;
        private readonly ILogger<TeamService> _logger;

        public TeamService(
            ITeamRepository teamRepository,
            IBallDontLieService ballDontLieService,
            IMapper mapper,
            IMemoryCache cache,
            ILogger<TeamService> logger)
        {
            _teamRepository = teamRepository;
            _ballDontLieService = ballDontLieService;
            _mapper = mapper;
            _cache = cache;
            _logger = logger;
        }

        public async Task<List<TeamResponse>> GetAllTeamsAsync()
        {
            try
            {
                _logger.LogInformation("Getting all teams");

                var teams = await _teamRepository.GetAllAsync();
                var response = _mapper.Map<List<TeamResponse>>(teams);

                _logger.LogInformation("Retrieved {TeamCount} teams", response.Count);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all teams");
                throw new BusinessException("Failed to retrieve teams", ex);
            }
        }

        public async Task<TeamResponse?> GetTeamByIdAsync(int id)
        {
            try
            {
                _logger.LogInformation("Getting team by ID: {TeamId}", id);

                var team = await _teamRepository.GetByIdAsync(id);
                if (team == null)
                {
                    _logger.LogWarning("Team not found: {TeamId}", id);
                    return null;
                }

                var response = _mapper.Map<TeamResponse>(team);
                _logger.LogInformation("Retrieved team: {TeamName}", response.DisplayName);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting team by ID: {TeamId}", id);
                throw new BusinessException($"Failed to retrieve team {id}", ex);
            }
        }

        public async Task<TeamResponse?> GetTeamByAbbreviationAsync(string abbreviation)
        {
            try
            {
                _logger.LogInformation("Getting team by abbreviation: {Abbreviation}", abbreviation);

                var team = await _teamRepository.GetByAbbreviationAsync(abbreviation);
                if (team == null)
                {
                    _logger.LogWarning("Team not found with abbreviation: {Abbreviation}", abbreviation);
                    return null;
                }

                var response = _mapper.Map<TeamResponse>(team);
                _logger.LogInformation("Retrieved team: {TeamName}", response.DisplayName);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting team by abbreviation: {Abbreviation}", abbreviation);
                throw new BusinessException($"Failed to retrieve team with abbreviation {abbreviation}", ex);
            }
        }

        public async Task SyncAllTeamsAsync()
        {
            try
            {
                _logger.LogInformation("Starting sync of all teams");

                var externalTeams = await _ballDontLieService.GetAllTeamsAsync();
                var entities = _mapper.Map<List<Models.Entities.Team>>(externalTeams);

                var syncCount = 0;
                foreach (var team in entities)
                {
                    var exists = await _teamRepository.ExistsAsync(team.ExternalId);
                    if (!exists)
                    {
                        await _teamRepository.InsertAsync(team);
                        syncCount++;
                    }
                }

                // Clear cache
                _cache.Remove(Constants.CacheKeys.ALL_TEAMS);

                _logger.LogInformation("Synced {SyncCount} new teams", syncCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing teams");
                throw new ExternalApiException("Ball Don't Lie", "Failed to sync teams from external API", null);
            }
        }
    }
}