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

        [Fact(DisplayName = "Deve conter regras obrigatórias e instrução de resposta em Markdown")]
        public void BuildPrompt_ShouldInclude_RestrictionRules_And_MarkdownUsage()
        {
            // Arrange
            var games = new List<GameResponse>();
            var question = "Jogos de hoje";

            // Act
            var prompt = _builder.BuildPrompt(question, games);

            // Assert
            prompt.Should().Contain("REGRAS CRÍTICAS DE RESPOSTA:", "Deve conter a seção de regras obrigatórias");
            prompt.Should().Contain("Nunca invente estatísticas", "Deve negar que a IA é fonte de verdade");
            prompt.Should().Contain("BASE DE DADOS", "Deve focar estritamente nos dados");
            prompt.Should().Contain("VERACIDADE", "Deve exigir informações verídicas");
            prompt.Should().Contain("Use Markdown", "Deve exigir formato Markdown");
        }

        [Fact(DisplayName = "Deve rotular datas corretamente como HOJE e AMANHÃ")]
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

        [Fact(DisplayName = "Deve lidar com lista vazia de jogos sem lançar exceção")]
        public void BuildPrompt_ShouldHandle_EmptyGames_Gracefully()
        {
            // Arrange
            var games = new List<GameResponse>();

            // Act
            var prompt = _builder.BuildPrompt("Tem jogo?", games);

            // Assert
            prompt.Should().Contain("Nenhum jogo encontrado no banco de dados para este período");
        }

        [Fact(DisplayName = "Deve formatar jogo finalizado com status FINALIZADO e placar")]
        public void BuildPrompt_ShouldFormat_FinalGame_Correctly()
        {
            // Arrange
            var games = new List<GameResponse>
            {
                new GameResponse
                {
                    Date = DateTime.Today,
                    Status = "Final",
                    HomeTeam = new TeamSummaryResponse { Abbreviation = "LAL" },
                    VisitorTeam = new TeamSummaryResponse { Abbreviation = "GSW" },
                    HomeTeamScore = 110,
                    VisitorTeamScore = 105
                }
            };

            // Act
            var prompt = _builder.BuildPrompt("Resultado de hoje?", games);

            // Assert
            prompt.Should().Contain("(Finalizado)");
            prompt.Should().Contain("LAL");
            prompt.Should().Contain("GSW");
        }

        [Fact(DisplayName = "Deve formatar jogo ao vivo com período e placar atual")]
        public void BuildPrompt_ShouldFormat_LiveGame_Correctly()
        {
            // Arrange
            var games = new List<GameResponse>
            {
                new GameResponse
                {
                    Date = DateTime.Today,
                    Status = "Live",
                    Period = 3,
                    HomeTeam = new TeamSummaryResponse { Abbreviation = "BOS" },
                    VisitorTeam = new TeamSummaryResponse { Abbreviation = "MIA" },
                    HomeTeamScore = 88,
                    VisitorTeamScore = 82
                }
            };

            // Act
            var prompt = _builder.BuildPrompt("Jogo ao vivo?", games);

            // Assert
            prompt.Should().Contain("(AO VIVO - P3)");
        }

        [Fact(DisplayName = "Deve incluir a pergunta do usuário no prompt gerado")]
        public void BuildPrompt_ShouldInclude_UserQuestion()
        {
            // Arrange
            var question = "Quais times jogam hoje?";

            // Act
            var prompt = _builder.BuildPrompt(question, new List<GameResponse>());

            // Assert
            prompt.Should().Contain(question, "A pergunta do usuário deve estar presente no prompt");
        }

        [Fact(DisplayName = "Deve incluir data e hora atual com referência ao horário de Brasília")]
        public void BuildPrompt_ShouldInclude_CurrentDateTime()
        {
            // Act
            var prompt = _builder.BuildPrompt("teste", new List<GameResponse>());

            // Assert
            prompt.Should().Contain(DateTime.Now.ToString("dd/MM/yyyy"), "Deve conter a data atual");
            prompt.Should().Contain("(Brasília)", "Deve referenciar o fuso horário");
        }

        [Fact(DisplayName = "Deve exibir total de jogos disponíveis no prompt")]
        public void BuildPrompt_ShouldShow_TotalGamesCount()
        {
            // Arrange
            var games = new List<GameResponse>
            {
                new GameResponse
                {
                    Date = DateTime.Today,
                    Status = "Scheduled",
                    HomeTeam = new TeamSummaryResponse { Abbreviation = "LAL" },
                    VisitorTeam = new TeamSummaryResponse { Abbreviation = "GSW" }
                },
                new GameResponse
                {
                    Date = DateTime.Today,
                    Status = "Scheduled",
                    HomeTeam = new TeamSummaryResponse { Abbreviation = "BOS" },
                    VisitorTeam = new TeamSummaryResponse { Abbreviation = "MIA" }
                }
            };

            // Act
            var prompt = _builder.BuildPrompt("Jogos?", games);

            // Assert
            prompt.Should().Contain("TOTAL: 2 jogo(s) disponível(is)");
        }

        [Fact(DisplayName = "Deve rotular jogos passados como ONTEM e futuros como FUTURO")]
        public void BuildPrompt_ShouldLabel_PastAndFuture_Dates_Correctly()
        {
            // Arrange
            var yesterday = DateTime.Today.AddDays(-1);
            var future = DateTime.Today.AddDays(3);

            var games = new List<GameResponse>
            {
                new GameResponse
                {
                    Date = yesterday,
                    Status = "Final",
                    HomeTeam = new TeamSummaryResponse { Abbreviation = "LAL" },
                    VisitorTeam = new TeamSummaryResponse { Abbreviation = "GSW" }
                },
                new GameResponse
                {
                    Date = future,
                    Status = "Scheduled",
                    HomeTeam = new TeamSummaryResponse { Abbreviation = "BOS" },
                    VisitorTeam = new TeamSummaryResponse { Abbreviation = "MIA" }
                }
            };

            // Act
            var prompt = _builder.BuildPrompt("Jogos?", games);

            // Assert
            prompt.Should().Contain($"ONTEM ({yesterday:dd/MM/yyyy})");
            prompt.Should().Contain($"FUTURO ({future:dd/MM/yyyy})");
        }
    }
}