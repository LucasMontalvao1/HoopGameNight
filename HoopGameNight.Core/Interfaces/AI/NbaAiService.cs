using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HoopGameNight.Core.Services.AI
{
    public class NbaAiService : INbaAiService
    {
        private readonly IGameService _gameService;
        private readonly ITeamRepository _teamRepository;
        private readonly NbaPromptBuilder _promptBuilder;
        private readonly OllamaClient _ollamaClient;
        private readonly ICacheService _cacheService;
        private readonly ILogger<NbaAiService> _logger;

        // Mapeamento completo de times
        private static readonly Dictionary<string, string> TeamKeywords = new()
        {
            // Lakers
            {"lakers", "LAL"}, {"lal", "LAL"}, {"la lakers", "LAL"}, {"los angeles lakers", "LAL"},
            
            // Warriors
            {"warriors", "GSW"}, {"gsw", "GSW"}, {"golden", "GSW"}, {"golden state", "GSW"},
            
            // Celtics
            {"celtics", "BOS"}, {"bos", "BOS"}, {"boston", "BOS"},
            
            // Heat
            {"heat", "MIA"}, {"mia", "MIA"}, {"miami", "MIA"},
            
            // Bucks
            {"bucks", "MIL"}, {"mil", "MIL"}, {"milwaukee", "MIL"},
            
            // Nets
            {"nets", "BKN"}, {"bkn", "BKN"}, {"brooklyn", "BKN"},
            
            // Bulls
            {"bulls", "CHI"}, {"chi", "CHI"}, {"chicago", "CHI"},
            
            // Knicks
            {"knicks", "NYK"}, {"nyk", "NYK"}, {"new york", "NYK"}, {"ny", "NYK"},
            
            // 76ers
            {"sixers", "PHI"}, {"76ers", "PHI"}, {"phi", "PHI"}, {"philadelphia", "PHI"},
            
            // Mavericks
            {"mavericks", "DAL"}, {"mavs", "DAL"}, {"dal", "DAL"}, {"dallas", "DAL"},
            
            // Clippers
            {"clippers", "LAC"}, {"lac", "LAC"},
            
            // Suns
            {"suns", "PHX"}, {"phx", "PHX"}, {"phoenix", "PHX"},
            
            // Nuggets
            {"nuggets", "DEN"}, {"den", "DEN"}, {"denver", "DEN"},
            
            // Grizzlies
            {"grizzlies", "MEM"}, {"mem", "MEM"}, {"memphis", "MEM"},
            
            // Rockets
            {"rockets", "HOU"}, {"hou", "HOU"}, {"houston", "HOU"},
            
            // Spurs
            {"spurs", "SAS"}, {"sas", "SAS"}, {"sa", "SAS"}, {"san antonio", "SAS"},
            
            // Pelicans
            {"pelicans", "NOP"}, {"nop", "NOP"}, {"no", "NOP"}, {"new orleans", "NOP"},
            
            // Jazz
            {"jazz", "UTA"}, {"uta", "UTA"}, {"utah", "UTA"},
            
            // Kings
            {"kings", "SAC"}, {"sac", "SAC"}, {"sacramento", "SAC"},
            
            // Blazers
            {"blazers", "POR"}, {"por", "POR"}, {"portland", "POR"},
            
            // Timberwolves
            {"timberwolves", "MIN"}, {"wolves", "MIN"}, {"min", "MIN"}, {"minnesota", "MIN"},
            
            // Thunder
            {"thunder", "OKC"}, {"okc", "OKC"}, {"oklahoma", "OKC"},
            
            // Cavaliers
            {"cavaliers", "CLE"}, {"cavs", "CLE"}, {"cle", "CLE"}, {"cleveland", "CLE"},
            
            // Raptors
            {"raptors", "TOR"}, {"tor", "TOR"}, {"toronto", "TOR"},
            
            // Pacers
            {"pacers", "IND"}, {"ind", "IND"}, {"indiana", "IND"},
            
            // Hornets
            {"hornets", "CHA"}, {"cha", "CHA"}, {"charlotte", "CHA"},
            
            // Pistons
            {"pistons", "DET"}, {"det", "DET"}, {"detroit", "DET"},
            
            // Magic
            {"magic", "ORL"}, {"orl", "ORL"}, {"orlando", "ORL"},
            
            // Hawks
            {"hawks", "ATL"}, {"atl", "ATL"}, {"atlanta", "ATL"},
            
            // Wizards
            {"wizards", "WAS"}, {"was", "WAS"}, {"wsh", "WAS"}, {"washington", "WAS"}
        };

        public NbaAiService(
            IGameService gameService,
            ITeamRepository teamRepository,
            NbaPromptBuilder promptBuilder,
            OllamaClient ollamaClient,
            ICacheService cacheService,
            ILogger<NbaAiService> logger)
        {
            _gameService = gameService;
            _teamRepository = teamRepository;
            _promptBuilder = promptBuilder;
            _ollamaClient = ollamaClient;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<AskResponse> AskAsync(AskRequest request)
        {
            var normalizedQuestion = NormalizeQuestion(request.Question);
            var cacheKey = $"ai:ask:{normalizedQuestion}";

            // Tentar cache (5 minutos)
            var cached = await _cacheService.GetAsync<AskResponse>(cacheKey);
            if (cached != null)
            {
                _logger.LogInformation("Cache hit para a pergunta: '{Question}'", request.Question);
                cached.FromCache = true;
                return cached;
            }

            _logger.LogInformation("Processando consulta: {Question}", request.Question);

            // Busca inteligente
            var (games, period, detectedTeams) = await GetRelevantGamesWithContextAsync(normalizedQuestion);

            _logger.LogInformation("Métricas da busca: {Count} jogos localizados | Período: {Period} | Times: {Teams}",
                games.Count, period, string.Join(", ", detectedTeams));

            // Validação: se não encontrou jogos
            if (!games.Any())
            {
                _logger.LogWarning("Nenhum registro de jogo localizado para o contexto fornecido.");
                return new AskResponse
                {
                    Question = request.Question,
                    Answer = "Não encontrei jogos no banco de dados para este período/time.",
                    GamesAnalyzed = 0,
                    FromCache = false,
                    DataSource = "Database",
                    Period = period,
                    DetectedTeams = detectedTeams
                };
            }

            // Montar prompt
            var prompt = _promptBuilder.BuildPrompt(request.Question, games);

            // Enviar ao Ollama
            var answer = await _ollamaClient.GenerateAsync(prompt);

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

            // Salvar em cache (5 minutos)
            await _cacheService.SetAsync(cacheKey, response, TimeSpan.FromMinutes(5));

            return response;
        }

        private string NormalizeQuestion(string question)
        {
            return question.Trim().ToLower()
                .Replace("?", "")
                .Replace("!", "")
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
            if (teamIds.Any())
            {
                // Buscar apenas jogos dos times detectados
                foreach (var teamId in teamIds)
                {
                    var currentDate = startDate;
                    while (currentDate <= endDate)
                    {
                        var dayGames = await _gameService.GetGamesByDateAsync(currentDate);
                        var teamGames = dayGames.Where(g =>
                            g.HomeTeam.Id == teamId || g.VisitorTeam.Id == teamId);
                        allGames.AddRange(teamGames);
                        currentDate = currentDate.AddDays(1);
                    }
                }
            }
            else
            {
                // Buscar todos os jogos do período
                var currentDate = startDate;
                while (currentDate <= endDate)
                {
                    var dayGames = await _gameService.GetGamesByDateAsync(currentDate);
                    allGames.AddRange(dayGames);
                    currentDate = currentDate.AddDays(1);
                }
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
                return (DateTime.Today, DateTime.Today.AddDays(7), "Próximos 7 dias");
            }

            // Últimos
            if (question.Contains("últim") || question.Contains("ultim") || question.Contains("recente") ||
                question.Contains("recent") || question.Contains("passad"))
            {
                return (DateTime.Today.AddDays(-7), DateTime.Today, "Últimos 7 dias");
            }

            // Padrão
            return (DateTime.Today.AddDays(-3), DateTime.Today.AddDays(3), "±3 dias");
        }

        /// <summary>
        /// Identifica equipes mencionadas na pergunta com base em palavras-chave.
        /// </summary>
        private async Task<(List<int> ids, List<string> names)> DetectTeamsWithNamesAsync(string question)
        {
            var teamIds = new List<int>();
            var teamNames = new List<string>();

            foreach (var (keyword, abbr) in TeamKeywords)
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
    }
}