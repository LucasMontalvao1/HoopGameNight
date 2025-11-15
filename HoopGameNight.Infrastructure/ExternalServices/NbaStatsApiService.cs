using System.Text.Json;
using System.Text.Json.Serialization;
using HoopGameNight.Core.DTOs.External.NBAStats;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Infrastructure.ExternalServices
{
    /// <summary>
    /// Serviço para integração com Ball Don't Lie API v2
    /// (usa dados oficiais da NBA Stats API)
    /// Base URL: https://api.balldontlie.io/v1
    /// </summary>
    public class NbaStatsApiService : INbaStatsApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<NbaStatsApiService> _logger;
        private readonly string _apiKey;
        private readonly JsonSerializerOptions _jsonOptions;

        public NbaStatsApiService(
            HttpClient httpClient,
            ILogger<NbaStatsApiService> logger,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = configuration["NbaStatsApiKey"] ?? string.Empty;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                PropertyNameCaseInsensitive = true
            };

            _logger.LogInformation("NbaStatsApiService initialized");
        }

        public async Task<NbaStatsPlayerDto?> SearchPlayerByNameAsync(string firstName, string lastName)
        {
            try
            {
                var searchTerm = $"{firstName} {lastName}".Trim();
                _logger.LogDebug("Searching NBA player: {SearchTerm}", searchTerm);

                var request = new HttpRequestMessage(HttpMethod.Get,
                    $"/v1/players?search={Uri.EscapeDataString(searchTerm)}&per_page=1");

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("NBA Stats API returned {StatusCode} for player search: {Name}",
                        response.StatusCode, searchTerm);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<NbaStatsApiResponseWrapper>(json, _jsonOptions);

                if (apiResponse?.Data == null || apiResponse.Data.Count == 0)
                {
                    _logger.LogDebug("No player found with name: {Name}", searchTerm);
                    return null;
                }

                var player = apiResponse.Data[0];

                var result = new NbaStatsPlayerDto
                {
                    PersonId = player.Id.ToString(),
                    FirstName = player.FirstName ?? string.Empty,
                    LastName = player.LastName ?? string.Empty,
                    FullName = $"{player.FirstName} {player.LastName}".Trim(),
                    IsActive = true
                };

                _logger.LogInformation("Found NBA player: {Name} (ID: {Id})", result.FullName, result.PersonId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching NBA player: {FirstName} {LastName}", firstName, lastName);
                return null;
            }
        }

        public async Task<NbaStatsSeasonStatsDto?> GetPlayerSeasonStatsAsync(string playerId, int season)
        {
            try
            {
                _logger.LogDebug("Fetching season stats for player {PlayerId}, season {Season}", playerId, season);

                var request = new HttpRequestMessage(HttpMethod.Get,
                    $"/v1/season_averages?season={season}&player_ids[]={playerId}");

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("NBA Stats API returned {StatusCode} for season stats", response.StatusCode);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<NbaStatsSeasonStatsWrapper>(json, _jsonOptions);

                if (apiResponse?.Data == null || apiResponse.Data.Count == 0)
                {
                    _logger.LogDebug("No season stats found for player {PlayerId} in season {Season}", playerId, season);
                    return null;
                }

                var stats = apiResponse.Data[0];

                var result = new NbaStatsSeasonStatsDto
                {
                    GamesPlayed = stats.GamesPlayed,
                    Points = (decimal)stats.Pts,
                    Rebounds = (decimal)stats.Reb,
                    Assists = (decimal)stats.Ast,
                    Steals = (decimal)stats.Stl,
                    Blocks = (decimal)stats.Blk,
                    Turnovers = (decimal)stats.Turnover,
                    FieldGoalPercentage = (decimal)(stats.FgPct ?? 0),
                    ThreePointPercentage = (decimal)(stats.Fg3Pct ?? 0),
                    FreeThrowPercentage = (decimal)(stats.FtPct ?? 0),
                    Minutes = stats.Min ?? "0:00"
                };

                _logger.LogInformation("Retrieved season stats for player {PlayerId}: {Points} PPG", playerId, result.Points);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching season stats for player {PlayerId}, season {Season}", playerId, season);
                return null;
            }
        }

        public async Task<bool> IsApiAvailableAsync()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "/v1/players?per_page=1");

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // Classes auxiliares para desserialização
        private class NbaStatsApiResponseWrapper
        {
            [JsonPropertyName("data")]
            public List<PlayerData> Data { get; set; } = new();
        }

        private class PlayerData
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonPropertyName("first_name")]
            public string? FirstName { get; set; }

            [JsonPropertyName("last_name")]
            public string? LastName { get; set; }
        }

        private class NbaStatsSeasonStatsWrapper
        {
            [JsonPropertyName("data")]
            public List<SeasonStatsData> Data { get; set; } = new();
        }

        private class SeasonStatsData
        {
            [JsonPropertyName("games_played")]
            public int GamesPlayed { get; set; }

            [JsonPropertyName("min")]
            public string? Min { get; set; }

            [JsonPropertyName("pts")]
            public double Pts { get; set; }

            [JsonPropertyName("reb")]
            public double Reb { get; set; }

            [JsonPropertyName("ast")]
            public double Ast { get; set; }

            [JsonPropertyName("stl")]
            public double Stl { get; set; }

            [JsonPropertyName("blk")]
            public double Blk { get; set; }

            [JsonPropertyName("turnover")]
            public double Turnover { get; set; }

            [JsonPropertyName("fg_pct")]
            public double? FgPct { get; set; }

            [JsonPropertyName("fg3_pct")]
            public double? Fg3Pct { get; set; }

            [JsonPropertyName("ft_pct")]
            public double? FtPct { get; set; }
        }
    }
}
