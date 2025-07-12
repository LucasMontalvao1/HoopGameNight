using AutoMapper;
using HoopGameNight.Core.Constants;
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

                // Verifica se existe no cache
                if (_cache.TryGetValue(CacheKeys.ALL_TEAMS, out List<TeamResponse> cachedTeams))
                {
                    _logger.LogInformation("Returning {TeamCount} teams from cache", cachedTeams.Count);
                    return cachedTeams;
                }

                // Se não está no cache, busca do repositório
                var teams = await _teamRepository.GetAllAsync();
                var response = _mapper.Map<List<TeamResponse>>(teams);

                // Armazena no cache com expiração
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(30)) // Expira após 30 minutos sem uso
                    .SetAbsoluteExpiration(TimeSpan.FromHours(2));  // Expira após 2 horas independentemente

                _cache.Set(CacheKeys.ALL_TEAMS, response, cacheOptions);

                _logger.LogInformation("Retrieved {TeamCount} teams from database and cached", response.Count);
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

                // Cache key específico para cada time
                var cacheKey = CacheKeys.GetTeamById(id);

                // Verifica se existe no cache
                if (_cache.TryGetValue(cacheKey, out TeamResponse cachedTeam))
                {
                    _logger.LogInformation("Returning team {TeamId} from cache", id);
                    return cachedTeam;
                }

                // Se não está no cache, busca do repositório
                var team = await _teamRepository.GetByIdAsync(id);
                if (team == null)
                {
                    _logger.LogWarning("Team not found: {TeamId}", id);
                    return null;
                }

                var response = _mapper.Map<TeamResponse>(team);

                // Armazena no cache
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(30));

                _cache.Set(cacheKey, response, cacheOptions);

                _logger.LogInformation("Retrieved team: {TeamName} and cached", response.DisplayName);
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

                // Clear all team-related cache entries
                _cache.Remove(CacheKeys.ALL_TEAMS);


                _logger.LogInformation("Synced {SyncCount} new teams and cleared cache", syncCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing teams");
                throw new ExternalApiException("Ball Don't Lie", "Failed to sync teams from external API", null);
            }
        }
    }
}