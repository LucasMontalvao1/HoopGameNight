using FluentAssertions;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Services.AI;
using Xunit;

namespace HoopGameNight.Tests.Unit.Core.AI
{
    public class NbaPromptBuilderTests
    {
        private readonly NbaPromptBuilder _builder;

        public NbaPromptBuilderTests()
        {
            _builder = new NbaPromptBuilder();
        }

        [Fact]
        public void BuildPrompt_ShouldInclude_RestrictionRules_And_MarkdownUsage()
        {
            // Arrange
            var games = new List<GameResponse>(); // Contexto vazio
            var question = "Jogos de hoje";

            // Act
            var prompt = _builder.BuildPrompt(question, games);

            // Assert
            prompt.Should().Contain("regras_de_ouro", "Deve conter a seção de regras obrigatórias");
            prompt.Should().Contain("NÃO é fonte de verdade", "Deve negar que a IA é fonte de verdade");
            prompt.Should().Contain("VALIDAÇÃO DE ESCUPO", "Deve exigir validação de escopo");
            prompt.Should().Contain("VALIDAÇÃO DE DADOS", "Deve exigir validação de dados");
            prompt.Should().Contain("Gere a resposta em MARKDOWN", "Deve exigir formato Markdown");
        }

        [Fact]
        public void BuildPrompt_ShouldLabel_Dates_Correctly()
        {
            // Arrange
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            
            var games = new List<GameResponse>
            {
                new GameResponse 
                { 
                    Date = today, 
                    HomeTeam = new TeamSummaryResponse { Abbreviation = "LAL" },
                    VisitorTeam = new TeamSummaryResponse { Abbreviation = "GSW" },
                    Status = "Scheduled"
                },
                new GameResponse 
                { 
                    Date = tomorrow, 
                    HomeTeam = new TeamSummaryResponse { Abbreviation = "BOS" },
                    VisitorTeam = new TeamSummaryResponse { Abbreviation = "MIA" },
                    Status = "Scheduled"
                }
            };

            // Act
            var prompt = _builder.BuildPrompt("Quais os jogos?", games);

            // Assert
            prompt.Should().Contain($"HOJE ({today:dd/MM/yyyy})");
            prompt.Should().Contain($"AMANHÃ ({tomorrow:dd/MM/yyyy})");
        }

        [Fact]
        public void BuildPrompt_ShouldHandle_EmptyGames_Gracefully()
        {
            // Arrange
            var games = new List<GameResponse>();

            // Act
            var prompt = _builder.BuildPrompt("Tem jogo?", games);

            // Assert
            prompt.Should().Contain("Nenhum jogo encontrado no banco de dados para este período");
            prompt.Should().Contain("Você DEVE responder: \"Não encontrei jogos no banco de dados para este período.\"");
        }
    }
}
