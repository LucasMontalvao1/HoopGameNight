using FluentAssertions;
using HoopGameNight.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace HoopGameNight.Tests.Unit.Infrastructure.Data
{
    public class SqlLoaderTests : IDisposable
    {
        private readonly Mock<ILogger<SqlLoader>> _mockLogger;
        private readonly SqlLoader _sqlLoader;
        private readonly string _diretorioSqlTeste;

        public SqlLoaderTests()
        {
            _mockLogger = new Mock<ILogger<SqlLoader>>();
            _sqlLoader = new SqlLoader(_mockLogger.Object);

            // Cria diretório temporário para testes
            _diretorioSqlTeste = Path.Combine(Path.GetTempPath(), "TestSql", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_diretorioSqlTeste);
        }

        #region Testes de LoadSql

        [Fact(DisplayName = "Deve tratar erro ao ler arquivo")]
        public void DeveTratarErro_AoLerArquivo()
        {
            // Arrange
            const string categoria = "Teams";
            const string nomeArquivo = "GetAll";
            var caminhoArquivo = Path.Combine(_diretorioSqlTeste, categoria, $"{nomeArquivo}.sql");

            Directory.CreateDirectory(Path.GetDirectoryName(caminhoArquivo)!);
            File.Create(caminhoArquivo).Dispose(); // Cria arquivo vazio
            File.SetAttributes(caminhoArquivo, FileAttributes.ReadOnly);

            // Simula erro de leitura tornando o arquivo inacessível
            try
            {
                using (var stream = File.Open(caminhoArquivo, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    // Act - tenta ler enquanto arquivo está bloqueado
                    var resultado = _sqlLoader.LoadSql(categoria, nomeArquivo);

                    // Assert
                    resultado.Should().NotBeNullOrEmpty("deve retornar fallback em caso de erro");
                }
            }
            finally
            {
                File.SetAttributes(caminhoArquivo, FileAttributes.Normal);
            }
        }

        #endregion

        #region Testes de LoadSqlAsync

        [Fact(DisplayName = "Deve retornar fallback assincronamente quando arquivo não existe")]
        public async Task DeveRetornarFallbackAsync_QuandoArquivoNaoExiste()
        {
            // Arrange
            const string categoria = "Players";
            const string nomeArquivo = "GetByPosition";

            // Act
            var resultado = await _sqlLoader.LoadSqlAsync(categoria, nomeArquivo);

            // Assert
            resultado.Should().NotBeNullOrEmpty();
            resultado.ToLower().Should().Contain("players");
        }

        #endregion

        #region Testes de LoadAllSqlInCategoryAsync

        [Fact(DisplayName = "Deve retornar dicionário vazio quando categoria não existe")]
        public async Task DeveRetornarDicionarioVazio_QuandoCategoriaNaoExiste()
        {
            // Arrange
            const string categoriaNaoExistente = "CategoriaInexistente";

            // Act
            var resultado = await _sqlLoader.LoadAllSqlInCategoryAsync(categoriaNaoExistente);

            // Assert
            resultado.Should().NotBeNull();
            resultado.Should().BeEmpty("categoria não existe");
        }

        #endregion

        #region Testes de SqlExists

        [Fact(DisplayName = "Deve retornar true quando arquivo SQL existe")]
        public void DeveRetornarTrue_QuandoArquivoSqlExiste()
        {
            // Arrange
            const string categoria = "Teams";
            const string nomeArquivo = "GetAll";
            CriarArquivoSqlTeste(categoria, nomeArquivo, "SELECT * FROM teams;");

            // Act
            var resultado = _sqlLoader.SqlExists(categoria, nomeArquivo);

            // Assert
            resultado.Should().BeTrue();
        }

        [Fact(DisplayName = "Deve retornar false quando arquivo SQL não existe")]
        public void DeveRetornarFalse_QuandoArquivoSqlNaoExiste()
        {
            // Arrange
            const string categoria = "Teams";
            const string nomeArquivo = "ArquivoInexistente";

            // Act
            var resultado = _sqlLoader.SqlExists(categoria, nomeArquivo);

            // Assert
            resultado.Should().BeFalse();
        }

        [Theory(DisplayName = "Deve validar existência para diferentes categorias e arquivos")]
        [InlineData("Teams", "GetAll", true)]
        [InlineData("Players", "GetById", true)]
        [InlineData("Games", "NonExistent", false)]
        [InlineData("NonExistentCategory", "GetAll", false)]
        public void DeveValidarExistencia_ParaDiferentesArquivos(
            string categoria, string nomeArquivo, bool deveExistir)
        {
            // Arrange
            if (deveExistir)
            {
                CriarArquivoSqlTeste(categoria, nomeArquivo, "-- SQL content");
            }

            // Act
            var resultado = _sqlLoader.SqlExists(categoria, nomeArquivo);

            // Assert
            resultado.Should().Be(deveExistir);
        }

        #endregion

        #region Testes de Cache

        
        [Fact(DisplayName = "ClearCache deve remover todos itens do cache")]
        public void DeveLimparTodoCache_QuandoClearCacheChamado()
        {
            // Arrange
            CriarArquivoSqlTeste("Teams", "GetAll", "SELECT * FROM teams;");
            CriarArquivoSqlTeste("Players", "GetAll", "SELECT * FROM players;");
            CriarArquivoSqlTeste("Games", "GetAll", "SELECT * FROM games;");

            // Carrega no cache
            _sqlLoader.LoadSql("Teams", "GetAll");
            _sqlLoader.LoadSql("Players", "GetAll");
            _sqlLoader.LoadSql("Games", "GetAll");

            // Act
            _sqlLoader.ClearCache();

            // Assert
            // Após limpar cache, deve recarregar do arquivo
            var resultado = _sqlLoader.LoadSql("Teams", "GetAll");
            resultado.Should().NotBeNullOrEmpty();

            // Deve logar novo carregamento após cache limpo
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) =>
                        v.ToString()!.Contains("loaded successfully")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Exactly(4),
                "deve recarregar após limpar cache");
        }

        [Fact(DisplayName = "ClearCache com categoria deve remover apenas itens da categoria")]
        public void DeveLimparApenasCategoria_QuandoClearCacheComCategoria()
        {
            // Arrange
            CriarArquivoSqlTeste("Teams", "GetAll", "SELECT * FROM teams;");
            CriarArquivoSqlTeste("Players", "GetAll", "SELECT * FROM players;");

            // Carrega ambos no cache
            _sqlLoader.LoadSql("Teams", "GetAll");
            _sqlLoader.LoadSql("Players", "GetAll");
            _mockLogger.Reset(); 

            // Act
            _sqlLoader.ClearCache("Teams");

            // Recarrega ambos
            var teamsResult = _sqlLoader.LoadSql("Teams", "GetAll");
            var playersResult = _sqlLoader.LoadSql("Players", "GetAll");

            // Assert
            teamsResult.Should().NotBeNullOrEmpty();
            playersResult.Should().NotBeNullOrEmpty();

            // Deve recarregar apenas Teams (Players ainda estava no cache)
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) =>
                        v.ToString()!.Contains("loaded successfully") && v.ToString()!.Contains("Teams")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once,
                "deve recarregar apenas arquivos da categoria Teams");
        }

        #endregion

        #region Testes de SQL Fallback

        [Fact(DisplayName = "Deve gerar fallback consistente para mesma operação")]
        public void DeveGerarFallbackConsistente_ParaMesmaOperacao()
        {
            // Arrange
            const string categoria = "Teams";
            const string nomeArquivo = "GetAll";

            // Act
            var resultado1 = _sqlLoader.LoadSql(categoria, nomeArquivo);
            var resultado2 = _sqlLoader.LoadSql(categoria, nomeArquivo);

            // Assert
            resultado1.Should().Be(resultado2, "fallback deve ser consistente");
        }

        #endregion

        #region Testes de Performance e Concorrência

        [Fact(DisplayName = "Deve carregar múltiplos arquivos rapidamente")]
        public async Task DeveCarregarMultiplosArquivos_Rapidamente()
        {
            // Arrange
            const int numeroArquivos = 50;
            const string categoria = "Performance";

            for (int i = 0; i < numeroArquivos; i++)
            {
                CriarArquivoSqlTeste(categoria, $"Query{i}", $"SELECT * FROM table{i};");
            }

            // Act
            var inicio = DateTime.UtcNow;
            var tarefas = Enumerable.Range(0, numeroArquivos)
                .Select(i => Task.Run(() => _sqlLoader.LoadSql(categoria, $"Query{i}")))
                .ToArray();

            await Task.WhenAll(tarefas);
            var duracao = DateTime.UtcNow - inicio;

            // Assert
            duracao.Should().BeLessThan(TimeSpan.FromSeconds(5),
                $"deve carregar {numeroArquivos} arquivos em menos de 5 segundos");
            tarefas.All(t => !string.IsNullOrEmpty(t.Result)).Should().BeTrue();
        }

        #endregion

        #region Métodos Auxiliares

        private void CriarArquivoSqlTeste(string categoria, string nomeArquivo, string conteudo)
        {
            var caminhoCategoria = Path.Combine(_diretorioSqlTeste, categoria);
            Directory.CreateDirectory(caminhoCategoria);

            var caminhoArquivo = Path.Combine(caminhoCategoria, $"{nomeArquivo}.sql");
            File.WriteAllText(caminhoArquivo, conteudo);
        }

        public void Dispose()
        {
            // Limpa diretório de teste
            if (Directory.Exists(_diretorioSqlTeste))
            {
                try
                {
                    Directory.Delete(_diretorioSqlTeste, true);
                }
                catch
                {
                    // Ignora erros na limpeza
                }
            }
        }

        #endregion
    }
}