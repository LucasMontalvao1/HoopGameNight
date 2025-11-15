using FluentAssertions;
using HoopGameNight.Core.Enums;
using HoopGameNight.Core.Interfaces.Infrastructure;
using HoopGameNight.Core.Models.Entities;
using HoopGameNight.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Xunit;

namespace HoopGameNight.Tests.Unit.Infrastructure.Repositories
{
    public class TeamRepositoryTests
    {
        private readonly Mock<IDatabaseConnection> _mockDatabaseConnection;
        private readonly Mock<ISqlLoader> _mockSqlLoader;
        private readonly Mock<ILogger<TeamRepository>> _mockLogger;
        private readonly TeamRepository _repository;

        public TeamRepositoryTests()
        {
            _mockDatabaseConnection = new Mock<IDatabaseConnection>();
            _mockSqlLoader = new Mock<ISqlLoader>();
            _mockLogger = new Mock<ILogger<TeamRepository>>();

            _repository = new TeamRepository(
                _mockDatabaseConnection.Object,
                _mockSqlLoader.Object,
                _mockLogger.Object
            );
        }

        #region Testes Unitários Focados em Lógica

        [Fact(DisplayName = "Repository deve ter nome de entidade correto")]
        public void Repository_DeveTerNomeEntidadeCorreto()
        {
            _repository.Should().NotBeNull();
            _repository.GetType().Name.Should().Be("TeamRepository");
        }

        [Fact(DisplayName = "Repository deve validar dependências no construtor")]
        public void Repository_DeveValidarDependencias()
        {
            // Assert
            Assert.Throws<ArgumentNullException>(() =>
                new TeamRepository(null!, _mockSqlLoader.Object, _mockLogger.Object));

            Assert.Throws<ArgumentNullException>(() =>
                new TeamRepository(_mockDatabaseConnection.Object, null!, _mockLogger.Object));

            Assert.Throws<ArgumentNullException>(() =>
                new TeamRepository(_mockDatabaseConnection.Object, _mockSqlLoader.Object, null!));
        }

        #endregion

        #region Testes de SqlLoader

        [Fact(DisplayName = "Deve carregar SQL corretamente")]
        public async Task DeveCarregarSqlCorretamente()
        {
            // Arrange
            var expectedSql = "SELECT * FROM Teams WHERE Id = @Id";

            _mockSqlLoader
                .Setup(x => x.LoadSqlAsync("Teams", "GetById"))
                .ReturnsAsync(expectedSql);

            // Act
            var sql = await _mockSqlLoader.Object.LoadSqlAsync("Teams", "GetById");

            // Assert
            sql.Should().Be(expectedSql);
            _mockSqlLoader.Verify(x => x.LoadSqlAsync("Teams", "GetById"), Times.Once);
        }

        [Fact(DisplayName = "Deve verificar se SQL existe")]
        public void DeveVerificarSeSqlExiste()
        {
            // Arrange
            _mockSqlLoader
                .Setup(x => x.SqlExists("Teams", "GetAll"))
                .Returns(true);

            _mockSqlLoader
                .Setup(x => x.SqlExists("Teams", "NonExistent"))
                .Returns(false);

            // Act & Assert
            _mockSqlLoader.Object.SqlExists("Teams", "GetAll").Should().BeTrue();
            _mockSqlLoader.Object.SqlExists("Teams", "NonExistent").Should().BeFalse();
        }

        #endregion

        #region Helper Methods

        private Team CriarTime(int id, int externalId = 0, string abbreviation = "LAL")
        {
            return new Team
            {
                Id = id,
                ExternalId = externalId == 0 ? id * 100 : externalId,
                Name = $"Team{id}",
                FullName = $"Full Team Name {id}",
                Abbreviation = abbreviation,
                City = $"City{id}",
                Conference = Conference.West,
                Division = "Pacific",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        private List<Team> CriarListaDeTimes()
        {
            return new List<Team>
            {
                CriarTime(1, 100, "LAL"),
                CriarTime(2, 200, "GSW"),
                CriarTime(3, 300, "BOS")
            };
        }

        #endregion
    }
}