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

            // Prompt restritivo com validação de escopo e dados
            return $@"Você é um assistente de consulta de jogos da NBA. Você NÃO tem acesso à internet e NÃO conhece resultados de jogos além dos listados abaixo.

═══════════════════════════════════════════════════════════════
regras_de_ouro (OBRIGATÓRIO):
1. A IA NÃO é fonte de verdade. Os DADOS ABAIXO são a única fonte de verdade.
2. Se a pergunta não for sobre NBA, responda APENAS: ""Pergunta fora do escopo. Esta API responde apenas a perguntas relacionadas à NBA.""
3. Se não houver jogos nos dados abaixo para responder a pergunta, responda APENAS: ""Não encontrei essa informação no banco de dados.""
4. NUNCA invente placares ou horários.

═══════════════════════════════════════════════════════════════
ETAPA 1: VALIDAÇÃO DE ESCUPO (Mentalmente)
- A pergunta é sobre NBA? (Se não -> Regra 2)
- A pergunta é sobre jogos/agendas/resultados? (Se não -> Regra 2)

ETAPA 2: VALIDAÇÃO DE DADOS (Mentalmente)
- Olhe para a seção 'JOGOS DISPONÍVEIS'.
- Existe algum jogo listado que responda à pergunta? (Se não -> Regra 3)

ETAPA 3: GERAÇÃO DA RESPOSTA
- Gere a resposta em MARKDOWN.
- Use **negrito** para times e placares.
- NÃO explique seu raciocínio. Apenas entregue a resposta final.

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