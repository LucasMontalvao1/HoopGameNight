using HoopGameNight.Core.Interfaces.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace HoopGameNight.Api.Services
{
    public class DataSyncBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DataSyncBackgroundService> _logger;
        private readonly TimeSpan _syncInterval = TimeSpan.FromHours(1); // Sync every hour

        public DataSyncBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<DataSyncBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🤖 Data Sync Background Service started");

            // Wait 30 seconds after startup before first sync
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            // Perform initial sync
            await PerformSyncAsync("Initial");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_syncInterval, stoppingToken);
                    await PerformSyncAsync("Scheduled");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("🛑 Data Sync Background Service is stopping");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error in Data Sync Background Service");
                }
            }
        }

        private async Task PerformSyncAsync(string syncType)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();

                var teamService = scope.ServiceProvider.GetRequiredService<ITeamService>();
                var gameService = scope.ServiceProvider.GetRequiredService<IGameService>();

                _logger.LogInformation("🔄 Starting {SyncType} data synchronization", syncType);

                // Check if teams need sync (only if less than 30 teams)
                var teams = await teamService.GetAllTeamsAsync();
                if (teams.Count < 30)
                {
                    _logger.LogInformation("📝 Syncing teams ({Count} found, expected 30)", teams.Count);
                    await teamService.SyncAllTeamsAsync();
                }

                // Always sync today's games
                _logger.LogInformation("🏀 Syncing today's games");
                await gameService.SyncTodayGamesAsync();

                _logger.LogInformation("✅ {SyncType} data synchronization completed successfully", syncType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error during {SyncType} data synchronization", syncType);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🛑 Data Sync Background Service is stopping...");
            await base.StopAsync(cancellationToken);
        }
    }
}