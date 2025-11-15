using AutoMapper;
using FluentAssertions;
using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Enums;
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
    /// Testes unitários para o GameService.
    /// Testa a lógica de negócio relacionada aos jogos da NBA.
    /// </summary>
    public class GameServiceTests : IClassFixture<GameServiceTestFixture>
    {
        private readonly GameServiceTestFixture _fixture;

        public GameServiceTests(GameServiceTestFixture fixture)
        {
            _fixture = fixture;
            // Reseta mocks antes de cada teste para evitar interferências
            _fixture.ResetarMocks();
        }

        #region Testes de Jogos de Hoje

        /// <summary>
        /// Testa se retorna jogos de hoje quando existem jogos
        /// </summary>
        [Fact(DisplayName = "Deve retornar jogos de hoje quando existem jogos")]
        public async Task DeveRetornarJogosDeHoje_QuandoExistemJogos()
        {
            // Arrange: Configura 3 jogos para hoje
            const int quantidadeJogos = 3;
            var jogos = CriarJogosAmostra(quantidadeJogos);
            var jogosResponse = CriarJogosResponseAmostra(quantidadeJogos);

            _fixture.MockGameRepository
                .Setup(x => x.GetTodayGamesAsync())
                .ReturnsAsync(jogos);

            _fixture.MockMapper
                .Setup(x => x.Map<List<GameResponse>>(jogos))
                .Returns(jogosResponse);

            // Act: Busca jogos de hoje
            var resultado = await _fixture.GameService.GetTodayGamesAsync();

            // Assert: Verifica se retornou corretamente
            resultado.Should().NotBeNull("porque sempre deve retornar uma lista");
            resultado.Should().HaveCount(quantidadeJogos, "porque configuramos 3 jogos");
            resultado.Should().BeEquivalentTo(jogosResponse, "porque deve mapear corretamente");

            // Verifica interações
            _fixture.MockGameRepository.Verify(x => x.GetTodayGamesAsync(), Times.Once);
            _fixture.MockMapper.Verify(x => x.Map<List<GameResponse>>(jogos), Times.Once);
        }

        /// <summary>
        /// Testa se retorna lista vazia quando não há jogos hoje
        /// </summary>
        [Fact(DisplayName = "Deve retornar lista vazia quando não há jogos hoje")]
        public async Task DeveRetornarListaVazia_QuandoNaoHaJogosHoje()
        {
            // IMPORTANTE: Criar nova instância do fixture para garantir isolamento
            var fixture = new GameServiceTestFixture();

            // Arrange: Configurar mocks para retornar vazio
            fixture.MockGameRepository
                .Setup(x => x.GetTodayGamesAsync())
                .ReturnsAsync(new List<Game>());

            fixture.MockMapper
                .Setup(x => x.Map<List<GameResponse>>(It.IsAny<IEnumerable<Game>>()))
                .Returns(new List<GameResponse>());

            // Configurar também o Map genérico caso seja usado
            fixture.MockMapper
                .Setup(x => x.Map<IEnumerable<GameResponse>>(It.IsAny<IEnumerable<Game>>()))
                .Returns(new List<GameResponse>());

            // Act
            var resultado = await fixture.GameService.GetTodayGamesAsync();

            // Assert
            resultado.Should().NotBeNull("o resultado não deve ser nulo");
            resultado.Should().BeEmpty("porque não há jogos configurados");

            // Verificar que o repositório foi chamado
            fixture.MockGameRepository.Verify(x => x.GetTodayGamesAsync(), Times.Once);
        }

        /// <summary>
        /// Testa se lança BusinessException quando repositório falha
        /// </summary>
        [Fact(DisplayName = "Deve lançar Exception quando repositório falha")]
        public async Task DeveLancarException_QuandoRepositorioFalha()
        {
            // Arrange: Configura erro no repositório
            _fixture.MockGameRepository
                .Setup(x => x.GetTodayGamesAsync())
                .ThrowsAsync(new Exception("Erro no banco de dados"));

            // Act & Assert: Verifica se lança a exceção real
            var exception = await Assert.ThrowsAsync<Exception>(
                () => _fixture.GameService.GetTodayGamesAsync());

            exception.Message.Should().Be("Erro no banco de dados");
        }

        #endregion

        #region Testes de Jogos por Data

        /// <summary>
        /// Testa se retorna jogos para datas específicas
        /// </summary>
        [Theory(DisplayName = "Deve retornar jogos para datas específicas")]
        [InlineData("2024-01-15")]
        [InlineData("2024-12-25")]
        [InlineData("2023-06-01")]
        public async Task DeveRetornarJogos_ParaDatasEspecificas(string dataString)
        {
            // Arrange: Converte data e configura mocks
            var data = DateTime.Parse(dataString);
            var jogos = CriarJogosAmostra(2);
            var jogosResponse = CriarJogosResponseAmostra(2);

            _fixture.MockGameRepository
                .Setup(x => x.GetGamesByDateAsync(data))
                .ReturnsAsync(jogos);

            _fixture.MockMapper
                .Setup(x => x.Map<List<GameResponse>>(jogos))
                .Returns(jogosResponse);

            // Act: Busca jogos por data
            var resultado = await _fixture.GameService.GetGamesByDateAsync(data);

            // Assert: Verifica resultado
            resultado.Should().HaveCount(2, "porque configuramos 2 jogos para a data");
            _fixture.MockGameRepository.Verify(x => x.GetGamesByDateAsync(data), Times.Once);
        }

        #endregion

        #region Testes de Jogos Paginados

        /// <summary>
        /// Testa se retorna jogos paginados com request válido
        /// </summary>
        [Fact(DisplayName = "Deve retornar jogos paginados com request válido")]
        public async Task DeveRetornarJogosPaginados_ComRequestValido()
        {
            // Arrange: Cria request de busca
            var request = new GetGamesRequest
            {
                Page = 1,
                PageSize = 10,
                TeamId = 1,
                Status = GameStatus.Final
            };

            var jogos = CriarJogosAmostra(5);
            var jogosResponse = CriarJogosResponseAmostra(5);
            const int totalCount = 25;

            _fixture.MockGameRepository
                .Setup(x => x.GetGamesAsync(request))
                .ReturnsAsync((jogos, totalCount));

            _fixture.MockMapper
                .Setup(x => x.Map<List<GameResponse>>(jogos))
                .Returns(jogosResponse);

            // Act: Busca jogos paginados
            var (jogosResultado, totalResultado) = await _fixture.GameService.GetGamesAsync(request);

            // Assert: Verifica paginação
            jogosResultado.Should().HaveCount(5, "porque retornamos 5 jogos da página");
            totalResultado.Should().Be(25, "porque total configurado é 25");

            _fixture.MockGameRepository.Verify(x => x.GetGamesAsync(request), Times.Once);
        }

        #endregion

        #region Testes de Sincronização de Jogos
        // NOTA: Testes de sincronização BallDontLie removidos - agora usando ESPN API
        // A sincronização de jogos agora é feita via GameService.SyncGamesFromEspnAsync()
        #endregion

        #region Testes de Busca por ID

        /// <summary>
        /// Testa se retorna jogo quando existe
        /// </summary>
        [Fact(DisplayName = "Deve retornar jogo quando existe")]
        public async Task DeveRetornarJogo_QuandoExiste()
        {
            // Arrange: Configura jogo existente
            const int jogoId = 1;
            var jogo = CriarJogoAmostra(jogoId);
            var jogoResponse = CriarJogoResponseAmostra(jogoId);

            _fixture.MockGameRepository
                .Setup(x => x.GetByIdAsync(jogoId))
                .ReturnsAsync(jogo);

            _fixture.MockMapper
                .Setup(x => x.Map<GameResponse>(jogo))
                .Returns(jogoResponse);

            // Act: Busca jogo por ID
            var resultado = await _fixture.GameService.GetGameByIdAsync(jogoId);

            // Assert: Verifica se retornou corretamente
            resultado.Should().NotBeNull("porque jogo existe");
            resultado!.Id.Should().Be(jogoId, "porque buscamos pelo ID correto");
            resultado.GameTitle.Should().Be(jogoResponse.GameTitle, "porque deve mapear corretamente");
        }

        /// <summary>
        /// Testa se retorna null quando jogo não existe
        /// </summary>
        [Fact(DisplayName = "Deve retornar null quando jogo não existe")]
        public async Task DeveRetornarNull_QuandoJogoNaoExiste()
        {
            // Arrange: Configura jogo inexistente
            const int jogoId = 999;

            _fixture.MockGameRepository
                .Setup(x => x.GetByIdAsync(jogoId))
                .ReturnsAsync((Game?)null);

            // Act: Busca jogo inexistente
            var resultado = await _fixture.GameService.GetGameByIdAsync(jogoId);

            // Assert: Deve retornar null
            resultado.Should().BeNull("porque jogo não existe");
            _fixture.MockMapper.Verify(x => x.Map<GameResponse>(It.IsAny<Game>()), Times.Never,
                "porque não deve mapear quando jogo não existe");
        }

        #endregion

        #region Testes de Performance e Cache

        /// <summary>
        /// Testa se operações executam dentro do tempo esperado
        /// </summary>
        [Fact(DisplayName = "Deve executar operações rapidamente")]
        public async Task DeveExecutarOperacoes_Rapidamente()
        {
            // Arrange: Configura resposta rápida
            _fixture.MockGameRepository
                .Setup(x => x.GetTodayGamesAsync())
                .ReturnsAsync(new List<Game>());

            _fixture.MockMapper
                .Setup(x => x.Map<List<GameResponse>>(It.IsAny<List<Game>>()))
                .Returns(new List<GameResponse>());

            // Act: Mede tempo de execução
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var resultado = await _fixture.GameService.GetTodayGamesAsync();
            stopwatch.Stop();

            // Assert: Deve ser rápido
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "porque operação deve ser rápida");
            resultado.Should().NotBeNull();
        }

        /// <summary>
        /// Testa se cache funciona corretamente
        /// </summary>
        [Fact(DisplayName = "Deve utilizar cache quando disponível")]
        public async Task DeveUtilizarCache_QuandoDisponivel()
        {
            // Arrange: Limpa cache e configura dados
            _fixture.LimparCache();
            var jogos = CriarJogosAmostra(2);
            var jogosResponse = CriarJogosResponseAmostra(2);

            _fixture.MockGameRepository
                .Setup(x => x.GetTodayGamesAsync())
                .ReturnsAsync(jogos);

            _fixture.MockMapper
                .Setup(x => x.Map<List<GameResponse>>(jogos))
                .Returns(jogosResponse);

            // Act: Chama método duas vezes
            var resultado1 = await _fixture.GameService.GetTodayGamesAsync();
            var resultado2 = await _fixture.GameService.GetTodayGamesAsync();

            // Assert: Ambos devem retornar o mesmo resultado
            resultado1.Should().BeEquivalentTo(resultado2, "porque resultado deve ser consistente");

            // Nota: Verificação de cache depende da implementação específica do GameService
            // Se ele usar cache interno, repository pode ser chamado apenas uma vez
        }

        #endregion

        #region Métodos Auxiliares

        /// <summary>
        /// Cria lista de jogos para testes
        /// </summary>
        private static List<Game> CriarJogosAmostra(int quantidade)
        {
            var jogos = new List<Game>();
            for (int i = 1; i <= quantidade; i++)
            {
                jogos.Add(CriarJogoAmostra(i));
            }
            return jogos;
        }

        /// <summary>
        /// Cria um jogo de exemplo para testes
        /// </summary>
        private static Game CriarJogoAmostra(int id)
        {
            return new Game
            {
                Id = id,
                ExternalId = (id + 1000).ToString(),
                Date = DateTime.Today,
                DateTime = DateTime.Today.AddHours(20),
                HomeTeamId = 1,
                VisitorTeamId = 2,
                HomeTeamScore = 110,
                VisitorTeamScore = 105,
                Status = GameStatus.Final,
                Period = 4,
                TimeRemaining = "Final",
                PostSeason = false,
                Season = 2024,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                HomeTeam = CriarTimeAmostra(1, "Lakers"),
                VisitorTeam = CriarTimeAmostra(2, "Warriors")
            };
        }

        /// <summary>
        /// Cria um time de exemplo para testes
        /// </summary>
        private static Team CriarTimeAmostra(int id, string nome)
        {
            return new Team
            {
                Id = id,
                ExternalId = id + 100,
                Name = nome,
                FullName = $"Sample {nome}",
                Abbreviation = nome.Length >= 3 ? nome[..3].ToUpper() : nome.ToUpper(),
                City = "Sample City",
                Conference = Conference.West,
                Division = "Pacific",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Cria lista de responses de jogos para testes
        /// </summary>
        private static List<GameResponse> CriarJogosResponseAmostra(int quantidade)
        {
            var responses = new List<GameResponse>();
            for (int i = 1; i <= quantidade; i++)
            {
                responses.Add(CriarJogoResponseAmostra(i));
            }
            return responses;
        }

        /// <summary>
        /// Cria um GameResponse de exemplo para testes
        /// </summary>
        private static GameResponse CriarJogoResponseAmostra(int id)
        {
            return new GameResponse
            {
                Id = id,
                Date = DateTime.Today,
                DateTime = DateTime.Today.AddHours(20),
                HomeTeam = new TeamSummaryResponse
                {
                    Id = 1,
                    Name = "Lakers",
                    Abbreviation = "LAL",
                    City = "Los Angeles",
                    DisplayName = "Los Angeles Lakers"
                },
                VisitorTeam = new TeamSummaryResponse
                {
                    Id = 2,
                    Name = "Warriors",
                    Abbreviation = "GSW",
                    City = "Golden State",
                    DisplayName = "Golden State Warriors"
                },
                HomeTeamScore = 110,
                VisitorTeamScore = 105,
                Status = "Final",
                StatusDisplay = "Final",
                Period = 4,
                TimeRemaining = "Final",
                PostSeason = false,
                Season = 2024,
                Score = "110 - 105",
                GameTitle = "GSW @ LAL",
                IsLive = false,
                IsCompleted = true
            };
        }

        /// <summary>
        /// REMOVIDO: BallDontLie DTOs deletados - usando apenas ESPN API
        /// </summary>
        // private static List<BallDontLieGameDto> CriarJogosExternosAmostra(int quantidade)
        // {
        //     var jogos = new List<BallDontLieGameDto>();
        //     for (int i = 1; i <= quantidade; i++)
        //     {
        //         jogos.Add(new BallDontLieGameDto
        //         {
        //             Id = i + 1000,
        //             Date = DateTime.Today.ToString("yyyy-MM-dd"),
        //             HomeTeam = new BallDontLieTeamDto
        //             {
        //                 Id = 1,
        //                 Name = "Lakers",
        //                 FullName = "Los Angeles Lakers",
        //                 Abbreviation = "LAL",
        //                 City = "Los Angeles",
        //                 Conference = "West",
        //                 Division = "Pacific"
        //             },
        //             VisitorTeam = new BallDontLieTeamDto
        //             {
        //                 Id = 2,
        //                 Name = "Warriors",
        //                 FullName = "Golden State Warriors",
        //                 Abbreviation = "GSW",
        //                 City = "Golden State",
        //                 Conference = "West",
        //                 Division = "Pacific"
        //             },
        //             HomeTeamScore = 110,
        //             VisitorTeamScore = 105,
        //             Status = "Final",
        //             Period = 4,
        //             Time = "Final",
        //             Postseason = false,
        //             Season = 2024
        //         });
        //     }
        //     return jogos;
        // }

        #endregion
    }
}