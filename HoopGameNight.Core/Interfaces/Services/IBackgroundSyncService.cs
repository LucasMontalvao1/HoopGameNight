using System.Threading.Tasks;

namespace HoopGameNight.Core.Interfaces.Services
{
    /// <summary>
    /// Orquestrador de tarefas em segundo plano (Hangfire)
    /// </summary>
    public interface IBackgroundSyncService
    {
        /// <summary>
        /// Sincronização mestre da madrugada (03:00 AM)
        /// </summary>
        Task DawnMasterSyncAsync();

        /// <summary>
        /// Processa um jogo que acabou de ser finalizado
        /// </summary>
        /// <param name="gameId">ID externo do jogo</param>
        Task ProcessFinishedGameAsync(string gameId);

        /// <summary>
        /// Sincronização rápida de "hot players" ou buscas sob demanda
        /// </summary>
        Task PriorityPlayerSyncAsync(int playerId);

        /// <summary>
        /// Sincronização períodica de Injury Reports
        /// </summary>
        Task SyncInjuryReportsAsync();

        /// <summary>
        /// Identifica e sincroniza estatísticas de jogos FINAL que estão faltando
        /// </summary>
        Task SyncMissingGamesStatsAsync();

        /// <summary>
        /// Atualiza o cache de dados semi-estáticos (Times/Jogadores)
        /// </summary>
        Task RefreshStaticDataCacheAsync();
    }
}
