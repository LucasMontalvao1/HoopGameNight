using FluentAssertions;
using HoopGameNight.Core.Interfaces.Infrastructure;
using HoopGameNight.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace HoopGameNight.Tests.Unit.Infrastructure.Data
{
    public class DatabaseInitializerTests : IDisposable
    {
        private readonly Mock<IDatabaseQueryExecutor> _mockQueryExecutor;
        private readonly Mock<ISqlLoader> _mockSqlLoader;
        private readonly Mock<ILogger<DatabaseInitializer>> _mockLogger;
        private readonly DatabaseInitializer _databaseInitializer;

        public DatabaseInitializerTests()
        {
            _mockQueryExecutor = new Mock<IDatabaseQueryExecutor>();
            _mockSqlLoader = new Mock<ISqlLoader>();
            _mockLogger = new Mock<ILogger<DatabaseInitializer>>();

            _databaseInitializer = new DatabaseInitializer(
                _mockQueryExecutor.Object,
                _mockSqlLoader.Object,
                _mockLogger.Object
            );
        }

        #region Testes de Health Check

        [Fact(DisplayName = "Deve retornar true quando banco de dados está saudável")]
        public async Task DeveRetornarTrue_QuandoBancoDadosSaudavel()
        {
            // Arrange
            _mockQueryExecutor
                .Setup(x => x.QuerySingleAsync<int>("SELECT 1", null))
                .ReturnsAsync(1);

            // Act
            var resultado = await _databaseInitializer.HealthCheckAsync();

            // Assert
            resultado.Should().BeTrue("banco de dados respondeu corretamente ao ping");

            _mockQueryExecutor.Verify(
                x => x.QuerySingleAsync<int>("SELECT 1", null),
                Times.Once,
                "deve executar query de verificação uma vez");
        }

        [Fact(DisplayName = "Deve retornar false quando query de health check falha")]
        public async Task DeveRetornarFalse_QuandoQueryFalha()
        {
            // Arrange
            var excecao = new InvalidOperationException("Erro de conexão com banco");
            _mockQueryExecutor
                .Setup(x => x.QuerySingleAsync<int>("SELECT 1", null))
                .ThrowsAsync(excecao);

            // Act
            var resultado = await _databaseInitializer.HealthCheckAsync();

            // Assert
            resultado.Should().BeFalse("falha na query indica banco não saudável");

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Database health check failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once,
                "deve logar erro quando health check falha");
        }

        [Theory(DisplayName = "Deve tratar diferentes tipos de exceções no health check")]
        [InlineData(typeof(TimeoutException), "Timeout na conexão")]
        [InlineData(typeof(InvalidOperationException), "Operação inválida")]
        [InlineData(typeof(Exception), "Erro genérico")]
        public async Task DeveTratarDiferentesExcecoes_NoHealthCheck(Type tipoExcecao, string mensagem)
        {
            // Arrange
            var excecao = (Exception)Activator.CreateInstance(tipoExcecao, mensagem)!;
            _mockQueryExecutor
                .Setup(x => x.QuerySingleAsync<int>("SELECT 1", null))
                .ThrowsAsync(excecao);

            // Act
            var resultado = await _databaseInitializer.HealthCheckAsync();

            // Assert
            resultado.Should().BeFalse($"deve retornar false para {tipoExcecao.Name}");
        }

        #endregion

        #region Testes de Inicialização

        [Fact(DisplayName = "Deve criar todas as tabelas na inicialização")]
        public async Task DeveCriarTodasTabelas_NaInicializacao()
        {
            // Arrange
            ConfigurarMocksParaInicializacaoCompleta();

            // Act
            await _databaseInitializer.InitializeAsync();

            // Assert
            VerificarCriacaoDeTodasTabelas();
        }

        [Fact(DisplayName = "Deve falhar se criação de tabela crítica falhar")]
        public async Task DeveFalhar_SeCriacaoTabelaCriticaFalhar()
        {
            // Arrange
            _mockSqlLoader
                .Setup(x => x.LoadSqlAsync("Teams", "CreateTable"))
                .ReturnsAsync("CREATE TABLE teams...");

            _mockQueryExecutor
                .Setup(x => x.ExecuteAsync(It.IsAny<string>(), null))
                .ThrowsAsync(new Exception("Erro ao criar teams"));

            // Act
            var acao = async () => await _databaseInitializer.InitializeAsync();

            // Assert
            await acao.Should().ThrowAsync<Exception>("a implementação não trata erros na criação de tabelas");
        }

        [Fact(DisplayName = "Deve logar informações durante inicialização")]
        public async Task DeveLogarInformacoes_DuranteInicializacao()
        {
            // Arrange
            ConfigurarMocksParaInicializacaoCompleta();

            // Act
            await _databaseInitializer.InitializeAsync();

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting database initialization")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once,
                "deve logar início da inicialização");

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Database initialization completed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once,
                "deve logar conclusão da inicialização");
        }

        [Fact(DisplayName = "Deve verificar se tabela teams possui dados após criar")]
        public async Task DeveVerificarDadosTeams_AposCriar()
        {
            // Arrange
            ConfigurarMocksParaInicializacaoCompleta();

            _mockQueryExecutor
                .Setup(x => x.QuerySingleAsync<int>("SELECT COUNT(*) FROM teams", null))
                .ReturnsAsync(10);

            // Act
            await _databaseInitializer.InitializeAsync();

            // Assert
            _mockQueryExecutor.Verify(
                x => x.QuerySingleAsync<int>("SELECT COUNT(*) FROM teams", null),
                Times.Once,
                "deve verificar contagem apenas da tabela teams");
        }

        #endregion

        #region Testes de Cenários Específicos

        [Fact(DisplayName = "Deve lançar exceção ao falhar carregar SQL")]
        public async Task DeveLancarExcecao_AoFalharCarregarSQL()
        {
            // Arrange
            _mockSqlLoader
                .Setup(x => x.LoadSqlAsync("Teams", "CreateTable"))
                .ThrowsAsync(new FileNotFoundException("SQL file not found"));

            // Act
            var acao = async () => await _databaseInitializer.InitializeAsync();

            // Assert
            await acao.Should().ThrowAsync<FileNotFoundException>(
                "a implementação não trata erro de carregamento de SQL");
        }

        [Fact(DisplayName = "Deve executar criação de tabelas em sequência")]
        public async Task DeveExecutarCriacaoTabelas_EmSequencia()
        {
            // Arrange
            var ordemExecucao = new List<string>();

            _mockSqlLoader
                .Setup(x => x.LoadSqlAsync(It.IsAny<string>(), "CreateTable"))
                .ReturnsAsync((string entity, string operation) =>
                {
                    ordemExecucao.Add(entity);
                    return $"CREATE TABLE {entity.ToLower()}...";
                });

            _mockQueryExecutor
                .Setup(x => x.ExecuteAsync(It.IsAny<string>(), null))
                .ReturnsAsync(1);

            // Act
            await _databaseInitializer.InitializeAsync();

            // Assert
            ordemExecucao.Should().BeEquivalentTo(new[] { "Teams", "Players", "Games" },
                options => options.WithStrictOrdering(),
                "tabelas devem ser criadas na ordem correta");
        }

        [Fact(DisplayName = "Deve logar debug ao criar cada tabela com sucesso")]
        public async Task DeveLogarDebug_AoCriarCadaTabela()
        {
            // Arrange
            ConfigurarMocksParaInicializacaoCompleta();

            // Act
            await _databaseInitializer.InitializeAsync();

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Table") && v.ToString()!.Contains("created")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Exactly(3),
                "deve logar sucesso para cada tabela criada");
        }

        #endregion

        #region Métodos Auxiliares

        private void ConfigurarMocksParaInicializacaoCompleta()
        {
            _mockSqlLoader
                .Setup(x => x.LoadSqlAsync("Teams", "CreateTable"))
                .ReturnsAsync("CREATE TABLE teams (id INT PRIMARY KEY)");

            _mockSqlLoader
                .Setup(x => x.LoadSqlAsync("Players", "CreateTable"))
                .ReturnsAsync("CREATE TABLE players (id INT PRIMARY KEY)");

            _mockSqlLoader
                .Setup(x => x.LoadSqlAsync("Games", "CreateTable"))
                .ReturnsAsync("CREATE TABLE games (id INT PRIMARY KEY)");

            _mockQueryExecutor
                .Setup(x => x.ExecuteAsync(It.IsAny<string>(), null))
                .ReturnsAsync(1);

            // Mock padrão para COUNT que retorna 0 (sem dados)
            _mockQueryExecutor
                .Setup(x => x.QuerySingleAsync<int>(It.Is<string>(s => s.Contains("COUNT")), null))
                .ReturnsAsync(0);
        }

        private void VerificarCriacaoDeTodasTabelas()
        {
            _mockQueryExecutor.Verify(
                x => x.ExecuteAsync(It.IsAny<string>(), null),
                Times.Exactly(3),
                "deve executar criação de 3 tabelas");

            _mockSqlLoader.Verify(
                x => x.LoadSqlAsync("Teams", "CreateTable"),
                Times.Once,
                "deve carregar SQL para tabela Teams");

            _mockSqlLoader.Verify(
                x => x.LoadSqlAsync("Players", "CreateTable"),
                Times.Once,
                "deve carregar SQL para tabela Players");

            _mockSqlLoader.Verify(
                x => x.LoadSqlAsync("Games", "CreateTable"),
                Times.Once,
                "deve carregar SQL para tabela Games");
        }

        public void Dispose()
        {
            // Limpa recursos se necessário
            _mockQueryExecutor.Reset();
            _mockSqlLoader.Reset();
            _mockLogger.Reset();
        }

        #endregion
    }
}