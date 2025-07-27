using AutoMapper;
using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Exceptions;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Core.Services
{
    public class PlayerService : IPlayerService
    {
        private readonly IPlayerRepository _playerRepository;
        private readonly IBallDontLieService _ballDontLieService;
        private readonly IMapper _mapper;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PlayerService> _logger;

        public PlayerService(
            IPlayerRepository playerRepository,
            IBallDontLieService ballDontLieService,
            IMapper mapper,
            IMemoryCache cache,
            ILogger<PlayerService> logger)
        {
            _playerRepository = playerRepository;
            _ballDontLieService = ballDontLieService;
            _mapper = mapper;
            _cache = cache;
            _logger = logger;
        }

        public async Task<(List<PlayerResponse> Players, int TotalCount)> SearchPlayersAsync(SearchPlayerRequest request)
        {
            try
            {
                _logger.LogInformation("Buscando jogadores com critérios: {@Request}", request);

                var (players, totalCount) = await _playerRepository.SearchPlayersAsync(request);
                var response = _mapper.Map<List<PlayerResponse>>(players);

                _logger.LogInformation("Encontrados {PlayerCount} jogadores (total: {TotalCount})", response.Count, totalCount);
                return (response, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar jogadores: {@Request}", request);
                throw new BusinessException("Falha ao buscar jogadores", ex);
            }
        }

        public async Task<(List<PlayerResponse> Players, int TotalCount)> GetPlayersByTeamAsync(int teamId, int page, int pageSize)
        {
            try
            {
                _logger.LogInformation("Buscando jogadores do time: {TeamId}", teamId);

                var (players, totalCount) = await _playerRepository.GetPlayersByTeamAsync(teamId, page, pageSize);
                var response = _mapper.Map<List<PlayerResponse>>(players);

                _logger.LogInformation("Encontrados {PlayerCount} jogadores para o time {TeamId}", response.Count, teamId);
                return (response, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar jogadores do time: {TeamId}", teamId);
                throw new BusinessException($"Falha ao recuperar jogadores do time {teamId}", ex);
            }
        }

        public async Task<PlayerResponse?> GetPlayerByIdAsync(int id)
        {
            try
            {
                _logger.LogInformation("Buscando jogador por ID: {PlayerId}", id);

                var player = await _playerRepository.GetByIdAsync(id);
                if (player == null)
                {
                    _logger.LogWarning("Jogador não encontrado: {PlayerId}", id);
                    return null;
                }

                var response = _mapper.Map<PlayerResponse>(player);
                _logger.LogInformation("Jogador recuperado: {PlayerName}", response.FullName);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar jogador por ID: {PlayerId}", id);
                throw new BusinessException($"Falha ao recuperar o jogador {id}", ex);
            }
        }

        public async Task SyncPlayersAsync(string? searchTerm = null)
        {
            try
            {
                _logger.LogInformation("Iniciando sincronização de jogadores com busca: {SearchTerm}", searchTerm ?? "Todos");

                var externalPlayers = await _ballDontLieService.SearchPlayersAsync(searchTerm ?? "");
                var entities = _mapper.Map<List<Models.Entities.Player>>(externalPlayers);

                var syncCount = 0;
                foreach (var player in entities)
                {
                    var exists = await _playerRepository.ExistsAsync(player.ExternalId);
                    if (!exists)
                    {
                        await _playerRepository.InsertAsync(player);
                        syncCount++;
                    }
                }

                _logger.LogInformation("Sincronizados {SyncCount} novos jogadores", syncCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao sincronizar jogadores");
                throw new ExternalApiException("Ball Don't Lie", "Falha ao sincronizar jogadores da API externa", null);
            }
        }
    }
}