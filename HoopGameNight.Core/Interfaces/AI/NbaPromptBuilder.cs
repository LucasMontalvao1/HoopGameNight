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
REGRAS DE PROCESSAMENTO:
═══════════════════════════════════════════════════════════════

1. Você deve usar exclusivamente os jogos listados abaixo.
2. Se a informação não estiver na lista, responda: ""Não encontrei essa informação no banco de dados"".
3. Nunca invente placares, datas, horários ou resultados.
4. Nunca use conhecimento prévio sobre NBA.
5. Nunca mencione jogadores, técnicos ou estatísticas (não temos esses dados).
6. Responda em português do Brasil.
7. Seja direto e conciso (máximo 4 linhas).

═══════════════════════════════════════════════════════════════
DATA DE REFERÊNCIA (HOJE): {today}
═══════════════════════════════════════════════════════════════

═══════════════════════════════════════════════════════════════
JOGOS DISPONÍVEIS NO BANCO DE DADOS:
═══════════════════════════════════════════════════════════════
{gamesText}

═══════════════════════════════════════════════════════════════
PERGUNTA DO USUÁRIO:
═══════════════════════════════════════════════════════════════
{question}

═══════════════════════════════════════════════════════════════
SUA RESPOSTA:
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