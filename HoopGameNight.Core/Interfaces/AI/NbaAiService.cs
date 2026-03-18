using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;

namespace HoopGameNight.Core.Services.AI
{
    public class NbaAiService : INbaAiService
    {
        private readonly IGameService _gameService;
        private readonly IGameRepository _gameRepository;
        private readonly ITeamRepository _teamRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IPlayerStatsService _playerStatsService;
        private readonly NbaPromptBuilder _promptBuilder;
        private readonly GroqClient _groqClient;
        private readonly ICacheService _cacheService;
        private readonly IGameStatsService _gameStatsService;
        private readonly ILogger<NbaAiService> _logger;

        private static readonly Dictionary<string, string> _teamKeywords = new();
        private static readonly Dictionary<string, string> _playerKeywords = new();
        private static bool _keywordsLoaded = false;
        private static readonly object _lock = new();

        public NbaAiService(
            IGameService gameService,
            IGameRepository gameRepository,
            ITeamRepository teamRepository,
            IPlayerRepository playerRepository,
            IPlayerStatsService playerStatsService,
            NbaPromptBuilder promptBuilder,
            GroqClient groqClient,
            ICacheService cacheService,
            IGameStatsService gameStatsService,
            ILogger<NbaAiService> logger)
        {
            _gameService = gameService;
            _gameRepository = gameRepository;
            _teamRepository = teamRepository;
            _playerRepository = playerRepository;
            _playerStatsService = playerStatsService;
            _promptBuilder = promptBuilder;
            _groqClient = groqClient;
            _cacheService = cacheService;
            _gameStatsService = gameStatsService;
            _logger = logger;

            if (!_keywordsLoaded)
            {
                lock (_lock)
                {
                    if (!_keywordsLoaded)
                    {
                        LoadTeamKeywords();
                        LoadPlayerKeywords();
                        _keywordsLoaded = true;
                    }
                }
            }
        }

        private void LoadTeamKeywords()
        {
            try
            {
                var filePath = Path.Combine(AppContext.BaseDirectory, "Resources", "teams_keywords.json");
                if (!File.Exists(filePath))
                {
                    filePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "HoopGameNight.Core", "Resources", "teams_keywords.json");
                }

                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var keywords = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (keywords != null)
                    {
                        foreach (var kvp in keywords)
                            _teamKeywords[kvp.Key] = kvp.Value;
                        
                        _logger.LogInformation("Carregados {Count} palavras-chave de times do JSON", _teamKeywords.Count);
                    }
                }
                else
                {
                    _logger.LogWarning("Arquivo de keywords não encontrado em: {Path}", filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar keywords do JSON");
            }
        }

        private void LoadPlayerKeywords()
        {
            try
            {
                var filePath = Path.Combine(AppContext.BaseDirectory, "Resources", "players_keywords.json");
                if (!File.Exists(filePath))
                {
                    filePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "HoopGameNight.Core", "Resources", "players_keywords.json");
                }

                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var keywords = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (keywords != null)
                    {
                        foreach (var kvp in keywords)
                            _playerKeywords[kvp.Key] = kvp.Value;
                        
                        _logger.LogInformation("Carregados {Count} palavras-chave de jogadores do JSON", _playerKeywords.Count);
                    }
                }
                else
                {
                    _logger.LogWarning("Arquivo de keywords de jogadores não encontrado em: {Path}", filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar keywords de jogadores do JSON");
            }
        }

