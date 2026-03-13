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
                ? "Nenhum dado estatístico de jogador foi encontrado nos registros locais."
                : playerStatsText;

            return $@"Você é o **Coach Assistant**, um analista técnico da NBA. Responda de forma DIRETA, CURTA e OBJETIVA.

### 📋 REGRAS DE OURO:
1. **OBJETIVIDADE:** Vá direto ao ponto. Use listas e negrito.
2. **SEM DESCULPAS:** Se os dados de jogos estão listados abaixo, NÃO diga ""infelizmente não tenho informações"". Use o que está nos DADOS DISPONÍVEIS.
3. **FORMATO:** 
   - Se perguntarem ""quais jogos"", liste apenas os confrontos, horários e placares.
   - Se perguntarem sobre um jogo específico, use: Placar -> Destaque -> Breve Comentário.
4. **VERACIDADE:** Se um dado (como rebotes de um jogador específico) não estiver nos DADOS DISPONÍVEIS, apenas não mencione ou diga que o registro local não possui detalhes deste scout.

### 📋 DADOS DISPONÍVEIS ({DateTime.Now:dd/MM/yyyy HH:mm}):

**JOGOS:**
{gamesText}

**ESTATÍSTICAS DE JOGADORES:**
{statsText}

---
**PERGUNTA:** ""{question}""

Responda agora (Português Brasil):";
        }

        public string BuildGameSummaryJsonPrompt(GameResponse game, GameLeadersResponse? leaders, GamePlayerStatsResponse? boxscore = null)
        {
            var homeTeam = game.HomeTeam;
            var awayTeam = game.VisitorTeam;
            var leadersText = FormatLeaders(leaders);
            var boxscoreText = FormatFullBoxscore(boxscore);

            return $@"
Você é um analista de jogos da NBA. Sua tarefa é gerar um resumo em JSON estruturado para um aplicativo.
O JSON DEVE seguir este formato exato:
{{
  ""summary"": ""Texto em MarkDown do resumo aqui..."",
  ""highlights"": [
    {{ ""title"": ""Destaque 1"", ""description"": ""Descrição curta"", ""type"": ""performance"" }},
    {{ ""title"": ""Destaque 2"", ""description"": ""Descrição curta"", ""type"": ""moment"" }}
  ]
}}

### REGRAS DO CONTEÚDO:
- Use APENAS os dados presentes no boxscore.
- O campo 'summary' deve conter o resumo tradicional em MarkDown (Análise da Partida, Líderes e Insights).
- O campo 'highlights' deve conter de 3 a 5 destaques curtos e impactantes.
- Tipos válidos para highlights: 'performance', 'stat', 'moment', 'bench'.
- Responda APENAS o JSON, sem textos explicativos antes ou depois.

### DADOS PARA O RESUMO:
- Placar Final: {awayTeam.DisplayName} {game.VisitorTeamScore} x {game.HomeTeamScore} {homeTeam.DisplayName}
- {leadersText}

**BOXSCORE DETALHADO:**
{boxscoreText}

Gere o JSON agora:";
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