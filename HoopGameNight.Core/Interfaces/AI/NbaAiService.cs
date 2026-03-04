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
        private readonly ITeamRepository _teamRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IPlayerStatsService _playerStatsService;
        private readonly NbaPromptBuilder _promptBuilder;
        private readonly GroqClient _groqClient;
        private readonly ICacheService _cacheService;
        private readonly ILogger<NbaAiService> _logger;

        private readonly Dictionary<string, string> _teamKeywords = new();
        private readonly Dictionary<string, string> _playerKeywords = new();

        public NbaAiService(
            IGameService gameService,
            ITeamRepository teamRepository,
            IPlayerRepository playerRepository,
            IPlayerStatsService playerStatsService,
            NbaPromptBuilder promptBuilder,
            GroqClient groqClient,
            ICacheService cacheService,
            ILogger<NbaAiService> logger)
        {
            _gameService = gameService;
            _teamRepository = teamRepository;
            _playerRepository = playerRepository;
            _playerStatsService = playerStatsService;
            _promptBuilder = promptBuilder;
            _groqClient = groqClient;
            _cacheService = cacheService;
            _logger = logger;

            LoadTeamKeywords();
            LoadPlayerKeywords();
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

            // Busca inteligente
            var (games, period, detectedTeams) = await GetRelevantGamesWithContextAsync(normalizedQuestion);

            _logger.LogInformation("Métricas da busca: {Count} jogos localizados | Período: {Period} | Times: {Teams}",
                games.Count, period, string.Join(", ", detectedTeams));

            // Validação: se não encontrou jogos E não encontrou jogadores
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

            // Montar prompt
            var prompt = _promptBuilder.BuildPrompt(request.Question, games, playerStatsText);

            // Enviar ao Groq
            var answer = await _groqClient.GenerateAsync(prompt);

            var response = new AskResponse
            {
                Question = request.Question,
                Answer = answer,
                GamesAnalyzed = games.Count,
                FromCache = false,
                DataSource = "Database",
                Period = period,
                DetectedTeams = detectedTeams
            };

            return response;
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

            // 3. Buscar jogos 
            var rangeGames = await _gameService.GetGamesByDateRangeAsync(startDate, endDate);

            if (teamIds.Any())
            {
                // Filtrar apenas jogos dos times detectados
                foreach (var teamId in teamIds)
                {
                    var teamGames = rangeGames.Where(g =>
                        g.HomeTeam.Id == teamId || g.VisitorTeam.Id == teamId);
                    allGames.AddRange(teamGames);
                }
            }
            else
            {
                // Usar todos os jogos do período
                allGames.AddRange(rangeGames);
            }

            // 4. Remover duplicatas e limitar
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
                        statsLines.Add($"{game.GameDate} vs {game.Opponent}: {game.Points} PTS, {game.Rebounds} REB, {game.Assists} AST, Minutos {game.Minutes}");
                    }
                }
            }

            var statsText = statsLines.Any() ? string.Join("\n", statsLines) : string.Empty;
            return (statsText, detectedPlayers);
        }
    }
}
