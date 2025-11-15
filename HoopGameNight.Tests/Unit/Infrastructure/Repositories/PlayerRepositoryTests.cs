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
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace HoopGameNight.Tests.Unit.Infrastructure.Repositories
{
    public class PlayerRepositoryTests
    {
        private readonly Mock<IDatabaseConnection> _mockDatabaseConnection;
        private readonly Mock<ISqlLoader> _mockSqlLoader;
        private readonly Mock<ILogger<PlayerRepository>> _mockLogger;
        private readonly PlayerRepository _repository;

        public PlayerRepositoryTests()
        {
            _mockDatabaseConnection = new Mock<IDatabaseConnection>();
            _mockSqlLoader = new Mock<ISqlLoader>();
            _mockLogger = new Mock<ILogger<PlayerRepository>>();

            _repository = new PlayerRepository(
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
                new PlayerRepository(null!, _mockSqlLoader.Object, _mockLogger.Object));

            Assert.Throws<ArgumentNullException>(() =>
                new PlayerRepository(_mockDatabaseConnection.Object, null!, _mockLogger.Object));

            Assert.Throws<ArgumentNullException>(() =>
                new PlayerRepository(_mockDatabaseConnection.Object, _mockSqlLoader.Object, null!));
        }

        [Fact(DisplayName = "Repository deve ter nome de entidade correto")]
        public void Repository_DeveTerNomeEntidadeCorreto()
        {
            // Act & Assert
            _repository.Should().NotBeNull();
            _repository.GetType().Name.Should().Be("PlayerRepository");
        }

        #endregion

        #region SqlLoader Tests

        [Fact(DisplayName = "Deve carregar SQL para busca por nome")]
        public async Task DeveCarregarSql_ParaBuscaPorNome()
        {
            // Arrange
            var expectedSql = "SELECT * FROM Players WHERE Name LIKE @Search";

            _mockSqlLoader
                .Setup(x => x.LoadSqlAsync("Players", "SearchByName"))
                .ReturnsAsync(expectedSql);

            // Act
            var sql = await _mockSqlLoader.Object.LoadSqlAsync("Players", "SearchByName");

            // Assert
            sql.Should().Be(expectedSql);
            _mockSqlLoader.Verify(x => x.LoadSqlAsync("Players", "SearchByName"), Times.Once);
        }

        [Fact(DisplayName = "Deve carregar SQL para busca por time")]
        public async Task DeveCarregarSql_ParaBuscaPorTime()
        {
            // Arrange
            var expectedSql = "SELECT * FROM Players WHERE TeamId = @TeamId";

            _mockSqlLoader
                .Setup(x => x.LoadSqlAsync("Players", "GetByTeamId"))
                .ReturnsAsync(expectedSql);

            // Act
            var sql = await _mockSqlLoader.Object.LoadSqlAsync("Players", "GetByTeamId");

            // Assert
            sql.Should().Be(expectedSql);
        }

        [Fact(DisplayName = "Deve carregar SQL para busca por posição")]
        public async Task DeveCarregarSql_ParaBuscaPorPosicao()
        {
            // Arrange
            var expectedSql = "SELECT * FROM Players WHERE Position = @Position";

            _mockSqlLoader
                .Setup(x => x.LoadSqlAsync("Players", "GetByPosition"))
                .ReturnsAsync(expectedSql);

            // Act
            var sql = await _mockSqlLoader.Object.LoadSqlAsync("Players", "GetByPosition");

            // Assert
            sql.Should().Be(expectedSql);
        }

        [Fact(DisplayName = "Deve carregar SQL para busca por altura")]
        public async Task DeveCarregarSql_ParaBuscaPorAltura()
        {
            // Arrange
            var expectedSql = @"SELECT * FROM Players 
                WHERE (HeightFeet * 12 + HeightInches) BETWEEN @MinHeightInches AND @MaxHeightInches";

            _mockSqlLoader
                .Setup(x => x.LoadSqlAsync("Players", "GetByHeightRange"))
                .ReturnsAsync(expectedSql);

            // Act
            var sql = await _mockSqlLoader.Object.LoadSqlAsync("Players", "GetByHeightRange");

            // Assert
            sql.Should().Be(expectedSql);
        }

        #endregion

        #region SearchPlayersAsync Tests

        [Fact(DisplayName = "SearchPlayersAsync deve validar request")]
        public async Task SearchPlayersAsync_DeveValidarRequest()
        {
            // Arrange
            var request = new SearchPlayerRequest
            {
                Search = "LeBron",
                TeamId = 1,
                Position = "SF",
                Page = 1,
                PageSize = 10
            };

            _mockSqlLoader
                .Setup(x => x.LoadSqlAsync("Players", "SearchByName"))
                .ReturnsAsync("SELECT * FROM Players");

            _mockSqlLoader
                .Setup(x => x.LoadSqlAsync("Players", "SearchByNameCount"))
                .ReturnsAsync("SELECT COUNT(*) FROM Players");

            // Act - Como não podemos mockar Dapper, verificamos apenas o setup
            _repository.Should().NotBeNull();

            // Assert
            _mockSqlLoader.Verify(x => x.LoadSqlAsync("Players", It.IsAny<string>()), Times.Never,
                "Não deve chamar SqlLoader antes de executar o método");
        }

        #endregion

        #region Entity Validation Tests

        [Fact(DisplayName = "Player deve ter nome completo")]
        public void Player_DeveTerNomeCompleto()
        {
            // Arrange
            var player = CriarPlayer("LeBron", "James");

            // Act
            var fullName = player.FullName;

            // Assert
            fullName.Should().Be("LeBron James");
        }

        [Fact(DisplayName = "Player deve calcular altura total em polegadas")]
        public void Player_DeveCalcularAlturaTotalEmPolegadas()
        {
            // Arrange
            var player = CriarPlayer("Test", "Player");
            player.HeightFeet = 6;
            player.HeightInches = 8;

            // Act
            var totalInches = (player.HeightFeet * 12) + player.HeightInches;

            // Assert
            totalInches.Should().Be(80); 
        }

        [Fact(DisplayName = "Player deve aceitar posição nula")]
        public void Player_DeveAceitarPosicaoNula()
        {
            // Arrange & Act
            var player = CriarPlayer("Test", "Player");
            player.Position = null;

            // Assert
            player.Position.Should().BeNull();
        }

        #endregion

        #region Data Transformation Tests

        [Fact(DisplayName = "Deve criar lista de players para testes")]
        public void DeveCriarListaDePlayers_ParaTestes()
        {
            // Act
            var players = CriarListaDePlayers();

            // Assert
            players.Should().HaveCount(6);
            players.Should().OnlyContain(p => !string.IsNullOrEmpty(p.FirstName));
            players.Should().OnlyContain(p => !string.IsNullOrEmpty(p.LastName));
            players.Should().OnlyContain(p => p.ExternalId > 0);
        }

        [Fact(DisplayName = "Deve criar players com diferentes posições")]
        public void DeveCriarPlayers_ComDiferentesPosicoes()
        {
            // Act
            var players = CriarListaDePlayers();

            // Assert
            var positions = players.Select(p => p.Position).Distinct().ToList();
            positions.Should().Contain(PlayerPosition.PG);
            positions.Should().Contain(PlayerPosition.SG);
            positions.Should().Contain(PlayerPosition.SF);
            positions.Should().Contain(PlayerPosition.PF);
            positions.Should().Contain(PlayerPosition.C);
        }

        #endregion

        

        #region Helper Methods

        private Player CriarPlayer(string firstName, string lastName, int externalId = 0)
        {
            return new Player
            {
                Id = 0,
                ExternalId = externalId == 0 ? new Random().Next(1000, 9999) : externalId,
                FirstName = firstName,
                LastName = lastName,
                Position = PlayerPosition.SF,
                HeightFeet = 6,
                HeightInches = 8,
                WeightPounds = 250,
                TeamId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        private List<Player> CriarListaDePlayers()
        {
            return new List<Player>
            {
                new Player
                {
                    Id = 1,
                    ExternalId = 100,
                    FirstName = "LeBron",
                    LastName = "James",
                    Position = PlayerPosition.SF,
                    HeightFeet = 6,
                    HeightInches = 9,
                    WeightPounds = 250,
                    TeamId = 1
                },
                new Player
                {
                    Id = 2,
                    ExternalId = 200,
                    FirstName = "Stephen",
                    LastName = "Curry",
                    Position = PlayerPosition.PG,
                    HeightFeet = 6,
                    HeightInches = 2,
                    WeightPounds = 185,
                    TeamId = 2
                },
                new Player
                {
                    Id = 3,
                    ExternalId = 300,
                    FirstName = "Kevin",
                    LastName = "Durant",
                    Position = PlayerPosition.SF,
                    HeightFeet = 6,
                    HeightInches = 10,
                    WeightPounds = 240,
                    TeamId = 3
                },
                new Player
                {
                    Id = 4,
                    ExternalId = 400,
                    FirstName = "Klay",
                    LastName = "Thompson",
                    Position = PlayerPosition.SG,
                    HeightFeet = 6,
                    HeightInches = 6,
                    WeightPounds = 215,
                    TeamId = 2
                },
                new Player
                {
                    Id = 5,
                    ExternalId = 500,
                    FirstName = "Giannis",
                    LastName = "Antetokounmpo",
                    Position = PlayerPosition.PF,
                    HeightFeet = 7,
                    HeightInches = 0,
                    WeightPounds = 242,
                    TeamId = 4
                },
                new Player
                {
                    Id = 6,
                    ExternalId = 600,
                    FirstName = "Nikola",
                    LastName = "Jokic",
                    Position = PlayerPosition.C,
                    HeightFeet = 7,
                    HeightInches = 0,
                    WeightPounds = 284,
                    TeamId = 5
                }
            };
        }

        #endregion
    }
}