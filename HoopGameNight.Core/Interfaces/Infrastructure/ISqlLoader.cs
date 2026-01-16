using System.Collections.Generic;
using System.Threading.Tasks;

namespace HoopGameNight.Core.Interfaces.Infrastructure
{
    public interface ISqlLoader
    {
        Task<string> LoadSqlAsync(string category, string fileName);
        string LoadSql(string category, string fileName);
        Task<Dictionary<string, string>> LoadAllSqlInCategoryAsync(string category);
        bool SqlExists(string category, string fileName);
        void ClearCache();
        void ClearCache(string category);
    }
}