namespace HoopGameNight.Core.Interfaces.Services
{
    /// <summary>
    /// Interface para enfileirar trabalhos em segundo plano de forma desacoplada
    /// </summary>
    public interface IBackgroundSyncQueue
    {
        /// <summary>
        /// Enfileira o processamento completo pós-jogo (Stats + IA)
        /// </summary>
        /// <param name="gameExternalId">ID externo do jogo na ESPN</param>
        void EnqueuePostGameProcessing(string gameExternalId);

        /// <summary>
        /// Enfileira a sincronização de carreira para um jogador específico
        /// </summary>
        /// <param name="playerId">ID interno do jogador</param>
        void EnqueuePriorityPlayerSync(int playerId);

        /// <summary>
        /// Aciona o Master Sync da madrugada imediatamente
        /// </summary>
        void EnqueueDawnMasterSync();
    }
}
