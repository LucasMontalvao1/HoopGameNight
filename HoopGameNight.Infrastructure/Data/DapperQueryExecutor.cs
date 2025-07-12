using Dapper;
using HoopGameNight.Core.Interfaces.Infrastructure;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HoopGameNight.Infrastructure.Data
{
    public class DapperQueryExecutor : IDatabaseQueryExecutor
    {
        private readonly IDatabaseConnection _connection;

        public DapperQueryExecutor(IDatabaseConnection connection)
        {
            _connection = connection;
        }

        public async Task<T> QuerySingleAsync<T>(string sql, object? parameters = null)
        {
            using var connection = _connection.CreateConnection();
            return await connection.QuerySingleAsync<T>(sql, parameters);
        }

        public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null)
        {
            using var connection = _connection.CreateConnection();
            return await connection.QueryAsync<T>(sql, parameters);
        }

        public async Task<int> ExecuteAsync(string sql, object? parameters = null)
        {
            using var connection = _connection.CreateConnection();
            return await connection.ExecuteAsync(sql, parameters);
        }
    }
}