using HoopGameNight.Core.DTOs.Response;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HoopGameNight.Core.Services.AI
{
    public class NbaPromptBuilder
    {
        public string BuildPrompt(string question, List<GameResponse> games, string playerStatsText = "", Dictionary<int, GameLeadersResponse>? leaders = null)
        {
            var gamesText = FormatGames(games, leaders);
            var statsText = string.IsNullOrWhiteSpace(playerStatsText)
                ? "Nenhum dado estatístico de jogador foi encontrado."
                : playerStatsText;

            return $@"Você é o **Coach Assistant**, um analista sênior da NBA responsável por responder perguntas de fãs de basquete.

### 📋 REGRAS CRÍTICAS DE RESPOSTA:
1. **BASE DE DADOS:** Use **APENAS** os dados fornecidos em ""DADOS DISPONÍVEIS"".
2. **VERACIDADE:** Nunca invente estatísticas, eventos, jogadores ou resultados. Se não houver dados, responda: ""Não encontrei essa informação nos registros oficiais.""
3. **TONALIDADE:** Responda de forma natural, clara e objetiva, como um resumo esportivo de um portal de notícias (ex: ESPN, Globo Esporte).
4. **FOCO:** Responda apenas sobre NBA. Para outros temas, informe que seu conhecimento é restrito ao basquete.
5. **ESTRUTURA:** Use Markdown (negrito, tabelas e listas) para organizar a resposta e facilitar a leitura.
6. **PROMPT DE JOGOS:** Se a pergunta for sobre como foi um jogo, siga a ordem: Resultado -> Destaque -> Análise curta.

---

### 📋 DADOS DISPONÍVEIS PARA CONSULTA:

**DATA E HORA ATUAL:** {DateTime.Now:dd/MM/yyyy HH:mm} (Brasília)

**JOGOS:**
{gamesText}

**ESTATÍSTICAS DE JOGADORES:**
{statsText}

---

### 💬 PERGUNTA DO USUÁRIO:
""{question}""

Responda agora de forma profissional em Português do Brasil:";
        }

        public string BuildGameSummaryPrompt(GameResponse game, GameLeadersResponse? leaders, GamePlayerStatsResponse? boxscore = null)
        {
            var homeTeam = game.HomeTeam;
            var awayTeam = game.VisitorTeam;
            var leadersText = FormatLeaders(leaders);
            var boxscoreText = FormatFullBoxscore(boxscore);

            return $@"
Você é um analista de jogos da NBA responsável por gerar um resumo envolvente para um aplicativo de fãs de basquete.
O resumo deve ser baseado APENAS nos dados do boxscore fornecidos abaixo.

### REGRAS IMPORTANTES:
- Use APENAS os dados presentes no boxscore.
- Não invente eventos do jogo (runs, viradas, momentos de quarto, etc.).
- Baseie todos os insights em números concretos.
- Evite frases genéricas como ""grande jogo"" ou ""partida intensa"".
- O resumo deve ser em Português do Brasil.
- Não gere insights muito longos.

### ANÁLISE DO BOXSCORE (OBRIGATÓRIO):
Ao gerar insights, considere:
- Diferença de pontuação entre os times ({game.VisitorTeamScore} x {game.HomeTeamScore})
- Rebotes totais e Assistências totais
- Roubos de bola e tocos
- Aproveitamento de arremessos (FG%, 3PT%, FT%)
- Jogadores com alto aproveitamento (FG > 60%)
- Jogadores com alto +/- (impacto em quadra)
- Jogadores que marcaram muitos pontos em poucos minutos
- Contribuição do banco

### ESTRUTURA DA RESPOSTA (OBRIGATÓRIO):

## Análise da Partida
Um parágrafo curto explicando quem venceu, a diferença de pontos e o destaque geral baseado nos números.

## Líderes Estatísticos
| Categoria | {awayTeam.Abbreviation} | {homeTeam.Abbreviation} |
| :--- | :--- | :--- |
| **Pontos** | [Nome] ([Valor]) | [Nome] ([Valor]) |
| **Rebotes** | [Nome] ([Valor]) | [Nome] ([Valor]) |
| **Assistências** | [Nome] ([Valor]) | [Nome] ([Valor]) |

## Insights da Partida
Gerar de 4 a 6 insights curtos, cada um em sua própria linha.
**Formato:** Use o caractere ""•"" seguido do insight.

---

### 📋 DADOS PARA O RESUMO:
- Placar Final: {awayTeam.DisplayName} {game.VisitorTeamScore} x {game.HomeTeamScore} {homeTeam.DisplayName}
- {leadersText}

**BOXSCORE DETALHADO:**
{boxscoreText}

### TOM DO TEXTO:
- Natural, claro, objetivo e funcional.
- Semelhante a um resumo esportivo de site de notícias.

Gere o resumo agora:";
        }

        public string FormatFullBoxscore(GamePlayerStatsResponse? boxscore)
        {
            if (boxscore == null) return "Dados detalhados do boxscore não disponíveis.";

            var lines = new List<string>();

            void FormatTeamStats(string teamName, List<PlayerGameStatsDetailedResponse> players)
            {
                lines.Add($"\n--- {teamName} ---");
                // Totais do Time
                var teamPts = players.Sum(p => p.Points);
                var teamReb = players.Sum(p => p.TotalRebounds);
                var teamAst = players.Sum(p => p.Assists);
                var teamStl = players.Sum(p => p.Steals);
                var teamBlk = players.Sum(p => p.Blocks);
                var teamTov = players.Sum(p => p.Turnovers);
                
                lines.Add($"TOTAIS: {teamPts} PTS, {teamReb} REB, {teamAst} AST, {teamStl} STL, {teamBlk} BLK, {teamTov} TOV");

                // Principais performances (filtrando DNP e limitando para não estourar contexto)
                foreach (var p in players.Where(p => !p.DidNotPlay).OrderByDescending(p => p.Points).Take(8))
                {
                    lines.Add($"{p.PlayerFullName}: {p.Points} PTS, {p.TotalRebounds} REB, {p.Assists} AST, {p.FieldGoalsFormatted} FG ({p.FieldGoalPercentage}%), {p.ThreePointersFormatted} 3PT ({p.ThreePointPercentage}%), +/-: {p.PlusMinus}, {p.MinutesPlayed} MIN");
                }
            }

            FormatTeamStats(boxscore.VisitorTeam, boxscore.VisitorTeamStats);
            FormatTeamStats(boxscore.HomeTeam, boxscore.HomeTeamStats);

            return string.Join("\n", lines);
        }

        private string FormatLeaders(GameLeadersResponse? leaders)
        {
            if (leaders == null) return "Estatísticas de líderes ainda não disponíveis para este jogo.";

            var lines = new List<string> { "LÍDERES DA PARTIDA:" };
            
            void AddTeamLeaders(TeamGameLeaders teamLeaders)
            {
                lines.Add($"\n🏀 **{teamLeaders.TeamName}**:");
                if (teamLeaders.PointsLeader != null)
                    lines.Add($"   - 🔥 Pontos: **{teamLeaders.PointsLeader.PlayerName}** ({teamLeaders.PointsLeader.Value} pts)");
                if (teamLeaders.ReboundsLeader != null)
                    lines.Add($"   - 🛡️ Rebotes: **{teamLeaders.ReboundsLeader.PlayerName}** ({teamLeaders.ReboundsLeader.Value} reb)");
                if (teamLeaders.AssistsLeader != null)
                    lines.Add($"   - 🪄 Assistências: **{teamLeaders.AssistsLeader.PlayerName}** ({teamLeaders.AssistsLeader.Value} ast)");
            }

            AddTeamLeaders(leaders.VisitorTeamLeaders);
            AddTeamLeaders(leaders.HomeTeamLeaders);

            return string.Join("\n", lines);
        }

        private string FormatGames(List<GameResponse> games, Dictionary<int, GameLeadersResponse>? gameLeaders = null)
        {
            if (!games.Any())
                return "Nenhum jogo encontrado no banco de dados para este período.";

            var grouped = games.GroupBy(g => g.Date.Date).OrderBy(g => g.Key);
            var lines = new List<string>();

            foreach (var group in grouped)
            {
                var dateLabel = group.Key == DateTime.Today ? "HOJE" :
                                group.Key == DateTime.Today.AddDays(1) ? "AMANHÃ" :
                                group.Key == DateTime.Today.AddDays(-1) ? "ONTEM" :
                                group.Key > DateTime.Today ? "FUTURO" : "PASSADO";

                lines.Add($"\n{dateLabel} ({group.Key:dd/MM/yyyy}):");
                lines.Add("─────────────────────────────────────");

                foreach (var game in group.OrderBy(g => g.DateTime))
                {
                    var vTeam = string.IsNullOrEmpty(game.VisitorTeam.Name)
                        ? game.VisitorTeam.Abbreviation
                        : game.VisitorTeam.Name;

                    var hTeam = string.IsNullOrEmpty(game.HomeTeam.Name)
                        ? game.HomeTeam.Abbreviation
                        : game.HomeTeam.Name;

                    var statusNormalized = game.Status?.Trim().ToUpperInvariant();
                    var status = statusNormalized switch
                    {
                        "FINAL" or "FINISHED" or "COMPLETE" =>
                            $"- **{vTeam}** {game.VisitorTeamScore} x {game.HomeTeamScore} **{hTeam}** (Finalizado)",
                        "LIVE" or "IN_PROGRESS" or "STATUS_IN_PROGRESS" =>
                            $"- **{vTeam}** {game.VisitorTeamScore} x {game.HomeTeamScore} **{hTeam}** (AO VIVO - P{game.Period})",
                        _ =>
                            $"- **{vTeam}** @ **{hTeam}** (Agendado às {game.DateTime:HH:mm})"
                    };

                    lines.Add($"   {status}");

                    if (gameLeaders != null && gameLeaders.TryGetValue(game.Id, out var leaders))
                    {
                        if (leaders.VisitorTeamLeaders.PointsLeader != null || leaders.HomeTeamLeaders.PointsLeader != null)
                        {
                            var bestLeader = (leaders.VisitorTeamLeaders.PointsLeader?.Value ?? 0) > (leaders.HomeTeamLeaders.PointsLeader?.Value ?? 0)
                                ? leaders.VisitorTeamLeaders.PointsLeader
                                : leaders.HomeTeamLeaders.PointsLeader;

                            if (bestLeader != null)
                            {
                                lines.Add($"      🔥 Destaque: **{bestLeader.PlayerName}** ({bestLeader.Value} pts)");
                            }
                        }
                    }
                }
            }

            lines.Add($"\nTOTAL: {games.Count} jogo(s) disponível(is)");

            return string.Join("\n", lines);
        }
    }
}