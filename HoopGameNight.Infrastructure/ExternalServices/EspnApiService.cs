using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HoopGameNight.Core.DTOs.External;
using HoopGameNight.Core.DTOs.External.ESPN;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HoopGameNight.Infrastructure.ExternalServices
{
    public class EspnApiService : IEspnApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<EspnApiService> _logger;
        private readonly ICacheService _cacheService;
        private readonly IEspnParser _espnParser;

        private const string BASE_URL = "https://site.api.espn.com/apis/site/v2/sports/basketball/nba";
        private const string CORE_API_BASE_URL = "https://sports.core.api.espn.com/v2/sports/basketball/leagues/nba";

        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        public EspnApiService(
            HttpClient httpClient,
            ILogger<EspnApiService> logger,
            ICacheService cacheService,
            IEspnParser espnParser)
        {
            _httpClient = httpClient;
            _logger = logger;
            _cacheService = cacheService;
            _espnParser = espnParser;

            _httpClient.DefaultRequestHeaders.Add("User-Agent", "HoopGameNight/1.0");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        #region Games/Events

        public async Task<List<EspnGameDto>> GetGamesByDateAsync(DateTime date, bool bypassCache = false)
        {
            var dateStr = date.ToString("yyyyMMdd");
            var cacheKey = $"espn_scoreboard_{dateStr}";
            
            // Se NÃO for bypass, tenta buscar do cache de 15s
            if (!bypassCache)
            {
                var cached = await _cacheService.GetAsync<List<EspnGameDto>>(cacheKey);
                if (cached != null) return cached;
            }

            // Se for bypass, adicionamos um timestamp para forçar refresh no CDN da ESPN
            var url = $"{BASE_URL}/scoreboard?dates={dateStr}";
            if (bypassCache)
            {
                url += $"&t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            }

            var result = await GetWithParsingAsync(url, 
                json => _espnParser.ParseScoreboardResponse(json), 
                $"games for date {dateStr} (Bypass: {bypassCache})");

            if (result != null && result.Any())
            {
                // Sempre salvamos no cache para beneficiar requisições subsequentes da UI
                await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromSeconds(15));
            }

            return result;
        }

        public async Task<List<EspnGameDto>> GetFutureGamesAsync(int days = 7)
        {
            var dates = Enumerable.Range(1, days).Select(i => DateTime.Today.AddDays(i).ToString("yyyyMMdd"));
            var url = $"{BASE_URL}/scoreboard?dates={string.Join(",", dates)}";
            return await GetWithParsingAsync(url, json => _espnParser.ParseScoreboardResponse(json), "future games");
        }

        public async Task<EspnGameDetailDto?> GetGameEventAsync(string gameId) =>
            await GetAsync<EspnGameDetailDto>($"{BASE_URL}/summary?event={gameId}", $"game event {gameId}");

        public async Task<EspnEventDto?> GetCoreEventAsync(string gameId) =>
            await GetAsync<EspnEventDto>($"{CORE_API_BASE_URL}/events/{gameId}", $"core event {gameId}");

        public async Task<EspnBoxscoreDto?> GetGameBoxscoreAsync(string gameId)
        {
            var detail = await GetGameEventAsync(gameId);
            return detail?.Boxscore;
        }

        public async Task<EspnGameLeadersDto?> GetGameLeadersAsync(string gameId) =>
            await GetAsync<EspnGameLeadersDto>($"{CORE_API_BASE_URL}/events/{gameId}/competitions/{gameId}/leaders", $"game leaders {gameId}");

        public async Task<EspnGameStatusDto?> GetGameStatusAsync(string gameId) =>
            await GetAsync<EspnGameStatusDto>($"{CORE_API_BASE_URL}/events/{gameId}/competitions/{gameId}/status", $"game status {gameId}");

        #endregion

        #region Teams

        public async Task<List<EspnTeamDto>> GetAllTeamsAsync()
        {
            try
            {
                var url = $"{BASE_URL}/teams?limit=100";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return new List<EspnTeamDto>();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                var teams = new List<EspnTeamDto>();
                var teamsArray = root.GetPropertySafe("sports").EnumerateArray()
                    .SelectMany(s => s.GetPropertySafe("leagues").EnumerateArray())
                    .SelectMany(l => l.GetPropertySafe("teams").EnumerateArray());

                foreach (var teamWrapper in teamsArray)
                {
                    if (teamWrapper.TryGetProperty("team", out var teamElement))
                    {
                        var team = JsonSerializer.Deserialize<EspnTeamDto>(teamElement.GetRawText(), JsonOptions);
                        if (team != null && team.IsActive && !team.IsAllStar) teams.Add(team);
                    }
                }
                return teams;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching teams");
                return new List<EspnTeamDto>();
            }
        }

        public async Task<EspnTeamStatisticsDto?> GetTeamStatisticsAsync(string teamId) =>
            await GetWithCacheAsync<EspnTeamStatisticsDto>($"{BASE_URL}/teams/{teamId}/statistics", $"team_stats_{teamId}", TimeSpan.FromHours(12));

        public async Task<EspnTeamLeadersDto?> GetTeamLeadersAsync(string teamId) =>
            await GetWithCacheAsync<EspnTeamLeadersDto>($"{BASE_URL}/teams/{teamId}/leaders", $"team_leaders_{teamId}", TimeSpan.FromHours(12));

        public async Task<List<EspnPlayerDetailsDto>> GetTeamRosterAsync(string teamId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BASE_URL}/teams/{teamId}/roster");
                if (!response.IsSuccessStatusCode) return new List<EspnPlayerDetailsDto>();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("athletes", out var athletes)) return new List<EspnPlayerDetailsDto>();

                return athletes.EnumerateArray()
                    .Select(a => _espnParser.ParsePlayerFromRoster(a))
                    .Where(p => p != null)
                    .Select(p => p!)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching roster for team {TeamId}", teamId);
                return new List<EspnPlayerDetailsDto>();
            }
        }

        #endregion

        #region Standings

        public async Task<EspnStandingsDto?> GetConferenceStandingsAsync() =>
            await GetAsync<EspnStandingsDto>($"{BASE_URL}/standings?type=conference", "conference standings");

        public async Task<EspnStandingsDto?> GetDivisionStandingsAsync() =>
            await GetAsync<EspnStandingsDto>($"{BASE_URL}/standings?type=division", "division standings");

        #endregion

        #region Players

        public async Task<List<EspnAthleteRefDto>> GetAllPlayersAsync()
        {
            try
            {
                var json = await _httpClient.GetStringAsync($"{CORE_API_BASE_URL}/athletes?lang=en&region=us");
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("items", out var items)) return new List<EspnAthleteRefDto>();

                return items.EnumerateArray()
                    .Select(i => new EspnAthleteRefDto { Ref = i.GetPropertySafe("$ref").GetString() ?? "" })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all athletes");
                return new List<EspnAthleteRefDto>();
            }
        }

        public async Task<EspnPlayerDetailsDto?> GetPlayerDetailsAsync(string playerId) =>
            await GetWithCacheAsync<EspnPlayerDetailsDto>($"{CORE_API_BASE_URL}/athletes/{playerId}?lang=en&region=us", $"player_details_{playerId}", TimeSpan.FromDays(1));

        public async Task<EspnPlayerStatsDto?> GetPlayerStatsAsync(string playerId) =>
            await GetPlayerSeasonStatsAsync(playerId, DateTime.Now.Month >= 10 ? DateTime.Now.Year : DateTime.Now.Year - 1);

        public async Task<EspnPlayerStatsDto?> GetPlayerStatisticsAsync(string playerId) =>
            await GetAsync<EspnPlayerStatsDto>($"{CORE_API_BASE_URL}/athletes/{playerId}/statistics", $"player stats {playerId}");

        public async Task<EspnPlayerSplitsDto?> GetPlayerSplitsAsync(string playerId) =>
            await GetAsync<EspnPlayerSplitsDto>($"https://site.api.espn.com/apis/common/v3/sports/basketball/nba/athletes/{playerId}/splits", $"player splits {playerId}");

        public async Task<EspnPlayerGamelogDto?> GetPlayerGamelogAsync(string playerId) =>
            await GetWithCacheAsync<EspnPlayerGamelogDto>($"https://site.api.espn.com/apis/common/v3/sports/basketball/nba/athletes/{playerId}/gamelog", $"player_gamelog_v37_{playerId}", TimeSpan.FromHours(4));

        public async Task<EspnPlayerStatsDto?> GetPlayerSeasonStatsAsync(string playerId, int season, int seasonType = 2)
        {
            var cacheKey = $"player_season_stats_{playerId}_{season}_{seasonType}";
            var cached = await _cacheService.GetAsync<EspnPlayerStatsDto>(cacheKey);
            if (cached != null) return cached;

            var stats = await GetAsync<EspnPlayerStatsDto>($"{CORE_API_BASE_URL}/seasons/{season}/types/{seasonType}/athletes/{playerId}/statistics/0?lang=en&region=us", $"season stats {playerId}");
            if (stats != null)
            {
                stats.SeasonTypeId = seasonType;
                if (stats.Season == null) stats.Season = new EspnSeasonRefDto();
                if (stats.Season.Year == 0) stats.Season.Year = season;
                
                if (stats.Team == null) stats.Team = new EspnTeamRefDto();
                if (string.IsNullOrEmpty(stats.Team.Id) && !string.IsNullOrEmpty(stats.Team.Ref))
                    stats.Team.Id = ExtractIdFromRef(stats.Team.Ref, @"teams/(\d+)");

                await _cacheService.SetAsync(cacheKey, stats, TimeSpan.FromHours(12));
            }
            return stats;
        }

        public async Task<List<EspnPlayerStatsDto>> GetPlayerCareerStatsAsync(string playerId)
        {
            var cacheKey = $"player_career_v38_{playerId}";
            var cached = await _cacheService.GetAsync<List<EspnPlayerStatsDto>>(cacheKey);
            if (cached != null) return cached;

            try
            {
                var url = $"https://site.api.espn.com/apis/common/v3/sports/basketball/nba/athletes/{playerId}/stats?region=us&lang=en&contentorigin=espn";
                var statsResponse = await GetAsync<EspnPlayerCareerStatsDto>(url, $"consolidated stats {playerId}");
                
                if (statsResponse != null)
                {
                    var allStats = new List<EspnPlayerStatsDto>();
                    var teamsDict = statsResponse.Teams != null 
                        ? statsResponse.Teams.Values
                            .Where(t => !string.IsNullOrEmpty(t.Id))
                            .GroupBy(t => t.Id!)
                            .ToDictionary(g => g.Key, g => g.First().DisplayName) 
                        : new Dictionary<string, string?>();

                    if (statsResponse.SeasonTypes != null)
                    {
                        foreach (var st in statsResponse.SeasonTypes)
                        {
                            if (st.Categories == null) continue;
                            foreach (var cat in st.Categories) {
                                if (cat.Stats == null) continue;
                                var tId = st.TeamId?.ToString() ?? "";
                                allStats.Add(new EspnPlayerStatsDto {
                                    Season = new EspnSeasonRefDto { Year = st.Year },
                                    SeasonTypeId = st.Type,
                                    Team = new EspnTeamRefDto { Id = tId, DisplayName = st.TeamName ?? (teamsDict.TryGetValue(tId, out var dn) ? dn : null) },
                                    Splits = new EspnPlayerStatsSplitDto { Categories = new List<EspnStatsCategoryDto> { 
                                        new EspnStatsCategoryDto { 
                                            Name = cat.Name ?? "stats", 
                                            Stats = cat.Stats.Select((s, i) => new EspnStatDto { 
                                                Value = (s.Value == 0 && !string.IsNullOrEmpty(s.DisplayValue) && s.DisplayValue != "0") ? SafeParseDouble(s.DisplayValue) : s.Value, 
                                                DisplayValue = s.DisplayValue ?? s.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                                Name = (cat.Names != null && i < cat.Names.Count) ? cat.Names[i] : (s.Name ?? i.ToString()),
                                                Label = (cat.Labels != null && i < cat.Labels.Count) ? cat.Labels[i] : (s.Label ?? s.Name ?? (cat.Names != null && i < cat.Names.Count ? cat.Names[i] : i.ToString()))
                                            }).ToList() 
                                        } 
                                    } }
                                });
                            }
                        }
                    }
                    else if (statsResponse.Categories != null)
                    {
                        foreach (var cat in statsResponse.Categories)
                        {
                            if (cat.Statistics == null) continue;
                            foreach (var stat in cat.Statistics) {
                                if (stat.Stats == null || stat.Season == null) continue;
                                var tId = stat.TeamId?.ToString() ?? "";
                                allStats.Add(new EspnPlayerStatsDto {
                                    Season = stat.Season,
                                    SeasonTypeId = stat.Type?.Type ?? 2,
                                    Team = new EspnTeamRefDto { Id = tId, DisplayName = stat.TeamName ?? (teamsDict.TryGetValue(tId, out var dn) ? dn : null) },
                                    Splits = new EspnPlayerStatsSplitDto { Categories = new List<EspnStatsCategoryDto> { 
                                        new EspnStatsCategoryDto { 
                                            Name = cat.Name ?? "stats", 
                                            Stats = stat.Stats.Select((s, i) => new EspnStatDto { 
                                                Value = SafeParseDouble(s), 
                                                DisplayValue = s,
                                                Name = (cat.Names != null && i < cat.Names.Count) ? cat.Names[i] : i.ToString(),
                                                Label = (cat.Labels != null && i < cat.Labels.Count) ? cat.Labels[i] : ((cat.Names != null && i < cat.Names.Count) ? cat.Names[i] : i.ToString())
                                            }).ToList() 
                                        } 
                                    } }
                                });
                            }
                        }
                    }

                    if (allStats.Any())
                    {
                        await _cacheService.SetAsync(cacheKey, allStats, TimeSpan.FromDays(1));
                        return allStats;
                    }
                }

                var years = await GetAthleteSeasonsAsync(playerId);
                if (!years.Any()) return new List<EspnPlayerStatsDto>();
                var tasks = years.SelectMany(y => new[] { GetPlayerSeasonStatsAsync(playerId, y, 2), GetPlayerSeasonStatsAsync(playerId, y, 3) });
                var results = (await Task.WhenAll(tasks)).Where(r => r?.Season != null).Cast<EspnPlayerStatsDto>().ToList();
                if (results.Any()) await _cacheService.SetAsync(cacheKey, results, TimeSpan.FromDays(1));
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching career stats for {PlayerId}", playerId);
                return new List<EspnPlayerStatsDto>();
            }
        }

        public async Task<EspnPlayerStatsDto?> GetPlayerGameStatsAsync(string playerId, string gameId)
        {
            var boxscore = await GetGameBoxscoreAsync(gameId);
            return boxscore == null ? null : _espnParser.ParsePlayerGameStatsFromBoxscore(boxscore, playerId, gameId);
        }

        #endregion

        #region Leaders

        public async Task<EspnLeadersDto?> GetLeagueLeadersAsync() =>
            await GetWithCacheAsync<EspnLeadersDto>($"{BASE_URL}/leaders", "league_leaders", TimeSpan.FromHours(6));

        #endregion

        #region Utility

        public async Task<bool> IsApiAvailableAsync()
        {
            try { return (await _httpClient.GetAsync($"{BASE_URL}/scoreboard")).IsSuccessStatusCode; }
            catch { return false; }
        }

        private async Task<T?> GetAsync<T>(string url, string context) where T : class
        {
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
                
                var response = await _httpClient.GetAsync(url, cts.Token);
                if (!response.IsSuccessStatusCode) return null;
                
                var json = await response.Content.ReadAsStringAsync(cts.Token);
                return JsonSerializer.Deserialize<T>(json, JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching {Context}", context);
                return null;
            }
        }

        private async Task<T?> GetWithCacheAsync<T>(string url, string cacheKey, TimeSpan ttl) where T : class
        {
            var cached = await _cacheService.GetAsync<T>(cacheKey);
            if (cached != null) return cached;

            var result = await GetAsync<T>(url, cacheKey);
            if (result != null) await _cacheService.SetAsync(cacheKey, result, ttl);
            return result;
        }

        private async Task<T> GetWithParsingAsync<T>(string url, Func<string, T> parser, string context) where T : class, new()
        {
            try
            {
                // Timeout estrito de 10s para evitar que requisições de polling travem o servidor
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                
                var response = await _httpClient.GetAsync(url, cts.Token);
                if (!response.IsSuccessStatusCode) return new T();
                
                var json = await response.Content.ReadAsStringAsync(cts.Token);
                return parser(json);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Timeout (10s) na chamada para ESPN: {Context}", context);
                return new T();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching {Context}", context);
                return new T();
            }
        }

        private async Task<List<int>> GetAthleteSeasonsAsync(string playerId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{CORE_API_BASE_URL}/athletes/{playerId}/seasons?lang=en&region=us");
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return new List<int>();
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("items", out var items)) return new List<int>();

                var currentYear = DateTime.Now.Month >= 10 ? DateTime.Now.Year + 1 : DateTime.Now.Year;
                return items.EnumerateArray()
                    .Select(i => ExtractIdFromRef(i.GetPropertySafe("$ref").GetString(), @"seasons/(\d+)"))
                    .Select(s => int.TryParse(s, out var y) ? y : 0)
                    .Where(y => y > 0 && y <= currentYear)
                    .OrderByDescending(y => y)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not fetch seasons for player {PlayerId}: {Message}", playerId, ex.Message);
                return new List<int>();
            }
        }

        private string ExtractIdFromRef(string? refUrl, string pattern)
        {
            if (string.IsNullOrEmpty(refUrl)) return string.Empty;
            var match = Regex.Match(refUrl, pattern);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        #endregion

        #region Legacy Support 
        public async Task<List<EspnGameDto>> GetTeamScheduleAsync(int teamId, DateTime startDate, DateTime endDate)
        {
            _logger.LogInformation("FETCHING TEAM SCHEDULE: Parallel ESPN request for Team {TeamId} from {Start} to {End}",
                teamId, startDate.ToShortDateString(), endDate.ToShortDateString());

            var results = new List<EspnGameDto>();
            var dateRange = Enumerable.Range(0, (int)(endDate - startDate).TotalDays + 1)
                                      .Select(offset => startDate.AddDays(offset))
                                      .ToList();

            var tasks = dateRange.Select(d => GetGamesByDateAsync(d));
            var gameLists = await Task.WhenAll(tasks);

            foreach (var list in gameLists)
            {
                if (list != null) results.AddRange(list);
            }

            return results;
        }
        #endregion

        private double SafeParseDouble(string? value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            var cleanValue = value.Split(new[] { '-', '/' })[0].Replace("%", "").Trim();
            return double.TryParse(cleanValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : 0;
        }
    }

    internal static class JsonExtensions
    {
        public static JsonElement GetPropertySafe(this JsonElement element, string name) =>
            element.TryGetProperty(name, out var prop) ? prop : default;
    }
}