using FluentAssertions;
using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.Enums;
using HoopGameNight.Core.Interfaces.Infrastructure;
using HoopGameNight.Core.Models.Entities;
using HoopGameNight.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace HoopGameNight.Tests.Unit.Infrastructure.Repositories
{
    public class GameRepositoryTests
    {
        private readonly Mock<IDatabaseConnection> _mockDatabaseConnection;
        private readonly Mock<ISqlLoader> _mockSqlLoader;
        private readonly Mock<ILogger<GameRepository>> _mockLogger;
        private readonly Mock<IDbConnection> _mockDbConnection;
        private readonly GameRepository _repository;

        public GameRepositoryTests()
        {
            _mockDatabaseConnection = new Mock<IDatabaseConnection>();
            _mockSqlLoader = new Mock<ISqlLoader>();
            _mockLogger = new Mock<ILogger<GameRepository>>();
            _mockDbConnection = new Mock<IDbConnection>();

            // Configurar o mock para sempre retornar a mesma conexão mockada
            _mockDatabaseConnection
                .Setup(x => x.CreateConnection())
                .Returns(_mockDbConnection.Object);

            _repository = new GameRepository(
                _mockDatabaseConnection.Object,
                _mockSqlLoader.Object,
                _mockLogger.Object
            );
        }

        #region Constructor Tests

        [Fact(DisplayName = "Repository deve validar dependências no construtor")]
        public void Repository_DeveValidarDependencias()
        {
            // Assert
            Assert.Throws<ArgumentNullException>(() =>
                new GameRepository(null!, _mockSqlLoader.Object, _mockLogger.Object));

            Assert.Throws<ArgumentNullException>(() =>
                new GameRepository(_mockDatabaseConnection.Object, null!, _mockLogger.Object));

            Assert.Throws<ArgumentNullException>(() =>
                new GameRepository(_mockDatabaseConnection.Object, _mockSqlLoader.Object, null!));
        }

        [Fact(DisplayName = "Repository deve ter nome de entidade correto")]
        public void Repository_DeveTerNomeEntidadeCorreto()
        {
            // Act & Assert
            _repository.Should().NotBeNull();
            _repository.GetType().Name.Should().Be("GameRepository");
        }

        #endregion

        #region SqlLoader Tests

        [Fact(DisplayName = "Deve carregar SQL para jogos de hoje")]
        public async Task DeveCarregarSql_ParaJogosDeHoje()
        {
            // Arrange
            var expectedSql = "SELECT * FROM Games WHERE DATE(date) = CURDATE()";

            _mockSqlLoader
                .Setup(x => x.LoadSqlAsync("Games", "GetTodayGames"))
                .ReturnsAsync(expectedSql);

            // Act
            var sql = await _mockSqlLoader.Object.LoadSqlAsync("Games", "GetTodayGames");

            // Assert
            sql.Should().Be(expectedSql);
            _mockSqlLoader.Verify(x => x.LoadSqlAsync("Games", "GetTodayGames"), Times.Once);
        }

        [Fact(DisplayName = "Deve carregar SQL para busca por data")]
        public async Task DeveCarregarSql_ParaBuscaPorData()
        {
            // Arrange
            var expectedSql = "SELECT * FROM Games WHERE DATE(date) = @Date";

            _mockSqlLoader
                .Setup(x => x.LoadSqlAsync("Games", "GetByDate"))
                .ReturnsAsync(expectedSql);

            // Act
            var sql = await _mockSqlLoader.Object.LoadSqlAsync("Games", "GetByDate");

            // Assert
            sql.Should().Be(expectedSql);
        }

        [Fact(DisplayName = "Deve carregar SQL para busca filtrada")]
        public async Task DeveCarregarSql_ParaBuscaFiltrada()
        {
            // Arrange
            var expectedSql = @"SELECT * FROM Games 
                WHERE (@Date IS NULL OR DATE(date) = @Date)
                AND (@TeamId IS NULL OR home_team_id = @TeamId OR visitor_team_id = @TeamId)";

            _mockSqlLoader
                .Setup(x => x.LoadSqlAsync("Games", "GetFiltered"))
                .ReturnsAsync(expectedSql);

            // Act
            var sql = await _mockSqlLoader.Object.LoadSqlAsync("Games", "GetFiltered");

            // Assert
            sql.Should().Contain("@Date");
            sql.Should().Contain("@TeamId");
        }

        #endregion

        #region GetGamesAsync Tests

        [Fact(DisplayName = "GetGamesAsync deve validar request")]
        public void GetGamesAsync_DeveValidarRequest()
        {
            // Arrange
            var request = new GetGamesRequest
            {
                Date = DateTime.Today,
                TeamId = 1,
                Status = GameStatus.Live,
                Page = 1,
                PageSize = 10
            };

            // Act & Assert
            request.Should().NotBeNull();
            request.Page.Should().Be(1);
            request.PageSize.Should().Be(10);
            request.Skip.Should().Be(0);
            request.Take.Should().Be(10);
        }

        #endregion

        #region Entity Tests

        [Fact(DisplayName = "Game deve calcular título correto")]
        public void Game_DeveCalcularTituloCorreto()
        {
            // Arrange
            var game = CriarJogo();
            game.HomeTeam = new Team { Name = "Lakers", Abbreviation = "LAL" };
            game.VisitorTeam = new Team { Name = "Warriors", Abbreviation = "GSW" };

            // Act
            var titulo = game.GameTitle;

            // Assert
            titulo.Should().Be("GSW @ LAL");
        }

        [Fact(DisplayName = "Game deve calcular score correto")]
        public void Game_DeveCalcularScoreCorreto()
        {
            // Arrange
            var game = CriarJogo();
            game.HomeTeamScore = 110;
            game.VisitorTeamScore = 105;

            // Act
            var score = game.Score;

            // Assert
            score.Should().Be("110 - 105");
        }

        [Fact(DisplayName = "Game deve mostrar score 0-0 quando não iniciado")]
        public void Game_DeveMostrarScore00_QuandoNaoIniciado()
        {
            // Arrange
            var game = CriarJogo();
            game.HomeTeamScore = null;
            game.VisitorTeamScore = null;

            // Act
            var score = game.Score;

            // Assert
            score.Should().Be("0 - 0");
        }

        [Fact(DisplayName = "Game deve calcular vencedor corretamente")]
        public void Game_DeveCalcularVencedorCorretamente()
        {
            // Arrange
            var game = CriarJogo();
            game.HomeTeam = new Team { Id = 1, Name = "Lakers", Abbreviation = "LAL" };
            game.VisitorTeam = new Team { Id = 2, Name = "Warriors", Abbreviation = "GSW" };
            game.HomeTeamScore = 110;
            game.VisitorTeamScore = 100;
            game.Status = GameStatus.Final;

            // Act
            var vencedor = game.WinningTeam;
            var scoreVencedor = game.WinningScore;
            var scorePerdedor = game.LosingScore;

            // Assert
            vencedor.Should().NotBeNull();
            vencedor!.Id.Should().Be(game.HomeTeamId);
            scoreVencedor.Should().Be(110);
            scorePerdedor.Should().Be(100);
        }

        [Fact(DisplayName = "Game deve retornar null para vencedor em empate")]
        public void Game_DeveRetornarNullParaVencedorEmEmpate()
        {
            // Arrange
            var game = CriarJogo();
            game.HomeTeam = new Team { Id = 1, Name = "Lakers" };
            game.VisitorTeam = new Team { Id = 2, Name = "Warriors" };
            game.HomeTeamScore = 100;
            game.VisitorTeamScore = 100;
            game.Status = GameStatus.Final;

            // Act
            var vencedor = game.WinningTeam;

            // Assert
            vencedor.Should().BeNull();
        }

        [Fact(DisplayName = "Game deve identificar jogo ao vivo")]
        public void Game_DeveIdentificarJogoAoVivo()
        {
            // Arrange
            var game = CriarJogo();
            game.Status = GameStatus.Live;

            // Act
            var aoVivo = game.IsLive;
            var finalizado = game.IsCompleted;
            var agendado = game.IsScheduled;

            // Assert
            aoVivo.Should().BeTrue();
            finalizado.Should().BeFalse();
            agendado.Should().BeFalse();
        }

        [Fact(DisplayName = "Game deve identificar jogo finalizado")]
        public void Game_DeveIdentificarJogoFinalizado()
        {
            // Arrange
            var game = CriarJogo();
            game.Status = GameStatus.Final;

            // Act
            var finalizado = game.IsCompleted;
            var aoVivo = game.IsLive;
            var agendado = game.IsScheduled;

            // Assert
            finalizado.Should().BeTrue();
            aoVivo.Should().BeFalse();
            agendado.Should().BeFalse();
        }

        #endregion

        #region Data Transformation Tests

        [Fact(DisplayName = "Deve criar lista de jogos para testes")]
        public void DeveCriarListaDeJogos_ParaTestes()
        {
            // Act
            var jogos = CriarListaDeJogos();

            // Assert
            jogos.Should().HaveCount(3);
            jogos.Should().OnlyContain(g => g.ExternalId > 0);
            jogos.Should().OnlyContain(g => g.HomeTeamId > 0);
            jogos.Should().OnlyContain(g => g.VisitorTeamId > 0);
        }

        [Fact(DisplayName = "Deve criar jogos com diferentes status")]
        public void DeveCriarJogos_ComDiferentesStatus()
        {
            // Act
            var jogos = CriarListaDeJogos();

            // Assert
            var statusList = jogos.Select(g => g.Status).Distinct().ToList();
            statusList.Should().Contain(GameStatus.Scheduled);
            statusList.Should().Contain(GameStatus.Live);
            statusList.Should().Contain(GameStatus.Final);
        }

        #endregion

        #region Additional Tests

        [Fact(DisplayName = "Game deve identificar jogo de hoje")]
        public void Game_DeveIdentificarJogoDeHoje()
        {
            // Arrange
            var game = CriarJogo();
            game.Date = DateTime.Today;

            // Act
            var hoje = game.IsToday;
            var amanha = game.IsTomorrow;

            // Assert
            hoje.Should().BeTrue();
            amanha.Should().BeFalse();
        }

        [Fact(DisplayName = "Game deve identificar jogo de amanhã")]
        public void Game_DeveIdentificarJogoDeAmanha()
        {
            // Arrange
            var game = CriarJogo();
            game.Date = DateTime.Today.AddDays(1);

            // Act
            var hoje = game.IsToday;
            var amanha = game.IsTomorrow;

            // Assert
            hoje.Should().BeFalse();
            amanha.Should().BeTrue();
        }

        [Fact(DisplayName = "Game deve validar dados corretamente")]
        public void Game_DeveValidarDadosCorretamente()
        {
            // Arrange
            var gameValido = CriarJogo();
            gameValido.ExternalId = 100;
            gameValido.HomeTeamId = 1;
            gameValido.VisitorTeamId = 2;
            gameValido.Season = 2024;

            var gameInvalido = CriarJogo();
            gameInvalido.HomeTeamId = 1;
            gameInvalido.VisitorTeamId = 1; // Times iguais

            // Act & Assert
            gameValido.IsValid().Should().BeTrue();
            gameInvalido.IsValid().Should().BeFalse();
        }

        #endregion

        #region Helper Methods

        private Game CriarJogo(int id = 0, int externalId = 0)
        {
            return new Game
            {
                Id = id,
                ExternalId = externalId == 0 ? new Random().Next(1000, 9999) : externalId,
                Date = DateTime.Today,
                DateTime = DateTime.Now,
                HomeTeamId = 1,
                VisitorTeamId = 2,
                HomeTeamScore = null,
                VisitorTeamScore = null,
                Status = GameStatus.Scheduled,
                Period = 0,
                TimeRemaining = null,
                PostSeason = false,
                Season = DateTime.Now.Year,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        private List<Game> CriarListaDeJogos()
        {
            return new List<Game>
            {
                new Game
                {
                    Id = 1,
                    ExternalId = 100,
                    Date = DateTime.Today,
                    DateTime = DateTime.Today.AddHours(19),
                    HomeTeamId = 1,
                    VisitorTeamId = 2,
                    HomeTeamScore = null,
                    VisitorTeamScore = null,
                    Status = GameStatus.Scheduled,
                    Period = 0,
                    PostSeason = false,
                    Season = DateTime.Now.Year
                },
                new Game
                {
                    Id = 2,
                    ExternalId = 200,
                    Date = DateTime.Today,
                    DateTime = DateTime.Today.AddHours(20),
                    HomeTeamId = 3,
                    VisitorTeamId = 4,
                    HomeTeamScore = 58,
                    VisitorTeamScore = 62,
                    Status = GameStatus.Live,
                    Period = 2,
                    TimeRemaining = "5:32",
                    PostSeason = false,
                    Season = DateTime.Now.Year
                },
                new Game
                {
                    Id = 3,
                    ExternalId = 300,
                    Date = DateTime.Today.AddDays(-1),
                    DateTime = DateTime.Today.AddDays(-1).AddHours(22),
                    HomeTeamId = 5,
                    VisitorTeamId = 6,
                    HomeTeamScore = 110,
                    VisitorTeamScore = 105,
                    Status = GameStatus.Final,
                    Period = 4,
                    TimeRemaining = "0:00",
                    PostSeason = false,
                    Season = DateTime.Now.Year
                }
            };
        }

        #endregion
    }
}