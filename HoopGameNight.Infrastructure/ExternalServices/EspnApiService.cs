using System.Text.Json;
using Microsoft.Extensions.Logging;
using HoopGameNight.Core.DTOs.External;
using HoopGameNight.Core.DTOs.External.ESPN;
using HoopGameNight.Core.Interfaces.Services;

namespace HoopGameNight.Infrastructure.ExternalServices
{
    /// <summary>
    /// Implementação do serviço para integração com ESPN API
    /// </summary>
    public class EspnApiService : IEspnApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<EspnApiService> _logger;
        private const string BASE_URL = "https://site.api.espn.com/apis/site/v2/sports/basketball/nba";
        private const string CORE_API_BASE_URL = "https://sports.core.api.espn.com/v2/sports/basketball/leagues/nba";

        public EspnApiService(HttpClient httpClient, ILogger<EspnApiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "HoopGameNight/1.0");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

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

        /// <summary>
        /// DEPRECATED: Use GetGamesByDateAsync instead (more efficient)
        /// Busca jogos de um time específico por período
        /// </summary>
        public async Task<List<EspnGameDto>> GetTeamScheduleAsync(int teamId, DateTime startDate, DateTime endDate)
        {
            try
            {
                _logger.LogWarning(
                    "GetTeamScheduleAsync is DEPRECATED and inefficient. Consider using GetGamesByDateAsync for date ranges.");

                var allGames = new List<EspnGameDto>();

                var currentDate = startDate;
                while (currentDate <= endDate)
                {
                    var dailyGames = await GetGamesByDateAsync(currentDate);
                    allGames.AddRange(dailyGames);
                    currentDate = currentDate.AddDays(1);
                }

                _logger.LogInformation("Found {Count} total games between {Start} and {End}",
                    allGames.Count, startDate.ToShortDateString(), endDate.ToShortDateString());

                return allGames;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching team schedule from ESPN for team {TeamId}", teamId);
                return new List<EspnGameDto>();
            }
        }

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

        public async Task<EspnAthleteRefDto?> SearchPlayerByNameAsync(string firstName, string lastName)
        {
            try
            {
                _logger.LogDebug("ESPN API does not support search by name directly. Player: {FirstName} {LastName}", firstName, lastName);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error searching ESPN player: {FirstName} {LastName}", firstName, lastName);
                return null;
            }
        }

