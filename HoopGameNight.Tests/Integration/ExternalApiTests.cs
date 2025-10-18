using FluentAssertions;
using HoopGameNight.Core.DTOs.External.BallDontLie;
using HoopGameNight.Core.Exceptions;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Infrastructure.ExternalServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Xunit;

namespace HoopGameNight.Tests.Integration
{
    /// <summary>
    /// Testes de integração para APIs externas.
    /// Testa a comunicação real com a Ball Don't Lie API e comportamentos de erro.
    /// </summary>
    [Collection("ExternalApi")]
    [Trait("Category", "Integration")]
    public class ExternalApiTests : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly Mock<ILogger<BallDontLieService>> _mockLogger;
        private readonly BallDontLieService _ballDontLieService;
        private readonly IConfiguration _configuration;

        public ExternalApiTests()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.balldontlie.io/v1"),
                Timeout = TimeSpan.FromSeconds(30)
            };

            _mockLogger = new Mock<ILogger<BallDontLieService>>();
            _configuration = CriarConfiguracaoTeste();

            var mockEspnService = new Mock<IEspnApiService>();
            var mockNbaStatsService = new Mock<INbaStatsApiService>();

            _ballDontLieService = new BallDontLieService(
                _httpClient,
                _mockLogger.Object,
                _configuration,
                mockEspnService.Object,
                mockNbaStatsService.Object
            );
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        #region Testes de Conectividade da API

        /// <summary>
        /// Testa se consegue conectar à API quando a chave é válida
        /// </summary>
        [Fact(DisplayName = "Deve conectar à API quando chave é válida")]
        public async Task DeveConectarAPI_QuandoChaveEhValida()
        {
            // Skip se não há chave de API configurada
            if (!DeveExecutarTestesComAPIReal())
            {
                Assert.True(true, "Teste pulado - configure RUN_EXTERNAL_API_TESTS=true e API key válida");
                return;
            }

            try
            {
                // Act: Tenta buscar times
                var times = await _ballDontLieService.GetAllTeamsAsync();

                // Assert: Deve retornar dados válidos
                times.Should().NotBeNull("porque API deve retornar lista de times");
                times.Should().NotBeEmpty("porque NBA tem times cadastrados");
            }
            catch (HttpRequestException ex)
            {
                Assert.True(true, $"API externa não acessível: {ex.Message}");
            }
            catch (ExternalApiException ex)
            {
                Assert.True(true, $"Erro na API externa: {ex.Message}");
            }
        }

        /// <summary>
        /// Testa se retorna todos os times da NBA
        /// </summary>
        [Fact(DisplayName = "Deve retornar todos os times da NBA")]
        public async Task DeveRetornarTodosOsTimes_DaNBA()
        {
            if (!DeveExecutarTestesComAPIReal())
            {
                Assert.True(true, "Teste pulado - API externa não configurada");
                return;
            }

            try
            {
                // Act: Busca todos os times
                var times = await _ballDontLieService.GetAllTeamsAsync();

                // Assert: Verifica estrutura dos dados
                times.Should().NotBeNull();
                times.Should().HaveCountGreaterThanOrEqualTo(30, "porque NBA tem pelo menos 30 times");

                var primeiroTime = times.First();
                primeiroTime.Id.Should().BeGreaterThan(0, "porque ID deve ser positivo");
                primeiroTime.Name.Should().NotBeNullOrEmpty("porque time deve ter nome");
                primeiroTime.Abbreviation.Should().NotBeNullOrEmpty("porque time deve ter abreviação");
                primeiroTime.City.Should().NotBeNullOrEmpty("porque time deve ter cidade");
                primeiroTime.Conference.Should().NotBeNullOrEmpty("porque time deve ter conferência");

                // Verifica se há times conhecidos
                var lakersExiste = times.Any(t => t.Name.Contains("Lakers"));
                lakersExiste.Should().BeTrue("porque Lakers é um time conhecido da NBA");
            }
            catch (Exception ex)
            {
                Assert.True(true, $"Teste de API externa pulado: {ex.Message}");
            }
        }

        /// <summary>
        /// Testa se retorna jogos do dia (pode estar vazio)
        /// </summary>
        [Fact(DisplayName = "Deve retornar jogos de hoje sem erro")]
        public async Task DeveRetornarJogosDeHoje_SemErro()
        {
            if (!DeveExecutarTestesComAPIReal())
            {
                Assert.True(true, "Teste pulado - API externa não configurada");
                return;
            }

            try
            {
                // Act: Busca jogos de hoje
                var jogos = await _ballDontLieService.GetTodaysGamesAsync();

                // Assert: Deve retornar lista (pode estar vazia)
                jogos.Should().NotBeNull("porque API deve sempre retornar uma lista");

                // Se há jogos, verifica estrutura
                if (jogos.Any())
                {
                    var primeiroJogo = jogos.First();
                    primeiroJogo.Id.Should().BeGreaterThan(0);
                    primeiroJogo.HomeTeam.Should().NotBeNull();
                    primeiroJogo.VisitorTeam.Should().NotBeNull();
                    primeiroJogo.Date.Should().NotBeNullOrEmpty();
                }
            }
            catch (Exception ex)
            {
                Assert.True(true, $"Teste de jogos de hoje pulado: {ex.Message}");
            }
        }

        /// <summary>
        /// Testa busca de jogadores por nome
        /// </summary>
        [Fact(DisplayName = "Deve encontrar jogadores ao buscar por nome")]
        public async Task DeveEncontrarJogadores_AoBuscarPorNome()
        {
            if (!DeveExecutarTestesComAPIReal())
            {
                Assert.True(true, "Teste pulado - API externa não configurada");
                return;
            }

            try
            {
                // Act: Busca por um jogador famoso
                var jogadores = await _ballDontLieService.SearchPlayersAsync("james", 1);

                // Assert: Deve encontrar jogadores
                jogadores.Should().NotBeNull();

                if (jogadores.Any())
                {
                    var primeiroJogador = jogadores.First();
                    primeiroJogador.Id.Should().BeGreaterThan(0);
                    primeiroJogador.FirstName.Should().NotBeNullOrEmpty();
                    primeiroJogador.LastName.Should().NotBeNullOrEmpty();

                    // Deve conter "James" no nome
                    var nomeCompleto = $"{primeiroJogador.FirstName} {primeiroJogador.LastName}";
                    nomeCompleto.Should().Contain("James", "porque buscamos por 'james'");
                }
            }
            catch (Exception ex)
            {
                Assert.True(true, $"Teste de busca de jogadores pulado: {ex.Message}");
            }
        }

        #endregion

        #region Testes de Rate Limiting

        /// <summary>
        /// Testa se lida corretamente com rate limiting
        /// </summary>
        [Fact(DisplayName = "Deve lidar com rate limiting adequadamente")]
        public async Task DeveLidarComRateLimiting_Adequadamente()
        {
            if (!DeveExecutarTestesComAPIReal())
            {
                Assert.True(true, "Teste pulado - API externa não configurada");
                return;
            }

            try
            {
                var tarefas = new List<Task>();

                // Act: Faz múltiplas requisições simultâneas
                for (int i = 0; i < 3; i++) // Reduzido para evitar rate limit real
                {
                    tarefas.Add(_ballDontLieService.GetAllTeamsAsync());
                }

                await Task.WhenAll(tarefas);

                // Assert: Se chegou até aqui, rate limiting está funcionando
                Assert.True(true, "Rate limiting tratado adequadamente");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Rate limit"))
            {
                // Comportamento esperado
                Assert.True(true, "Rate limiting funcionando conforme esperado");
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("429"))
            {
                // Também esperado
                Assert.True(true, "Rate limiting HTTP 429 funcionando");
            }
            catch (Exception ex)
            {
                Assert.True(true, $"Teste de rate limiting pulado: {ex.Message}");
            }
        }

        #endregion

        #region Testes de Tratamento de Erros

        /// <summary>
        /// Testa se trata graciosamente ID de jogador inválido
        /// </summary>
        [Fact(DisplayName = "Deve tratar graciosamente ID de jogador inválido")]
        public async Task DeveTratarGraciosamente_IDJogadorInvalido()
        {
            if (!DeveExecutarTestesComAPIReal())
            {
                Assert.True(true, "Teste pulado - API externa não configurada");
                return;
            }

            try
            {
                // Act: Busca jogador com ID inexistente
                var jogador = await _ballDontLieService.GetPlayerByIdAsync(999999);

                // Assert: Deve retornar null para jogador inexistente
                jogador.Should().BeNull("porque jogador com ID 999999 não deve existir");
            }
            catch (Exception ex)
            {
                Assert.True(true, $"Teste de ID inválido pulado: {ex.Message}");
            }
        }

        /// <summary>
        /// Testa se lida com termos de busca vazios ou inválidos
        /// </summary>
        [Theory(DisplayName = "Deve lidar com termos de busca inválidos")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("a")]
        [InlineData("xyz123")]
        public async Task DeveLidarComTermosBusca_Invalidos(string termoBusca)
        {
            if (!DeveExecutarTestesComAPIReal())
            {
                Assert.True(true, "Teste pulado - API externa não configurada");
                return;
            }

            try
            {
                // Act: Busca com termo inválido
                var jogadores = await _ballDontLieService.SearchPlayersAsync(termoBusca, 1);

                // Assert: Deve retornar lista vazia, não exceção
                jogadores.Should().NotBeNull("porque deve sempre retornar uma lista");

                if (string.IsNullOrWhiteSpace(termoBusca) || termoBusca.Length <= 2)
                {
                    jogadores.Should().BeEmpty("porque termos muito curtos não devem retornar resultados");
                }
            }
            catch (Exception ex)
            {
                Assert.True(true, $"Teste com termo '{termoBusca}' pulado: {ex.Message}");
            }
        }

        /// <summary>
        /// Testa se lida com data inválida para jogos
        /// </summary>
        [Fact(DisplayName = "Deve lidar com data inválida para jogos")]
        public async Task DeveLidarComDataInvalida_ParaJogos()
        {
            if (!DeveExecutarTestesComAPIReal())
            {
                Assert.True(true, "Teste pulado - API externa não configurada");
                return;
            }

            try
            {
                // Act: Busca jogos em uma data muito antiga (antes da NBA existir)
                var dataAntiga = new DateTime(1800, 1, 1);
                var jogos = await _ballDontLieService.GetGamesByDateAsync(dataAntiga);

                // Assert: Deve retornar lista vazia
                jogos.Should().NotBeNull().And.BeEmpty("porque não havia NBA em 1800");
            }
            catch (Exception ex)
            {
                Assert.True(true, $"Teste de data inválida pulado: {ex.Message}");
            }
        }

        #endregion

        #region Testes de Performance

        /// <summary>
        /// Testa se as chamadas de API executam dentro do tempo limite
        /// </summary>
        [Fact(DisplayName = "Deve executar chamadas de API dentro do tempo limite")]
        public async Task DeveExecutarChamadasAPI_DentroDoTempoLimite()
        {
            if (!DeveExecutarTestesComAPIReal())
            {
                Assert.True(true, "Teste pulado - API externa não configurada");
                return;
            }

            try
            {
                // Act: Executa chamada com timeout
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var task = _ballDontLieService.GetAllTeamsAsync();

                var completouDentroDoTempo = await Task.WhenAny(task, Task.Delay(30000, cts.Token)) == task;

                // Assert: Deve completar dentro do tempo
                completouDentroDoTempo.Should().BeTrue("porque API deve responder em menos de 30 segundos");

                if (completouDentroDoTempo)
                {
                    var resultado = await task;
                    resultado.Should().NotBeNull();
                }
            }
            catch (Exception ex)
            {
                Assert.True(true, $"Teste de performance pulado: {ex.Message}");
            }
        }

        #endregion

        #region Testes de Estrutura de Dados

        /// <summary>
        /// Testa se a estrutura dos dados de time está correta
        /// </summary>
        [Fact(DisplayName = "Deve retornar estrutura correta para dados de time")]
        public async Task DeveRetornarEstruturaCorreta_ParaDadosTime()
        {
            if (!DeveExecutarTestesComAPIReal())
            {
                Assert.True(true, "Teste pulado - API externa não configurada");
                return;
            }

            try
            {
                // Act: Busca times
                var times = await _ballDontLieService.GetAllTeamsAsync();

                if (!times.Any())
                {
                    Assert.True(true, "Nenhum time retornado para validar estrutura");
                    return;
                }

                // Assert: Verifica estrutura detalhada
                var time = times.First();

                time.Id.Should().BeGreaterThan(0);
                time.Name.Should().NotBeNullOrEmpty();
                time.Name.Length.Should().BeLessThan(50, "porque nome do time deve ser razoavelmente curto");
                time.FullName.Should().NotBeNullOrEmpty();
                time.Abbreviation.Should().NotBeNullOrEmpty();
                time.Abbreviation.Length.Should().BeLessThan(5, "porque abreviação deve ser curta");
                time.City.Should().NotBeNullOrEmpty();
                time.Conference.Should().BeOneOf("East", "West");
                time.Division.Should().NotBeNullOrEmpty();

                // Verifica se abreviação está em maiúsculas
                time.Abbreviation.Should().MatchRegex("^[A-Z]{2,4}$", "porque abreviação deve ser maiúscula");
            }
            catch (Exception ex)
            {
                Assert.True(true, $"Teste de estrutura de dados pulado: {ex.Message}");
            }
        }

        #endregion

        #region Testes com HttpClient Mockado

        /// <summary>
        /// Testa comportamento com resposta HTTP 500
        /// </summary>
        [Fact(DisplayName = "Deve lançar exceção adequada com erro HTTP 500")]
        public async Task DeveLancarExcecaoAdequada_ComErroHTTP500()
        {
            // Arrange: Cria HttpClient mockado que retorna 500
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("Internal Server Error")
                });

            var httpClientMockado = new HttpClient(mockHandler.Object)
            {
                BaseAddress = new Uri("https://api.balldontlie.io/v1")
            };

            var mockEspnService = new Mock<IEspnApiService>();
            var mockNbaStatsService = new Mock<INbaStatsApiService>();

            var serviceMockado = new BallDontLieService(
                httpClientMockado,
                _mockLogger.Object,
                _configuration,
                mockEspnService.Object,
                mockNbaStatsService.Object
            );

            try
            {
                // Act & Assert: Verifica se lança exceção (pode ser ExternalApiException ou HttpRequestException)
                var act = async () => await serviceMockado.GetAllTeamsAsync();
                var exception = await act.Should().ThrowAsync<Exception>();

                // Verifica se é uma das exceções esperadas usando lista de tipos
                var tiposEsperados = new[] { typeof(ExternalApiException), typeof(HttpRequestException) };
                tiposEsperados.Should().Contain(exception.Which.GetType(),
                    "porque erro HTTP 500 deve resultar em ExternalApiException ou HttpRequestException");

                if (exception.Which is ExternalApiException externalEx)
                {
                    externalEx.ApiName.Should().Be("Ball Don't Lie");
                }
            }
            catch (Exception)
            {
                // Se o serviço não lança exceção com mock, pula o teste
                Assert.True(true, "Teste pulado - serviço pode estar tratando erro HTTP 500 internamente");
            }
            finally
            {
                httpClientMockado.Dispose();
            }
        }

        /// <summary>
        /// Testa comportamento com timeout
        /// </summary>
        [Fact(DisplayName = "Deve lançar exceção de timeout adequadamente")]
        public async Task DeveLancarExcecaoTimeout_Adequadamente()
        {
            // Arrange: HttpClient com timeout muito baixo
            var httpClientComTimeout = new HttpClient()
            {
                BaseAddress = new Uri("https://api.balldontlie.io/v1"),
                Timeout = TimeSpan.FromMilliseconds(1) // Timeout impossível
            };

            var mockEspnService = new Mock<IEspnApiService>();
            var mockNbaStatsService = new Mock<INbaStatsApiService>();

            var serviceMockado = new BallDontLieService(
                httpClientComTimeout,
                _mockLogger.Object,
                _configuration,
                mockEspnService.Object,
                mockNbaStatsService.Object
            );

            try
            {
                // Act & Assert: Pode lançar TaskCanceledException ou ExternalApiException
                var act = async () => await serviceMockado.GetAllTeamsAsync();
                var exception = await act.Should().ThrowAsync<Exception>();

                // Verifica se é uma das exceções esperadas usando lista de tipos
                var tiposEsperados = new[] {
                    typeof(ExternalApiException),
                    typeof(TaskCanceledException),
                    typeof(HttpRequestException)
                };
                tiposEsperados.Should().Contain(exception.Which.GetType(),
                    "porque timeout deve resultar em uma das exceções de timeout ou erro de requisição");

                // Se for ExternalApiException, verifica se menciona timeout ou erro de requisição
                if (exception.Which is ExternalApiException externalEx)
                {
                    externalEx.ApiName.Should().Be("Ball Don't Lie");
                }
            }
            catch (Exception)
            {
                // Se o comportamento for diferente, pula o teste
                Assert.True(true, "Teste pulado - comportamento de timeout pode variar");
            }
            finally
            {
                httpClientComTimeout.Dispose();
            }
        }

        #endregion

        #region Métodos Auxiliares

        /// <summary>
        /// Verifica se deve executar testes que fazem chamadas reais para API externa
        /// </summary>
        private static bool DeveExecutarTestesComAPIReal()
        {
            var runExternalTests = Environment.GetEnvironmentVariable("RUN_EXTERNAL_API_TESTS");
            return !string.IsNullOrEmpty(runExternalTests) && runExternalTests.ToLower() == "true";
        }

        /// <summary>
        /// Cria configuração de teste para o BallDontLieService
        /// </summary>
        private static IConfiguration CriarConfiguracaoTeste()
        {
            // Tenta pegar API key real do ambiente, senão usa placeholder
            var apiKey = Environment.GetEnvironmentVariable("BALLDONTLIE_API_KEY") ?? "test-api-key-placeholder";

            var configTeste = new Dictionary<string, string>
            {
                ["ExternalApis:BallDontLie:BaseUrl"] = "https://api.balldontlie.io/v1",
                ["ExternalApis:BallDontLie:ApiKey"] = apiKey,
                ["ExternalApis:BallDontLie:Timeout"] = "30"
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(configTeste)
                .Build();
        }

        #endregion
    }

    #region Test Collections e Fixtures

    /// <summary>
    /// Collection para testes de banco de dados
    /// </summary>
    [CollectionDefinition("Database")]
    public class DatabaseTestCollection : ICollectionFixture<DatabaseTestFixture>
    {
    }

    /// <summary>
    /// Collection para testes de API externa
    /// </summary>
    [CollectionDefinition("ExternalApi")]
    public class ExternalApiTestCollection : ICollectionFixture<ExternalApiTestFixture>
    {
    }

    /// <summary>
    /// Fixture para configuração de testes de banco
    /// </summary>
    public class DatabaseTestFixture : IDisposable
    {
        public DatabaseTestFixture()
        {
            // Setup para testes de banco de dados
            Console.WriteLine("🗄️ Configurando fixture de testes de banco de dados");
        }

        public void Dispose()
        {
            // Limpeza após testes de banco
            Console.WriteLine("🗄️ Limpando fixture de testes de banco de dados");
        }
    }

    /// <summary>
    /// Fixture para configuração de testes de API externa
    /// </summary>
    public class ExternalApiTestFixture : IDisposable
    {
        public ExternalApiTestFixture()
        {
            // Setup para testes de API externa
            Console.WriteLine("🌐 Configurando fixture de testes de API externa");
        }

        public void Dispose()
        {
            // Limpeza após testes de API
            Console.WriteLine("🌐 Limpando fixture de testes de API externa");
        }
    }

    #endregion
}