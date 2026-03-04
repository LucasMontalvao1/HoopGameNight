using Hangfire;
using HoopGameNight.Core.Interfaces.Services;

namespace HoopGameNight.Infrastructure.Services
{
    public class BackgroundSyncQueue : IBackgroundSyncQueue
    {
        private readonly IBackgroundJobClient _jobClient;

        public BackgroundSyncQueue(IBackgroundJobClient jobClient)
        {
            _jobClient = jobClient;
        }

        public void EnqueuePostGameProcessing(string gameExternalId)
        {
            _jobClient.Enqueue<IBackgroundSyncService>(s => s.ProcessFinishedGameAsync(gameExternalId));
        }

        public void EnqueuePriorityPlayerSync(int playerId)
        {
            _jobClient.Enqueue<IBackgroundSyncService>(s => s.PriorityPlayerSyncAsync(playerId));
        }

        public void EnqueueDawnMasterSync()
        {
            _jobClient.Enqueue<IBackgroundSyncService>(s => s.DawnMasterSyncAsync());
        }
    }
}
