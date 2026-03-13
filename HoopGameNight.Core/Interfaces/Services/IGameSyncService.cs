using System;
using System.Threading.Tasks;

namespace HoopGameNight.Core.Interfaces.Services
{
    /// <summary>
    /// Responsável exclusivamente por sincronização de jogos entre ESPN e banco de dados.
    /// </summary>
    public interface IGameSyncService
    {
        /// <summary>Sincroniza jogos de hoje com a ESPN.</summary>
        Task SyncTodayGamesAsync(bool bypassCache = false);

        /// <summary>Sincroniza jogos de uma data específica.</summary>
        Task<int> SyncGamesByDateAsync(DateTime date, bool bypassCache = false);

        /// <summary>Sincroniza jogos de um período (day-by-day).</summary>
        Task<int> SyncGamesForPeriodAsync(DateTime startDate, DateTime endDate);

        /// <summary>Sincroniza jogos futuros da ESPN para os próximos dias.</summary>
        Task<int> SyncFutureGamesAsync(int days = 10);

        /// <summary>Sincroniza um jogo específico pelo ESPN Game ID.</summary>
        Task<int> SyncGameByIdAsync(string gameId);
    }
}
