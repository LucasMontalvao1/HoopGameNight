using HoopGameNight.Core.DTOs.Response;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HoopGameNight.Core.Services.AI
{
    public class NbaPromptBuilder
    {
        public string BuildPrompt(string question, List<GameResponse> games)
        {
            var today = DateTime.Today.ToString("dd/MM/yyyy");
            var gamesText = FormatGames(games);

            // Prompt restritivo para garantir que a IA utilize apenas os dados fornecidos.
            return $@"Você é um assistente de consulta de jogos da NBA. Você NÃO tem acesso à internet e NÃO conhece resultados de jogos.

═══════════════════════════════════════════════════════════════
ETAPA DE RACIOCÍNIO INTERNO (NÃO EXPOR NA RESPOSTA):
═══════════════════════════════════════════════════════════════
1. Identifique a data de hoje: {today}
2. Identifique o período solicitado na pergunta: ""{question}""
3. Filtre os jogos fornecidos abaixo que correspondem à pergunta.
4. Valide se há placares disponíveis ou apenas horários agendados.
5. Formate a resposta final seguindo as regras abaixo.

═══════════════════════════════════════════════════════════════
REGRAS DE FORMATAÇÃO E RESPOSTA:
═══════════════════════════════════════════════════════════════
1. Use exclusivamente os jogos listados abaixo. Se não houver, diga: ""Não encontrei essa informação no banco de dados"".
2. NUNCA invente dados. Seja direto e conciso.
3. Use MARKDOWN para a resposta final:
   - Use **negrito** para placares e nomes de times.
   - Use tabelas Markdown se listar mais de 3 jogos.
   - Use emoticons discretos (🏀, ✅, 🕒) se apropriado.
4. NUNCA mencione o processo de raciocínio interno na resposta final.

═══════════════════════════════════════════════════════════════
JOGOS DISPONÍVEIS NO BANCO DE DADOS:
═══════════════════════════════════════════════════════════════
{gamesText}

═══════════════════════════════════════════════════════════════
PERGUNTA DO USUÁRIO:
═══════════════════════════════════════════════════════════════
{question}

═══════════════════════════════════════════════════════════════
SUA RESPOSTA (EM PORTUGUÊS):
═══════════════════════════════════════════════════════════════";
        }

        private string FormatGames(List<GameResponse> games)
        {
            if (!games.Any())
            {
                return @"
AVISO: Nenhum jogo encontrado no banco de dados para este período.

Você DEVE responder: ""Não encontrei jogos no banco de dados para este período.""
";
            }

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
                    var status = game.Status switch
                    {
                        "Final" => $"[FINALIZADO]: {game.VisitorTeam.Abbreviation} {game.VisitorTeamScore} x {game.HomeTeamScore} {game.HomeTeam.Abbreviation}",
                        "Live" => $"[AO VIVO] (Período {game.Period}): {game.VisitorTeam.Abbreviation} {game.VisitorTeamScore} x {game.HomeTeamScore} {game.HomeTeam.Abbreviation}",
                        _ => $"[AGENDADO]: {game.VisitorTeam.Abbreviation} @ {game.HomeTeam.Abbreviation} às {game.DateTime:HH:mm}"
                    };

                    lines.Add($"   {status}");
                }
            }

            lines.Add("\n═══════════════════════════════════════════════════════════════");
            lines.Add($"TOTAL: {games.Count} jogos disponíveis");
            lines.Add("═══════════════════════════════════════════════════════════════");

            return string.Join("\n", lines);
        }
    }
}