        public async Task<AskResponse> AskAsync(AskRequest request)
        {
            var normalizedQuestion = NormalizeQuestion(request.Question);

            _logger.LogInformation("Processando consulta: {Question}", request.Question);

            var (games, period, detectedTeams) = await GetRelevantGamesWithContextAsync(normalizedQuestion);

            _logger.LogInformation("Métricas da busca: {Count} jogos localizados | Período: {Period} | Times: {Teams}",
                games.Count, period, string.Join(", ", detectedTeams));

            var (playerStatsText, detectedPlayers) = await GetRelevantPlayerStatsAsync(normalizedQuestion);

            if (!games.Any() && string.IsNullOrEmpty(playerStatsText))
            {
                _logger.LogWarning("Nenhum registro de jogo ou jogador localizado para o contexto fornecido.");
                return new AskResponse
                {
                    Question = request.Question,
                    Answer = "Não encontrei informações no banco de dados para este período/time/jogador.",
                    GamesAnalyzed = 0,
                    FromCache = false,
                    DataSource = "Database",
                    Period = period,
                    DetectedTeams = detectedTeams
                };
            }

            var gamesWithPerformance = games
                .Where(g => g.IsCompleted || g.IsLive)
                .OrderByDescending(g => g.Date)
                .Take(3) 
                .ToList();

            var gameLeaders = new Dictionary<int, GameLeadersResponse>();
            foreach (var g in gamesWithPerformance)
            {
                var leaders = await CalculateLeadersFromBoxscoreAsync(g.Id);
                
                if (leaders != null)
                {
                    gameLeaders[g.Id] = leaders;
                }
            }

            var statsText = playerStatsText;
            if (gamesWithPerformance.Count == 1)
            {
                var focusedGame = gamesWithPerformance[0];
                var boxscore = await _gameStatsService.GetGamePlayerStatsAsync(focusedGame.Id);
                if (boxscore != null)
                {
                    var boxscoreText = _promptBuilder.FormatFullBoxscore(boxscore);
                    statsText += $"\n--- Boxscore Detalhado ({focusedGame.GameTitle}) ---\n{boxscoreText}";
                }
            }

            var prompt = _promptBuilder.BuildPrompt(request.Question, games, statsText, gameLeaders);

            var answer = await _groqClient.GenerateAsync(prompt);

            var response = new AskResponse
            {
                Question = request.Question,
                Answer = answer,
                GamesAnalyzed = games.Count,
                FromCache = false,
                DataSource = "Database (Enriched)",
                Period = period,
                DetectedTeams = detectedTeams
            };

            return response;
        }