        public async Task<EspnPlayerDetailsDto?> GetPlayerDetailsAsync(string playerId)
        {
            try
            {
                var url = $"{CORE_API_BASE_URL}/athletes/{playerId}?lang=en&region=us";
                _logger.LogInformation("Fetching ESPN player details for ID: {PlayerId}", playerId);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var playerDetails = JsonSerializer.Deserialize<EspnPlayerDetailsDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

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
            try
            {
                var url = $"{CORE_API_BASE_URL}/athletes/{playerId}/statistics?lang=en&region=us";
                _logger.LogInformation("Fetching ESPN player stats for ID: {PlayerId}", playerId);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var playerStats = JsonSerializer.Deserialize<EspnPlayerStatsDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return playerStats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN player stats for ID: {PlayerId}", playerId);
                return null;
            }
        }

        public async Task<EspnPlayerStatsDto?> GetPlayerSeasonStatsAsync(string playerId, int season)
        {
            try
            {
                var url = $"{CORE_API_BASE_URL}/seasons/{season}/types/3/athletes/{playerId}/statistics/0?lang=en&region=us";
                _logger.LogInformation("Fetching ESPN player season stats for ID: {PlayerId}, Season: {Season}", playerId, season);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var seasonStats = JsonSerializer.Deserialize<EspnPlayerStatsDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return seasonStats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN player season stats for ID: {PlayerId}, Season: {Season}", playerId, season);
                return null;
            }
        }

        public async Task<List<EspnPlayerStatsDto>> GetPlayerCareerStatsAsync(string playerId)
        {
            try
            {
                var url = $"{CORE_API_BASE_URL}/athletes/{playerId}/statisticslog?lang=en&region=us";
                _logger.LogInformation("Fetching ESPN player career stats for ID: {PlayerId}", playerId);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                var careerStats = new List<EspnPlayerStatsDto>();

                if (root.TryGetProperty("entries", out var entries))
                {
                    foreach (var entry in entries.EnumerateArray())
                    {
                        if (entry.TryGetProperty("statistics", out var statistics))
                        {
                            foreach (var stat in statistics.EnumerateArray())
                            {
                                if (stat.TryGetProperty("type", out var type) &&
                                    type.GetString() == "total" &&
                                    stat.TryGetProperty("statistics", out var statsRef) &&
                                    statsRef.TryGetProperty("$ref", out var refUrl))
                                {
                                    var seasonStats = await GetPlayerStatsFromUrl(refUrl.GetString());
                                    if (seasonStats != null)
                                    {
                                        careerStats.Add(seasonStats);
                                    }
                                }
                            }
                        }
                    }
                }

                _logger.LogInformation("Found {Count} career seasons for player {PlayerId}", careerStats.Count, playerId);
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
                var url = $"{CORE_API_BASE_URL}/events/{gameId}/competitions/{gameId}/competitors/home/roster/{playerId}/statistics?lang=en&region=us";
                _logger.LogInformation("Fetching ESPN player game stats for Player ID: {PlayerId}, Game ID: {GameId}", playerId, gameId);

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    url = $"{CORE_API_BASE_URL}/events/{gameId}/competitions/{gameId}/competitors/away/roster/{playerId}/statistics?lang=en&region=us";
                    response = await _httpClient.GetAsync(url);
                }

                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var gameStats = JsonSerializer.Deserialize<EspnPlayerStatsDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return gameStats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN player game stats for Player ID: {PlayerId}, Game ID: {GameId}", playerId, gameId);
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

        private EspnPlayerDetailsDto? ParsePlayerFromRoster(JsonElement item)
        {
            try
            {
                var player = new EspnPlayerDetailsDto();

                if (item.TryGetProperty("id", out var id))
                    player.Id = id.GetString() ?? "";

                if (item.TryGetProperty("uid", out var uid))
                    player.Uid = uid.GetString() ?? "";

                if (item.TryGetProperty("guid", out var guid))
                    player.Guid = guid.GetString() ?? "";

                if (item.TryGetProperty("firstName", out var firstName))
                    player.FirstName = firstName.GetString() ?? "";

                if (item.TryGetProperty("lastName", out var lastName))
                    player.LastName = lastName.GetString() ?? "";

                if (item.TryGetProperty("fullName", out var fullName))
                    player.FullName = fullName.GetString() ?? "";

                if (item.TryGetProperty("displayName", out var displayName))
                    player.DisplayName = displayName.GetString() ?? "";

                if (item.TryGetProperty("shortName", out var shortName))
                    player.ShortName = shortName.GetString() ?? "";

                if (item.TryGetProperty("weight", out var weight))
                    player.Weight = weight.TryGetDouble(out var w) ? w : 0;

                if (item.TryGetProperty("displayWeight", out var displayWeight))
                    player.DisplayWeight = displayWeight.GetString() ?? "";

                if (item.TryGetProperty("height", out var height))
                    player.Height = height.TryGetDouble(out var h) ? h : 0;

                if (item.TryGetProperty("displayHeight", out var displayHeight))
                    player.DisplayHeight = displayHeight.GetString() ?? "";

                if (item.TryGetProperty("age", out var age))
                    player.Age = age.TryGetInt32(out var a) ? a : 0;

                if (item.TryGetProperty("dateOfBirth", out var dob))
                    player.DateOfBirth = dob.GetString() ?? "";

                if (item.TryGetProperty("debutYear", out var debutYear))
                    player.DebutYear = debutYear.TryGetInt32(out var dy) ? dy : 0;

                if (item.TryGetProperty("jersey", out var jersey))
                    player.Jersey = jersey.GetString() ?? "";

                if (item.TryGetProperty("position", out var position))
                {
                    player.Position = new EspnPositionDto
                    {
                        Id = position.TryGetProperty("id", out var posId) ? posId.GetString() ?? "" : "",
                        Name = position.TryGetProperty("name", out var posName) ? posName.GetString() ?? "" : "",
                        DisplayName = position.TryGetProperty("displayName", out var posDisplayName) ? posDisplayName.GetString() ?? "" : "",
                        Abbreviation = position.TryGetProperty("abbreviation", out var posAbbr) ? posAbbr.GetString() ?? "" : ""
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

        private async Task<EspnPlayerStatsDto?> GetPlayerStatsFromUrl(string? url)
        {
            if (string.IsNullOrEmpty(url)) return null;

            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<EspnPlayerStatsDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching stats from URL: {Url}", url);
                return null;
            }
        }

        #region Private Parsing Methods

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
                            if (game != null)
                            {
                                games.Add(game);
                            }
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

        private List<EspnGameDto> ParseTeamScheduleResponse(string json)
        {
            var games = new List<EspnGameDto>();

            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (root.TryGetProperty("events", out var events) ||
                    (root.TryGetProperty("team", out var team) && team.TryGetProperty("events", out events)))
                {
                    foreach (var eventElement in events.EnumerateArray())
                    {
                        try
                        {
                            var game = ParseGameEvent(eventElement);
                            if (game != null)
                            {
                                games.Add(game);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error parsing individual team schedule event");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing ESPN team schedule response");
            }

            return games;
        }

        private EspnGameDto? ParseGameEvent(JsonElement eventElement)
        {
            try
            {
                var game = new EspnGameDto
                {
                    Id = eventElement.GetProperty("id").GetString() ?? ""
                };

                _logger.LogDebug("🔍 Parsing ESPN Game ID: {GameId}", game.Id);

                if (eventElement.TryGetProperty("date", out var dateElement))
                {
                    if (dateElement.ValueKind == JsonValueKind.String)
                    {
                        var dateString = dateElement.GetString() ?? "";
                        if (DateTime.TryParse(dateString, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedDate))
                        {
                            game.Date = parsedDate.Kind == DateTimeKind.Utc
                                ? parsedDate.ToLocalTime()
                                : parsedDate;
                        }
                        else
                        {
                            game.Date = DateTime.Parse(dateString);
                        }
                    }
                    else if (dateElement.ValueKind == JsonValueKind.Object)
                    {
                        if (dateElement.TryGetProperty("date", out var innerDate))
                        {
                            var dateString = innerDate.GetString() ?? "";
                            if (DateTime.TryParse(dateString, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedDate))
                            {
                                game.Date = parsedDate.Kind == DateTimeKind.Utc
                                    ? parsedDate.ToLocalTime()
                                    : parsedDate;
                            }
                            else
                            {
                                game.Date = DateTime.Parse(dateString);
                            }
                        }
                    }
                }

                _logger.LogDebug("Game Date: {Date} (UTC: {IsUtc})", game.Date, game.Date.Kind == DateTimeKind.Utc);

                if (eventElement.TryGetProperty("status", out var status))
                {
                    if (status.TryGetProperty("type", out var statusType))
                    {
                        game.Status = statusType.TryGetProperty("name", out var statusName)
                            ? statusName.GetString() ?? "Scheduled"
                            : "Scheduled";
                    }

                    if (status.TryGetProperty("period", out var periodElement))
                    {
                        game.Period = periodElement.GetInt32();
                    }

                    if (status.TryGetProperty("displayClock", out var clockElement))
                    {
                        game.TimeRemaining = clockElement.GetString();
                    }
                }

                if (eventElement.TryGetProperty("competitions", out var competitions))
                {
                    var competition = competitions.EnumerateArray().FirstOrDefault();
                    if (competition.ValueKind != JsonValueKind.Undefined)
                    {
                        if (game.Date == default && competition.TryGetProperty("date", out var compDate))
                        {
                            if (compDate.ValueKind == JsonValueKind.String)
                            {
                                var dateString = compDate.GetString() ?? "";
                                if (DateTime.TryParse(dateString, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedDate))
                                {
                                    game.Date = parsedDate.Kind == DateTimeKind.Utc
                                        ? parsedDate.ToLocalTime()
                                        : parsedDate;
                                }
                                else
                                {
                                    game.Date = DateTime.Parse(dateString);
                                }
                            }
                        }

                        if (competition.TryGetProperty("competitors", out var competitors))
                        {
                            foreach (var competitor in competitors.EnumerateArray())
                            {
                                var homeAwayValue = competitor.TryGetProperty("homeAway", out var homeAway)
                                    ? homeAway.GetString()
                                    : "";

                                var isHome = homeAwayValue == "home";

                                if (competitor.TryGetProperty("team", out var teamElement))
                                {
                                    var teamId = teamElement.TryGetProperty("id", out var idElement)
                                        ? idElement.GetString() ?? ""
                                        : "";

                                    var teamName = teamElement.TryGetProperty("displayName", out var nameElement)
                                        ? nameElement.GetString() ?? ""
                                        : teamElement.TryGetProperty("name", out var altNameElement)
                                            ? altNameElement.GetString() ?? ""
                                            : "";

                                    var teamAbbr = teamElement.TryGetProperty("abbreviation", out var abbr)
                                        ? abbr.GetString() ?? ""
                                        : "";

                                    if (isHome)
                                    {
                                        game.HomeTeamId = teamId;
                                        game.HomeTeamName = teamName;
                                        game.HomeTeamAbbreviation = teamAbbr;
                                        _logger.LogDebug(
                                            "HOME: {Abbr} (ESPN ID: {EspnId})",
                                            teamAbbr, teamId);
                                    }
                                    else
                                    {
                                        game.AwayTeamId = teamId;
                                        game.AwayTeamName = teamName;
                                        game.AwayTeamAbbreviation = teamAbbr;
                                        _logger.LogDebug(
                                            "AWAY: {Abbr} (ESPN ID: {EspnId})",
                                            teamAbbr, teamId);
                                    }
                                }

                                if (competitor.TryGetProperty("score", out var scoreElement))
                                {
                                    if (scoreElement.ValueKind == JsonValueKind.String)
                                    {
                                        var score = scoreElement.GetString();
                                        if (int.TryParse(score, out var scoreValue))
                                        {
                                            if (isHome)
                                                game.HomeTeamScore = scoreValue;
                                            else
                                                game.AwayTeamScore = scoreValue;
                                        }
                                    }
                                    else if (scoreElement.ValueKind == JsonValueKind.Number)
                                    {
                                        if (isHome)
                                            game.HomeTeamScore = scoreElement.GetInt32();
                                        else
                                            game.AwayTeamScore = scoreElement.GetInt32();
                                    }
                                }
                            }
                        }
                    }
                }

                if (eventElement.TryGetProperty("season", out var seasonElement))
                {
                    if (seasonElement.TryGetProperty("year", out var yearElement))
                    {
                        game.Season = yearElement.GetInt32();
                    }
                }

                if (eventElement.TryGetProperty("season", out var seasonInfo))
                {
                    if (seasonInfo.TryGetProperty("type", out var seasonType))
                    {
                        var typeValue = seasonType.GetInt32();
                        game.IsPostseason = typeValue == 3;
                    }
                }

                if (string.IsNullOrEmpty(game.HomeTeamId) || string.IsNullOrEmpty(game.AwayTeamId))
                {
                    _logger.LogDebug("Game event missing team data, skipping");
                    return null;
                }

                return game;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing game event");
                return null;
            }
        }

        #endregion
    }

    /// <summary>
    /// Extension methods para JsonElement
    /// </summary>
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