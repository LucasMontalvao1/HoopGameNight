using AutoMapper;
using FluentAssertions;
using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.DTOs.Response;
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
    /// Testes unitários para o PlayerService.
    /// Testa a lógica de negócio relacionada aos jogadores da NBA.
    /// </summary>
    public class PlayerServiceTests : IDisposable
    {
        private readonly Mock<IPlayerRepository> _mockPlayerRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<ILogger<PlayerService>> _mockLogger;
        private readonly IMemoryCache _memoryCache;
        private readonly PlayerService _playerService;

        public PlayerServiceTests()
        {
            _mockPlayerRepository = new Mock<IPlayerRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockLogger = new Mock<ILogger<PlayerService>>();
            _memoryCache = new MemoryCache(new MemoryCacheOptions());

            var mockEspnService = new Mock<IEspnApiService>();

            _playerService = new PlayerService(
                _mockPlayerRepository.Object,
                mockEspnService.Object,
                _mockMapper.Object,
                _memoryCache,
                _mockLogger.Object
            );
        }

        public void Dispose()
        {
            _memoryCache?.Dispose();
        }

        #region Testes de Busca de Jogadores

        /// <summary>
        /// Testa se retorna jogadores e contagem quando request é válido
        /// </summary>
        [Fact(DisplayName = "Deve retornar jogadores e contagem quando request é válido")]
        public async Task DeveRetornarJogadoresEContagem_QuandoRequestEhValido()
        {
            // Arrange: Configura busca de jogadores
            var request = new SearchPlayerRequest
            {
                Search = "LeBron",
                Page = 1,
                PageSize = 10
            };

            var jogadores = TestDataBuilder.CreatePlayers(3);
            var jogadoresResponse = jogadores.Select(p => TestDataBuilder.CreatePlayerResponse(p.Id)).ToList();
            const int totalCount = 15;

            _mockPlayerRepository
                .Setup(x => x.SearchPlayersAsync(request))
                .ReturnsAsync((jogadores, totalCount));

            _mockMapper
                .Setup(x => x.Map<List<PlayerResponse>>(jogadores))
                .Returns(jogadoresResponse);

            // Act: Executa busca
            var (jogadoresResultado, totalResultado) = await _playerService.SearchPlayersAsync(request);

            // Assert: Verifica se retornou corretamente
            jogadoresResultado.Should().HaveCount(3, "porque configuramos 3 jogadores");
            totalResultado.Should().Be(totalCount, "porque total configurado é 15");
            jogadoresResultado.Should().BeEquivalentTo(jogadoresResponse, "porque deve mapear corretamente");

            // Verifica interações
            _mockPlayerRepository.Verify(x => x.SearchPlayersAsync(request), Times.Once);
            _mockMapper.Verify(x => x.Map<List<PlayerResponse>>(jogadores), Times.Once);
        }

        /// <summary>
        /// Testa se trata resultados vazios quando nenhum jogador é encontrado
        /// </summary>
        [Fact(DisplayName = "Deve tratar resultados vazios quando nenhum jogador é encontrado")]
        public async Task DeveTratarResultadosVazios_QuandoNenhumJogadorEhEncontrado()
        {
            // Arrange: Configura busca sem resultados
            var request = new SearchPlayerRequest
            {
                Search = "JogadorInexistente",
                Page = 1,
                PageSize = 10
            };

            var jogadoresVazios = new List<Player>();
            var responsesVazias = new List<PlayerResponse>();
            const int zeroCount = 0;

            _mockPlayerRepository
                .Setup(x => x.SearchPlayersAsync(request))
                .ReturnsAsync((jogadoresVazios, zeroCount));

            _mockMapper
                .Setup(x => x.Map<List<PlayerResponse>>(jogadoresVazios))
                .Returns(responsesVazias);

            // Act: Executa busca
            var (jogadoresResultado, totalResultado) = await _playerService.SearchPlayersAsync(request);

            // Assert: Deve retornar listas vazias
            jogadoresResultado.Should().BeEmpty("porque nenhum jogador foi encontrado");
            totalResultado.Should().Be(0, "porque não há jogadores para o termo buscado");
        }

        /// <summary>
        /// Testa se lança BusinessException quando repositório falha
        /// </summary>
        [Fact(DisplayName = "Deve lançar BusinessException quando o repositório falha")]
        public async Task DeveLancarBusinessException_QuandoRepositorioFalha()
        {
            // Arrange
            var request = new SearchPlayerRequest
            {
                Search = "LeBron",
                Page = 1,
                PageSize = 10
            };

            _mockPlayerRepository
                .Setup(x => x.SearchPlayersAsync(request))
                .ThrowsAsync(new Exception("Erro no banco de dados"));

            // Act & Assert: Esperar BusinessException (não Exception genérica)
            var exception = await Assert.ThrowsAsync<BusinessException>(
                () => _playerService.SearchPlayersAsync(request));

            // Verificar mensagem da BusinessException
            exception.Message.Should().Be("Falha ao buscar jogadores");

            // Verificar a Exception original encapsulada
            exception.InnerException.Should().NotBeNull();
            exception.InnerException.Should().BeOfType<Exception>();
            exception.InnerException!.Message.Should().Be("Erro no banco de dados");
        }


        /// <summary>
        /// Testa busca com diferentes termos válidos
        /// </summary>
        [Theory(DisplayName = "Deve buscar jogadores com diferentes termos válidos")]
        [InlineData("LeBron", 3)]
        [InlineData("Stephen", 2)]
        [InlineData("Kobe", 1)]
        [InlineData("Jordan", 5)]
        public async Task DeveBuscarJogadores_ComDiferentesTermosValidos(string termoBusca, int quantidadeEsperada)
        {
            // Arrange: Configura busca com termo específico
            var request = new SearchPlayerRequest
            {
                Search = termoBusca,
                Page = 1,
                PageSize = 10
            };

            var jogadores = TestDataBuilder.CreatePlayers(quantidadeEsperada);
            var jogadoresResponse = jogadores.Select(p => TestDataBuilder.CreatePlayerResponse(p.Id)).ToList();

            _mockPlayerRepository
                .Setup(x => x.SearchPlayersAsync(request))
                .ReturnsAsync((jogadores, quantidadeEsperada));

            _mockMapper
                .Setup(x => x.Map<List<PlayerResponse>>(jogadores))
                .Returns(jogadoresResponse);

            // Act: Executa busca
            var (jogadoresResultado, totalResultado) = await _playerService.SearchPlayersAsync(request);

            // Assert: Verifica resultado específico para o termo
            jogadoresResultado.Should().HaveCount(quantidadeEsperada,
                $"porque esperamos {quantidadeEsperada} jogadores para '{termoBusca}'");
            totalResultado.Should().Be(quantidadeEsperada);
        }

        #endregion

        #region Testes de Jogadores por Time

        /// <summary>
        /// Testa se retorna jogadores do time quando ID do time é válido
        /// </summary>
        [Fact(DisplayName = "Deve retornar jogadores do time quando ID do time é válido")]
        public async Task DeveRetornarJogadoresDoTime_QuandoIDDoTimeEhValido()
        {
            // Arrange: Configura busca por time
            const int timeId = 1;
            const int page = 1;
            const int pageSize = 10;

            var jogadores = TestDataBuilder.CreatePlayers(5, timeId);
            var jogadoresResponse = jogadores.Select(p => TestDataBuilder.CreatePlayerResponse(p.Id)).ToList();
            const int totalCount = 25;

            _mockPlayerRepository
                .Setup(x => x.GetPlayersByTeamAsync(timeId, page, pageSize))
                .ReturnsAsync((jogadores, totalCount));

            _mockMapper
                .Setup(x => x.Map<List<PlayerResponse>>(jogadores))
                .Returns(jogadoresResponse);

            // Act: Busca jogadores por time
            var (jogadoresResultado, totalResultado) = await _playerService.GetPlayersByTeamAsync(timeId, page, pageSize);

            // Assert: Verifica resultado
            jogadoresResultado.Should().HaveCount(5, "porque configuramos 5 jogadores para o time");
            totalResultado.Should().Be(totalCount, "porque total configurado é 25");
            jogadoresResultado.Should().BeEquivalentTo(jogadoresResponse, "porque deve mapear corretamente");

            // Todos os jogadores devem ser do mesmo time
            jogadores.Should().OnlyContain(j => j.TeamId == timeId, "porque todos devem ser do mesmo time");
        }

        /// <summary>
        /// Testa se trata parâmetros inválidos graciosamente
        /// </summary>
        [Theory(DisplayName = "Deve tratar parâmetros inválidos graciosamente")]
        [InlineData(0, 1, 10)]      // Time ID zero
        [InlineData(-1, 1, 10)]     // Time ID negativo
        [InlineData(1, 0, 10)]      // Página zero
        [InlineData(1, -1, 10)]     // Página negativa
        [InlineData(1, 1, 0)]       // Page size zero
        [InlineData(1, 1, -5)]      // Page size negativo
        [InlineData(999, 1, 10)]    // Time inexistente
        public async Task DeveTratarParametrosInvalidos_Graciosamente(int timeId, int page, int pageSize)
        {
            // Arrange: Configura resposta vazia para parâmetros inválidos
            var jogadoresVazios = new List<Player>();
            var responsesVazias = new List<PlayerResponse>();

            _mockPlayerRepository
                .Setup(x => x.GetPlayersByTeamAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync((jogadoresVazios, 0));

            _mockMapper
                .Setup(x => x.Map<List<PlayerResponse>>(jogadoresVazios))
                .Returns(responsesVazias);

            // Act: Busca com parâmetros inválidos
            var (jogadoresResultado, totalResultado) = await _playerService.GetPlayersByTeamAsync(timeId, page, pageSize);

            // Assert: Deve retornar resultado vazio sem erro
            jogadoresResultado.Should().BeEmpty($"porque parâmetros inválidos (timeId:{timeId}, page:{page}, pageSize:{pageSize}) devem retornar lista vazia");
            totalResultado.Should().Be(0, "porque não há jogadores para parâmetros inválidos");
        }

        /// <summary>
        /// Testa paginação com diferentes configurações válidas
        /// </summary>
        [Theory(DisplayName = "Deve paginar resultados corretamente")]
        [InlineData(1, 1, 5, 5)]    // Primeira página, 5 itens
        [InlineData(1, 2, 5, 3)]    // Segunda página, 3 itens restantes
        [InlineData(1, 1, 20, 15)]  // Página única, menos itens que o limite
        public async Task DevePaginarResultados_Corretamente(int timeId, int page, int pageSize, int quantidadeEsperada)
        {
            // Arrange: Configura paginação
            var jogadores = TestDataBuilder.CreatePlayers(quantidadeEsperada, timeId);
            var jogadoresResponse = jogadores.Select(p => TestDataBuilder.CreatePlayerResponse(p.Id)).ToList();
            const int totalGeral = 15;

            _mockPlayerRepository
                .Setup(x => x.GetPlayersByTeamAsync(timeId, page, pageSize))
                .ReturnsAsync((jogadores, totalGeral));

            _mockMapper
                .Setup(x => x.Map<List<PlayerResponse>>(jogadores))
                .Returns(jogadoresResponse);

            // Act: Busca página específica
            var (jogadoresResultado, totalResultado) = await _playerService.GetPlayersByTeamAsync(timeId, page, pageSize);

            // Assert: Verifica paginação
            jogadoresResultado.Should().HaveCount(quantidadeEsperada,
                $"porque página {page} com tamanho {pageSize} deve retornar {quantidadeEsperada} itens");
            totalResultado.Should().Be(totalGeral, "porque total geral deve ser consistente");
        }

        #endregion

        #region Testes de Busca por ID

        /// <summary>
        /// Testa se retorna jogador quando jogador existe
        /// </summary>
        [Fact(DisplayName = "Deve retornar jogador quando jogador existe")]
        public async Task DeveRetornarJogador_QuandoJogadorExiste()
        {
            // Arrange: Configura jogador existente
            const int jogadorId = 1;
            var jogador = TestDataBuilder.CreatePlayer(jogadorId);
            var jogadorResponse = TestDataBuilder.CreatePlayerResponse(jogadorId);

            _mockPlayerRepository
                .Setup(x => x.GetByIdAsync(jogadorId))
                .ReturnsAsync(jogador);

            _mockMapper
                .Setup(x => x.Map<PlayerResponse>(jogador))
                .Returns(jogadorResponse);

            // Act: Busca jogador por ID
            var resultado = await _playerService.GetPlayerByIdAsync(jogadorId);

            // Assert: Verifica se retornou corretamente
            resultado.Should().NotBeNull("porque jogador existe");
            resultado!.Id.Should().Be(jogadorId, "porque buscamos pelo ID correto");
            resultado.FullName.Should().Be(jogadorResponse.FullName, "porque deve mapear corretamente");

            // Verifica interações
            _mockPlayerRepository.Verify(x => x.GetByIdAsync(jogadorId), Times.Once);
            _mockMapper.Verify(x => x.Map<PlayerResponse>(jogador), Times.Once);
        }

        /// <summary>
        /// Testa se retorna null quando jogador não existe
        /// </summary>
        [Fact(DisplayName = "Deve retornar null quando jogador não existe")]
        public async Task DeveRetornarNull_QuandoJogadorNaoExiste()
        {
            // Arrange: Configura jogador inexistente
            const int jogadorId = 999;

            _mockPlayerRepository
                .Setup(x => x.GetByIdAsync(jogadorId))
                .ReturnsAsync((Player?)null);

            // Act: Busca jogador inexistente
            var resultado = await _playerService.GetPlayerByIdAsync(jogadorId);

            // Assert: Deve retornar null
            resultado.Should().BeNull("porque jogador não existe");

            // Verifica que mapper não foi chamado
            _mockPlayerRepository.Verify(x => x.GetByIdAsync(jogadorId), Times.Once);
            _mockMapper.Verify(x => x.Map<PlayerResponse>(It.IsAny<Player>()), Times.Never,
                "porque não deve mapear quando jogador não existe");
        }

        /// <summary>
        /// Testa se trata IDs inválidos adequadamente
        /// </summary>
        [Theory(DisplayName = "Deve tratar IDs inválidos adequadamente")]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(-999)]
        public async Task DeveTratarIDsInvalidos_Adequadamente(int idInvalido)
        {
            // Arrange: Configura ID inválido
            _mockPlayerRepository
                .Setup(x => x.GetByIdAsync(idInvalido))
                .ReturnsAsync((Player?)null);

            // Act: Busca com ID inválido
            var resultado = await _playerService.GetPlayerByIdAsync(idInvalido);

            // Assert: Deve retornar null sem erro
            resultado.Should().BeNull($"porque ID {idInvalido} é inválido");
        }

        #endregion

        #region Testes de Sincronização de Jogadores
        // REMOVIDO: Testes de sincronização com BallDontLie API
        // A funcionalidade SyncPlayersAsync foi desabilitada após remoção do BallDontLie
        // Sincronização de jogadores agora deve ser feita via ESPN API ou manualmente
        #endregion

        #region Testes de Performance

        /// <summary>
        /// Testa se operações executam rapidamente
        /// </summary>
        [Fact(DisplayName = "Deve executar operações rapidamente")]
        public async Task DeveExecutarOperacoes_Rapidamente()
        {
            // Arrange: Configura resposta rápida
            const int jogadorId = 1;
            var jogador = TestDataBuilder.CreatePlayer(jogadorId);
            var jogadorResponse = TestDataBuilder.CreatePlayerResponse(jogadorId);

            _mockPlayerRepository
                .Setup(x => x.GetByIdAsync(jogadorId))
                .ReturnsAsync(jogador);

            _mockMapper
                .Setup(x => x.Map<PlayerResponse>(jogador))
                .Returns(jogadorResponse);

            // Act: Mede tempo de execução
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var resultado = await _playerService.GetPlayerByIdAsync(jogadorId);
            stopwatch.Stop();

            // Assert: Deve ser rápido
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "porque operação deve ser rápida");
            resultado.Should().NotBeNull();
        }

        #endregion

        #region Testes de Cache

        /// <summary>
        /// Testa se utiliza cache quando disponível (se implementado)
        /// </summary>
        [Fact(DisplayName = "Deve manter consistência entre chamadas")]
        public async Task DeveManterConsistencia_EntreChamadas()
        {
            // Arrange: Configura dados consistentes
            const int jogadorId = 1;
            var jogador = TestDataBuilder.CreatePlayer(jogadorId);
            var jogadorResponse = TestDataBuilder.CreatePlayerResponse(jogadorId);

            _mockPlayerRepository
                .Setup(x => x.GetByIdAsync(jogadorId))
                .ReturnsAsync(jogador);

            _mockMapper
                .Setup(x => x.Map<PlayerResponse>(jogador))
                .Returns(jogadorResponse);

            // Act: Chama método duas vezes
            var resultado1 = await _playerService.GetPlayerByIdAsync(jogadorId);
            var resultado2 = await _playerService.GetPlayerByIdAsync(jogadorId);

            // Assert: Resultados devem ser consistentes
            resultado1.Should().BeEquivalentTo(resultado2, "porque resultado deve ser consistente");
            resultado1.Should().NotBeNull();
            resultado2.Should().NotBeNull();
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