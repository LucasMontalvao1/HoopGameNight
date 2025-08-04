using System.Text.Json;
using Microsoft.Extensions.Logging;
using HoopGameNight.Core.DTOs.External;
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

        // Mapeamento de IDs do sistema para IDs da ESPN
        private readonly Dictionary<int, string> _teamIdMapping = new()
        {
            { 1, "1" },   // Hawks
            { 2, "2" },   // Celtics
            { 3, "17" },  // Nets
            { 4, "3" },   // Hornets
            { 5, "5" },   // Bulls
            { 6, "6" },   // Cavaliers
            { 7, "7" },   // Mavericks
            { 8, "8" },   // Nuggets
            { 9, "9" },   // Pistons
            { 10, "10" }, // Warriors
            { 11, "11" }, // Rockets
            { 12, "12" }, // Pacers
            { 13, "13" }, // Clippers
            { 14, "14" }, // Lakers
            { 15, "15" }, // Grizzlies
            { 16, "16" }, // Heat
            { 17, "18" }, // Bucks
            { 18, "19" }, // Timberwolves
            { 19, "20" }, // Pelicans
            { 20, "21" }, // Knicks
            { 21, "22" }, // Thunder
            { 22, "23" }, // Magic
            { 23, "24" }, // 76ers
            { 24, "25" }, // Suns
            { 25, "26" }, // Trail Blazers
            { 26, "27" }, // Kings
            { 27, "28" }, // Spurs
            { 28, "29" }, // Raptors
            { 29, "30" }, // Jazz
            { 30, "4" }   // Wizards
        };

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

        public async Task<List<EspnGameDto>> GetTeamScheduleAsync(int teamId, DateTime startDate, DateTime endDate)
        {
            try
            {
                if (!_teamIdMapping.TryGetValue(teamId, out var espnTeamId))
                {
                    _logger.LogWarning("Team ID {TeamId} not found in ESPN mapping", teamId);
                    return new List<EspnGameDto>();
                }

                // Validar se estamos buscando dados muito no futuro
                var currentDate = DateTime.Today;
                var currentMonth = currentDate.Month;
                var currentYear = currentDate.Year;

                // Se estamos no offseason (julho-setembro) e buscando datas futuras
                if (currentMonth >= 7 && currentMonth <= 9 && startDate > currentDate)
                {
                    _logger.LogWarning(
                        "Requesting future games during offseason. Team {TeamId}, dates {Start} to {End}. Next season starts in October.",
                        teamId, startDate, endDate
                    );

                    // Se a data é para a próxima temporada que ainda não começou
                    if (startDate.Year > currentYear || (startDate.Year == currentYear && startDate.Month > 9))
                    {
                        _logger.LogInformation("Schedule for next season not available yet");
                        return new List<EspnGameDto>();
                    }
                }

                var url = $"{BASE_URL}/teams/{espnTeamId}/schedule";

                _logger.LogInformation("Fetching ESPN schedule for team {TeamId} (ESPN: {EspnId})", teamId, espnTeamId);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var allGames = ParseTeamScheduleResponse(json);

                var filteredGames = allGames
                    .Where(g => g.Date >= startDate && g.Date <= endDate)
                    .ToList();

                _logger.LogInformation("Found {Count} games for team {TeamId} between {Start} and {End}",
                    filteredGames.Count, teamId, startDate.ToShortDateString(), endDate.ToShortDateString());

                return filteredGames;
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

                // Parse date - pode vir como string ou objeto
                if (eventElement.TryGetProperty("date", out var dateElement))
                {
                    if (dateElement.ValueKind == JsonValueKind.String)
                    {
                        game.Date = DateTime.Parse(dateElement.GetString() ?? "");
                    }
                    else if (dateElement.ValueKind == JsonValueKind.Object)
                    {
                        // ESPN às vezes retorna date como objeto com propriedades
                        if (dateElement.TryGetProperty("date", out var innerDate))
                        {
                            game.Date = DateTime.Parse(innerDate.GetString() ?? "");
                        }
                    }
                }

                // Parse status
                if (eventElement.TryGetProperty("status", out var status))
                {
                    if (status.TryGetProperty("type", out var statusType))
                    {
                        game.Status = statusType.TryGetProperty("name", out var statusName)
                            ? statusName.GetString() ?? "Scheduled"
                            : "Scheduled";
                    }
                }

                // Parse competitions
                if (eventElement.TryGetProperty("competitions", out var competitions))
                {
                    var competition = competitions.EnumerateArray().FirstOrDefault();
                    if (competition.ValueKind != JsonValueKind.Undefined)
                    {
                        // Parse date from competition if not found above
                        if (game.Date == default && competition.TryGetProperty("date", out var compDate))
                        {
                            if (compDate.ValueKind == JsonValueKind.String)
                            {
                                game.Date = DateTime.Parse(compDate.GetString() ?? "");
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
                                    }
                                    else
                                    {
                                        game.AwayTeamId = teamId;
                                        game.AwayTeamName = teamName;
                                        game.AwayTeamAbbreviation = teamAbbr;
                                    }
                                }

                                // Parse score
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

                // Validar se temos dados mínimos
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
}