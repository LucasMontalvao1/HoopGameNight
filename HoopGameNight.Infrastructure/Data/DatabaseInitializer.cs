using HoopGameNight.Core.Interfaces.Infrastructure;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Infrastructure.Data
{
    public class DatabaseInitializer
    {
        private readonly IDatabaseQueryExecutor _queryExecutor;
        private readonly ISqlLoader _sqlLoader;
        private readonly ILogger<DatabaseInitializer> _logger;

        public DatabaseInitializer(
            IDatabaseQueryExecutor queryExecutor,
            ISqlLoader sqlLoader,
            ILogger<DatabaseInitializer> logger)
        {
            _queryExecutor = queryExecutor;
            _sqlLoader = sqlLoader;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Starting database initialization...");

                // Criar tabelas
                await CreateTablesAsync();

                // Verificar se precisa de seed data
                await SeedInitialDataAsync();

                _logger.LogInformation("Database initialization completed successfully!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database initialization failed");
                throw;
            }
        }

        private async Task CreateTablesAsync()
        {
            _logger.LogInformation("Creating database tables...");

            // Executar script de inicialização do banco
            try
            {
                var initScript = await _sqlLoader.LoadSqlAsync("Database", "InitDatabase");
                if (!string.IsNullOrEmpty(initScript))
                {
                    await _queryExecutor.ExecuteAsync(initScript);
                    _logger.LogDebug("Database initialization script executed successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Database initialization script failed, but continuing...");
            }

            // Criar tabelas na ordem correta
            var tableOrder = new[] { "Teams", "Players", "Games" };

            foreach (var table in tableOrder)
            {
                try
                {
                    var createTableSql = await _sqlLoader.LoadSqlAsync(table, "CreateTable");
                    await _queryExecutor.ExecuteAsync(createTableSql);
                    _logger.LogDebug("Table {Table} created/verified successfully", table);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create table {Table}", table);
                    throw;
                }
            }
        }

        private async Task SeedInitialDataAsync()
        {
            try
            {
                // Verificar se já existem times
                var teamCount = await _queryExecutor.QuerySingleAsync<int>("SELECT COUNT(*) FROM teams");

                if (teamCount == 0)
                {
                    _logger.LogInformation("Seeding initial data...");

                    var seedSql = await _sqlLoader.LoadSqlAsync("Database", "SeedData");
                    if (!string.IsNullOrEmpty(seedSql))
                    {
                        await _queryExecutor.ExecuteAsync(seedSql);
                        _logger.LogInformation("Initial data seeded successfully");
                    }
                }
                else
                {
                    _logger.LogInformation("Database already contains data, skipping seed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to seed initial data, but continuing...");
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
                _logger.LogError(ex, "Database health check failed");
                return false;
            }
        }
    }
}