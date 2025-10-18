using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Core.Services
{
    /// <summary>
    /// Serviço para mapear IDs de jogadores entre Ball Don't Lie e ESPN APIs
    /// </summary>
    public class PlayerIdMappingService : IPlayerIdMappingService
    {
        private readonly IPlayerRepository _playerRepository;
        private readonly IEspnApiService _espnApiService;
        private readonly ILogger<PlayerIdMappingService> _logger;
        private readonly Dictionary<int, string> _cachedMappings = new();

        public PlayerIdMappingService(
            IPlayerRepository playerRepository,
            IEspnApiService espnApiService,
            ILogger<PlayerIdMappingService> logger)
        {
            _playerRepository = playerRepository;
            _espnApiService = espnApiService;
            _logger = logger;
        }

        public async Task<string?> GetEspnPlayerIdAsync(int ballDontLiePlayerId)
        {
            // Verificar cache
            if (_cachedMappings.TryGetValue(ballDontLiePlayerId, out var cachedId))
            {
                return cachedId;
            }

            try
            {
                // Buscar jogador no banco local
                var player = await _playerRepository.GetByExternalIdAsync(ballDontLiePlayerId);
                if (player == null)
                {
                    _logger.LogWarning("Player with Ball Don't Lie ID {PlayerId} not found in database", ballDontLiePlayerId);
                    return null;
                }

                // Se já temos o ESPN ID armazenado, usar
                if (!string.IsNullOrEmpty(player.EspnId))
                {
                    _cachedMappings[ballDontLiePlayerId] = player.EspnId;
                    return player.EspnId;
                }

                // Tentar encontrar pelo nome na ESPN API
                var espnPlayerId = await FindEspnPlayerByNameAsync(player.FirstName, player.LastName);
                if (!string.IsNullOrEmpty(espnPlayerId))
                {
                    // Atualizar no banco
                    player.EspnId = espnPlayerId;
                    await _playerRepository.UpdateAsync(player);

                    _cachedMappings[ballDontLiePlayerId] = espnPlayerId;
                    return espnPlayerId;
                }

                _logger.LogWarning("Could not find ESPN ID for player: {FirstName} {LastName} (Ball Don't Lie ID: {PlayerId})",
                    player.FirstName, player.LastName, ballDontLiePlayerId);

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mapping Ball Don't Lie player ID {PlayerId} to ESPN", ballDontLiePlayerId);
                return null;
            }
        }

        private async Task<string?> FindEspnPlayerByNameAsync(string firstName, string lastName)
        {
            try
            {
                // Por enquanto, retorna null porque não temos um endpoint de busca por nome na ESPN
                // Em produção, isso seria implementado com busca fuzzy ou banco de dados de mapeamento
                _logger.LogDebug("ESPN player search by name not implemented yet: {FirstName} {LastName}", firstName, lastName);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching ESPN player by name: {FirstName} {LastName}", firstName, lastName);
                return null;
            }
        }

        public void ClearCache()
        {
            _cachedMappings.Clear();
        }
    }
}
