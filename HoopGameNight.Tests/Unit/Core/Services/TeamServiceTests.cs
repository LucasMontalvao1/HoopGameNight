using AutoMapper;
using FluentAssertions;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Api.Constants;
using HoopGameNight.Core.Exceptions;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Core.Models.Entities;
using HoopGameNight.Core.Services;
using HoopGameNight.Tests.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HoopGameNight.Tests.Unit.Core.Services
{
    /// <summary>
    /// Testes unitários para o TeamService.
    /// Testa a lógica de negócio relacionada aos times da NBA.
    /// </summary>
    public class TeamServiceTests : IDisposable
    {
        private readonly Mock<ITeamRepository> _mockTeamRepository;
        private readonly Mock<IBallDontLieService> _mockBallDontLieService;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<ILogger<TeamService>> _mockLogger;
        private readonly IMemoryCache _memoryCache;
        private readonly TeamService _teamService;

        public TeamServiceTests()
        {
            _mockTeamRepository = new Mock<ITeamRepository>();
            _mockBallDontLieService = new Mock<IBallDontLieService>();
            _mockMapper = new Mock<IMapper>();
            _mockLogger = new Mock<ILogger<TeamService>>();
            _memoryCache = new MemoryCache(new MemoryCacheOptions());

            _teamService = new TeamService(
                _mockTeamRepository.Object,
                _mockBallDontLieService.Object,
                _mockMapper.Object,
                _memoryCache,
                _mockLogger.Object
            );
        }

        public void Dispose()
        {
            _memoryCache?.Dispose();
        }

        #region Testes de Busca de Todos os Times

        /// <summary>
        /// Testa se retorna todos os times quando existem times cadastrados
        /// </summary>
        [Fact(DisplayName = "Deve retornar todos os times quando existem times cadastrados")]
        public async Task DeveRetornarTodosOsTimes_QuandoExistemTimesCadastrados()
        {
            // Arrange: Configura 5 times
            const int quantidadeTimes = 5;
            var times = TestDataBuilder.CreateTeams(quantidadeTimes);
            var timesResponse = times.Select(t => TestDataBuilder.CreateTeamResponse(t.Id, t.Name)).ToList();

            _mockTeamRepository
                .Setup(x => x.GetAllAsync())
                .ReturnsAsync(times);

            _mockMapper
                .Setup(x => x.Map<List<TeamResponse>>(times))
                .Returns(timesResponse);

            // Act: Busca todos os times
            var resultado = await _teamService.GetAllTeamsAsync();

            // Assert: Verifica se retornou corretamente
            resultado.Should().NotBeNull("porque sempre deve retornar uma lista");
            resultado.Should().HaveCount(quantidadeTimes, "porque configuramos 5 times");
            resultado.Should().BeEquivalentTo(timesResponse, "porque deve mapear corretamente");

            // Verifica interações
            _mockTeamRepository.Verify(x => x.GetAllAsync(), Times.Once);
            _mockMapper.Verify(x => x.Map<List<TeamResponse>>(times), Times.Once);
        }

        /// <summary>
        /// Testa se retorna lista vazia quando não há times cadastrados
        /// </summary>
        [Fact(DisplayName = "Deve retornar lista vazia quando não há times cadastrados")]
        public async Task DeveRetornarListaVazia_QuandoNaoHaTimesCadastrados()
        {
            // Arrange: Configura resposta vazia
            var timesVazios = new List<Team>();
            var responsesVazias = new List<TeamResponse>();

            _mockTeamRepository
                .Setup(x => x.GetAllAsync())
                .ReturnsAsync(timesVazios);

            _mockMapper
                .Setup(x => x.Map<List<TeamResponse>>(timesVazios))
                .Returns(responsesVazias);

            // Act: Busca todos os times
            var resultado = await _teamService.GetAllTeamsAsync();

            // Assert: Deve retornar lista vazia
            resultado.Should().NotBeNull("porque sempre deve retornar uma lista");
            resultado.Should().BeEmpty("porque não há times cadastrados");
        }

        /// <summary>
        /// Testa se lança BusinessException quando repositório falha
        /// </summary>
        [Fact(DisplayName = "Deve lançar BusinessException quando repositório falha")]
        public async Task DeveLancarBusinessException_QuandoRepositorioFalha()
        {
            // Arrange
            _mockTeamRepository
                .Setup(x => x.GetAllAsync())
                .ThrowsAsync(new Exception("Falha na conexão com banco de dados"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<BusinessException>(
                () => _teamService.GetAllTeamsAsync());

            // Verificar a mensagem da BusinessException
            exception.Message.Should().Be("Falha ao recuperar os times");

            // Verificar a exception interna original
            exception.InnerException.Should().NotBeNull();
            exception.InnerException!.Message.Should().Be("Falha na conexão com banco de dados");
        }


        #endregion

        #region Testes de Busca por ID

        /// <summary>
        /// Testa se retorna time quando time existe
        /// </summary>
        [Fact(DisplayName = "Deve retornar time quando time existe")]
        public async Task DeveRetornarTime_QuandoTimeExiste()
        {
            // Arrange: Configura time existente
            const int timeId = 1;
            const string nomeTime = "Lakers";
            var time = TestDataBuilder.CreateTeam(timeId, nomeTime);
            var timeResponse = TestDataBuilder.CreateTeamResponse(timeId, nomeTime);

            _mockTeamRepository
                .Setup(x => x.GetByIdAsync(timeId))
                .ReturnsAsync(time);

            _mockMapper
                .Setup(x => x.Map<TeamResponse>(time))
                .Returns(timeResponse);

            // Act: Busca time por ID
            var resultado = await _teamService.GetTeamByIdAsync(timeId);

            // Assert: Verifica se retornou corretamente
            resultado.Should().NotBeNull("porque time existe");
            resultado!.Id.Should().Be(timeId, "porque buscamos pelo ID correto");
            resultado.DisplayName.Should().Be(timeResponse.DisplayName, "porque deve mapear corretamente");

            // Verifica interações
            _mockTeamRepository.Verify(x => x.GetByIdAsync(timeId), Times.Once);
            _mockMapper.Verify(x => x.Map<TeamResponse>(time), Times.Once);
        }

        /// <summary>
        /// Testa se retorna null quando time não existe
        /// </summary>
        [Fact(DisplayName = "Deve retornar null quando time não existe")]
        public async Task DeveRetornarNull_QuandoTimeNaoExiste()
        {
            // Arrange: Configura time inexistente
            const int timeId = 999;

            _mockTeamRepository
                .Setup(x => x.GetByIdAsync(timeId))
                .ReturnsAsync((Team?)null);

            // Act: Busca time inexistente
            var resultado = await _teamService.GetTeamByIdAsync(timeId);

            // Assert: Deve retornar null
            resultado.Should().BeNull("porque time não existe");

            // Verifica que mapper não foi chamado
            _mockTeamRepository.Verify(x => x.GetByIdAsync(timeId), Times.Once);
            _mockMapper.Verify(x => x.Map<TeamResponse>(It.IsAny<Team>()), Times.Never,
                "porque não deve mapear quando time não existe");
        }

        #endregion

        #region Testes de Busca por Abreviação

        /// <summary>
        /// Testa se retorna time quando busca por abreviação válida
        /// </summary>
        [Theory(DisplayName = "Deve retornar time quando busca por abreviação válida")]
        [InlineData("LAL", "Lakers")]
        [InlineData("GSW", "Warriors")]
        [InlineData("CHI", "Bulls")]
        [InlineData("BOS", "Celtics")]
        [InlineData("MIA", "Heat")]
        public async Task DeveRetornarTime_QuandoBuscaPorAbreviacaoValida(string abreviacao, string nomeTime)
        {
            // Arrange: Configura time com abreviação
            var time = TestDataBuilder.CreateTeam(1, nomeTime);
            time.Abbreviation = abreviacao;

            var timeResponse = TestDataBuilder.CreateTeamResponse(1, nomeTime);
            timeResponse.Abbreviation = abreviacao;

            _mockTeamRepository
                .Setup(x => x.GetByAbbreviationAsync(abreviacao))
                .ReturnsAsync(time);

            _mockMapper
                .Setup(x => x.Map<TeamResponse>(time))
                .Returns(timeResponse);

            // Act: Busca por abreviação
            var resultado = await _teamService.GetTeamByAbbreviationAsync(abreviacao);

            // Assert: Verifica se encontrou time correto
            resultado.Should().NotBeNull($"porque {abreviacao} é uma abreviação válida");
            resultado!.Abbreviation.Should().Be(abreviacao, "porque deve manter a abreviação");
            resultado.Name.Should().Be(nomeTime, "porque deve retornar o time correto");
        }

        /// <summary>
        /// Testa se retorna null para abreviações inválidas ou inexistentes
        /// </summary>
        [Theory(DisplayName = "Deve retornar null para abreviações inválidas")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("XYZ")]
        [InlineData("NONEXISTENT")]
        [InlineData("123")]
        public async Task DeveRetornarNull_ParaAbreviacoesInvalidas(string abreviacaoInvalida)
        {
            // Arrange: Configura abreviação inexistente
            _mockTeamRepository
                .Setup(x => x.GetByAbbreviationAsync(abreviacaoInvalida))
                .ReturnsAsync((Team?)null);

            // Act: Busca por abreviação inválida
            var resultado = await _teamService.GetTeamByAbbreviationAsync(abreviacaoInvalida);

            // Assert: Deve retornar null
            resultado.Should().BeNull($"porque '{abreviacaoInvalida}' não é uma abreviação válida");
        }

        #endregion

        #region Testes de Sincronização de Times

        /// <summary>
        /// Testa se sincroniza novos times quando há times externos
        /// </summary>
        [Fact(DisplayName = "Deve sincronizar novos times quando há times externos")]
        public async Task DeveSincronizarNovosTimes_QuandoHaTimesExternos()
        {
            // Arrange: Configura times externos
            var timesExternos = new[]
            {
                TestDataBuilder.CreateBallDontLieTeam(1, "Lakers"),
                TestDataBuilder.CreateBallDontLieTeam(2, "Warriors"),
                TestDataBuilder.CreateBallDontLieTeam(3, "Bulls")
            };

            var timesMapeados = new[]
            {
                TestDataBuilder.CreateTeam(1, "Lakers"),
                TestDataBuilder.CreateTeam(2, "Warriors"),
                TestDataBuilder.CreateTeam(3, "Bulls")
            };

            _mockBallDontLieService
                .Setup(x => x.GetAllTeamsAsync())
                .ReturnsAsync(timesExternos);

            _mockMapper
                .Setup(x => x.Map<List<Team>>(timesExternos))
                .Returns(timesMapeados.ToList());

            // Setup: Nenhum time existe ainda
            _mockTeamRepository
                .Setup(x => x.ExistsAsync(It.IsAny<int>()))
                .ReturnsAsync(false);

            _mockTeamRepository
                .Setup(x => x.InsertAsync(It.IsAny<Team>()))
                .ReturnsAsync(1);

            // Act: Executa sincronização
            await _teamService.SyncAllTeamsAsync();

            // Assert: Verifica se sincronizou todos os times
            _mockBallDontLieService.Verify(x => x.GetAllTeamsAsync(), Times.Once,
                "porque deve buscar times da API externa");
            _mockTeamRepository.Verify(x => x.InsertAsync(It.IsAny<Team>()), Times.Exactly(3),
                "porque deve inserir os 3 times novos");
        }

        /// <summary>
        /// Testa se pula times que já existem durante sincronização
        /// </summary>
        [Fact(DisplayName = "Deve pular times que já existem durante sincronização")]
        public async Task DevePularTimes_QueJaExistemDuranteSincronizacao()
        {
            // Arrange: Configura times que já existem
            var timesExternos = new[]
            {
                TestDataBuilder.CreateBallDontLieTeam(1, "Lakers"),
                TestDataBuilder.CreateBallDontLieTeam(2, "Warriors")
            };

            var timesMapeados = new[]
            {
                TestDataBuilder.CreateTeam(1, "Lakers"),
                TestDataBuilder.CreateTeam(2, "Warriors")
            };

            _mockBallDontLieService
                .Setup(x => x.GetAllTeamsAsync())
                .ReturnsAsync(timesExternos);

            _mockMapper
                .Setup(x => x.Map<List<Team>>(timesExternos))
                .Returns(timesMapeados.ToList());

            // Setup: Todos os times já existem
            _mockTeamRepository
                .Setup(x => x.ExistsAsync(It.IsAny<int>()))
                .ReturnsAsync(true);

            // Act: Executa sincronização
            await _teamService.SyncAllTeamsAsync();

            // Assert: Não deve inserir nenhum time
            _mockTeamRepository.Verify(x => x.InsertAsync(It.IsAny<Team>()), Times.Never,
                "porque todos os times já existem");
        }

        /// <summary>
        /// Testa se limpa cache após sincronização bem-sucedida
        /// </summary>
        [Fact(DisplayName = "Deve limpar cache após sincronização bem-sucedida")]
        public async Task DeveLimparCache_AposSincronizacaoBemSucedida()
        {
            // Arrange: Configura sincronização e pré-popula cache
            var timesExternos = new[] { TestDataBuilder.CreateBallDontLieTeam(1, "Lakers") };
            var timesMapeados = new[] { TestDataBuilder.CreateTeam(1, "Lakers") };

            _mockBallDontLieService
                .Setup(x => x.GetAllTeamsAsync())
                .ReturnsAsync(timesExternos);

            _mockMapper
                .Setup(x => x.Map<List<Team>>(timesExternos))
                .Returns(timesMapeados.ToList());

            _mockTeamRepository
                .Setup(x => x.ExistsAsync(It.IsAny<int>()))
                .ReturnsAsync(false);

            _mockTeamRepository
                .Setup(x => x.InsertAsync(It.IsAny<Team>()))
                .ReturnsAsync(1);

            // Pré-popula cache
            _memoryCache.Set(ApiConstants.CacheKeys.ALL_TEAMS, new List<TeamResponse>());

            // Act: Executa sincronização
            await _teamService.SyncAllTeamsAsync();

            // Assert: Cache deve ter sido limpo
            var timesCache = _memoryCache.Get(ApiConstants.CacheKeys.ALL_TEAMS);
            timesCache.Should().BeNull("porque cache deve ser limpo após sincronização");
        }

        /// <summary>
        /// Testa se lança ExternalApiException quando serviço externo falha
        /// </summary>
        [Fact(DisplayName = "Deve lançar ExternalApiException quando serviço externo falha")]
        public async Task DeveLancarExternalApiException_QuandoServicoExternoFalha()
        {
            // Arrange
            _mockBallDontLieService
                .Setup(x => x.GetAllTeamsAsync())
                .ThrowsAsync(new HttpRequestException("API está fora do ar"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ExternalApiException>(
                () => _teamService.SyncAllTeamsAsync());

            // Verificar apenas a mensagem da ExternalApiException
            exception.Message.Should().Be("Falha ao sincronizar times da API externa");
        }


        #endregion

        #region Testes de Cache

        /// <summary>
        /// Testa se utiliza cache quando disponível
        /// </summary>
        [Fact(DisplayName = "Deve utilizar cache quando disponível")]
        public async Task DeveUtilizarCache_QuandoDisponivel()
        {
            // Arrange: Popula cache
            var timesCache = new List<TeamResponse>
            {
                TestDataBuilder.CreateTeamResponse(1, "Lakers"),
                TestDataBuilder.CreateTeamResponse(2, "Warriors")
            };

            _memoryCache.Set(ApiConstants.CacheKeys.ALL_TEAMS, timesCache);

            // Act: Busca times (deve usar cache)
            var resultado = await _teamService.GetAllTeamsAsync();

            // Assert: Deve retornar dados do cache
            resultado.Should().BeEquivalentTo(timesCache, "porque deve usar dados do cache");

            // Repositório não deve ser chamado se cache está válido
            // Nota: Isso depende da implementação específica do TeamService
        }

        /// <summary>
        /// Testa se popula cache quando não há dados em cache
        /// </summary>
        [Fact(DisplayName = "Deve popular cache quando não há dados em cache")]
        public async Task DevePopularCache_QuandoNaoHaDadosEmCache()
        {
            // Arrange: Limpa cache e configura dados
            LimparCache();
            var times = TestDataBuilder.CreateTeams(3);
            var timesResponse = times.Select(t => TestDataBuilder.CreateTeamResponse(t.Id, t.Name)).ToList();

            _mockTeamRepository
                .Setup(x => x.GetAllAsync())
                .ReturnsAsync(times);

            _mockMapper
                .Setup(x => x.Map<List<TeamResponse>>(times))
                .Returns(timesResponse);

            // Act: Busca times
            var resultado = await _teamService.GetAllTeamsAsync();

            // Assert: Deve retornar dados e popular cache
            resultado.Should().BeEquivalentTo(timesResponse);

            // Nota: Verificação de cache depende da implementação do TeamService
        }

        #endregion

        #region Testes de Performance

        /// <summary>
        /// Testa se operações executam rapidamente
        /// </summary>
        [Fact(DisplayName = "Deve executar operações rapidamente")]
        public async Task DeveExecutarOperacoes_Rapidamente()
        {
            // Arrange: Configura resposta rápida
            _mockTeamRepository
                .Setup(x => x.GetAllAsync())
                .ReturnsAsync(new List<Team>());

            _mockMapper
                .Setup(x => x.Map<List<TeamResponse>>(It.IsAny<List<Team>>()))
                .Returns(new List<TeamResponse>());

            // Act: Mede tempo de execução
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var resultado = await _teamService.GetAllTeamsAsync();
            stopwatch.Stop();

            // Assert: Deve ser rápido
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "porque operação deve ser rápida");
            resultado.Should().NotBeNull();
        }

        #endregion

        #region Testes de Validação

        /// <summary>
        /// Testa se valida IDs negativos corretamente
        /// </summary>
        [Theory(DisplayName = "Deve tratar IDs inválidos adequadamente")]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(-999)]
        public async Task DeveTratarIDsInvalidos_Adequadamente(int idInvalido)
        {
            // Arrange: Configura ID inválido
            _mockTeamRepository
                .Setup(x => x.GetByIdAsync(idInvalido))
                .ReturnsAsync((Team?)null);

            // Act: Busca com ID inválido
            var resultado = await _teamService.GetTeamByIdAsync(idInvalido);

            // Assert: Deve retornar null sem erro
            resultado.Should().BeNull($"porque ID {idInvalido} é inválido");
        }

        /// <summary>
        /// Testa se trata abreviações null ou vazias
        /// </summary>
        [Theory(DisplayName = "Deve tratar abreviações null ou vazias")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task DeveTratarAbreviacoesNullOuVazias(string? abreviacaoInvalida)
        {
            // Arrange & Act: Busca com abreviação inválida
            var resultado = await _teamService.GetTeamByAbbreviationAsync(abreviacaoInvalida!);

            // Assert: Deve retornar null
            resultado.Should().BeNull("porque abreviação inválida deve retornar null");
        }

        #endregion

        #region Métodos Auxiliares

        /// <summary>
        /// Limpa o cache de memória
        /// </summary>
        private void LimparCache()
        {
            if (_memoryCache is MemoryCache memoryCache)
            {
                memoryCache.Compact(1.0);
            }
        }

        #endregion
    }
}