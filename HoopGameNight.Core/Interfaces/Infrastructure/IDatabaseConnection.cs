using System.Data;

namespace HoopGameNight.Core.Interfaces.Infrastructure
{
    public interface IDatabaseConnection
    {
        IDbConnection CreateConnection();
        Task<bool> TestConnectionAsync();
    }
}