        public async Task<AskResponse> GetGameSummaryAsync(int gameId)
        {
            _logger.LogInformation("Gerando resumo IA estruturado para o jogo ID: {GameId}", gameId);
    
            var cacheKey = $"ai:summary:game:{gameId}:v2";

            var cachedJson = await _cacheService.GetAsync<string>(cacheKey);
            if (!string.IsNullOrEmpty(cachedJson))
            {
                _logger.LogInformation("Retornando resumo estruturado do CACHE para o jogo {GameId}", gameId);
                return new AskResponse
                {
                    Question = $"Resumo do jogo {gameId}",
                    Answer = cachedJson,
                    GamesAnalyzed = 1,
                    FromCache = true,
                    DataSource = "Cache (Permanent)"
                };
            }

            var gameEntity = await _gameRepository.GetByIdAsync(gameId);
            if (gameEntity == null) return new AskResponse { Answer = "Jogo não encontrado." };

            if (!string.IsNullOrEmpty(gameEntity.AiSummary) && !string.IsNullOrEmpty(gameEntity.AiHighlights))
            {
                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                try
                {
                    var combined = JsonSerializer.Serialize(new { summary = gameEntity.AiSummary, highlights = JsonSerializer.Deserialize<JsonElement>(gameEntity.AiHighlights) }, options);
                    await _cacheService.SetAsync(cacheKey, combined, expiration: null);
                    
                    return new AskResponse
                    {
                        Question = $"Resumo do jogo {gameEntity.GameTitle}",
                        Answer = combined,
                        GamesAnalyzed = 1,
                        DataSource = "Database (Persisted)"
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro ao desserializar highlights persistidos para o jogo {GameId}. Gerando novo resumo.", gameId);
                }
            }

            var game = await _gameService.GetGameByIdAsync(gameId);
            if (game == null) return new AskResponse { Answer = "Detalhes do jogo não localizados." };
    
            var leaders = await CalculateLeadersFromBoxscoreAsync(gameId);
            var boxscore = await _gameStatsService.GetGamePlayerStatsAsync(gameId);

            var prompt = _promptBuilder.BuildGameSummaryJsonPrompt(game, leaders, boxscore);
            var rawResponse = await _groqClient.GenerateAsync(prompt);
            
            try 
            {
                var cleanerResponse = rawResponse.Trim();
                if (cleanerResponse.StartsWith("```json")) cleanerResponse = cleanerResponse.Substring(7);
                if (cleanerResponse.StartsWith("```")) cleanerResponse = cleanerResponse.Substring(3);
                if (cleanerResponse.EndsWith("```")) cleanerResponse = cleanerResponse.Substring(0, cleanerResponse.Length - 3);
                cleanerResponse = cleanerResponse.Trim();

                cleanerResponse = SanitizeJsonString(cleanerResponse);

                var aiData = JsonSerializer.Deserialize<AiGameStory>(cleanerResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                if (aiData != null && !string.IsNullOrEmpty(aiData.Summary))
                {
                    var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                    
                    gameEntity.AiSummary = aiData.Summary;
                    gameEntity.AiHighlights = JsonSerializer.Serialize(aiData.Highlights, options);
                    await _gameRepository.UpdateAsync(gameEntity);
                    
                    var cacheData = JsonSerializer.Serialize(new { summary = aiData.Summary, highlights = aiData.Highlights }, options);
                    await _cacheService.SetAsync(cacheKey, cacheData, expiration: null);
                    
                    _logger.LogInformation("Resumo e Highlights salvos para o jogo {GameId} (Status: {Status})", gameId, gameEntity.Status);

                    return new AskResponse
                    {
                        Question = $"Resumo do jogo {game.VisitorTeam.Abbreviation} vs {game.HomeTeam.Abbreviation}",
                        Answer = JsonSerializer.Serialize(new { summary = aiData.Summary, highlights = aiData.Highlights }, options),
                        GamesAnalyzed = 1,
                        DataSource = "Database (Processed)",
                        DetectedTeams = new List<string> { game.HomeTeam.Abbreviation, game.VisitorTeam.Abbreviation }
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao parsear resposta JSON da IA. Retornando texto bruto.");
            }
    
            return new AskResponse
            {
                Question = $"Resumo do jogo {game.VisitorTeam.Abbreviation} vs {game.HomeTeam.Abbreviation}",
                Answer = rawResponse,
                GamesAnalyzed = 1,
                DataSource = "Database (Raw Error Fallback)"
            };
        }

        private class AiGameStory
        {
            public string Summary { get; set; } = string.Empty;
            public List<AiHighlight> Highlights { get; set; } = new();
        }

        private class AiHighlight
        {
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
        }

        private string SanitizeJsonString(string json)
        {
            var sb = new System.Text.StringBuilder(json.Length);
            bool inString = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"' && (i == 0 || json[i - 1] != '\\'))
                {
                    inString = !inString;
                    sb.Append(c);
                }
                else if (inString)
                {
                    switch (c)
                    {
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default: sb.Append(c); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private async Task<GameLeadersResponse?> CalculateLeadersFromBoxscoreAsync(int gameId)
        {
            var stats = await _gameStatsService.GetGamePlayerStatsAsync(gameId);
            if (stats == null) return null;

            var result = new GameLeadersResponse
            {
                GameId = gameId,
                HomeTeam = stats.HomeTeam,
                VisitorTeam = stats.VisitorTeam,
                HomeTeamLeaders = new TeamGameLeaders { TeamName = stats.HomeTeam },
                VisitorTeamLeaders = new TeamGameLeaders { TeamName = stats.VisitorTeam }
            };

            void FillLeaders(List<PlayerGameStatsDetailedResponse> teamStats, TeamGameLeaders teamLeaders)
            {
                var ptsLeader = teamStats.OrderByDescending(s => s.Points).FirstOrDefault();
                var rebLeader = teamStats.OrderByDescending(s => s.TotalRebounds).FirstOrDefault();
                var astLeader = teamStats.OrderByDescending(s => s.Assists).FirstOrDefault();

                if (ptsLeader != null) teamLeaders.PointsLeader = new StatLeader { PlayerName = ptsLeader.PlayerFullName, Value = ptsLeader.Points };
                if (rebLeader != null) teamLeaders.ReboundsLeader = new StatLeader { PlayerName = rebLeader.PlayerFullName, Value = rebLeader.TotalRebounds };
                if (astLeader != null) teamLeaders.AssistsLeader = new StatLeader { PlayerName = astLeader.PlayerFullName, Value = astLeader.Assists };
            }

            FillLeaders(stats.HomeTeamStats, result.HomeTeamLeaders);
            FillLeaders(stats.VisitorTeamStats, result.VisitorTeamLeaders);

            return result;
        }

        private string NormalizeQuestion(string question)
        {
            return question.Trim().ToLower()
                .Replace("?", "")
                .Replace("!", "")
                .Replace(" do ", " ")
                .Replace(" da ", " ")
                .Replace(" de ", " ")
                .Replace("  ", " ");
        }

        /// <summary>
        /// Realiza a busca de jogos aplicando filtros de data e identificação de equipes.
        /// </summary>
        private async Task<(List<GameResponse> games, string period, List<string> detectedTeams)> GetRelevantGamesWithContextAsync(string question)
        {
            var questionLower = question.ToLower();

            // 1. Detectar período
            var (startDate, endDate, periodLabel) = DetectDateRangeWithLabel(questionLower);

            // 2. Detectar times
            var (teamIds, teamNames) = await DetectTeamsWithNamesAsync(questionLower);

            var allGames = new List<GameResponse>();

            // 3. Buscar jogos do período detectado
            var rangeGames = await _gameService.GetGamesByDateRangeAsync(startDate, endDate);

            if (teamIds.Any())
            {
                // Filtrar apenas jogos dos times detectados no período original
                foreach (var teamId in teamIds)
                {
                    var teamGames = rangeGames.Where(g =>
                        g.HomeTeam.Id == teamId || g.VisitorTeam.Id == teamId);
                    allGames.AddRange(teamGames);
                }

                // 4. Fallback Inteligente: Se detectou um time mas não achou jogo no período estrito
                if (!allGames.Any())
                {
                    _logger.LogInformation("Nenhum jogo encontrado para {Teams} no período {Period}. Tentando fallback de 10 dias...", 
                        string.Join(", ", teamNames), periodLabel);
                    
                    var fallbackStart = DateTime.Today.AddDays(-10);
                    var fallbackEnd = DateTime.Today.AddDays(1);
                    var fallbackGames = await _gameService.GetGamesByDateRangeAsync(fallbackStart, fallbackEnd);

                    foreach (var teamId in teamIds)
                    {
                        var teamGames = fallbackGames.Where(g =>
                            g.HomeTeam.Id == teamId || g.VisitorTeam.Id == teamId);
                        allGames.AddRange(teamGames);
                    }
                    
                    if (allGames.Any())
                        periodLabel += " (Fallback: Jogos Recentes)";
                }
            }
            else
            {
                // Usar todos os jogos do período
                allGames.AddRange(rangeGames);
            }

            // 5. Remover duplicatas e limitar
            var uniqueGames = allGames
                .GroupBy(g => new { GameDate = g.Date, HomeTeamId = g.HomeTeam.Id, VisitorTeamId = g.VisitorTeam.Id })
                .Select(g => g.First())
                .OrderByDescending(g => g.Date)
                .Take(50)
                .ToList();

            var period = $"{startDate:dd/MM} a {endDate:dd/MM} ({periodLabel})";

            return (uniqueGames, period, teamNames);
        }

        /// <summary>
        /// Identifica o intervalo de datas a partir da linguagem natural.
        /// </summary>
        private (DateTime startDate, DateTime endDate, string label) DetectDateRangeWithLabel(string question)
        {
            // Hoje
            if (question.Contains("hoje") || question.Contains("today"))
            {
                return (DateTime.Today, DateTime.Today, "Hoje");
            }

            // Ontem
            if (question.Contains("ontem") || question.Contains("yesterday"))
            {
                return (DateTime.Today.AddDays(-1), DateTime.Today.AddDays(-1), "Ontem");
            }

            // Amanhã
            if (question.Contains("amanhã") || question.Contains("amanha") || question.Contains("tomorrow"))
            {
                return (DateTime.Today.AddDays(1), DateTime.Today.AddDays(1), "Amanhã");
            }

            // Esta semana
            if (question.Contains("esta semana") || question.Contains("this week"))
            {
                var start = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                var end = start.AddDays(6);
                return (start, end, "Esta semana");
            }

            // Próximos
            if (question.Contains("próxim") || question.Contains("proxim") || question.Contains("next") || question.Contains("futur"))
            {
                return (DateTime.Today, DateTime.Today.AddDays(30), "Próximos Jogos (Amplo)");
            }

            // Últimos
            if (question.Contains("últim") || question.Contains("ultim") || question.Contains("recente") ||
                question.Contains("recent") || question.Contains("passad") || question.Contains("históric") || question.Contains("esquenta"))
            {
                return (DateTime.Today.AddDays(-45), DateTime.Today, "Últimos Jogos (Histórico Amplo)");
            }

            // Padrão Ampliado
            return (DateTime.Today.AddDays(-10), DateTime.Today.AddDays(7), "Recentes e Próximos");
        }

        /// <summary>
        /// Identifica equipes mencionadas na pergunta com base em palavras-chave.
        /// </summary>
        private async Task<(List<int> ids, List<string> names)> DetectTeamsWithNamesAsync(string question)
        {
            var teamIds = new List<int>();
            var teamNames = new List<string>();

            foreach (var (keyword, abbr) in _teamKeywords)
            {
                if (question.Contains(keyword))
                {
                    var team = await _teamRepository.GetByAbbreviationAsync(abbr);
                    if (team != null && !teamIds.Contains(team.Id))
                    {
                        teamIds.Add(team.Id);
                        teamNames.Add(team.Abbreviation);
                        _logger.LogDebug("Time detectado: '{Keyword}' → {Abbr} (ID: {Id})",
                            keyword, abbr, team.Id);
                    }
                }
            }
            return (teamIds, teamNames);
        }

        /// <summary>
        /// Analisa a pergunta para identificar jogadores e recuperar estatísticas recentes.
        /// </summary>
        private async Task<(string statsText, List<string> detectedPlayers)> GetRelevantPlayerStatsAsync(string question)
        {
            var detectedPlayers = new List<string>();
            var statsLines = new List<string>();

            var activePlayers = await _playerRepository.GetActivePlayersAsync();
            var matchedPlayers = new List<HoopGameNight.Core.Models.Entities.Player>();

            foreach (var p in activePlayers)
            {
                var fName = p.FirstName?.ToLowerInvariant() ?? "";
                var lName = p.LastName?.ToLowerInvariant() ?? "";
                var fullName = p.FullName?.ToLowerInvariant() ?? "";

                bool isMatch = false;

                var keywordMatch = _playerKeywords.FirstOrDefault(k => question.Contains(k.Key) && fullName.Contains(k.Value));
                if (keywordMatch.Key != null)
                {
                    isMatch = true;
                }
                else if (question.Contains(fullName))
                {
                    isMatch = true;
                }
                else if (!string.IsNullOrEmpty(fName) && fName.Length >= 4 && question.Contains(fName) && !question.Contains("hoje") && !question.Contains("ontem") && !question.Contains("amanha"))
                {
                    isMatch = true;
                }
                else if (!string.IsNullOrEmpty(lName) && lName.Length >= 4 && question.Contains(lName))
                {
                    isMatch = true;
                }

                if (isMatch && !matchedPlayers.Any(m => m.Id == p.Id))
                {
                    matchedPlayers.Add(p);
                    detectedPlayers.Add(p.FullName);
                }
            }

            if (!matchedPlayers.Any())
                return (string.Empty, detectedPlayers);

            foreach (var player in matchedPlayers.Take(3))
            {
                var gamelog = await _playerStatsService.GetPlayerRecentGamesAsync(player.Id, 5); 
                if (gamelog != null && gamelog.Games.Any())
                {
                    statsLines.Add($"--- Estatísticas Recentes: {player.FullName} ---");
                    foreach (var game in gamelog.Games.Take(5))
                    {
                        statsLines.Add($"{game.GameDate} vs {game.Opponent}: {game.Points} PTS, {game.Rebounds} REB, {game.Assists} AST, Minutos {game.Minutes}, FG: {game.FieldGoalsMade}/{game.FieldGoalsAttempted}, 3PT: {game.ThreePointersMade}/{game.ThreePointersAttempted}, FT: {game.FreeThrowsMade}/{game.FreeThrowsAttempted}");
                    }
                }
            }

            var statsText = statsLines.Any() ? string.Join("\n", statsLines) : string.Empty;
            return (statsText, detectedPlayers);
        }
    }
}
