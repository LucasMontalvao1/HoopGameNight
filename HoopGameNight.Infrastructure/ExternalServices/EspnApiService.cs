using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HoopGameNight.Core.DTOs.External;
using HoopGameNight.Core.DTOs.External.ESPN;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Models.Entities;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace HoopGameNight.Infrastructure.ExternalServices
{
    /// <summary>
    /// Implementação da integração com a API da ESPN para consumo de dados da NBA (jogos, times, elencos e estatísticas).
    /// Utiliza tanto a Site API (V2) quanto a Core API da ESPN para obter informações detalhadas.
    /// </summary>
    public class EspnApiService : IEspnApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<EspnApiService> _logger;
        private readonly ICacheService _cacheService;
        private readonly IPlayerStatsRepository _playerStatsRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly ITeamRepository _teamRepository;

        private const string BASE_URL = "https://site.api.espn.com/apis/site/v2/sports/basketball/nba";
        private const string CORE_API_BASE_URL = "https://sports.core.api.espn.com/v2/sports/basketball/leagues/nba";

        public EspnApiService(
            HttpClient httpClient,
            ILogger<EspnApiService> logger,
            ICacheService cacheService,
            IPlayerStatsRepository playerStatsRepository,
            IPlayerRepository playerRepository,
            ITeamRepository teamRepository)
        {
            _httpClient = httpClient;
            _logger = logger;
            _cacheService = cacheService;
            _playerStatsRepository = playerStatsRepository;
            _playerRepository = playerRepository;
            _teamRepository = teamRepository;

            _httpClient.DefaultRequestHeaders.Add("User-Agent", "HoopGameNight/1.0");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        #region Games/Events

        public async Task<List<EspnGameDto>> GetGamesByDateAsync(DateTime date)
        {
            try
            {
                var dateStr = date.ToString("yyyyMMdd");
                var url = $"{BASE_URL}/scoreboard?dates={dateStr}";

                _logger.LogInformation("Fetching ESPN games for date: {Date}", dateStr);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var games = ParseEspnResponse(json);

                _logger.LogInformation("Found {Count} games from ESPN for {Date}", games.Count, date.ToShortDateString());

                return games;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN games for date: {Date}", date);
                return new List<EspnGameDto>();
            }
        }

        public async Task<List<EspnGameDto>> GetFutureGamesAsync(int days = 7)
        {
            var games = new List<EspnGameDto>();

            _logger.LogInformation("Fetching future games for next {Days} days", days);

            try
            {
                var dates = new List<string>();
                for (int i = 1; i <= days; i++)
                {
                    dates.Add(DateTime.Today.AddDays(i).ToString("yyyyMMdd"));
                }

                var datesParam = string.Join(",", dates);
                var url = $"{BASE_URL}/scoreboard?dates={datesParam}";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                games = ParseEspnResponse(json);

                _logger.LogInformation("Found {Count} future games from ESPN", games.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching future games from ESPN");
            }

            return games;
        }

        public async Task<EspnGameDetailDto?> GetGameEventAsync(string gameId)
        {
            try
            {
                var url = $"{BASE_URL}/summary?event={gameId}";
                _logger.LogInformation("Fetching ESPN game event: {GameId}", gameId);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var gameDetail = JsonSerializer.Deserialize<EspnGameDetailDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return gameDetail;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN game event: {GameId}", gameId);
                return null;
            }
        }

        public async Task<EspnEventDto?> GetCoreEventAsync(string gameId)
        {
            try
            {
                var url = $"{CORE_API_BASE_URL}/events/{gameId}";
                _logger.LogInformation("Fetching ESPN core event: {GameId}", gameId);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var eventData = JsonSerializer.Deserialize<EspnEventDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return eventData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN core event: {GameId}", gameId);
                return null;
            }
        }

        public async Task<EspnBoxscoreDto?> GetGameBoxscoreAsync(string gameId)
        {
            try
            {
                // O endpoint dedicado de boxscore (games/{id}/boxscore) frequentemente retorna 404 (Not Found).
                // O endpoint 'summary' (acessado via GetGameEventAsync) retorna consistentemente os dados do boxscore.
                var detail = await GetGameEventAsync(gameId);
                return detail?.Boxscore;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN game boxscore via summary: {GameId}", gameId);
                return null;
            }
        }

        public async Task<EspnPlaysDto?> GetGamePlaysAsync(string gameId)
        {
            try
            {
                var url = $"{CORE_API_BASE_URL}/events/{gameId}/competitions/{gameId}/plays";
                _logger.LogInformation("Fetching ESPN game plays: {GameId}", gameId);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var plays = JsonSerializer.Deserialize<EspnPlaysDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return plays;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN game plays: {GameId}", gameId);
                return null;
            }
        }

        public async Task<EspnGameLeadersDto?> GetGameLeadersAsync(string gameId)
        {
            try
            {
                var url = $"{CORE_API_BASE_URL}/events/{gameId}/competitions/{gameId}/leaders";
                _logger.LogInformation("Fetching ESPN game leaders: {GameId}", gameId);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var leaders = JsonSerializer.Deserialize<EspnGameLeadersDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return leaders;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN game leaders: {GameId}", gameId);
                return null;
            }
        }

        public async Task<EspnGameStatusDto?> GetGameStatusAsync(string gameId)
        {
            try
            {
                var url = $"{CORE_API_BASE_URL}/events/{gameId}/competitions/{gameId}/status";
                _logger.LogInformation("Fetching ESPN game status: {GameId}", gameId);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var status = JsonSerializer.Deserialize<EspnGameStatusDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN game status: {GameId}", gameId);
                return null;
            }
        }

        public async Task<EspnGameRosterDto?> GetGameHomeRosterAsync(string gameId)
        {
            try
            {
                var url = $"{CORE_API_BASE_URL}/events/{gameId}/competitions/{gameId}/competitors/home/roster";
                _logger.LogInformation("Fetching ESPN game home roster: {GameId}", gameId);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var roster = JsonSerializer.Deserialize<EspnGameRosterDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return roster;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN game home roster: {GameId}", gameId);
                return null;
            }
        }

        public async Task<EspnGameRosterDto?> GetGameAwayRosterAsync(string gameId)
        {
            try
            {
                var url = $"{CORE_API_BASE_URL}/events/{gameId}/competitions/{gameId}/competitors/away/roster";
                _logger.LogInformation("Fetching ESPN game away roster: {GameId}", gameId);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var roster = JsonSerializer.Deserialize<EspnGameRosterDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return roster;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN game away roster: {GameId}", gameId);
                return null;
            }
        }

        #endregion

        #region Teams

        public async Task<List<EspnTeamDto>> GetAllTeamsAsync()
        {
            try
            {
                var url = $"{BASE_URL}/teams?limit=100";
                _logger.LogInformation("Fetching all NBA teams from ESPN: {Url}", url);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                var teams = new List<EspnTeamDto>();

                if (root.TryGetProperty("sports", out var sports))
                {
                    foreach (var sport in sports.EnumerateArray())
                    {
                        if (sport.TryGetProperty("leagues", out var leagues))
                        {
                            foreach (var league in leagues.EnumerateArray())
                            {
                                if (league.TryGetProperty("teams", out var teamsArray))
                                {
                                    foreach (var teamWrapper in teamsArray.EnumerateArray())
                                    {
                                        if (teamWrapper.TryGetProperty("team", out var teamElement))
                                        {
                                            var team = new EspnTeamDto
                                            {
                                                Id = teamElement.GetPropertySafe("id"),
                                                Uid = teamElement.GetPropertySafe("uid"),
                                                Slug = teamElement.GetPropertySafe("slug"),
                                                Location = teamElement.GetPropertySafe("location"),
                                                Name = teamElement.GetPropertySafe("name"),
                                                Abbreviation = teamElement.GetPropertySafe("abbreviation"),
                                                DisplayName = teamElement.GetPropertySafe("displayName"),
                                                ShortDisplayName = teamElement.GetPropertySafe("shortDisplayName"),
                                                Color = teamElement.GetPropertySafe("color"),
                                                AlternateColor = teamElement.GetPropertySafe("alternateColor"),
                                                IsActive = teamElement.TryGetProperty("isActive", out var isActive) && isActive.GetBoolean(),
                                                IsAllStar = teamElement.TryGetProperty("isAllStar", out var isAllStar) && isAllStar.GetBoolean()
                                            };

                                            if (teamElement.TryGetProperty("logos", out var logos))
                                            {
                                                foreach (var logo in logos.EnumerateArray())
                                                {
                                                    team.Logos.Add(new EspnLogoDto
                                                    {
                                                        Href = logo.GetPropertySafe("href"),
                                                        Width = logo.TryGetProperty("width", out var width) ? width.GetInt32() : 0,
                                                        Height = logo.TryGetProperty("height", out var height) ? height.GetInt32() : 0,
                                                        Alt = logo.GetPropertySafe("alt"),
                                                        Rel = logo.TryGetProperty("rel", out var rel)
                                                            ? rel.EnumerateArray().Select(r => r.GetString() ?? "").ToList()
                                                            : new List<string>()
                                                    });
                                                }
                                            }

                                            if (team.IsActive && !team.IsAllStar)
                                            {
                                                teams.Add(team);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                _logger.LogInformation("Found {Count} active NBA teams from ESPN", teams.Count);
                return teams;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching teams from ESPN");
                return new List<EspnTeamDto>();
            }
        }

        public async Task<EspnTeamStatisticsDto?> GetTeamStatisticsAsync(string teamId)
        {
            var cacheKey = $"team_stats_{teamId}";

            var cached = await _cacheService.GetAsync<EspnTeamStatisticsDto>(cacheKey);
            if (cached != null) return cached;

            try
            {
                var url = $"{BASE_URL}/teams/{teamId}/statistics";
                _logger.LogInformation("Fetching ESPN team statistics: {TeamId}", teamId);

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                var statistics = JsonSerializer.Deserialize<EspnTeamStatisticsDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (statistics != null)
                {
                    await _cacheService.SetAsync(cacheKey, statistics, TimeSpan.FromHours(12));
                }

                return statistics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN team statistics: {TeamId}", teamId);
                return null;
            }
        }

        public async Task<EspnTeamLeadersDto?> GetTeamLeadersAsync(string teamId)
        {
            var cacheKey = $"team_leaders_{teamId}";

            var cached = await _cacheService.GetAsync<EspnTeamLeadersDto>(cacheKey);
            if (cached != null) return cached;

            try
            {
                var url = $"{BASE_URL}/teams/{teamId}/leaders";
                _logger.LogInformation("Fetching ESPN team leaders: {TeamId}", teamId);

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                var leaders = JsonSerializer.Deserialize<EspnTeamLeadersDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (leaders != null)
                {
                    await _cacheService.SetAsync(cacheKey, leaders, TimeSpan.FromHours(12));
                }

                return leaders;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN team leaders: {TeamId}", teamId);
                return null;
            }
        }

        public async Task<List<EspnPlayerDetailsDto>> GetTeamRosterAsync(string teamId)
        {
            try
            {
                var url = $"{BASE_URL}/teams/{teamId}/roster";
                _logger.LogInformation("Fetching ESPN team roster for team: {TeamId}", teamId);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                var players = new List<EspnPlayerDetailsDto>();

                if (root.TryGetProperty("athletes", out var athletes))
                {
                    foreach (var athleteElement in athletes.EnumerateArray())
                    {
                        var player = ParsePlayerFromRoster(athleteElement);
                        if (player != null)
                        {
                            players.Add(player);
                        }
                    }
                }

                _logger.LogInformation("Found {Count} players in team {TeamId} roster", players.Count, teamId);
                return players;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN team roster for team: {TeamId}", teamId);
                return new List<EspnPlayerDetailsDto>();
            }
        }

        #endregion

        #region Standings

        public async Task<EspnStandingsDto?> GetConferenceStandingsAsync()
        {
            try
            {
                var url = $"{BASE_URL}/standings?type=conference";
                _logger.LogInformation("Fetching ESPN conference standings");

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var standings = JsonSerializer.Deserialize<EspnStandingsDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return standings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN conference standings");
                return null;
            }
        }

        public async Task<EspnStandingsDto?> GetDivisionStandingsAsync()
        {
            try
            {
                var url = $"{BASE_URL}/standings?type=division";
                _logger.LogInformation("Fetching ESPN division standings");

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var standings = JsonSerializer.Deserialize<EspnStandingsDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return standings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN division standings");
                return null;
            }
        }

        #endregion

        #region Content/News

        public async Task<EspnInjuriesDto?> GetInjuriesAsync()
        {
            try
            {
                var url = $"{BASE_URL}/injuries";
                _logger.LogInformation("Fetching ESPN injuries");

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var injuries = JsonSerializer.Deserialize<EspnInjuriesDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return injuries;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN injuries");
                return null;
            }
        }

        public async Task<EspnTransactionsDto?> GetTransactionsAsync()
        {
            try
            {
                var url = $"{BASE_URL}/transactions";
                _logger.LogInformation("Fetching ESPN transactions");

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var transactions = JsonSerializer.Deserialize<EspnTransactionsDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return transactions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN transactions");
                return null;
            }
        }

        #endregion

        #region Players

        public async Task<List<EspnAthleteRefDto>> GetAllPlayersAsync()
        {
            try
            {
                var url = $"{CORE_API_BASE_URL}/athletes?lang=en&region=us";
                _logger.LogInformation("Fetching all ESPN athletes from: {Url}", url);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                var athletes = new List<EspnAthleteRefDto>();

                if (root.TryGetProperty("items", out var items))
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        if (item.TryGetProperty("$ref", out var refProperty))
                        {
                            athletes.Add(new EspnAthleteRefDto
                            {
                                Ref = refProperty.GetString() ?? ""
                            });
                        }
                    }
                }

                _logger.LogInformation("Found {Count} athletes from ESPN", athletes.Count);
                return athletes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN athletes");
                return new List<EspnAthleteRefDto>();
            }
        }

        public async Task<EspnPlayerDetailsDto?> GetPlayerDetailsAsync(string playerId)
        {
            var cacheKey = $"player_details_{playerId}";

            var cached = await _cacheService.GetAsync<EspnPlayerDetailsDto>(cacheKey);
            if (cached != null) return cached;

            try
            {
                var url = $"{CORE_API_BASE_URL}/athletes/{playerId}?lang=en&region=us";
                _logger.LogInformation("Fetching ESPN player details for ID: {PlayerId}", playerId);

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                var playerDetails = JsonSerializer.Deserialize<EspnPlayerDetailsDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (playerDetails != null)
                {
                    await _cacheService.SetAsync(cacheKey, playerDetails, TimeSpan.FromDays(1));
                }

                return playerDetails;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN player details for ID: {PlayerId}", playerId);
                return null;
            }
        }

        public async Task<EspnPlayerStatsDto?> GetPlayerStatsAsync(string playerId)
        {
            return await GetPlayerSeasonStatsAsync(playerId, DateTime.Now.Month >= 10 ? DateTime.Now.Year : DateTime.Now.Year - 1);
        }

        public async Task<EspnPlayerSplitsDto?> GetPlayerSplitsAsync(string playerId)
        {
            try
            {
                var url = $"https://site.api.espn.com/apis/common/v3/sports/basketball/nba/athletes/{playerId}/splits";
                _logger.LogInformation("Fetching ESPN player splits for ID: {PlayerId}", playerId);

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                     _logger.LogWarning("Failed to fetch splits for player {PlayerId}. Status: {StatusCode}", playerId, response.StatusCode);
                     return null;
                }

                // response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var splits = JsonSerializer.Deserialize<EspnPlayerSplitsDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return splits;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN player splits for ID: {PlayerId}", playerId);
                return null;
            }
        }

        public async Task<EspnPlayerGamelogDto?> GetPlayerGamelogAsync(string playerId)
        {
            var cacheKey = $"player_gamelog_v1_{playerId}";

            var cached = await _cacheService.GetAsync<EspnPlayerGamelogDto>(cacheKey);
            if (cached != null) return cached;

            try
            {
                var url = $"https://site.api.espn.com/apis/common/v3/sports/basketball/nba/athletes/{playerId}/gamelog";
                _logger.LogInformation("Fetching ESPN player gamelog for ID: {PlayerId}", playerId);

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to fetch gamelog for player {PlayerId}. Status: {StatusCode}, Reason: {Reason}", playerId, response.StatusCode, response.ReasonPhrase);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var gamelog = JsonSerializer.Deserialize<EspnPlayerGamelogDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (gamelog != null)
                {
                    await _cacheService.SetAsync(cacheKey, gamelog, TimeSpan.FromHours(6));
                }

                return gamelog;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN player gamelog for ID: {PlayerId}", playerId);
                return null;
            }
        }

        public async Task<EspnPlayerStatsDto?> GetPlayerSeasonStatsAsync(string playerId, int season, int seasonType = 2)
        {
            var cacheKey = $"player_season_stats_v18_{playerId}_{season}_{seasonType}"; // Include seasonType in cache

            var cached = await _cacheService.GetAsync<EspnPlayerStatsDto>(cacheKey);
            if (cached != null) return cached;

            try
            {
                var url = $"{CORE_API_BASE_URL}/seasons/{season}/types/{seasonType}/athletes/{playerId}/statistics/0?lang=en&region=us";
                _logger.LogInformation("Fetching ESPN player season stats for ID: {PlayerId}, Season: {Season}, Type: {SeasonType}", playerId, season, seasonType);

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                var seasonStats = JsonSerializer.Deserialize<EspnPlayerStatsDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (seasonStats != null)
                {
                    // Set the season type ID
                    seasonStats.SeasonTypeId = seasonType;

                    // Always ensure Season object exists because the stats endpoint usually doesn't include it in the root
                    if (seasonStats.Season == null)
                    {
                        seasonStats.Season = new EspnSeasonRefDto();
                    }
                    
                    if (seasonStats.Season.Year == 0)
                    {
                        seasonStats.Season.Year = season;
                    }

                    // Always ensure Team object exists if stats are present
                    if (seasonStats.Team == null)
                    {
                        seasonStats.Team = new EspnTeamRefDto();
                    }

                    // Enrich Team ID if missing from Ref
                    if (string.IsNullOrEmpty(seasonStats.Team.Id) && !string.IsNullOrEmpty(seasonStats.Team.Ref))
                    {
                        seasonStats.Team.Id = ExtractIdFromRef(seasonStats.Team.Ref, @"teams/(\d+)");
                    }

                    // Also check for season ID if year is 0
                    if (seasonStats.Season.Year == 0 && !string.IsNullOrEmpty(seasonStats.Season.Ref))
                    {
                        var yearStr = ExtractIdFromRef(seasonStats.Season.Ref, @"seasons/(\d+)");
                        if (int.TryParse(yearStr, out var y)) seasonStats.Season.Year = y;
                    }

                    // Ensure Year is set from parameter if still 0
                    if (seasonStats.Season.Year == 0 && season > 0) seasonStats.Season.Year = season;

                    await _cacheService.SetAsync(cacheKey, seasonStats, TimeSpan.FromHours(12));
                }

                return seasonStats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN player season stats for ID: {PlayerId}, Season: {Season}, Type: {SeasonType}", playerId, season, seasonType);
                return null;
            }
        }

        public async Task<List<EspnPlayerStatsDto>> GetPlayerCareerStatsAsync(string playerId)
        {
            var cacheKey = $"player_career_all_v19_{playerId}"; // Version bumped for playoffs support

            var cached = await _cacheService.GetAsync<List<EspnPlayerStatsDto>>(cacheKey);
            if (cached != null) return cached;

            try
            {
                var careerStats = new List<EspnPlayerStatsDto>();
                
                // 1. Fetch the list of seasons this player actually has
                var seasonsUrl = $"{CORE_API_BASE_URL}/athletes/{playerId}/seasons?lang=en&region=us";
                var seasonsResponse = await _httpClient.GetAsync(seasonsUrl);
                
                var years = new List<int>();
                if (seasonsResponse.IsSuccessStatusCode)
                {
                    var json = await seasonsResponse.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("items", out var items))
                    {
                        // Calculate current NBA season year
                        var now = DateTime.Now;
                        var currentSeasonYear = now.Month >= 10 ? now.Year + 1 : now.Year;
                        
                        foreach (var item in items.EnumerateArray())
                        {
                            var refUrl = item.GetPropertySafe("$ref");
                            var match = System.Text.RegularExpressions.Regex.Match(refUrl, @"seasons/(\d+)");
                            if (match.Success && int.TryParse(match.Groups[1].Value, out var year))
                            {
                                // Only add seasons that are not in the future
                                if (year <= currentSeasonYear)
                                {
                                    years.Add(year);
                                }
                                else
                                {
                                    _logger.LogDebug("Skipping future season {Year} for player {PlayerId}", year, playerId);
                                }
                            }
                        }
                    }
                }

                // Fallback if seasons list failed but we want to try anyway (legacy behavior)
                if (!years.Any())
                {
                    _logger.LogWarning("Could not fetch seasons list for player {PlayerId}. Falling back to iterative guess.", playerId);
                    var now = DateTime.Now;
                    var currentYear = now.Month >= 10 ? now.Year + 1 : now.Year;
                    
                    // Only go back in time, never forward (max 25 years back)
                    for (int i = 0; i < 25; i++)
                    {
                        var year = currentYear - i;
                        if (year >= 1946) // NBA founded in 1946
                        {
                            years.Add(year);
                        }
                    }
                }

                _logger.LogInformation("Fetching career stats for Player {PlayerId} for years: {Years}", playerId, string.Join(",", years.Take(5)));

                var tasks = new List<Task<EspnPlayerStatsDto?>>();
                foreach (var year in years)
                {
                    tasks.Add(GetPlayerSeasonStatsAsync(playerId, year, 2));
                    tasks.Add(GetPlayerSeasonStatsAsync(playerId, year, 3));
                }

                var results = await Task.WhenAll(tasks);
                foreach (var result in results)
                {
                    if (result != null && result.Season != null)
                    {
                        careerStats.Add(result);
                    }
                }

                if (careerStats.Any())
                {
                    await _cacheService.SetAsync(cacheKey, careerStats, TimeSpan.FromDays(1));
                }

                return careerStats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN player career stats for ID: {PlayerId}", playerId);
                return new List<EspnPlayerStatsDto>();
            }
        }

        public async Task<EspnPlayerStatsDto?> GetPlayerGameStatsAsync(string playerId, string gameId)
        {
            try
            {
                _logger.LogInformation("Fetching player game stats from boxscore for Player: {PlayerId}, Game: {GameId}", playerId, gameId);
                
                var boxscore = await GetGameBoxscoreAsync(gameId);
                if (boxscore?.Players == null) return null;

                foreach (var teamGroup in boxscore.Players)
                {
                    if (teamGroup.Statistics == null) continue;

                    foreach (var statGroup in teamGroup.Statistics)
                    {
                        if (statGroup.Athletes == null || statGroup.Keys == null) continue;

                        var athleteEntry = statGroup.Athletes.FirstOrDefault(a => a.Athlete?.Id == playerId);
                        if (athleteEntry != null && athleteEntry.Stats != null)
                        {
                            var result = new EspnPlayerStatsDto
                            {
                                Athlete = athleteEntry.Athlete ?? new EspnAthleteRefDto { Id = playerId },
                                Team = teamGroup.Team,
                                Splits = new EspnPlayerStatsSplitDto
                                {
                                    Categories = new List<EspnStatsCategoryDto>
                                    {
                                        new EspnStatsCategoryDto
                                        {
                                            Name = "all",
                                            Stats = new List<EspnStatDto>()
                                        }
                                    }
                                }
                            };

                            var targetStats = result.Splits.Categories[0].Stats;

                            for (int i = 0; i < statGroup.Keys.Count && i < athleteEntry.Stats.Count; i++)
                            {
                                var key = statGroup.Keys[i];
                                var valStr = athleteEntry.Stats[i];

                                // Handle split stats like "10-21"
                                if (valStr.Contains("-"))
                                {
                                    var parts = valStr.Split('-');
                                    if (parts.Length == 2)
                                    {
                                        if (key == "fieldGoalsMade-fieldGoalsAttempted")
                                        {
                                            if (double.TryParse(parts[0], out var made)) targetStats.Add(new EspnStatDto { Name = "fieldGoalsMade", Value = made });
                                            if (double.TryParse(parts[1], out var att)) targetStats.Add(new EspnStatDto { Name = "fieldGoalsAttempted", Value = att });
                                        }
                                        else if (key == "threePointFieldGoalsMade-threePointFieldGoalsAttempted")
                                        {
                                            if (double.TryParse(parts[0], out var made)) targetStats.Add(new EspnStatDto { Name = "threePointFieldGoalsMade", Value = made });
                                            if (double.TryParse(parts[1], out var att)) targetStats.Add(new EspnStatDto { Name = "threePointFieldGoalsAttempted", Value = att });
                                        }
                                        else if (key == "freeThrowsMade-freeThrowsAttempted")
                                        {
                                            if (double.TryParse(parts[0], out var made)) targetStats.Add(new EspnStatDto { Name = "freeThrowsMade", Value = made });
                                            if (double.TryParse(parts[1], out var att)) targetStats.Add(new EspnStatDto { Name = "freeThrowsAttempted", Value = att });
                                        }
                                    }
                                }
                                else
                                {
                                    // Clean non-numeric like "+14"
                                    var cleanVal = valStr.Replace("+", "");
                                    if (double.TryParse(cleanVal, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var val))
                                    {
                                        targetStats.Add(new EspnStatDto { Name = key, Value = val });
                                    }
                                }
                            }

                            return result;
                        }
                    }
                }

                _logger.LogWarning("Player {PlayerId} not found in boxscore for game {GameId}", playerId, gameId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN player game stats for Player ID: {PlayerId}, Game ID: {GameId}", playerId, gameId);
                return null;
            }
        }

        #endregion

        #region Seasons

        public async Task<EspnSeasonsDto?> GetSeasonsAsync()
        {
            try
            {
                var url = $"{CORE_API_BASE_URL}/seasons";
                _logger.LogInformation("Fetching ESPN seasons");

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var seasons = JsonSerializer.Deserialize<EspnSeasonsDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return seasons;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN seasons");
                return null;
            }
        }

        public async Task<EspnSeasonDto?> GetSeasonAsync(int season)
        {
            try
            {
                var url = $"{CORE_API_BASE_URL}/seasons/{season}";
                _logger.LogInformation("Fetching ESPN season: {Season}", season);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var seasonData = JsonSerializer.Deserialize<EspnSeasonDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return seasonData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN season: {Season}", season);
                return null;
            }
        }

        public async Task<EspnSeasonTypesDto?> GetSeasonTypesAsync(int season)
        {
            try
            {
                var url = $"{CORE_API_BASE_URL}/seasons/{season}/types";
                _logger.LogInformation("Fetching ESPN season types: {Season}", season);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var types = JsonSerializer.Deserialize<EspnSeasonTypesDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return types;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN season types: {Season}", season);
                return null;
            }
        }

        public async Task<EspnSeasonEventsDto?> GetSeasonEventsAsync(int season, int seasonType = 2)
        {
            try
            {
                var url = $"{CORE_API_BASE_URL}/seasons/{season}/types/{seasonType}/events";
                _logger.LogInformation("Fetching ESPN season events: {Season}, Type: {Type}", season, seasonType);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var events = JsonSerializer.Deserialize<EspnSeasonEventsDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return events;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN season events: {Season}, Type: {Type}", season, seasonType);
                return null;
            }
        }

        public async Task<EspnSeasonStandingsDto?> GetSeasonStandingsAsync(int season, int seasonType = 2)
        {
            try
            {
                var url = $"{CORE_API_BASE_URL}/seasons/{season}/types/{seasonType}/standings";
                _logger.LogInformation("Fetching ESPN season standings: {Season}, Type: {Type}", season, seasonType);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var standings = JsonSerializer.Deserialize<EspnSeasonStandingsDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return standings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN season standings: {Season}, Type: {Type}", season, seasonType);
                return null;
            }
        }

        #endregion

        #region Legacy/Deprecated

        public async Task<List<EspnGameDto>> GetTeamScheduleAsync(int teamId, DateTime startDate, DateTime endDate)
        {
            _logger.LogWarning("GetTeamScheduleAsync is DEPRECATED. Use GetGamesByDateAsync instead.");

            var allGames = new List<EspnGameDto>();
            var currentDate = startDate;

            while (currentDate <= endDate)
            {
                var dailyGames = await GetGamesByDateAsync(currentDate);
                allGames.AddRange(dailyGames);
                currentDate = currentDate.AddDays(1);
            }

            return allGames;
        }


        #endregion

        #region Leaders

        public async Task<EspnLeadersDto?> GetLeagueLeadersAsync()
        {
            var cacheKey = "league_leaders";

            var cached = await _cacheService.GetAsync<EspnLeadersDto>(cacheKey);
            if (cached != null) return cached;

            try
            {
                var url = $"{BASE_URL}/leaders";
                _logger.LogInformation("Fetching ESPN league leaders");

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                var leaders = JsonSerializer.Deserialize<EspnLeadersDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (leaders != null)
                {
                    await _cacheService.SetAsync(cacheKey, leaders, TimeSpan.FromHours(6));
                }

                return leaders;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN league leaders");
                return null;
            }
        }

        #endregion

        #region Utility

        public async Task<bool> IsApiAvailableAsync()
        {
            try
            {
                var url = $"{BASE_URL}/scoreboard";
                var response = await _httpClient.GetAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task<EspnPlayerStatsDto?> GetPlayerStatsFromUrl(string? url)
        {
            if (string.IsNullOrEmpty(url)) return null;

            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<EspnPlayerStatsDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result != null && result.SeasonType is JsonElement element)
                {
                    if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("id", out var idProp))
                    {
                        if (idProp.ValueKind == JsonValueKind.String && int.TryParse(idProp.GetString(), out var id))
                            result.SeasonTypeId = id;
                        else if (idProp.ValueKind == JsonValueKind.Number && idProp.TryGetInt32(out var id2))
                            result.SeasonTypeId = id2;
                    }
                    else if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var id))
                    {
                        result.SeasonTypeId = id;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching stats from URL: {Url}", url);
                return null;
            }
        }

        #endregion

        #region Parsing

        private List<EspnGameDto> ParseEspnResponse(string json)
        {
            var games = new List<EspnGameDto>();

            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (root.TryGetProperty("events", out var events))
                {
                    foreach (var eventElement in events.EnumerateArray())
                    {
                        try
                        {
                            var game = ParseGameEvent(eventElement);
                            if (game != null) games.Add(game);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error parsing individual game event");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing ESPN response");
            }

            return games;
        }

        private EspnPlayerDetailsDto? ParsePlayerFromRoster(JsonElement item)
        {
            try
            {
                var player = new EspnPlayerDetailsDto
                {
                    Id = item.GetPropertySafe("id"),
                    Uid = item.GetPropertySafe("uid"),
                    FirstName = item.GetPropertySafe("firstName"),
                    LastName = item.GetPropertySafe("lastName"),
                    FullName = item.GetPropertySafe("fullName"),
                    DisplayName = item.GetPropertySafe("displayName"),
                    DisplayWeight = item.GetPropertySafe("displayWeight"),
                    DisplayHeight = item.GetPropertySafe("displayHeight"),
                    DateOfBirth = item.GetPropertySafe("dateOfBirth"),
                    Jersey = item.GetPropertySafe("jersey")
                };

                if (item.TryGetProperty("weight", out var weight))
                    player.Weight = weight.TryGetDouble(out var w) ? w : 0;

                if (item.TryGetProperty("height", out var height))
                    player.Height = height.TryGetDouble(out var h) ? h : 0;

                if (item.TryGetProperty("age", out var age))
                    player.Age = age.TryGetInt32(out var a) ? a : 0;

                if (item.TryGetProperty("position", out var position))
                {
                    player.Position = new EspnPositionDto
                    {
                        Id = position.GetPropertySafe("id"),
                        Name = position.GetPropertySafe("name"),
                        DisplayName = position.GetPropertySafe("displayName"),
                        Abbreviation = position.GetPropertySafe("abbreviation")
                    };
                }

                if (item.TryGetProperty("active", out var active))
                    player.Active = active.GetBoolean();

                return player;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing player from roster");
                return null;
            }
        }

        private EspnGameDto? ParseGameEvent(JsonElement eventElement)
        {
            try
            {
                var game = new EspnGameDto
                {
                    Id = eventElement.GetProperty("id").GetString() ?? ""
                };

                if (eventElement.TryGetProperty("date", out var dateElement))
                {
                    if (dateElement.ValueKind == JsonValueKind.String)
                    {
                        var dateString = dateElement.GetString() ?? "";
                        game.Date = DateTime.TryParse(dateString, out var d) ? d : DateTime.Now;
                    }
                }

                if (eventElement.TryGetProperty("status", out var status))
                {
                    game.Status = status.GetPropertySafe("name") ?? "Scheduled";

                    if (status.TryGetProperty("period", out var period))
                        game.Period = period.GetInt32();

                    if (status.TryGetProperty("displayClock", out var clock))
                        game.TimeRemaining = clock.GetString();
                }

                if (eventElement.TryGetProperty("competitions", out var competitions))
                {
                    var competition = competitions.EnumerateArray().FirstOrDefault();
                    if (competition.ValueKind != JsonValueKind.Undefined &&
                        competition.TryGetProperty("competitors", out var competitors))
                    {
                        foreach (var competitor in competitors.EnumerateArray())
                        {
                            var isHome = competitor.GetPropertySafe("homeAway") == "home";

                            if (competitor.TryGetProperty("team", out var teamElement))
                            {
                                var teamId = teamElement.GetPropertySafe("id");
                                var teamName = teamElement.GetPropertySafe("displayName");
                                var teamAbbr = teamElement.GetPropertySafe("abbreviation");

                                if (isHome)
                                {
                                    game.HomeTeamId = teamId;
                                    game.HomeTeamName = teamName;
                                    game.HomeTeamAbbreviation = teamAbbr;
                                }
                                else
                                {
                                    game.AwayTeamId = teamId;
                                    game.AwayTeamName = teamName;
                                    game.AwayTeamAbbreviation = teamAbbr;
                                }
                            }

                            if (competitor.TryGetProperty("score", out var scoreElement))
                            {
                                if (scoreElement.ValueKind == JsonValueKind.String)
                                {
                                    if (int.TryParse(scoreElement.GetString(), out var scoreValue))
                                    {
                                        if (isHome) game.HomeTeamScore = scoreValue;
                                        else game.AwayTeamScore = scoreValue;
                                    }
                                }
                                else if (scoreElement.ValueKind == JsonValueKind.Number)
                                {
                                    if (isHome) game.HomeTeamScore = scoreElement.GetInt32();
                                    else game.AwayTeamScore = scoreElement.GetInt32();
                                }
                            }
                        }
                    }
                }

                if (eventElement.TryGetProperty("season", out var seasonElement))
                {
                    if (seasonElement.TryGetProperty("year", out var year))
                        game.Season = year.GetInt32();

                    if (seasonElement.TryGetProperty("type", out var type))
                        game.IsPostseason = type.GetInt32() == 3;
                }

                if (string.IsNullOrEmpty(game.HomeTeamId) || string.IsNullOrEmpty(game.AwayTeamId))
                    return null;

                return game;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing game event");
                return null;
            }
        }
        
        #endregion

        private static string ExtractIdFromRef(string? refUrl, string pattern)
        {
            if (string.IsNullOrEmpty(refUrl)) return string.Empty;
            var match = System.Text.RegularExpressions.Regex.Match(refUrl, pattern);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }
    }

    internal static class JsonElementExtensions
    {
        public static string GetPropertySafe(this JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? ""
                : "";
        }
    }
}