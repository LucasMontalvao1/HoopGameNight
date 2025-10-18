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
                _logger.LogInformation("Buscando todos os times");

                // Verifica se existe no cache
                if (_cache.TryGetValue(CacheKeys.ALL_TEAMS, out List<TeamResponse> cachedTeams))
                {
                    _logger.LogInformation("Retornando {TeamCount} times do cache", cachedTeams.Count);
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

                // Cache key específico para cada time
                var cacheKey = CacheKeys.GetTeamById(id);

                // Verifica se existe no cache
                if (_cache.TryGetValue(cacheKey, out TeamResponse cachedTeam))
                {
                    _logger.LogInformation("Retornando time {TeamId} do cache", id);
                    return cachedTeam;
                }

                // Se não está no cache, busca do repositório
                var team = await _teamRepository.GetByIdAsync(id);
                if (team == null)
                {
                    _logger.LogWarning("Time não encontrado: {TeamId}", id);
                    return null;
                }

                var response = _mapper.Map<TeamResponse>(team);

                // Armazena no cache
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(30));

                _cache.Set(cacheKey, response, cacheOptions);

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
                _logger.LogInformation("Iniciando sincronização de todos os times");

                var externalTeams = await _ballDontLieService.GetAllTeamsAsync();
                var entities = _mapper.Map<List<Models.Entities.Team>>(externalTeams);

                var syncCount = 0;
                foreach (var team in entities)
                {
                    // Verificar por external_id E por abbreviation para evitar duplicatas
                    var existsByExternalId = await _teamRepository.ExistsAsync(team.ExternalId);
                    var existsByAbbreviation = await _teamRepository.GetByAbbreviationAsync(team.Abbreviation);

                    if (!existsByExternalId && existsByAbbreviation == null)
                    {
                        await _teamRepository.InsertAsync(team);
                        syncCount++;
                    }
                    else if (existsByAbbreviation != null)
                    {
                        _logger.LogDebug("Time já existe com abreviação {Abbreviation}, pulando inserção", team.Abbreviation);
                    }
                }

                // Clear all team-related cache entries
                _cache.Remove(CacheKeys.ALL_TEAMS);

                _logger.LogInformation("Sincronizados {SyncCount} novos times e cache limpo", syncCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao sincronizar times");
                throw new ExternalApiException("Ball Don't Lie", "Falha ao sincronizar times da API externa", null);
            }
        }
    }
}