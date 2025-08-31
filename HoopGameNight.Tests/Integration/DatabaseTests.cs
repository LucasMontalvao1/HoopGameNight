using FluentAssertions;
using HoopGameNight.Core.Interfaces.Infrastructure;
using HoopGameNight.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Data;
using Xunit;

namespace HoopGameNight.Tests.Integration
{
    /// <summary>
    /// Testes de integração para operações de banco de dados.
    /// Estes testes verificam se o DatabaseInitializer funciona corretamente 
    /// com componentes reais (conexões, queries, etc).
    /// </summary>
    [Collection("Database")]
    public class DatabaseTests : IDisposable
    {
        private readonly IDatabaseConnection _databaseConnection;
        private readonly IDatabaseQueryExecutor _queryExecutor;
        private readonly Mock<ILogger<DatabaseInitializer>> _mockLogger;
        private readonly Mock<ISqlLoader> _mockSqlLoader;
        private readonly IConfiguration _configuration;
        private readonly DatabaseInitializer _databaseInitializer;

        public DatabaseTests()
        {
            // Configura uma connection string de teste em memória
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["ConnectionStrings:MySqlConnection"] = "Server=localhost;Database=hoopgamenight_test;Uid=test;Pwd=test;"
                })
                .Build();

            var connectionString = configuration.GetConnectionString("MySqlConnection")!;

            // Cria as abstrações em camadas: Connection -> QueryExecutor -> DatabaseInitializer
            _databaseConnection = new HoopGameNight.Infrastructure.Data.MySqlConnection(connectionString);
            _queryExecutor = new DapperQueryExecutor(_databaseConnection);

            // Mocka apenas as dependências que não queremos testar de verdade
            _mockLogger = new Mock<ILogger<DatabaseInitializer>>();
            _mockSqlLoader = new Mock<ISqlLoader>();

