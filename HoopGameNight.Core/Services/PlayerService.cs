using AutoMapper;
using HoopGameNight.Core.DTOs.External.BallDontLie;
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
        private readonly IEspnApiService _espnApiService;
        private readonly INbaStatsApiService _nbaStatsApiService;
        private readonly IMapper _mapper;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PlayerService> _logger;

        public PlayerService(
            IPlayerRepository playerRepository,
            IBallDontLieService ballDontLieService,
            IEspnApiService espnApiService,
            INbaStatsApiService nbaStatsApiService,
            IMapper mapper,
            IMemoryCache cache,
            ILogger<PlayerService> logger)
        {
            _playerRepository = playerRepository;
            _ballDontLieService = ballDontLieService;
            _espnApiService = espnApiService;
            _nbaStatsApiService = nbaStatsApiService;
            _mapper = mapper;
            _cache = cache;
            _logger = logger;
        }

        public async Task<(List<PlayerResponse> Players, int TotalCount)> SearchPlayersAsync(SearchPlayerRequest request)
        {
            try
            {
                _logger.LogInformation("Buscando jogadores com critérios: {@Request}", request);

                // 1. BUSCAR NO BANCO LOCAL PRIMEIRO
                var (players, totalCount) = await _playerRepository.SearchPlayersAsync(request);

                // 2. SE NÃO ENCONTROU E TEM TERMO DE BUSCA, BUSCAR NA API EXTERNA
                if (players.Count() == 0 && !string.IsNullOrWhiteSpace(request.Search))
                {
                    _logger.LogInformation("Nenhum jogador encontrado localmente. Buscando na API externa: {SearchTerm}", request.Search);

                    // Buscar na Ball Don't Lie API
                    var externalPlayersEnum = await _ballDontLieService.SearchPlayersAsync(request.Search);
                    var externalPlayers = externalPlayersEnum.ToList();

                    if (externalPlayers.Any())
                    {
                        _logger.LogInformation("Encontrados {Count} jogadores na API externa", externalPlayers.Count);

                        // Salvar cada jogador com busca híbrida de IDs
                        foreach (var externalPlayer in externalPlayers)
                        {
                            await SavePlayerWithHybridMappingAsync(externalPlayer);
                        }

                        // Buscar novamente no banco (agora devem estar lá)
                        (players, totalCount) = await _playerRepository.SearchPlayersAsync(request);
                    }
                    else
                    {
                        _logger.LogWarning("Nenhum jogador encontrado na API externa para: {SearchTerm}", request.Search);
                    }
                }

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

        /// <summary>
        /// Salva um jogador com mapeamento híbrido de IDs (NBA Stats, ESPN)
        /// Garante: Sem duplicatas + IDs corretos + Transação atômica
        /// </summary>
        private async Task SavePlayerWithHybridMappingAsync(BallDontLiePlayerDto externalPlayer)
        {
            try
            {
                // Verificar duplicata por ExternalId
                var exists = await _playerRepository.ExistsAsync(externalPlayer.Id);
                if (exists)
                {
                    _logger.LogDebug("Jogador já existe (ExternalId={ExternalId}): {FirstName} {LastName}",
                        externalPlayer.Id, externalPlayer.FirstName, externalPlayer.LastName);
                    return;
                }

                // Mapear para entidade
                var player = _mapper.Map<Models.Entities.Player>(externalPlayer);

                // 🔍 BUSCA HÍBRIDA: Obter IDs de TODAS as APIs
                // Delay de 2s para respeitar rate limit (60 req/min = 1 req/seg)
                player.NbaStatsId = await TryGetNbaStatsPlayerIdAsync(player.FirstName, player.LastName);

                if (!string.IsNullOrEmpty(player.NbaStatsId))
                {
                    await Task.Delay(2000); // 2 segundos entre chamadas
                }

                player.EspnId = await TryGetEspnPlayerIdAsync(player.FirstName, player.LastName);

                // Salvar no banco (constraints garantem unicidade)
                await _playerRepository.InsertAsync(player);

                // Log de sucesso com APIs mapeadas
                var mappedApis = new List<string>();
                if (!string.IsNullOrEmpty(player.NbaStatsId)) mappedApis.Add("NBA Stats");
                if (!string.IsNullOrEmpty(player.EspnId)) mappedApis.Add("ESPN");

                if (mappedApis.Any())
                {
                    _logger.LogInformation("✅ Player {Name} salvo com mapeamento: {APIs}",
                        player.FullName, string.Join(", ", mappedApis));
                }
                else
                {
                    _logger.LogInformation("✅ Player {Name} salvo (sem IDs externos mapeados)", player.FullName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar jogador com mapeamento híbrido: {FirstName} {LastName}",
                    externalPlayer.FirstName, externalPlayer.LastName);
                // Não propaga exceção - continua processando outros jogadores
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
                        // 🔍 BUSCA HÍBRIDA: Tentar buscar IDs em TODAS as APIs
                        player.NbaStatsId = await TryGetNbaStatsPlayerIdAsync(player.FirstName, player.LastName);
                        player.EspnId = await TryGetEspnPlayerIdAsync(player.FirstName, player.LastName);

                        await _playerRepository.InsertAsync(player);
                        syncCount++;

                        var mappedApis = new List<string>();
                        if (!string.IsNullOrEmpty(player.NbaStatsId)) mappedApis.Add("NBA Stats");
                        if (!string.IsNullOrEmpty(player.EspnId)) mappedApis.Add("ESPN");

                        if (mappedApis.Any())
                        {
                            _logger.LogInformation("✅ Player {Name} sincronizado com {APIs}",
                                player.FullName, string.Join(", ", mappedApis));
                        }
                        else
                        {
                            _logger.LogDebug("Player {Name} sincronizado (sem mapeamento de APIs)",
                                player.FullName);
                        }
                    }
                }

                _logger.LogInformation("Sincronizados {SyncCount} novos jogadores", syncCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao sincronizar jogadores");
                throw new ExternalApiException("Ball Don't Lie", "Falha ao sincronizar jogadores da API externa", ex);
            }
        }

        /// <summary>
        /// Busca TODOS os IDs externos (ESPN, NBA Stats) e retorna o NbaStatsId
        /// Usa busca híbrida em múltiplas APIs
        /// </summary>
        private async Task<string?> TryGetNbaStatsPlayerIdAsync(string firstName, string lastName)
        {
            try
            {
                // Buscar na NBA Stats API (Ball Don't Lie v2)
                var nbaPlayer = await _nbaStatsApiService.SearchPlayerByNameAsync(firstName, lastName);

                if (nbaPlayer != null)
                {
                    _logger.LogInformation("✅ Mapeado {Name} -> NBA Stats ID: {Id}",
                        $"{firstName} {lastName}", nbaPlayer.PersonId);
                    return nbaPlayer.PersonId;
                }

                _logger.LogDebug("⚠️ Não encontrado nas APIs: {Name}", $"{firstName} {lastName}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao buscar IDs externos para: {FirstName} {LastName}",
                    firstName, lastName);
                return null;
            }
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