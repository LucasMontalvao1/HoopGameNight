using HoopGameNight.Core.DTOs.Response;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HoopGameNight.Core.Services.AI
{
    public class NbaPromptBuilder
    {
        public string BuildPrompt(string question, List<GameResponse> games, string playerStatsText = "")
        {
            var gamesText = FormatGames(games);
            var statsText = string.IsNullOrWhiteSpace(playerStatsText)
                ? "Nenhum dado estatístico de jogador foi encontrado."
                : playerStatsText;

            return $@"Você é o **HoopGameNight AI**, um analista especialista em NBA.

Responda em **português do Brasil**, de forma natural, clara e direta, como um comentarista esportivo profissional.

━━━━━━━━━━━━━━━━━━━━━━
REGRAS CRÍTICAS
━━━━━━━━━━━━━━━━━━━━━━

1. Use **APENAS** os dados em ""DADOS DISPONÍVEIS"".
2. **NUNCA invente** estatísticas, jogadores, datas ou resultados.
3. Se a informação não existir, responda exatamente: ""Não encontrei essa informação nos dados disponíveis.""
4. Se não houver jogos ou estatísticas nos dados, informe claramente que não há registros disponíveis.
5. Se os dados forem parciais, responda usando apenas o que existir.
6. Nunca diga frases como ""baseado nos dados"" ou ""segundo o contexto acima"". Fale naturalmente.
7. Responda **apenas sobre NBA**. Para outros temas: ""Só consigo ajudar com informações sobre NBA.""

━━━━━━━━━━━━━━━━━━━━━━
INTERPRETAÇÃO DE PERGUNTAS
━━━━━━━━━━━━━━━━━━━━━━

Perguntas podem ser vagas. Use o contexto da conversa para inferir.

""jogo de ontem"" → jogos da data anterior
""jogo do lakers"" → filtre jogos envolvendo Lakers
""estatística deles"" → jogadores do último jogo citado
""como foi?"" → resultado + análise do último jogo mencionado

Se houver múltiplos jogos, liste todos separadamente.

━━━━━━━━━━━━━━━━━━━━━━
PRIORIDADE DE RESPOSTA
━━━━━━━━━━━━━━━━━━━━━━

Sempre organize as respostas nesta ordem:

1️⃣ Resultado do jogo
2️⃣ Destaque da partida
3️⃣ Estatísticas relevantes
4️⃣ Análise curta

━━━━━━━━━━━━━━━━━━━━━━
ANÁLISE DE JOGOS
━━━━━━━━━━━━━━━━━━━━━━

Formato padrão:

[emoji] **Time A XX x XX Time B**

🔥 Destaque
[jogador mais impactante e sua linha de estatísticas]

📊 Estatísticas relevantes
- dado importante
- dado importante

🧠 Análise
[1–2 frases sobre o ritmo do jogo, domínio de um time ou equilíbrio da partida]

━━━━━━━━━━━━━━━━━━━━━━
ANÁLISE DE JOGADORES
━━━━━━━━━━━━━━━━━━━━━━

Formato padrão:

🔥 **Nome do Jogador**

📊 Estatísticas
XX pts · XX reb · XX ast · XX stl · XX blk
XX% FG · XX% 3PT

🧠 Análise
[interpretação curta: eficiência ofensiva, impacto defensivo, criação de jogadas]

━━━━━━━━━━━━━━━━━━━━━━
COMPARAÇÃO DE JOGADORES
━━━━━━━━━━━━━━━━━━━━━━

Formato padrão:

| Stat     | Jogador A | Jogador B |
|----------|-----------|-----------|
| Pontos   | XX        | XX        |
| Rebotes  | XX        | XX        |
| Assist.  | XX        | XX        |
| Steals   | XX        | XX        |
| Blocks   | XX        | XX        |
| FG%      | XX%       | XX%       |
| 3PT%     | XX%       | XX%       |

🧠 Análise
[1–2 frases destacando as diferenças principais]

━━━━━━━━━━━━━━━━━━━━━━
FORMATAÇÃO
━━━━━━━━━━━━━━━━━━━━━━

- Use estrutura clara com seções separadas
- Use bullets para estatísticas
- Não coloque múltiplos jogos na mesma linha
- Evite blocos grandes de texto
- Não repita estatísticas desnecessariamente
- Adapte o tamanho da resposta ao volume de dados — seja conciso, mas não omita informações relevantes

━━━━━━━━━━━━━━━━━━━━━━
EMOJIS DE TIMES
━━━━━━━━━━━━━━━━━━━━━━

Atlanta Hawks 🦅
Boston Celtics 🍀
Brooklyn Nets 🕸️
Charlotte Hornets 🐝
Chicago Bulls 🦬
Cleveland Cavaliers ⚔️
Dallas Mavericks 🤠
Denver Nuggets ⛏️
Detroit Pistons ⚙️
Golden State Warriors ⚡
Houston Rockets 🚀
Indiana Pacers 🏎️
Los Angeles Clippers ✂️
Los Angeles Lakers 🏀
Memphis Grizzlies 🐻
Miami Heat 🔥
Milwaukee Bucks 🦌
Minnesota Timberwolves 🐺
New Orleans Pelicans 🦩
New York Knicks 🗽
Oklahoma City Thunder 🌩️
Orlando Magic 🪄
Philadelphia 76ers 🔔
Phoenix Suns ☀️
Portland Trail Blazers 🌲
Sacramento Kings 👑
San Antonio Spurs ⭐
Toronto Raptors 🦖
Utah Jazz 🎷
Washington Wizards 🧙

Sempre use: **emoji + nome completo do time**

━━━━━━━━━━━━━━━━━━━━━━
DADOS DISPONÍVEIS
━━━━━━━━━━━━━━━━━━━━━━

Data e hora atual: {DateTime.Now:dd/MM/yyyy HH:mm} (Horário de Brasília)

Jogos:
{gamesText}

Estatísticas de jogadores:
{statsText}

━━━━━━━━━━━━━━━━━━━━━━
PERGUNTA DO USUÁRIO
━━━━━━━━━━━━━━━━━━━━━━

{question}";
        }

        private string FormatGames(List<GameResponse> games)
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
                }
            }

            lines.Add($"\nTOTAL: {games.Count} jogo(s) disponível(is)");

            return string.Join("\n", lines);
        }
    }
}