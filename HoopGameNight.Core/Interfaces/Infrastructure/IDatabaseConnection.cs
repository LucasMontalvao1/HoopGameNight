using System.Data;
using System.Threading.Tasks;

namespace HoopGameNight.Core.Interfaces.Infrastructure
{
    public interface IDatabaseConnection
    {
        IDbConnection CreateConnection();
        Task<bool> TestConnectionAsync();
    }
}