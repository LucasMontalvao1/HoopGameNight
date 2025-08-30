using HoopGameNight.Core.Interfaces.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Infrastructure.Data
{
    public class DatabaseInitializer
    {
        private readonly IDatabaseQueryExecutor _queryExecutor;
        private readonly ISqlLoader _sqlLoader;
        private readonly ILogger<DatabaseInitializer> _logger;
        private readonly IConfiguration _configuration;

        public DatabaseInitializer(
            IDatabaseQueryExecutor queryExecutor,
            ISqlLoader sqlLoader,
            ILogger<DatabaseInitializer> logger,
            IConfiguration configuration)
        {
            _queryExecutor = queryExecutor;
            _sqlLoader = sqlLoader;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Iniciando a inicialização do banco de dados...");

                // criar o schema
                await EnsureSchemaExistsAsync();

                // criar tabelas
                await CreateTablesAsync();

                // criar triggers
                await CreateTriggersAsync();

                // criar views
                await CreateViewsAsync();

                // verificar se precisa enviar dados iniciais
                await SeedInitialDataAsync();

                _logger.LogInformation("Inicialização do banco de dados concluída com sucesso");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Inicialização do banco de dados falhou");
                throw;
            }
        }

        private async Task EnsureSchemaExistsAsync()
        {
            _logger.LogInformation("Verificar se existe o esquema do banco de dados...");

            var connectionString = _configuration.GetConnectionString("MySqlConnection");
            var builder = new MySqlConnector.MySqlConnectionStringBuilder(connectionString);

            // Extrair o nome do database da connection string
            var schemaName = builder.Database;
            if (string.IsNullOrEmpty(schemaName))
            {
                schemaName = "hoop_game_night"; // Nome padrão se não especificado
            }

            _logger.LogInformation("Nome do esquema: {SchemaName}", schemaName);

            // Criar uma conexão SEM especificar o database para poder criar o schema
            builder.Database = null;

            try
            {
                using var connection = new MySqlConnector.MySqlConnection(builder.ConnectionString);
                await connection.OpenAsync();

                // Verificar se o schema já existe
                using var checkCommand = connection.CreateCommand();
                checkCommand.CommandText = @"
                    SELECT COUNT(*) 
                    FROM INFORMATION_SCHEMA.SCHEMATA 
                    WHERE SCHEMA_NAME = @schemaName";
                checkCommand.Parameters.AddWithValue("@schemaName", schemaName);

                var exists = Convert.ToInt32(await checkCommand.ExecuteScalarAsync()) > 0;

                if (!exists)
                {
                    _logger.LogInformation("Schema '{SchemaName}' não existe. Criando...", schemaName);

                    // Criar o schema
                    using var createCommand = connection.CreateCommand();
                    createCommand.CommandText = $@"
                        CREATE SCHEMA IF NOT EXISTS `{schemaName}` 
                        DEFAULT CHARACTER SET utf8mb4 
                        COLLATE utf8mb4_unicode_ci";
                    await createCommand.ExecuteNonQueryAsync();

                    _logger.LogInformation("Schema '{SchemaName}' criado com sucesso", schemaName);
                }
                else
                {
                    _logger.LogInformation("Schema '{SchemaName}' já existe", schemaName);
                }

                // Agora usar o schema
                using var useCommand = connection.CreateCommand();
                useCommand.CommandText = $"USE `{schemaName}`";
                await useCommand.ExecuteNonQueryAsync();

                // Tentar executar o script InitDatabase se existir 
                try
                {
                    var initScript = await _sqlLoader.LoadSqlAsync("Database", "InitDatabase");
                    if (!string.IsNullOrEmpty(initScript))
                    {
                        using var scriptCommand = connection.CreateCommand();
                        scriptCommand.CommandText = initScript;
                        await scriptCommand.ExecuteNonQueryAsync();
                        _logger.LogDebug("InitDatabase script executado com sucesso");
                    }
                }
                catch (Exception scriptEx)
                {
                    _logger.LogDebug(scriptEx, "O script InitDatabase não foi encontrado ou falhou");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao garantir que o esquema exista");
                throw;
            }
        }

        private async Task CreateTablesAsync()
        {
            _logger.LogInformation("Criando tabelas de banco de dados...");

            var tableOrder = new[] { "Teams", "Players", "Games", "Statistics" };

            foreach (var table in tableOrder)
            {
                try
                {
                    var createTableSql = await _sqlLoader.LoadSqlAsync(table, "CreateTable");
                    if (string.IsNullOrEmpty(createTableSql))
                    {
                        _logger.LogWarning("Script CreateTable não encontrado para {Table}", table);
                        continue;
                    }

                    await _queryExecutor.ExecuteAsync(createTableSql);
                    _logger.LogInformation("Tabela {Table} criado/verificado com sucesso", table);
                }
                catch (MySqlConnector.MySqlException ex) when (ex.Number == 1050) 
                {
                    _logger.LogDebug("A tabela {Table} já existe", table);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "\r\nFalha ao criar a tabela {Table}", table);
                    throw;
                }
            }
        }

        private async Task CreateTriggersAsync()
        {
            _logger.LogInformation("Criando triggers do banco de dados...");

            var triggers = new[]
            {
                ("Statistics/Triggers", "trg_player_game_stats_after_insert"),
                ("Statistics/Triggers", "trg_player_season_stats_before_update")
            };

            foreach (var (folder, triggerName) in triggers)
            {
                try
                {
                    await DropTriggerIfExistsAsync(triggerName);

                    var triggerSql = await _sqlLoader.LoadSqlAsync(folder, triggerName);
                    if (string.IsNullOrEmpty(triggerSql))
                    {
                        _logger.LogWarning("Script de trigger não encontrado: {Folder}/{TriggerName}", folder, triggerName);
                        continue;
                    }

                    await _queryExecutor.ExecuteAsync(triggerSql);
                    _logger.LogInformation("Trigger {TriggerName} criada com sucesso", triggerName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Falha ao criar trigger {TriggerName}", triggerName);
                    throw;
                }
            }
        }

        private async Task CreateViewsAsync()
        {
            _logger.LogInformation("Criando views do banco de dados...");

            var views = new[]
            {
                ("Statistics/Views", "vw_players_current_season"),
                ("Statistics/Views", "vw_scoring_leaders")
            };

            foreach (var (folder, viewName) in views)
            {
                try
                {
                    await DropViewIfExistsAsync(viewName);

                    var viewSql = await _sqlLoader.LoadSqlAsync(folder, viewName);
                    if (string.IsNullOrEmpty(viewSql))
                    {
                        _logger.LogWarning("Script de view não encontrado: {Folder}/{ViewName}", folder, viewName);
                        continue;
                    }

                    await _queryExecutor.ExecuteAsync(viewSql);
                    _logger.LogInformation("View {ViewName} criada com sucesso", viewName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Falha ao criar view {ViewName}", viewName);
                    throw;
                }
            }
        }

        private async Task DropTriggerIfExistsAsync(string triggerName)
        {
            try
            {
                var dropSql = $"DROP TRIGGER IF EXISTS `{triggerName}`";
                await _queryExecutor.ExecuteAsync(dropSql);
                _logger.LogDebug("Trigger {TriggerName} removida se existia", triggerName);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Erro ao tentar remover trigger {TriggerName}", triggerName);
            }
        }

        private async Task DropViewIfExistsAsync(string viewName)
        {
            try
            {
                var dropSql = $"DROP VIEW IF EXISTS `{viewName}`";
                await _queryExecutor.ExecuteAsync(dropSql);
                _logger.LogDebug("View {ViewName} removida se existia", viewName);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Erro ao tentar remover view {ViewName}", viewName);
            }
        }

        private async Task SeedInitialDataAsync()
        {
            try
            {
                var teamCount = await _queryExecutor.QuerySingleAsync<int>("SELECT COUNT(*) FROM teams");

                if (teamCount == 0)
                {
                    _logger.LogInformation("Enviando dados iniciais...");

                    var seedSql = await _sqlLoader.LoadSqlAsync("Database", "SeedData");
                    if (!string.IsNullOrEmpty(seedSql))
                    {
                        await _queryExecutor.ExecuteAsync(seedSql);
                        _logger.LogInformation("Dados iniciais semeados com sucesso");
                    }
                    else
                    {
                        _logger.LogWarning("Script SeedData não encontrado");
                    }
                }
                else
                {
                    _logger.LogInformation("O banco de dados já contém {TeamCount} equipes, ignorando a semente", teamCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao semear os dados iniciais, mas continuando...");
            }
        }

        public async Task<bool> HealthCheckAsync()
        {
            try
            {
                var result = await _queryExecutor.QuerySingleAsync<int>("SELECT 1");
                return result == 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha na verificação de integridade do banco de dados");
                return false;
            }
        }
    }
}