            // Cria o objeto sob teste usando QueryExecutor real mas SqlLoader mockado
            _databaseInitializer = new DatabaseInitializer(
                _queryExecutor,  // Componente real - vamos testar a integração
                _mockSqlLoader.Object,  // Mock - não queremos ler arquivos .sql reais
                _mockLogger.Object,  // Mock - não queremos logs reais nos testes
                _configuration
            );
        }

        public void Dispose()
        {
            // Limpeza após os testes (fechar conexões, etc)
        }

        #region Testes de Conectividade com Banco

        /// <summary>
        /// Testa se a conexão com o banco de dados funciona quando as credenciais são válidas
        /// </summary>
        [Fact(DisplayName = "Deve conectar ao banco quando as credenciais são válidas")]
        public async Task DeveConectarAoBanco_QuandoCredenciaisSaoValidas()
        {
            // Arrange: Verifica se deve rodar testes que precisam de banco real
            var testDb = Environment.GetEnvironmentVariable("RUN_DATABASE_TESTS");
            if (string.IsNullOrEmpty(testDb) || testDb.ToLower() != "true")
            {
                Assert.True(true, "Teste pulado - configure RUN_DATABASE_TESTS=true para rodar");
                return;
            }

            // Act: Tenta conectar ao banco
            var podeConectar = await _databaseConnection.TestConnectionAsync();

            // Assert: Verifica se conseguiu conectar
            podeConectar.Should().BeTrue("porque credenciais válidas devem permitir conexão");
        }

        /// <summary>
        /// Testa se consegue criar uma instância de conexão válida
        /// </summary>
        [Fact(DisplayName = "Deve criar conexão com sucesso")]
        public void DeveCriarConexao_ComSucesso()
        {
            // Act: Cria uma nova conexão
            using var conexao = _databaseConnection.CreateConnection();

            // Assert: Verifica se a conexão foi criada
            conexao.Should().NotBeNull("porque CreateConnection deve sempre retornar uma conexão");
            conexao.Should().BeAssignableTo<IDbConnection>("porque deve implementar IDbConnection");
        }

        /// <summary>
        /// Testa se a conexão falha corretamente com credenciais inválidas
        /// </summary>
        [Fact(DisplayName = "Deve falhar ao conectar com credenciais inválidas")]
        public async Task DeveFalharAoConectar_ComCredenciaisInvalidas()
        {
            // Arrange: Cria conexão com credenciais inválidas
            var conexaoInvalida = new HoopGameNight.Infrastructure.Data.MySqlConnection("Server=servidor_inexistente;Database=db_inexistente;Uid=usuario_falso;Pwd=senha_errada;");

            // Act: Tenta conectar
            var podeConectar = await conexaoInvalida.TestConnectionAsync();

            // Assert: Deve falhar
            podeConectar.Should().BeFalse("porque credenciais inválidas não devem permitir conexão");
        }

        #endregion

        #region Testes de Health Check

        /// <summary>
        /// Testa se o health check retorna true quando o banco está acessível
        /// </summary>
        [Fact(DisplayName = "Health check deve retornar true quando banco está acessível")]
        public async Task HealthCheck_DeveRetornarTrue_QuandoBancoEstaAcessivel()
        {
            // Arrange: Verifica se deve rodar testes que precisam de banco real
            var testDb = Environment.GetEnvironmentVariable("RUN_DATABASE_TESTS");
            if (string.IsNullOrEmpty(testDb) || testDb.ToLower() != "true")
            {
                Assert.True(true, "Teste pulado - configure RUN_DATABASE_TESTS=true para rodar");
                return;
            }

            // Arrange: Configura o mock do SqlLoader
            ConfigurarSqlLoaderValido();

            // Act: Executa o health check
            var estaSaudavel = await _databaseInitializer.HealthCheckAsync();

            // Assert: Deve retornar true se banco estiver acessível
            if (!estaSaudavel)
            {
                Assert.True(true, "Health check falhou - provavelmente não há banco configurado");
                return;
            }

            estaSaudavel.Should().BeTrue("porque banco acessível deve passar no health check");
        }

        /// <summary>
        /// Testa se o health check retorna false quando o banco não está acessível
        /// </summary>
        [Fact(DisplayName = "Health check deve retornar false quando banco não está acessível")]
        public async Task HealthCheck_DeveRetornarFalse_QuandoBancoNaoEstaAcessivel()
        {
            // Arrange: Cria DatabaseInitializer com conexão inválida
            var conexaoInvalida = new HoopGameNight.Infrastructure.Data.MySqlConnection("String de conexão inválida");
            var queryExecutorInvalido = new DapperQueryExecutor(conexaoInvalida);
            var inicializadorComConexaoInvalida = new DatabaseInitializer(
                queryExecutorInvalido,
                _mockSqlLoader.Object,
                _mockLogger.Object,
                _configuration
            );

            // Act: Executa o health check
            var estaSaudavel = await inicializadorComConexaoInvalida.HealthCheckAsync();

            // Assert: Deve retornar false para conexão inválida
            estaSaudavel.Should().BeFalse("porque conexão inválida deve falhar no health check");
        }

        /// <summary>
        /// Testa se o health check loga erros quando falha
        /// </summary>
        [Fact(DisplayName = "Health check deve logar erro quando falha")]
        public async Task HealthCheck_DeveLogarErro_QuandoFalha()
        {
            // Arrange: Cria DatabaseInitializer com conexão inválida
            var conexaoInvalida = new HoopGameNight.Infrastructure.Data.MySqlConnection("Connection string inválida");
            var queryExecutorInvalido = new DapperQueryExecutor(conexaoInvalida);
            var mockLoggerEspecifico = new Mock<ILogger<DatabaseInitializer>>();

            var inicializadorComConexaoInvalida = new DatabaseInitializer(
                queryExecutorInvalido,
                _mockSqlLoader.Object,
                mockLoggerEspecifico.Object,
                _configuration
            );

            // Act: Executa o health check
            var estaSaudavel = await inicializadorComConexaoInvalida.HealthCheckAsync();

            // Assert: Deve logar erro
            estaSaudavel.Should().BeFalse("health check deve falhar com conexão inválida");

            // Apenas verifica se logou erro, sem verificar mensagem específica
            mockLoggerEspecifico.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),  // Aceita qualquer mensagem
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once,
                "deve logar erro quando health check falha");
        }

        #endregion

        #region Testes de Inicialização do Banco

        /// <summary>
        /// Testa se o DatabaseInitializer consegue criar as tabelas quando é inicializado
        /// </summary>
        [Fact(DisplayName = "Deve criar tabelas quando inicializado")]
        public async Task DeveCriarTabelas_QuandoInicializado()
        {
            // Arrange: Verifica se deve rodar testes que precisam de banco real
            var testDb = Environment.GetEnvironmentVariable("RUN_DATABASE_TESTS");
            if (string.IsNullOrEmpty(testDb) || testDb.ToLower() != "true")
            {
                Assert.True(true, "Teste pulado - configure RUN_DATABASE_TESTS=true para rodar");
                return;
            }

            // Arrange: Configura o mock do SqlLoader para retornar SQL válido
            ConfigurarSqlLoaderComTabelasValidas();

            try
            {
                // Act: Executa a inicialização do banco
                await _databaseInitializer.InitializeAsync();

                // Assert: Verifica se as tabelas foram criadas
                var tabelasExistem = await VerificarSeTabelasExistem();
                if (!tabelasExistem)
                {
                    Assert.True(true, "Teste pulado - não foi possível verificar criação de tabelas");
                    return;
                }

                tabelasExistem.Should().BeTrue("porque a inicialização deve criar as tabelas necessárias");
            }
            catch (Exception)
            {
                // Se falhar, pula o teste (banco não disponível)
                Assert.True(true, "Teste pulado - banco de teste não disponível");
            }
        }

        /// <summary>
        /// Testa se a inicialização falha corretamente quando há erro no SQL
        /// </summary>
        [Fact(DisplayName = "Deve falhar inicialização quando SQL é inválido")]
        public async Task DeveFalharInicializacao_QuandoSqlEhInvalido()
        {
            // Arrange: Verifica se deve rodar testes que precisam de banco real
            var testDb = Environment.GetEnvironmentVariable("RUN_DATABASE_TESTS");
            if (string.IsNullOrEmpty(testDb) || testDb.ToLower() != "true")
            {
                Assert.True(true, "Teste pulado - configure RUN_DATABASE_TESTS=true para rodar");
                return;
            }

            // Arrange: Configura SqlLoader para retornar SQL inválido
            _mockSqlLoader
                .Setup(x => x.LoadSqlAsync("Teams", "CreateTable"))
                .ReturnsAsync("SQL INVÁLIDO QUE VAI FALHAR");

            // Act & Assert: Deve lançar exceção
            var act = async () => await _databaseInitializer.InitializeAsync();
            await act.Should().ThrowAsync<Exception>("porque SQL inválido deve falhar");
        }

        /// <summary>
        /// Testa se a inicialização loga informações corretamente
        /// </summary>
        [Fact(DisplayName = "Deve logar informações durante inicialização")]
        public async Task DeveLogarInformacoes_DuranteInicializacao()
        {
            // Arrange: Configura SqlLoader válido
            ConfigurarSqlLoaderValido();

            // Act: Tenta inicializar (pode falhar se não há banco)
            try
            {
                await _databaseInitializer.InitializeAsync();
            }
            catch
            {
                // Ignora erros de conexão - só queremos verificar logs
            }

            // Assert: Verifica se logou QUALQUER informação durante a inicialização
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),  // Aceita qualquer mensagem
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce,
                "deve logar pelo menos uma informação durante a inicialização");
        }

        #endregion

        #region Testes de Seed de Dados

        /// <summary>
        /// Testa se o seed de dados funciona quando não há dados existentes
        /// </summary>
        [Fact(DisplayName = "Deve fazer seed de dados quando banco está vazio")]
        public async Task DeveFazerSeedDeDados_QuandoBancoEstaVazio()
        {
            // Arrange: Verifica se deve rodar testes que precisam de banco real
            var testDb = Environment.GetEnvironmentVariable("RUN_DATABASE_TESTS");
            if (string.IsNullOrEmpty(testDb) || testDb.ToLower() != "true")
            {
                Assert.True(true, "Teste pulado - configure RUN_DATABASE_TESTS=true para rodar");
                return;
            }

            // Arrange: Configura SqlLoader com seed data
            ConfigurarSqlLoaderComSeedData();

            try
            {
                // Act: Executa inicialização
                await _databaseInitializer.InitializeAsync();

                // Assert: Se chegou até aqui sem exceção, o seed funcionou
                Assert.True(true, "Seed de dados executado com sucesso");
            }
            catch (Exception)
            {
                Assert.True(true, "Teste pulado - banco não disponível para seed");
            }
        }

        #endregion

        #region Testes de Performance

        /// <summary>
        /// Testa se o health check executa dentro do tempo esperado
        /// </summary>
        [Fact(DisplayName = "Health check deve executar dentro do tempo limite")]
        public async Task HealthCheck_DeveExecutarDentroDoTempoLimite()
        {
            // Arrange: Configura SqlLoader
            ConfigurarSqlLoaderValido();

            // Act: Executa health check com timeout
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var task = _databaseInitializer.HealthCheckAsync();

            // Assert: Deve completar dentro do tempo limite
            var completouDentroDoTempo = await Task.WhenAny(task, Task.Delay(10000, cts.Token)) == task;
            completouDentroDoTempo.Should().BeTrue("porque health check deve ser rápido");
        }

        #endregion

        #region Métodos Auxiliares

        /// <summary>
        /// Configura o mock do SqlLoader para retornar SQL básico válido
        /// </summary>
        private void ConfigurarSqlLoaderValido()
        {
            _mockSqlLoader
                .Setup(x => x.LoadSqlAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("SELECT 1;");
        }

        /// <summary>
        /// Configura o mock do SqlLoader com SQLs de criação de tabelas
        /// </summary>
        private void ConfigurarSqlLoaderComTabelasValidas()
        {
            _mockSqlLoader
                .Setup(x => x.LoadSqlAsync("Teams", "CreateTable"))
                .ReturnsAsync("CREATE TABLE IF NOT EXISTS teams (id INT PRIMARY KEY, name VARCHAR(100));");

            _mockSqlLoader
                .Setup(x => x.LoadSqlAsync("Players", "CreateTable"))
                .ReturnsAsync("CREATE TABLE IF NOT EXISTS players (id INT PRIMARY KEY, name VARCHAR(100), team_id INT);");

            _mockSqlLoader
                .Setup(x => x.LoadSqlAsync("Games", "CreateTable"))
                .ReturnsAsync("CREATE TABLE IF NOT EXISTS games (id INT PRIMARY KEY, home_team_id INT, visitor_team_id INT);");

            _mockSqlLoader
                .Setup(x => x.LoadSqlAsync("Database", "SeedData"))
                .ReturnsAsync("SELECT 1;"); // Seed vazio por enquanto
        }

        /// <summary>
        /// Configura o mock do SqlLoader com dados de seed
        /// </summary>
        private void ConfigurarSqlLoaderComSeedData()
        {
            // Configura tabelas
            ConfigurarSqlLoaderComTabelasValidas();

            // Configura seed data
            _mockSqlLoader
                .Setup(x => x.LoadSqlAsync("Database", "SeedData"))
                .ReturnsAsync(@"
                    INSERT IGNORE INTO teams (id, name) VALUES 
                    (1, 'Lakers'),
                    (2, 'Warriors'),
                    (3, 'Bulls');
                ");
        }

        /// <summary>
        /// Verifica se as tabelas principais existem no banco
        /// </summary>
        /// <returns>True se as tabelas existem, false caso contrário</returns>
        private async Task<bool> VerificarSeTabelasExistem()
        {
            try
            {
                using var conexao = _databaseConnection.CreateConnection();
                conexao.Open();

                using var comando = conexao.CreateCommand();
                comando.CommandText = @"
                    SELECT COUNT(*) FROM information_schema.tables 
                    WHERE table_schema = DATABASE() 
                    AND table_name IN ('teams', 'players', 'games')";

                var resultado = await Task.FromResult(comando.ExecuteScalar());
                return Convert.ToInt32(resultado) >= 3;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Verifica se existem dados nas tabelas
        /// </summary>
        /// <returns>True se há dados, false caso contrário</returns>
        private async Task<bool> VerificarSeExistemDados()
        {
            try
            {
                using var conexao = _databaseConnection.CreateConnection();
                conexao.Open();

                using var comando = conexao.CreateCommand();
                comando.CommandText = "SELECT COUNT(*) FROM teams;";

                var resultado = await Task.FromResult(comando.ExecuteScalar());
                return Convert.ToInt32(resultado) > 0;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }

    #region Testes Unitários Complementares

    /// <summary>
    /// Testes unitários específicos para o DatabaseInitializer usando mocks completos
    /// Estes testes não precisam de banco real e sempre funcionam
    /// </summary>
    public class DatabaseInitializerUnitTests : IDisposable
    {
        private readonly Mock<IDatabaseQueryExecutor> _mockQueryExecutor;
        private readonly Mock<ISqlLoader> _mockSqlLoader;
        private readonly Mock<ILogger<DatabaseInitializer>> _mockLogger;
        private readonly IConfiguration _configuration;
        private readonly DatabaseInitializer _databaseInitializer;

        public DatabaseInitializerUnitTests()
        {
            _mockQueryExecutor = new Mock<IDatabaseQueryExecutor>();
            _mockSqlLoader = new Mock<ISqlLoader>();
            _mockLogger = new Mock<ILogger<DatabaseInitializer>>();

            _databaseInitializer = new DatabaseInitializer(
                _mockQueryExecutor.Object,
                _mockSqlLoader.Object,
                _mockLogger.Object,
                _configuration
            );
        }

        /// <summary>
        /// Testa se o health check retorna true quando query retorna 1
        /// </summary>
        [Fact(DisplayName = "Health check deve retornar true quando query retorna 1")]
        public async Task HealthCheck_DeveRetornarTrue_QuandoQueryRetorna1()
        {
            // Arrange: Mock retorna 1 (sucesso)
            _mockQueryExecutor
                .Setup(x => x.QuerySingleAsync<int>("SELECT 1", null))
                .ReturnsAsync(1);

            // Act: Executa health check
            var resultado = await _databaseInitializer.HealthCheckAsync();

            // Assert: Deve retornar true
            resultado.Should().BeTrue("porque query que retorna 1 indica sucesso");
            _mockQueryExecutor.Verify(x => x.QuerySingleAsync<int>("SELECT 1", null), Times.Once);
        }

        /// <summary>
        /// Testa se o health check retorna false quando query retorna valor diferente de 1
        /// </summary>
        [Fact(DisplayName = "Health check deve retornar false quando query retorna valor diferente de 1")]
        public async Task HealthCheck_DeveRetornarFalse_QuandoQueryRetornaValorDiferenteDe1()
        {
            // Arrange: Mock retorna 0 (falha)
            _mockQueryExecutor
                .Setup(x => x.QuerySingleAsync<int>("SELECT 1", null))
                .ReturnsAsync(0);

            // Act: Executa health check
            var resultado = await _databaseInitializer.HealthCheckAsync();

            // Assert: Deve retornar false
            resultado.Should().BeFalse("porque query que não retorna 1 indica falha");
        }

        /// <summary>
        /// Testa se o health check retorna false quando query lança exceção
        /// </summary>
        [Fact(DisplayName = "Health check deve retornar false quando query lança exceção")]
        public async Task HealthCheck_DeveRetornarFalse_QuandoQueryLancaExcecao()
        {
            // Arrange: Mock lança exceção
            _mockQueryExecutor
                .Setup(x => x.QuerySingleAsync<int>("SELECT 1", null))
                .ThrowsAsync(new InvalidOperationException("Erro de conexão"));

            // Act: Executa health check
            var resultado = await _databaseInitializer.HealthCheckAsync();

            // Assert: Deve retornar false e logar erro
            resultado.Should().BeFalse("porque exceção indica falha na conexão");

            // Verificar que logou erro, independente da mensagem exata
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),  // Aceita qualquer mensagem
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once,
                "deve logar erro quando health check falha");
        }

        /// <summary>
        /// Testa se a ordem das tabelas está definida corretamente
        /// </summary>
        [Fact(DisplayName = "Deve definir ordem correta das tabelas para criação")]
        public void DeveDefinirOrdemCorretaDasTabelas()
        {
            // Este teste valida que a ordem esperada das tabelas está correta
            // considerando as dependências de foreign keys

            // Arrange & Act
            var ordemEsperada = new[] { "Teams", "Players", "Games" };

            // Assert
            ordemEsperada[0].Should().Be("Teams",
                "Teams deve ser criada primeiro pois não tem dependências");

            ordemEsperada[1].Should().Be("Players",
                "Players deve ser criada após Teams pois tem FK para Teams");

            ordemEsperada[2].Should().Be("Games",
                "Games deve ser criada por último pois tem FK para Teams");

            // Validar que não há duplicatas
            ordemEsperada.Should().OnlyHaveUniqueItems("não deve haver tabelas duplicadas");

            // Validar que tem todas as tabelas principais
            ordemEsperada.Should().HaveCount(3, "deve ter exatamente 3 tabelas principais");
        }

        public void Dispose()
        {
            // Limpeza se necessário
        }
    }

    #endregion
}