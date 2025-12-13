using AutoMapper;
using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Helpers;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Core.Models.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Core.Services
{
    public class PlayerStatsService : IPlayerStatsService
    {
        private readonly IPlayerStatsRepository _statsRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly ITeamRepository _teamRepository;
        private readonly IEspnApiService _espnApiService;
        private readonly IMapper _mapper;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PlayerStatsService> _logger;

        public PlayerStatsService(
            IPlayerStatsRepository statsRepository,
            IPlayerRepository playerRepository,
            ITeamRepository teamRepository,
            IEspnApiService espnApiService,
            IMapper mapper,
            IMemoryCache cache,
            ILogger<PlayerStatsService> logger)
        {
            _statsRepository = statsRepository;
            _playerRepository = playerRepository;
            _teamRepository = teamRepository;
            _espnApiService = espnApiService;
            _mapper = mapper;
            _cache = cache;
            _logger = logger;
        }

        public async Task<PlayerDetailedResponse?> GetPlayerDetailedStatsAsync(PlayerStatsRequest request)
        {
            try
            {
                var cacheKey = $"player_stats_{request.PlayerId}_{request.Season}_{request.IncludeCareer}_{request.IncludeCurrentSeason}_{request.LastGames}";

                if (_cache.TryGetValue(cacheKey, out PlayerDetailedResponse? cached))
                {
                    return cached;
                }

                var player = await _playerRepository.GetByIdAsync(request.PlayerId);
                if (player == null)
                {
                    _logger.LogWarning("Jogador não encontrado: {PlayerId}", request.PlayerId);
                    return null;
                }

                var response = _mapper.Map<PlayerDetailedResponse>(player);

                if (request.IncludeCurrentSeason)
                {
                    try
                    {
                        var season = request.Season ?? NbaSeasonHelper.GetCurrentSeason();
                        response.CurrentSeasonStats = await GetPlayerSeasonStatsAsync(request.PlayerId, season);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao buscar estatísticas da temporada atual para jogador {PlayerId}", request.PlayerId);
                    }
                }

                if (request.IncludeCareer)
                {
                    try
                    {
                        response.CareerStats = await GetPlayerCareerStatsAsync(request.PlayerId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao buscar estatísticas de carreira para jogador {PlayerId}", request.PlayerId);
                    }
                }

                if (request.LastGames > 0)
                {
                    try
                    {
                        response.RecentGames = await GetPlayerRecentGamesAsync(request.PlayerId, request.LastGames);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao buscar jogos recentes para jogador {PlayerId}", request.PlayerId);
                    }
                }

                _cache.Set(cacheKey, response, TimeSpan.FromMinutes(15));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado em GetPlayerDetailedStatsAsync para jogador {PlayerId}", request.PlayerId);
                throw;
            }
        }

        public async Task<PlayerSeasonStatsResponse?> GetPlayerSeasonStatsAsync(int playerId, int season)
        {
            _logger.LogWarning("GetPlayerSeasonStatsAsync INICIADO: PlayerId={PlayerId}, Season={Season}", playerId, season);

            // 1. Buscar no cache
            var cacheKey = $"player_season_stats_{playerId}_{season}";
            if (_cache.TryGetValue(cacheKey, out PlayerSeasonStatsResponse? cachedResponse))
            {
                _logger.LogWarning("Stats encontradas no CACHE para jogador {PlayerId}, temporada {Season}", playerId, season);
                _logger.LogWarning("Dados do cache: PPG={PPG}, RPG={RPG}, APG={APG}", cachedResponse.PPG, cachedResponse.RPG, cachedResponse.APG);
                return cachedResponse;
            }

            _logger.LogWarning("Buscando no banco...");

            // 2. Buscar no banco
            var stats = await _statsRepository.GetSeasonStatsAsync(playerId, season);

            // 3. Se não encontrar no banco, buscar da ESPN (AUTO-SYNC)
            if (stats == null)
            {
                _logger.LogWarning(
                    "Stats NÃO encontradas no BANCO para jogador {PlayerId}, temporada {Season}. Buscando da ESPN...",
                    playerId,
                    season);

                // Buscar player para pegar o ESPN ID
                var player = await _playerRepository.GetByIdAsync(playerId);
                if (player == null || string.IsNullOrEmpty(player.EspnId))
                {
                    _logger.LogError("ERRO: Jogador {PlayerId} não tem ESPN ID. Impossível buscar stats.", playerId);
                    return null;
                }

                _logger.LogWarning("Buscando ESPN com ID: {EspnId}, Season: {Season}", player.EspnId, season);

                // Buscar da ESPN
                var espnStats = await _espnApiService.GetPlayerSeasonStatsAsync(player.EspnId, season);
                if (espnStats != null)
                {
                    _logger.LogWarning("ESPN: Stats encontradas na ESPN para jogador {PlayerId}, temporada {Season}", playerId, season);

                    // Converter ESPN DTO para Entity
                    stats = await MapAndSaveEspnStatsAsync(player, espnStats, season);
                    _logger.LogWarning("SALVO: Stats salvas no banco com sucesso");
                }
                else
                {
                    _logger.LogError("ESPN: Stats NÃO encontradas na ESPN para jogador {PlayerId}, temporada {Season}", playerId, season);
                    return null;
                }
            }
            else
            {
                _logger.LogWarning("BANCO: Stats encontradas no BANCO para jogador {PlayerId}, temporada {Season}", playerId, season);
            }

            // 4. Mapear para Response
            var response = _mapper.Map<PlayerSeasonStatsResponse>(stats);

            // 5. Buscar nome do time
            if (stats.TeamId.HasValue)
            {
                var team = await _teamRepository.GetByIdAsync(stats.TeamId.Value);
                if (team != null)
                {
                    response.TeamName = team.FullName;
                }
            }

            // 6. Salvar no cache (15 minutos)
            _cache.Set(cacheKey, response, TimeSpan.FromMinutes(15));

            _logger.LogWarning("RETORNANDO RESPOSTA: PPG={PPG}, RPG={RPG}, APG={APG}, GP={GP}",
                response.PPG, response.RPG, response.APG, response.GamesPlayed);

            return response;
        }

        public async Task<List<PlayerSeasonStatsResponse>> GetPlayerAllSeasonsAsync(int playerId)
        {
            _logger.LogWarning("GetPlayerAllSeasonsAsync INICIADO: PlayerId={PlayerId}", playerId);

            // 1. Buscar no cache
            var cacheKey = $"player_all_seasons_{playerId}";
            if (_cache.TryGetValue(cacheKey, out List<PlayerSeasonStatsResponse>? cachedSeasons))
            {
                _logger.LogWarning("CACHE HIT: {Count} temporadas encontradas no CACHE para jogador {PlayerId}", cachedSeasons!.Count, playerId);
                return cachedSeasons!;
            }

            _logger.LogWarning("CACHE MISS: Buscando no banco...");

            // 2. Buscar no banco
            var seasons = await _statsRepository.GetAllSeasonStatsAsync(playerId);

            // 3. Se não encontrar no banco, buscar APENAS TEMPORADA ATUAL da ESPN (AUTO-SYNC)
            if (!seasons.Any())
            {
                _logger.LogWarning(
                    "✗Nenhuma temporada encontrada no BANCO para jogador {PlayerId}. Buscando temporada atual da ESPN...",
                    playerId);

                // Buscar player para pegar o ESPN ID
                var player = await _playerRepository.GetByIdAsync(playerId);
                if (player == null || string.IsNullOrEmpty(player.EspnId))
                {
                    _logger.LogError("ERRO: Jogador {PlayerId} não tem ESPN ID. Impossível buscar stats.", playerId);
                    return new List<PlayerSeasonStatsResponse>();
                }

                var currentSeason = NbaSeasonHelper.GetCurrentSeason();
                _logger.LogWarning("Temporada atual calculada: {Season} (usando NbaSeasonHelper)", currentSeason);
                _logger.LogWarning("Buscando ESPN com ID: {EspnId}, Season: {Season}", player.EspnId, currentSeason);

                var espnStats = await _espnApiService.GetPlayerSeasonStatsAsync(player.EspnId, currentSeason);
                if (espnStats != null)
                {
                    _logger.LogWarning("ESPN: Stats da temporada {Season} encontradas na ESPN para jogador {PlayerId}", currentSeason, playerId);

                    await MapAndSaveEspnStatsAsync(player, espnStats, currentSeason);

                    seasons = await _statsRepository.GetAllSeasonStatsAsync(playerId);
                    _logger.LogWarning("SALVO: Stats salvas. Re-buscando do banco: {Count} temporadas encontradas", seasons.Count());
                }
                else
                {
                    _logger.LogError("ESPN: Stats da temporada {Season} NÃO encontradas na ESPN para jogador {PlayerId}", currentSeason, playerId);
                }
            }
            else
            {
                _logger.LogWarning("BANCO: {Count} temporadas encontradas no BANCO para jogador {PlayerId}", seasons.Count(), playerId);
            }

            // 4. Mapear para Response
            var responses = _mapper.Map<List<PlayerSeasonStatsResponse>>(seasons);

            // 5. Buscar nomes dos times
            foreach (var response in responses)
            {
                var season = seasons.FirstOrDefault(s => s.Season == response.Season);
                if (season?.TeamId.HasValue == true)
                {
                    var team = await _teamRepository.GetByIdAsync(season.TeamId.Value);
                    if (team != null)
                    {
                        response.TeamName = team.FullName;
                    }
                }
            }

            // 6. Salvar no cache (30 minutos)
            if (responses.Any())
            {
                _cache.Set(cacheKey, responses, TimeSpan.FromMinutes(30));
            }

            _logger.LogWarning("RETORNANDO {Count} TEMPORADAS", responses.Count);
            foreach (var resp in responses)
            {
                _logger.LogWarning("Season {Season}: PPG={PPG}, RPG={RPG}, APG={APG}, GP={GP}",
                    resp.Season, resp.PPG, resp.RPG, resp.APG, resp.GamesPlayed);
            }

            return responses;
        }

        public async Task<PlayerCareerStatsResponse?> GetPlayerCareerStatsAsync(int playerId)
        {
            var stats = await _statsRepository.GetCareerStatsAsync(playerId);

            if (stats == null)
            {
                _logger.LogWarning(
                    "Estatísticas de carreira não encontradas para jogador {PlayerId}",
                    playerId);
                return null;
            }

            return _mapper.Map<PlayerCareerStatsResponse>(stats);
        }

        public async Task<List<PlayerRecentGameResponse>> GetPlayerRecentGamesAsync(int playerId, int limit)
        {
            var games = await _statsRepository.GetRecentGamesAsync(playerId, Math.Min(limit, 20));
            return _mapper.Map<List<PlayerRecentGameResponse>>(games);
        }

        public async Task<PlayerRecentGameResponse?> GetPlayerGameStatsAsync(int playerId, int gameId)
        {
            var stats = await _statsRepository.GetGameStatsAsync(playerId, gameId);

            if (stats == null)
            {
                _logger.LogWarning(
                    "Estatísticas do jogo não encontradas para jogador {PlayerId}, jogo {GameId}",
                    playerId,
                    gameId);
                return null;
            }

            return _mapper.Map<PlayerRecentGameResponse>(stats);
        }

        public async Task<PlayerComparisonResponse?> ComparePlayersAsync(int player1Id, int player2Id, int? season = null)
        {
            try
            {
                var request1 = new PlayerStatsRequest
                {
                    PlayerId = player1Id,
                    Season = season,
                    IncludeCareer = true,
                    IncludeCurrentSeason = true,
                    LastGames = 5
                };

                var request2 = new PlayerStatsRequest
                {
                    PlayerId = player2Id,
                    Season = season,
                    IncludeCareer = true,
                    IncludeCurrentSeason = true,
                    LastGames = 5
                };

                var player1Stats = await GetPlayerDetailedStatsAsync(request1);
                var player2Stats = await GetPlayerDetailedStatsAsync(request2);

                if (player1Stats == null || player2Stats == null)
                {
                    _logger.LogWarning(
                        "Não foi possível comparar - Jogador1: {Player1Found}, Jogador2: {Player2Found}",
                        player1Stats != null,
                        player2Stats != null);
                    return null;
                }

                return new PlayerComparisonResponse
                {
                    Player1 = player1Stats,
                    Player2 = player2Stats,
                    Comparison = CompareStats(player1Stats, player2Stats)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao comparar jogadores {Player1Id} e {Player2Id}", player1Id, player2Id);
                throw;
            }
        }

        public async Task<StatLeadersResponse> GetStatLeadersAsync(int season, int minGames, int limit)
        {
            var cacheKey = $"stat_leaders_{season}_{minGames}_{limit}";

            if (_cache.TryGetValue(cacheKey, out StatLeadersResponse? cached))
            {
                return cached!;
            }

            var scoringLeaders = await _statsRepository.GetScoringLeadersAsync(season, minGames, limit);
            var reboundLeaders = await _statsRepository.GetReboundLeadersAsync(season, minGames, limit);
            var assistLeaders = await _statsRepository.GetAssistLeadersAsync(season, minGames, limit);

            var response = new StatLeadersResponse
            {
                ScoringLeaders = MapLeaders(scoringLeaders),
                ReboundLeaders = MapLeaders(reboundLeaders),
                AssistLeaders = MapLeaders(assistLeaders),
                LastUpdated = DateTime.UtcNow
            };

            _cache.Set(cacheKey, response, TimeSpan.FromMinutes(30));
            return response;
        }

        public async Task<bool> UpdatePlayerCareerStatsAsync(int playerId)
        {
            var seasons = await _statsRepository.GetAllSeasonStatsAsync(playerId);
            if (!seasons.Any())
            {
                return false;
            }

            var careerStats = new PlayerCareerStats
            {
                PlayerId = playerId,
                TotalSeasons = seasons.Count(),
                TotalGames = seasons.Sum(s => s.GamesPlayed),
                TotalGamesStarted = seasons.Sum(s => s.GamesStarted),
                TotalMinutes = seasons.Sum(s => s.MinutesPlayed),
                TotalPoints = seasons.Sum(s => s.Points),
                TotalRebounds = seasons.Sum(s => s.TotalRebounds),
                TotalAssists = seasons.Sum(s => s.Assists),
                TotalSteals = seasons.Sum(s => s.Steals),
                TotalBlocks = seasons.Sum(s => s.Blocks),
                TotalTurnovers = seasons.Sum(s => s.Turnovers),
                TotalFieldGoalsMade = seasons.Sum(s => s.FieldGoalsMade),
                TotalFieldGoalsAttempted = seasons.Sum(s => s.FieldGoalsAttempted),
                TotalThreePointersMade = seasons.Sum(s => s.ThreePointersMade),
                TotalThreePointersAttempted = seasons.Sum(s => s.ThreePointersAttempted),
                TotalFreeThrowsMade = seasons.Sum(s => s.FreeThrowsMade),
                TotalFreeThrowsAttempted = seasons.Sum(s => s.FreeThrowsAttempted),
                LastGameDate = DateTime.Today
            };

            // Calcular médias por jogo
            if (careerStats.TotalGames > 0)
            {
                careerStats.CareerPPG = Math.Round((decimal)careerStats.TotalPoints / careerStats.TotalGames, 2);
                careerStats.CareerRPG = Math.Round((decimal)careerStats.TotalRebounds / careerStats.TotalGames, 2);
                careerStats.CareerAPG = Math.Round((decimal)careerStats.TotalAssists / careerStats.TotalGames, 2);
            }

            // Calcular porcentagens
            if (careerStats.TotalFieldGoalsAttempted > 0)
            {
                careerStats.CareerFgPercentage = Math.Round(
                    (decimal)careerStats.TotalFieldGoalsMade / careerStats.TotalFieldGoalsAttempted * 100,
                    1);
            }

            if (careerStats.TotalThreePointersAttempted > 0)
            {
                careerStats.Career3PtPercentage = Math.Round(
                    (decimal)careerStats.TotalThreePointersMade / careerStats.TotalThreePointersAttempted * 100,
                    1);
            }

            if (careerStats.TotalFreeThrowsAttempted > 0)
            {
                careerStats.CareerFtPercentage = Math.Round(
                    (decimal)careerStats.TotalFreeThrowsMade / careerStats.TotalFreeThrowsAttempted * 100,
                    1);
            }

            // Calcular career highs
            try
            {
                var allGames = await _statsRepository.GetAllPlayerGamesAsync(playerId);
                if (allGames.Any())
                {
                    careerStats.HighestPointsGame = allGames.Max(g => g.Points);
                    careerStats.HighestReboundsGame = allGames.Max(g => g.TotalRebounds);
                    careerStats.HighestAssistsGame = allGames.Max(g => g.Assists);
                }
                else
                {
                    _logger.LogWarning(
                        "Nenhuma estatística de jogo encontrada para jogador {PlayerId}, usando máximos das temporadas",
                        playerId);
                    careerStats.HighestPointsGame = (int)Math.Ceiling(seasons.Max(s => s.PPG));
                    careerStats.HighestReboundsGame = (int)Math.Ceiling(seasons.Max(s => s.RPG));
                    careerStats.HighestAssistsGame = (int)Math.Ceiling(seasons.Max(s => s.APG));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao calcular career highs para jogador {PlayerId}", playerId);
            }

            var cachePattern = $"player_stats_{playerId}_";
            _cache.Remove(cachePattern);

            return await _statsRepository.UpsertCareerStatsAsync(careerStats);
        }

        private ComparisonStats CompareStats(PlayerDetailedResponse p1, PlayerDetailedResponse p2)
        {
            var comparison = new ComparisonStats
            {
                StatsDifference = new Dictionary<string, decimal>()
            };

            if (p1.CurrentSeasonStats != null && p2.CurrentSeasonStats != null)
            {
                var s1 = p1.CurrentSeasonStats;
                var s2 = p2.CurrentSeasonStats;

                comparison.BetterScorer = s1.PPG > s2.PPG ? p1.FullName : p2.FullName;
                comparison.BetterRebounder = s1.RPG > s2.RPG ? p1.FullName : p2.FullName;
                comparison.BetterPasser = s1.APG > s2.APG ? p1.FullName : p2.FullName;

                comparison.StatsDifference["PPG"] = Math.Abs(s1.PPG - s2.PPG);
                comparison.StatsDifference["RPG"] = Math.Abs(s1.RPG - s2.RPG);
                comparison.StatsDifference["APG"] = Math.Abs(s1.APG - s2.APG);
            }

            return comparison;
        }

        private List<StatLeader> MapLeaders(IEnumerable<dynamic> leaders)
        {
            var result = new List<StatLeader>();
            int rank = 1;

            foreach (var leader in leaders)
            {
                result.Add(new StatLeader
                {
                    Rank = rank++,
                    PlayerId = leader.id,
                    PlayerName = $"{leader.first_name} {leader.last_name}",
                    Team = leader.team_abbreviation ?? "N/A",
                    Value = leader.value,
                    GamesPlayed = leader.games_played
                });
            }

            return result;
        }

        // ===== MÉTODOS AUXILIARES PARA AUTO-SYNC =====

        private async Task<PlayerSeasonStats?> MapAndSaveEspnStatsAsync(Player player, Core.DTOs.External.ESPN.EspnPlayerStatsDto espnStats, int season)
        {
            try
            {
                // Parsear estatísticas do formato ESPN
                var parsedStats = ParseEspnStatsDto(espnStats);
                if (!parsedStats.HasValue)
                {
                    _logger.LogWarning("Não foi possível parsear stats da ESPN para jogador {PlayerId}", player.Id);
                    return null;
                }

                var stats = parsedStats.Value;

                // Criar entidade PlayerSeasonStats
                var seasonStats = new PlayerSeasonStats
                {
                    PlayerId = player.Id,
                    Season = season,
                    TeamId = player.TeamId,

                    // Jogos
                    GamesPlayed = stats.GamesPlayed,
                    GamesStarted = stats.GamesStarted,
                    MinutesPlayed = stats.MinutesPlayed,

                    // Pontos
                    Points = stats.Points,
                    FieldGoalsMade = stats.FieldGoalsMade,
                    FieldGoalsAttempted = stats.FieldGoalsAttempted,
                    FieldGoalPercentage = stats.FieldGoalsAttempted > 0
                        ? Math.Round((decimal)stats.FieldGoalsMade / stats.FieldGoalsAttempted, 3)
                        : null,

                    // 3 pontos
                    ThreePointersMade = stats.ThreePointersMade,
                    ThreePointersAttempted = stats.ThreePointersAttempted,
                    ThreePointPercentage = stats.ThreePointersAttempted > 0
                        ? Math.Round((decimal)stats.ThreePointersMade / stats.ThreePointersAttempted, 3)
                        : null,

                    // Lances livres
                    FreeThrowsMade = stats.FreeThrowsMade,
                    FreeThrowsAttempted = stats.FreeThrowsAttempted,
                    FreeThrowPercentage = stats.FreeThrowsAttempted > 0
                        ? Math.Round((decimal)stats.FreeThrowsMade / stats.FreeThrowsAttempted, 3)
                        : null,

                    // Rebotes
                    OffensiveRebounds = stats.OffensiveRebounds,
                    DefensiveRebounds = stats.DefensiveRebounds,
                    TotalRebounds = stats.TotalRebounds,

                    // Outras
                    Assists = stats.Assists,
                    Steals = stats.Steals,
                    Blocks = stats.Blocks,
                    Turnovers = stats.Turnovers,
                    PersonalFouls = stats.PersonalFouls,

                    // Médias
                    AvgPoints = stats.GamesPlayed > 0 ? Math.Round((decimal)stats.Points / stats.GamesPlayed, 1) : 0,
                    AvgRebounds = stats.GamesPlayed > 0 ? Math.Round((decimal)stats.TotalRebounds / stats.GamesPlayed, 1) : 0,
                    AvgAssists = stats.GamesPlayed > 0 ? Math.Round((decimal)stats.Assists / stats.GamesPlayed, 1) : 0,
                    AvgMinutes = stats.GamesPlayed > 0 ? Math.Round(stats.MinutesPlayed / stats.GamesPlayed, 1) : 0
                };

                // Salvar no banco (UPSERT)
                await _statsRepository.UpsertSeasonStatsAsync(seasonStats);

                _logger.LogInformation(
                    "Stats da temporada {Season} SALVOS no banco para jogador {PlayerId}",
                    season,
                    player.Id);

                return seasonStats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao mapear e salvar stats da ESPN para jogador {PlayerId}, temporada {Season}", player.Id, season);
                return null;
            }
        }

        private (int GamesPlayed, int GamesStarted, decimal MinutesPlayed, int Points, int FieldGoalsMade,
                 int FieldGoalsAttempted, int ThreePointersMade, int ThreePointersAttempted, int FreeThrowsMade,
                 int FreeThrowsAttempted, int OffensiveRebounds, int DefensiveRebounds, int TotalRebounds,
                 int Assists, int Steals, int Blocks, int Turnovers, int PersonalFouls)? ParseEspnStatsDto(
            Core.DTOs.External.ESPN.EspnPlayerStatsDto espnStats)
        {
            try
            {
                // ESPN retorna stats em formato aninhado
                // Vamos pegar do "splits.categories"
                var categories = espnStats.Splits?.Categories;
                if (categories == null || !categories.Any())
                {
                    _logger.LogDebug("Nenhuma categoria de stats encontrada no DTO da ESPN");
                    return null;
                }

                // Criar dicionário para facilitar busca
                var statsDict = new Dictionary<string, decimal>();
                foreach (var category in categories)
                {
                    foreach (var stat in category.Stats ?? new List<Core.DTOs.External.ESPN.EspnStatDto>())
                    {
                        if (!string.IsNullOrEmpty(stat.Name) && stat.Value != 0)
                        {
                            statsDict[stat.Name.ToLower()] = (decimal)stat.Value;
                        }
                    }
                }

                // Helper para buscar valores
                decimal GetStatValue(string name) => statsDict.ContainsKey(name.ToLower()) ? statsDict[name.ToLower()] : 0;
                int GetIntValue(string name) => (int)Math.Round(GetStatValue(name));

                return (
                    GamesPlayed: GetIntValue("gamesPlayed"),
                    GamesStarted: GetIntValue("gamesStarted"),
                    MinutesPlayed: GetStatValue("avgMinutes") * GetIntValue("gamesPlayed"),
                    Points: GetIntValue("points"),
                    FieldGoalsMade: GetIntValue("fieldGoalsMade"),
                    FieldGoalsAttempted: GetIntValue("fieldGoalsAttempted"),
                    ThreePointersMade: GetIntValue("threePointFieldGoalsMade"),
                    ThreePointersAttempted: GetIntValue("threePointFieldGoalsAttempted"),
                    FreeThrowsMade: GetIntValue("freeThrowsMade"),
                    FreeThrowsAttempted: GetIntValue("freeThrowsAttempted"),
                    OffensiveRebounds: GetIntValue("offensiveRebounds"),
                    DefensiveRebounds: GetIntValue("defensiveRebounds"),
                    TotalRebounds: GetIntValue("totalRebounds"),
                    Assists: GetIntValue("assists"),
                    Steals: GetIntValue("steals"),
                    Blocks: GetIntValue("blocks"),
                    Turnovers: GetIntValue("turnovers"),
                    PersonalFouls: GetIntValue("fouls")
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao parsear stats do DTO da ESPN");
                return null;
            }
        }


    }
}
