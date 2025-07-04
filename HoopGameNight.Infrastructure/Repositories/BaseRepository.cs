using HoopGameNight.Core.Interfaces.Infrastructure;
using HoopGameNight.Core.Models.Entities;
using Dapper;
using System.Data;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Infrastructure.Repositories
{
    public abstract class BaseRepository<T> where T : BaseEntity
    {
        protected readonly IDatabaseConnection _connection;
        protected readonly ISqlLoader _sqlLoader;
        protected readonly ILogger Logger;
        protected abstract string EntityName { get; }

        protected BaseRepository(
            IDatabaseConnection connection,
            ISqlLoader sqlLoader,
            ILogger logger)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _sqlLoader = sqlLoader ?? throw new ArgumentNullException(nameof(sqlLoader));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected async Task<IEnumerable<TResult>> ExecuteQueryAsync<TResult>(string sql, object? parameters = null)
        {
            try
            {
                using var connection = _connection.CreateConnection();
                var result = await connection.QueryAsync<TResult>(sql, parameters);

                Logger.LogTrace("Query executed successfully. Returned {Count} rows", result.Count());
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error executing query. SQL: {Sql}, Parameters: {@Parameters}",
                    sql.Substring(0, Math.Min(100, sql.Length)), parameters);
                throw;
            }
        }

        protected async Task<TResult?> ExecuteQuerySingleOrDefaultAsync<TResult>(string sql, object? parameters = null)
        {
            try
            {
                using var connection = _connection.CreateConnection();
                var result = await connection.QuerySingleOrDefaultAsync<TResult>(sql, parameters);

                Logger.LogTrace("Single query executed successfully. Result: {HasResult}", result != null);
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error executing single query. SQL: {Sql}, Parameters: {@Parameters}",
                    sql.Substring(0, Math.Min(100, sql.Length)), parameters);
                throw;
            }
        }

        protected async Task<int> ExecuteAsync(string sql, object? parameters = null)
        {
            try
            {
                using var connection = _connection.CreateConnection();
                var rowsAffected = await connection.ExecuteAsync(sql, parameters);

                Logger.LogTrace("Command executed successfully. Rows affected: {RowsAffected}", rowsAffected);
                return rowsAffected;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error executing command. SQL: {Sql}, Parameters: {@Parameters}",
                    sql.Substring(0, Math.Min(100, sql.Length)), parameters);
                throw;
            }
        }

        protected async Task<TResult> ExecuteScalarAsync<TResult>(string sql, object? parameters = null)
        {
            try
            {
                using var connection = _connection.CreateConnection();
                var result = await connection.ExecuteScalarAsync<TResult>(sql, parameters);

                Logger.LogTrace("Scalar query executed successfully. Result: {Result}", result);
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error executing scalar query. SQL: {Sql}, Parameters: {@Parameters}",
                    sql.Substring(0, Math.Min(100, sql.Length)), parameters);
                throw;
            }
        }

        protected async Task<string> LoadSqlAsync(string fileName)
        {
            try
            {
                return await _sqlLoader.LoadSqlAsync(EntityName, fileName);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load SQL file: {EntityName}/{FileName}", EntityName, fileName);
                throw;
            }
        }

        protected string LoadSql(string fileName)
        {
            try
            {
                return _sqlLoader.LoadSql(EntityName, fileName);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load SQL file: {EntityName}/{FileName}", EntityName, fileName);
                throw;
            }
        }

        protected bool SqlExists(string fileName)
        {
            return _sqlLoader.SqlExists(EntityName, fileName);
        }

        // Método auxiliar para criar transações
        protected async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<IDbConnection, IDbTransaction, Task<TResult>> operation)
        {
            using var connection = _connection.CreateConnection();
            connection.Open(); 

            using var transaction = connection.BeginTransaction();
            try
            {
                var result = await operation(connection, transaction);
                transaction.Commit();

                Logger.LogDebug("Transaction completed successfully");
                return result;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                Logger.LogError(ex, "Transaction rolled back due to error");
                throw;
            }
        }

        // Método para operações batch
        protected async Task<int> ExecuteBatchAsync(string sql, IEnumerable<object> parameters)
        {
            try
            {
                using var connection = _connection.CreateConnection();
                var totalRowsAffected = 0;

                foreach (var param in parameters)
                {
                    totalRowsAffected += await connection.ExecuteAsync(sql, param);
                }

                Logger.LogDebug("Batch operation completed. Total rows affected: {TotalRows}", totalRowsAffected);
                return totalRowsAffected;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error executing batch operation");
                throw;
            }
        }
    }
}