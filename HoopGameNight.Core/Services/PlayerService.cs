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
        private readonly IEspnApiService _espnApiService;
        private readonly IMapper _mapper;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PlayerService> _logger;

        public PlayerService(
            IPlayerRepository playerRepository,
            IEspnApiService espnApiService,
            IMapper mapper,
            IMemoryCache cache,
            ILogger<PlayerService> logger)
        {
            _playerRepository = playerRepository;
            _espnApiService = espnApiService;
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
                _logger.LogInformation("Retornando {PlayerCount} jogadores (total: {TotalCount})", response.Count, totalCount);
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
            _logger.LogWarning("Sincronização de jogadores de API externa desabilitada. Busca: {SearchTerm}", searchTerm ?? "Todos");
            await Task.CompletedTask;
        }

        /// <summary>
        /// Busca TODOS os IDs externos (ESPN, NBA Stats) e retorna o NbaStatsId
        /// </summary>
        private async Task<string?> TryGetNbaStatsPlayerIdAsync(string firstName, string lastName)
        {
            _logger.LogDebug("NBA Stats mapping desabilitado para: {Name}", $"{firstName} {lastName}");
            await Task.CompletedTask; 
            return null;
        }

        private async Task<string?> TryGetEspnPlayerIdAsync(string firstName, string lastName)
        {
            try
            {
                var espnPlayer = await _espnApiService.SearchPlayerByNameAsync(firstName, lastName);

                if (espnPlayer != null && !string.IsNullOrEmpty(espnPlayer.Ref))
                {
                    // Extrair o ID da referência (ex: http://sports.core.api.espn.com/v2/sports/basketball/leagues/nba/athletes/123)
                    var parts = espnPlayer.Ref.Split('/');
                    var playerId = parts.Length > 0 ? parts[^1] : null;

                    if (!string.IsNullOrEmpty(playerId))
                    {
                        _logger.LogInformation("✅ Mapeado {Name} -> ESPN ID: {Id}",
                            $"{firstName} {lastName}", playerId);
                        return playerId;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao buscar ESPN ID para: {FirstName} {LastName}",
                    firstName, lastName);
                return null;
            }
        }
    }
}