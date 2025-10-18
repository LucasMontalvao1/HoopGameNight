using FluentAssertions;
using HoopGameNight.Core.DTOs.External.BallDontLie;
using HoopGameNight.Core.Exceptions;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Infrastructure.ExternalServices;
using HoopGameNight.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HoopGameNight.Tests.Unit.Infrastructure.ExternalServices
{
    public class BallDontLieServiceTests : IDisposable
    {
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly Mock<ILogger<BallDontLieService>> _mockLogger;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly HttpClient _httpClient;
        private readonly BallDontLieService _ballDontLieService;
        private const string API_KEY = "test-api-key";
        private const string BASE_URL = "https://api.balldontlie.io/v1";

        public BallDontLieServiceTests()
        {
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _mockLogger = new Mock<ILogger<BallDontLieService>>();
            _mockConfiguration = new Mock<IConfiguration>();

            _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri(BASE_URL)
            };

            ConfigurarMocks();

            var mockEspnService = new Mock<IEspnApiService>();
            var mockNbaStatsService = new Mock<INbaStatsApiService>();

            _ballDontLieService = new BallDontLieService(
                _httpClient,
                _mockLogger.Object,
                _mockConfiguration.Object,
                mockEspnService.Object,
                mockNbaStatsService.Object
            );
        }

        #region Testes de GetTodaysGamesAsync

        [Fact(DisplayName = "Deve retornar jogos de hoje quando API retorna dados válidos")]
        public async Task DeveRetornarJogosDeHoje_QuandoApiRetornaDadosValidos()
        {
            // Arrange
            var jogosEsperados = TestDataBuilder.CreateBallDontLieGames(3);
            var respostaApi = new BallDontLieApiResponse<BallDontLieGameDto>
            {
                Data = jogosEsperados
            };

            ConfigurarRespostaHttp(HttpStatusCode.OK, respostaApi);

            // Act
            var resultado = await _ballDontLieService.GetTodaysGamesAsync();

            // Assert
            resultado.Should().NotBeNull();
            resultado.Should().HaveCount(3);
            resultado.Should().BeEquivalentTo(jogosEsperados);

            VerificarRequisicaoHttp(HttpMethod.Get, $"/v1/games?dates[]={DateTime.Today:yyyy-MM-dd}");
        }

        [Fact(DisplayName = "Deve retornar lista vazia quando não há jogos hoje")]
        public async Task DeveRetornarListaVazia_QuandoNaoHaJogosHoje()
        {
            // Arrange
            var respostaApi = new BallDontLieApiResponse<BallDontLieGameDto>
            {
                Data = new List<BallDontLieGameDto>()
            };

            ConfigurarRespostaHttp(HttpStatusCode.OK, respostaApi);

            // Act
            var resultado = await _ballDontLieService.GetTodaysGamesAsync();

            // Assert
            resultado.Should().NotBeNull();
            resultado.Should().BeEmpty();
        }

        #endregion

        #region Testes de GetGamesByDateAsync

        [Theory(DisplayName = "Deve retornar jogos para data específica")]
        [InlineData("2024-01-15", 5)]
        [InlineData("2024-12-25", 10)]
        [InlineData("2023-06-01", 0)]
        public async Task DeveRetornarJogosParaData_QuandoDataValida(string dataString, int quantidadeJogos)
        {
            // Arrange
            var data = DateTime.Parse(dataString);
            var jogosEsperados = TestDataBuilder.CreateBallDontLieGames(quantidadeJogos);
            var respostaApi = new BallDontLieApiResponse<BallDontLieGameDto>
            {
                Data = jogosEsperados
            };

            ConfigurarRespostaHttp(HttpStatusCode.OK, respostaApi);

            // Act
            var resultado = await _ballDontLieService.GetGamesByDateAsync(data);

            // Assert
            resultado.Should().HaveCount(quantidadeJogos);
            VerificarRequisicaoHttp(HttpMethod.Get, $"/v1/games?dates[]={data:yyyy-MM-dd}");
        }

        #endregion

        #region Testes de GetAllTeamsAsync

        [Fact(DisplayName = "Deve retornar todos os times da NBA")]
        public async Task DeveRetornarTodosTimesDaNBA()
        {
            // Arrange
            var timesEsperados = CriarTimesNBA();
            var respostaApi = new BallDontLieApiResponse<BallDontLieTeamDto>
            {
                Data = timesEsperados
            };

            ConfigurarRespostaHttp(HttpStatusCode.OK, respostaApi);

            // Act
            var resultado = await _ballDontLieService.GetAllTeamsAsync();

            // Assert
            resultado.Should().HaveCount(30);
            resultado.Should().BeEquivalentTo(timesEsperados);
            VerificarRequisicaoHttp(HttpMethod.Get, "/v1/teams");
        }

        [Fact(DisplayName = "Deve usar cache ao buscar times múltiplas vezes")]
        public async Task DeveUsarCache_AoBuscarTimesMultiplasVezes()
        {
            // Arrange
            var times = CriarTimesNBA();
            var respostaApi = new BallDontLieApiResponse<BallDontLieTeamDto>
            {
                Data = times
            };

            // Configurar apenas uma resposta (cache deve evitar múltiplas chamadas)
            var respostaHttp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(respostaApi), Encoding.UTF8, "application/json")
            };

            _mockHttpMessageHandler.Protected()
                .SetupSequence<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(respostaHttp)
                .ReturnsAsync(respostaHttp)
                .ReturnsAsync(respostaHttp);

            // Act
            var resultado1 = await _ballDontLieService.GetAllTeamsAsync();
            var resultado2 = await _ballDontLieService.GetAllTeamsAsync();
            var resultado3 = await _ballDontLieService.GetAllTeamsAsync();

            // Assert
            resultado1.Should().BeEquivalentTo(resultado2);
            resultado2.Should().BeEquivalentTo(resultado3);
        }

        #endregion

        #region Testes de SearchPlayersAsync

        [Theory(DisplayName = "Deve retornar jogadores ao pesquisar")]
        [InlineData("LeBron", 2)]
        [InlineData("Jordan", 5)]
        public async Task DeveRetornarJogadores_AoPesquisar(string termoPesquisa, int quantidadeEsperada)
        {
            // Arrange
            var jogadoresEsperados = new List<BallDontLiePlayerDto>();
            for (int i = 0; i < quantidadeEsperada; i++)
            {
                jogadoresEsperados.Add(TestDataBuilder.CreateBallDontLiePlayer(i + 1));
            }

            var respostaApi = new BallDontLieApiResponse<BallDontLiePlayerDto>
            {
                Data = jogadoresEsperados
            };

            ConfigurarRespostaHttp(HttpStatusCode.OK, respostaApi);

            // Act
            var resultado = await _ballDontLieService.SearchPlayersAsync(termoPesquisa, 1);

            // Assert
            resultado.Should().HaveCount(quantidadeEsperada);
        }

        [Fact(DisplayName = "Deve retornar jogadores ao pesquisar com espaços")]
        public async Task DeveRetornarJogadores_AoPesquisarComEspacos()
        {
            // Arrange
            var termoPesquisa = "Stephen Curry";
            var jogadorEsperado = TestDataBuilder.CreateBallDontLiePlayer(1);
            var respostaApi = new BallDontLieApiResponse<BallDontLiePlayerDto>
            {
                Data = new List<BallDontLiePlayerDto> { jogadorEsperado }
            };

            ConfigurarRespostaHttp(HttpStatusCode.OK, respostaApi);

            // Act
            var resultado = await _ballDontLieService.SearchPlayersAsync(termoPesquisa, 1);

            // Assert
            resultado.Should().HaveCount(1);

            // Verificar que a requisição foi feita com o termo de pesquisa
            VerificarRequisicaoHttp(HttpMethod.Get, "search=Stephen Curry");
        }

        [Theory(DisplayName = "Deve tratar termos de pesquisa inválidos")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task DeveTratarTermosPesquisaInvalidos(string termoPesquisa)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ExternalApiException>(
                async () => await _ballDontLieService.SearchPlayersAsync(termoPesquisa, 1)
            );
        }

        #endregion

        #region Testes de GetPlayerByIdAsync

        [Fact(DisplayName = "Deve retornar jogador quando existe")]
        public async Task DeveRetornarJogador_QuandoExiste()
        {
            // Arrange
            const int idJogador = 123;
            var jogadorEsperado = TestDataBuilder.CreateBallDontLiePlayer(idJogador);
            // Ajustar o ID do jogador retornado pelo TestDataBuilder
            jogadorEsperado.Id = idJogador;

            ConfigurarRespostaHttp(HttpStatusCode.OK, jogadorEsperado);

            // Act
            var resultado = await _ballDontLieService.GetPlayerByIdAsync(idJogador);

            // Assert
            resultado.Should().NotBeNull();
            resultado!.Id.Should().Be(idJogador);

            VerificarRequisicaoHttp(HttpMethod.Get, $"/v1/players/{idJogador}");
        }

        [Fact(DisplayName = "Deve retornar null quando jogador não existe")]
        public async Task DeveRetornarNull_QuandoJogadorNaoExiste()
        {
            // Arrange
            const int idJogadorInexistente = 99999;
            ConfigurarRespostaHttp(HttpStatusCode.NotFound, "Player not found");

            // Act
            var resultado = await _ballDontLieService.GetPlayerByIdAsync(idJogadorInexistente);

            // Assert
            resultado.Should().BeNull();
        }

        #endregion

        #region Testes de Rate Limiting e Erros

        [Fact(DisplayName = "Deve tratar erro 429 Too Many Requests")]
        public async Task DeveTratarErro429_TooManyRequests()
        {
            // Arrange
            ConfigurarRespostaHttp(HttpStatusCode.TooManyRequests, "Rate limit exceeded");

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _ballDontLieService.GetTodaysGamesAsync()
            );

            exception.Message.Should().Contain("Rate limit exceeded");
        }

        [Fact(DisplayName = "Deve tratar timeout de requisição")]
        public async Task DeveTratarTimeout_DeRequisicao()
        {
            // Arrange
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new TaskCanceledException("Request timeout"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ExternalApiException>(
                async () => await _ballDontLieService.GetAllTeamsAsync()
            );

            exception.Message.Should().Contain("Failed to fetch teams");
        }

        #endregion

        #region Testes de Autenticação

        [Fact(DisplayName = "Deve incluir API key no header Authorization")]
        public async Task DeveIncluirApiKey_NoHeaderAuthorization()
        {
            // Arrange
            var respostaApi = new BallDontLieApiResponse<BallDontLieTeamDto>
            {
                Data = new List<BallDontLieTeamDto>()
            };

            ConfigurarRespostaHttp(HttpStatusCode.OK, respostaApi);

            // Act
            await _ballDontLieService.GetAllTeamsAsync();

            // Assert
            _mockHttpMessageHandler.Protected()
                .Verify("SendAsync",
                    Times.Once(),
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Headers.Authorization != null &&
                        req.Headers.Authorization.Scheme == "Bearer" &&
                        req.Headers.Authorization.Parameter == API_KEY),
                    ItExpr.IsAny<CancellationToken>());
        }

        #endregion

        #region Métodos Auxiliares

        private void ConfigurarMocks()
        {
            var secaoBallDontLie = new Mock<IConfigurationSection>();
            secaoBallDontLie.Setup(x => x["ApiKey"]).Returns(API_KEY);
            secaoBallDontLie.Setup(x => x["BaseUrl"]).Returns(BASE_URL);

            _mockConfiguration
                .Setup(x => x.GetSection("ExternalApis:BallDontLie"))
                .Returns(secaoBallDontLie.Object);
        }

        private void ConfigurarRespostaHttp<T>(HttpStatusCode statusCode, T objetoResposta)
        {
            var jsonResposta = JsonSerializer.Serialize(objetoResposta);
            var respostaHttp = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(jsonResposta, Encoding.UTF8, "application/json")
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(respostaHttp);
        }

        private void ConfigurarRespostaHttp(HttpStatusCode statusCode, string conteudoResposta)
        {
            var respostaHttp = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(conteudoResposta, Encoding.UTF8, "text/plain")
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(respostaHttp);
        }

        private void VerificarRequisicaoHttp(HttpMethod metodoEsperado, string uriEsperada, Times? vezes = null)
        {
            _mockHttpMessageHandler.Protected()
                .Verify("SendAsync",
                    vezes ?? Times.Once(),
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == metodoEsperado &&
                        req.RequestUri!.ToString().Contains(uriEsperada)),
                    ItExpr.IsAny<CancellationToken>());
        }

        private List<BallDontLieTeamDto> CriarTimesNBA()
        {
            var times = new List<BallDontLieTeamDto>();
            var nomesTimesPorConferencia = new Dictionary<string, List<string>>
            {
                ["East"] = new() { "Celtics", "Nets", "Knicks", "76ers", "Raptors",
                                  "Bulls", "Cavaliers", "Pistons", "Pacers", "Bucks",
                                  "Hawks", "Hornets", "Heat", "Magic", "Wizards" },
                ["West"] = new() { "Nuggets", "Timberwolves", "Thunder", "Trail Blazers", "Jazz",
                                  "Warriors", "Clippers", "Lakers", "Suns", "Kings",
                                  "Mavericks", "Grizzlies", "Pelicans", "Rockets", "Spurs" }
            };

            int id = 1;
            foreach (var (conferencia, nomesTime) in nomesTimesPorConferencia)
            {
                foreach (var nome in nomesTime)
                {
                    var time = TestDataBuilder.CreateBallDontLieTeam(id++, nome);
                    time.Conference = conferencia;
                    times.Add(time);
                }
            }

            return times;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        #endregion
    }
}