using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using AutoMapper;
using HoopGameNight.Core.Configuration;
using HoopGameNight.Core.Constants;
using HoopGameNight.Core.DTOs.External.ESPN;
using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Enums;
using HoopGameNight.Core.Exceptions;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Core.Models.Entities;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Core.Services
{
    public class PlayerService : IPlayerService
    {
        private readonly IPlayerRepository _playerRepository;
        private readonly ITeamRepository _teamRepository;
        private readonly IEspnApiService _espnService;
        private readonly IMapper _mapper;
        private readonly ICacheService _cacheService;
        private readonly ILogger<PlayerService> _logger;

        public PlayerService(
            IPlayerRepository playerRepository,
            ITeamRepository teamRepository,
            IEspnApiService espnService,
            IMapper mapper,
            ICacheService cacheService,
            ILogger<PlayerService> logger)
        {
            _playerRepository = playerRepository;
            _teamRepository = teamRepository;
            _espnService = espnService;
            _mapper = mapper;
            _cacheService = cacheService;
            _logger = logger;
        }

        /// <summary>
        /// Search players with cache support (Redis → DB → ESPN)
        /// </summary>
        public async Task<(List<PlayerResponse> Players, int TotalCount)> SearchPlayersAsync(SearchPlayerRequest request)
        {
            try
            {
                var searchKey = GenerateSearchCacheKey(request);
                var cacheKey = CacheKeys.PlayersSearch(searchKey, request.Page);

                _logger.LogInformation("SEARCH PLAYERS: {@Request}", request);

                // 1. Try cache (Redis → Memory)
                var cachedData = await _cacheService.GetAsync<PlayerSearchCacheResult>(cacheKey);
                if (cachedData != null)
                {
                    _logger.LogInformation("CACHE HIT: {Count} jogadores encontrados", cachedData.Players.Count);
                    return (cachedData.Players, cachedData.TotalCount);
                }

                // 2. Buscar do banco
                _logger.LogInformation("Consultando BANCO...");
                var (players, totalCount) = await _playerRepository.SearchPlayersAsync(request);

                // 3. If not found and searching by name, sync from ESPN
                if (!players.Any() && !string.IsNullOrWhiteSpace(request.Search))
                {
                    _logger.LogInformation("BANCO vazio para busca '{Search}', enfileirando sync em background...", request.Search);
                    _ = Task.Run(() => SyncAllTeamRostersAsync());

                    // Tentar novamente após sync
                    (players, totalCount) = await _playerRepository.SearchPlayersAsync(request);
                    _logger.LogInformation("Após SYNC: {Count} jogadores encontrados", players.Count());
                }

                // 4. If team search yields no results, sync team roster
                if (!players.Any() && request.TeamId.HasValue)
                {
                    _logger.LogInformation("BANCO vazio para time {TeamId}, sincronizando roster...", request.TeamId);
                    await SyncTeamRosterAsync(request.TeamId.Value);

                    // Retry
                    (players, totalCount) = await _playerRepository.SearchPlayersAsync(request);
                    _logger.LogInformation("Após SYNC: {Count} jogadores encontrados", players.Count());
                }

                var response = _mapper.Map<List<PlayerResponse>>(players);
                _logger.LogInformation("BANCO: Encontrou {Count} jogadores (total: {Total})",
                    response.Count, totalCount);

                // 5. Save to cache
                var cacheResult = new PlayerSearchCacheResult
                {
                    Players = response,
                    TotalCount = totalCount
                };
                await _cacheService.SetAsync(cacheKey, cacheResult, CacheDurations.PlayersSearch);

                return (response, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar jogadores: {@Request}", request);
                throw new BusinessException("Falha ao buscar jogadores", ex);
            }
        }

        /// <summary>
        /// Fetch players by team with cache (Redis → DB → ESPN)
        /// </summary>
        public async Task<(List<PlayerResponse> Players, int TotalCount)> GetPlayersByTeamAsync(
            int teamId,
            int page,
            int pageSize)
        {
            try
            {
                var cacheKey = CacheKeys.PlayersByTeam(teamId, page);

                _logger.LogInformation("GET PLAYERS BY TEAM: Time {TeamId}, Página {Page}", teamId, page);

                // 1. Tentar cache
                var cachedData = await _cacheService.GetAsync<PlayerSearchCacheResult>(cacheKey);
                if (cachedData != null)
                {
                    _logger.LogInformation("CACHE HIT: {Count} jogadores do time {TeamId}",
                        cachedData.Players.Count, teamId);
                    return (cachedData.Players, cachedData.TotalCount);
                }

                // 2. Buscar do banco
                _logger.LogInformation("Consultando BANCO para time {TeamId}...", teamId);
                var (players, totalCount) = await _playerRepository.GetPlayersByTeamAsync(teamId, page, pageSize);

                // 3. If missing, sync from ESPN
                if (!players.Any())
                {
                    _logger.LogInformation("BANCO vazio para time {TeamId}, sincronizando da ESPN...", teamId);
                    await SyncTeamRosterAsync(teamId);

                    // Tentar novamente após sync
                    (players, totalCount) = await _playerRepository.GetPlayersByTeamAsync(teamId, page, pageSize);
                    _logger.LogInformation("Após SYNC: {Count} jogadores para time {TeamId}", players.Count(), teamId);
                }

                var response = _mapper.Map<List<PlayerResponse>>(players);
                _logger.LogInformation("BANCO: {Count} jogadores para time {TeamId}. IDs: {Ids}", 
                    response.Count, teamId, 
                    string.Join(",", response.Take(5).Select(p => $"{p.FullName}={p.Id}")));

                // 4. Salvar em cache
                var cacheResult = new PlayerSearchCacheResult
                {
                    Players = response,
                    TotalCount = totalCount
                };
                await _cacheService.SetAsync(cacheKey, cacheResult, CacheDurations.PlayersByTeam);

                return (response, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar jogadores do time: {TeamId}", teamId);
                throw new BusinessException($"Falha ao recuperar jogadores do time {teamId}", ex);
            }
        }

        /// <summary>
        /// List all players with pagination (Redis → DB → ESPN)
        /// </summary>
        public async Task<(List<PlayerResponse> Players, int TotalCount)> GetAllPlayersAsync(
            int page,
            int pageSize)
        {
            try
            {
                var cacheKey = CacheKeys.PlayersSearch($"all", page);

                _logger.LogInformation("GET ALL PLAYERS: Página {Page}, Tamanho {PageSize}", page, pageSize);

                // 1. Tentar cache
                var cachedData = await _cacheService.GetAsync<PlayerSearchCacheResult>(cacheKey);
                if (cachedData != null)
                {
                    _logger.LogInformation("CACHE HIT: {Count} jogadores", cachedData.Players.Count);
                    return (cachedData.Players, cachedData.TotalCount);
                }

                // 2. Buscar do banco
                _logger.LogInformation("Consultando BANCO para todos os jogadores...");
                var (players, totalCount) = await _playerRepository.GetAllPlayersAsync(page, pageSize);

                // 3. If none found, sync all rosters from ESPN
                if (!players.Any())
                {
                    _logger.LogInformation("BANCO vazio, enfileirando sync total em background...");
                    _ = Task.Run(() => SyncAllTeamRostersAsync());

                    // Tentar novamente após sync
                    (players, totalCount) = await _playerRepository.GetAllPlayersAsync(page, pageSize);
                    _logger.LogInformation("Após SYNC: {Count} jogadores encontrados", players.Count());
                }

                var response = _mapper.Map<List<PlayerResponse>>(players);
                _logger.LogInformation("BANCO: {Count} jogadores (total: {Total})", response.Count, totalCount);

                // 4. Salvar em cache
                var cacheResult = new PlayerSearchCacheResult
                {
                    Players = response,
                    TotalCount = totalCount
                };
                await _cacheService.SetAsync(cacheKey, cacheResult, CacheDurations.PlayersSearch);

                return (response, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao listar todos os jogadores");
                throw new BusinessException("Falha ao listar jogadores", ex);
            }
        }

        /// <summary>
        /// Get player by ID (Redis → DB)
        /// </summary>
        public async Task<PlayerResponse?> GetPlayerByIdAsync(int id)
        {
            try
            {
                var cacheKey = CacheKeys.PlayerById(id);

                _logger.LogInformation("GET PLAYER BY ID: {PlayerId}", id);

                // 1. Tentar cache
                var cachedData = await _cacheService.GetAsync<PlayerResponse>(cacheKey);
                if (cachedData != null)
                {
                    _logger.LogInformation("CACHE HIT: Jogador {PlayerId}", id);
                    return cachedData;
                }

                // 2. Buscar do banco
                _logger.LogInformation("Consultando BANCO para jogador {PlayerId}...", id);
                var player = await _playerRepository.GetByIdAsync(id);

                if (player == null && id > 0)
                {
                    _logger.LogInformation("Jogador não encontrado por ID interno {Id}, tentando como ExternalId...", id);
                    player = await _playerRepository.GetByExternalIdAsync(id);
                }

                if (player == null)
                {
                    _logger.LogWarning("Jogador não encontrado no BANCO com ID ou ExternalId: {PlayerId}", id);
                    return null;
                }

                var response = _mapper.Map<PlayerResponse>(player);
                _logger.LogInformation("BANCO: Jogador encontrado {PlayerName}", response.FullName);

                // 3. Salvar em cache
                await _cacheService.SetAsync(cacheKey, response, CacheDurations.Player);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar jogador por ID: {PlayerId}", id);
                throw new BusinessException($"Falha ao recuperar o jogador {id}", ex);
            }
        }

        /// <summary>
        /// Get multiple players by IDs
        /// </summary>
        public async Task<List<PlayerResponse>> GetPlayersByIdsAsync(IEnumerable<int> ids)
        {
            if (ids == null || !ids.Any()) return new List<PlayerResponse>();

            try
            {
                _logger.LogInformation("GET PLAYERS BY IDS: {Count} ids", ids.Count());

                var players = await _playerRepository.GetByIdsAsync(ids);
                return _mapper.Map<List<PlayerResponse>>(players);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar múltiplos jogadores");
                throw new BusinessException("Falha ao recuperar lote de jogadores", ex);
            }
        }

        /// <summary>
        /// Synchronizes players from ESPN API to local database
        /// </summary>
        public async Task SyncPlayersAsync(string? searchTerm = null)
        {
            _logger.LogInformation("SYNC PLAYERS: Iniciando sincronização {SearchTerm}",
                searchTerm ?? "ALL");

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                // Sincronizar todos os jogadores de todos os times
                await SyncAllTeamRostersAsync();
            }
            else
            {
                // Sincronizar jogadores de todos os times 
                await SyncAllTeamRostersAsync();
            }

            // Clear cache
            await _cacheService.RemoveByPatternAsync(CacheKeys.AllPlayersPattern);
            _logger.LogInformation("SYNC PLAYERS: Sincronização concluída, cache limpo");
        }

        #region Métodos Privados - Sincronização ESPN

        /// <summary>
        /// Sync roster for a specific team
        /// </summary>
        private async Task SyncTeamRosterAsync(int teamId)
        {
            try
            {
                var team = await _teamRepository.GetByIdAsync(teamId);
                if (team == null)
                {
                    _logger.LogWarning("Time {TeamId} não encontrado no banco", teamId);
                    return;
                }

                var espnTeamId = team.ExternalId.ToString();
                _logger.LogInformation("Sincronizando roster do time {TeamName} (ESPN ID: {EspnId})",
                    team.FullName, espnTeamId);

                var roster = await _espnService.GetTeamRosterAsync(espnTeamId);
                _logger.LogInformation("ESPN retornou {Count} jogadores para {TeamName}", roster.Count, team.FullName);

                var savedCount = 0;
                foreach (var espnPlayer in roster)
                {
                    var saved = await SaveEspnPlayerAsync(espnPlayer, teamId);
                    if (saved) savedCount++;
                }

                _logger.LogInformation("SYNC: {SavedCount}/{TotalCount} jogadores salvos para {TeamName}",
                    savedCount, roster.Count, team.FullName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao sincronizar roster do time {TeamId}", teamId);
            }
        }

        /// <summary>
        /// Sync rosters for all teams
        /// </summary>
        private async Task SyncAllTeamRostersAsync()
        {
            try
            {
                var teams = await _teamRepository.GetAllAsync();
                var teamList = teams.ToList();

                _logger.LogInformation("Sincronizando rosters de {Count} times...", teamList.Count);

                var totalSaved = 0;
                foreach (var team in teamList)
                {
                    try
                    {
                        var espnTeamId = team.ExternalId.ToString();
                        var roster = await _espnService.GetTeamRosterAsync(espnTeamId);
                        
                        _logger.LogInformation("Time {TeamName} (ID: {TeamId}): Roster com {Count} jogadores", 
                            team.FullName, team.Id, roster?.Count ?? 0);

                        if (roster == null || !roster.Any()) continue;

                        foreach (var espnPlayer in roster)
                        {
                            var saved = await SaveEspnPlayerAsync(espnPlayer, team.Id);
                            if (saved) totalSaved++;
                        }

                        _logger.LogDebug("Sincronizado roster de {TeamName}: {Count} jogadores",
                            team.FullName, roster.Count);

                        await Task.Delay(100);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Erro ao sincronizar roster do time {TeamName}", team.FullName);
                    }
                }

                _logger.LogInformation("SYNC ALL: {TotalSaved} jogadores salvos no total", totalSaved);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao sincronizar todos os rosters");
            }
        }

        /// <summary>
        /// Saves ESPN player details to database
        /// </summary>
        private async Task<bool> SaveEspnPlayerAsync(EspnPlayerDetailsDto espnPlayer, int teamId)
        {
            try
            {
                if (string.IsNullOrEmpty(espnPlayer.Id))
                {
                    return false;
                }

                var externalId = int.Parse(espnPlayer.Id);

                var existingPlayer = await _playerRepository.GetByExternalIdAsync(externalId);
                
                int heightFeet = 0, heightInches = 0, weightPounds = 0;

                if (espnPlayer.Height > 0)
                {
                    var totalInches = (int)espnPlayer.Height;
                    heightFeet = totalInches / 12;
                    heightInches = totalInches % 12;
                }
                else if (!string.IsNullOrEmpty(espnPlayer.DisplayHeight))
                {
                    var match = Regex.Match(espnPlayer.DisplayHeight, @"(\d+)'\s*(\d+)\""");
                    if (match.Success)
                    {
                        heightFeet = int.Parse(match.Groups[1].Value);
                        heightInches = int.Parse(match.Groups[2].Value);
                    }
                }

                weightPounds = (int)espnPlayer.Weight;
                if (weightPounds == 0 && !string.IsNullOrEmpty(espnPlayer.DisplayWeight))
                {
                    var match = Regex.Match(espnPlayer.DisplayWeight, @"(\d+)");
                    if (match.Success) weightPounds = int.Parse(match.Groups[1].Value);
                }

                var position = ParsePosition(espnPlayer.Position?.Abbreviation);

                if (existingPlayer != null)
                {
                    _logger.LogDebug("Atualizando biometria do jogador existente: {PlayerName}", espnPlayer.FullName);
                    existingPlayer.HeightFeet = heightFeet > 0 ? heightFeet : existingPlayer.HeightFeet;
                    existingPlayer.HeightInches = heightInches > 0 || (heightFeet > 0) ? heightInches : existingPlayer.HeightInches;
                    existingPlayer.WeightPounds = weightPounds > 0 ? weightPounds : existingPlayer.WeightPounds;
                    existingPlayer.Position = position ?? existingPlayer.Position;
                    existingPlayer.TeamId = teamId;
                    
                    // Always ensure EspnId is set for existing players (critical fix for Jaylen Brown case)
                    if (string.IsNullOrEmpty(existingPlayer.EspnId) || existingPlayer.EspnId != espnPlayer.Id)
                    {
                        existingPlayer.EspnId = espnPlayer.Id;
                    }

                    // Map birth date from ESPN if not already set (or if it's default min value)
                    if (!existingPlayer.BirthDate.HasValue || existingPlayer.BirthDate.Value == DateTime.MinValue)
                    {
                        if (!string.IsNullOrEmpty(espnPlayer.DateOfBirth))
                        {
                            if (DateTime.TryParse(espnPlayer.DateOfBirth, out var dob))
                                existingPlayer.BirthDate = dob;
                        }
                        
                        // If still null/min, fetch from full detail API
                        if (!existingPlayer.BirthDate.HasValue || existingPlayer.BirthDate.Value == DateTime.MinValue)
                        {
                            try
                            {
                                var details = await _espnService.GetPlayerDetailsAsync(espnPlayer.Id);
                                if (details != null && !string.IsNullOrEmpty(details.DateOfBirth))
                                {
                                    if (DateTime.TryParse(details.DateOfBirth, out var dob))
                                        existingPlayer.BirthDate = dob;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning("Não foi possível buscar detalhes extras para o jogador {PlayerId}: {Message}", espnPlayer.Id, ex.Message);
                            }
                        }
                    }
                    if (espnPlayer.Jersey != null && int.TryParse(espnPlayer.Jersey, out var jersey))
                        existingPlayer.JerseyNumber = jersey;

                    await _playerRepository.UpdateAsync(existingPlayer);
                    return true;
                }

                DateTime? birthDate = null;
                if (!string.IsNullOrEmpty(espnPlayer.DateOfBirth))
                {
                    if (DateTime.TryParse(espnPlayer.DateOfBirth, out var parsedDob))
                        birthDate = parsedDob;
                }
                
                // If new player and birthdate missing from roster, fetch details
                if (!birthDate.HasValue)
                {
                    try
                    {
                        var details = await _espnService.GetPlayerDetailsAsync(espnPlayer.Id);
                        if (details != null && !string.IsNullOrEmpty(details.DateOfBirth))
                        {
                            if (DateTime.TryParse(details.DateOfBirth, out var parsedDob))
                                birthDate = parsedDob;
                        }
                    }
                    catch { /* ignorar erro na busca de detalhes para novos jogadores */ }
                }

                int? jerseyNumber = null;
                if (!string.IsNullOrEmpty(espnPlayer.Jersey) && int.TryParse(espnPlayer.Jersey, out var jn))
                    jerseyNumber = jn;

                var player = new Player
                {
                    ExternalId = externalId,
                    FirstName = espnPlayer.FirstName,
                    LastName = espnPlayer.LastName,
                    Position = position,
                    HeightFeet = heightFeet,
                    HeightInches = heightInches,
                    WeightPounds = weightPounds,
                    TeamId = teamId,
                    EspnId = espnPlayer.Id,
                    BirthDate = birthDate,
                    JerseyNumber = jerseyNumber
                };

                var id = await _playerRepository.InsertAsync(player);
                _logger.LogInformation("Jogador salvo: {PlayerName} (ID: {Id})", player.FullName, id);

                return id > 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao salvar jogador ESPN: {PlayerName}", espnPlayer?.FullName);
                return false;
            }
        }

        /// <summary>
        /// Maps position abbreviation to PlayerPosition enum
        /// </summary>
        private static PlayerPosition? ParsePosition(string? positionAbbreviation)
        {
            if (string.IsNullOrWhiteSpace(positionAbbreviation))
                return null;

            return positionAbbreviation.ToUpperInvariant() switch
            {
                "PG" => PlayerPosition.PG,
                "SG" => PlayerPosition.SG,
                "SF" => PlayerPosition.SF,
                "PF" => PlayerPosition.PF,
                "C" => PlayerPosition.C,
                "G" => PlayerPosition.SG,  
                "F" => PlayerPosition.SF,  
                "G-F" or "F-G" => PlayerPosition.SG,
                "F-C" or "C-F" => PlayerPosition.PF,
                _ => null
            };
        }

        #endregion

        #region Métodos Privados - Helpers

        private string GenerateSearchCacheKey(SearchPlayerRequest request)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(request.Search))
                parts.Add($"s:{request.Search.ToLowerInvariant()}");
            if (request.TeamId.HasValue)
                parts.Add($"t:{request.TeamId}");
            if (!string.IsNullOrWhiteSpace(request.Position))
                parts.Add($"p:{request.Position.ToUpperInvariant()}");

            return parts.Any() ? string.Join("_", parts) : "all";
        }

        #endregion
    }

    /// <summary>
    /// Search results cache DTO
    /// </summary>
    internal class PlayerSearchCacheResult
    {
        public List<PlayerResponse> Players { get; set; } = new();
        public int TotalCount { get; set; }
    }
}
