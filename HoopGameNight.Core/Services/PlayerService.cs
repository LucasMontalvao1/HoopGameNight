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
                _logger.LogInformation("Searching players with criteria: {@Request}", request);

                var (players, totalCount) = await _playerRepository.SearchPlayersAsync(request);
                var response = _mapper.Map<List<PlayerResponse>>(players);

                _logger.LogInformation("Found {PlayerCount} players (total: {TotalCount})", response.Count, totalCount);
                return (response, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching players: {@Request}", request);
                throw new BusinessException("Failed to search players", ex);
            }
        }

        public async Task<(List<PlayerResponse> Players, int TotalCount)> GetPlayersByTeamAsync(int teamId, int page, int pageSize)
        {
            try
            {
                _logger.LogInformation("Getting players for team: {TeamId}", teamId);

                var (players, totalCount) = await _playerRepository.GetPlayersByTeamAsync(teamId, page, pageSize);
                var response = _mapper.Map<List<PlayerResponse>>(players);

                _logger.LogInformation("Found {PlayerCount} players for team {TeamId}", response.Count, teamId);
                return (response, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting players for team: {TeamId}", teamId);
                throw new BusinessException($"Failed to retrieve players for team {teamId}", ex);
            }
        }

        public async Task<PlayerResponse?> GetPlayerByIdAsync(int id)
        {
            try
            {
                _logger.LogInformation("Getting player by ID: {PlayerId}", id);

                var player = await _playerRepository.GetByIdAsync(id);
                if (player == null)
                {
                    _logger.LogWarning("Player not found: {PlayerId}", id);
                    return null;
                }

                var response = _mapper.Map<PlayerResponse>(player);
                _logger.LogInformation("Retrieved player: {PlayerName}", response.FullName);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting player by ID: {PlayerId}", id);
                throw new BusinessException($"Failed to retrieve player {id}", ex);
            }
        }

        public async Task SyncPlayersAsync(string? searchTerm = null)
        {
            try
            {
                _logger.LogInformation("Starting sync of players with search: {SearchTerm}", searchTerm ?? "All");

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

                _logger.LogInformation("Synced {SyncCount} new players", syncCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing players");
                throw new ExternalApiException("Ball Don't Lie", "Failed to sync players from external API", null);
            }
        }
    }
}