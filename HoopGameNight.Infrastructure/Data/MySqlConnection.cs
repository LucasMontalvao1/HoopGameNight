using MySqlConnector;
using System.Data;
using HoopGameNight.Core.Interfaces.Infrastructure;

namespace HoopGameNight.Infrastructure.Data
{
    public class MySqlConnection : IDatabaseConnection
    {
        private readonly string _connectionString;

        public MySqlConnection(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public IDbConnection CreateConnection()
        {
            return new MySqlConnector.MySqlConnection(_connectionString);
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using var connection = CreateConnection();
                connection.Open(); // Usar Open() síncrono
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}