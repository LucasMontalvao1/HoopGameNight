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
        private readonly TimeSpan _liveGamesSyncInterval = TimeSpan.FromMinutes(2);   // Com jogos ao vivo
        private readonly TimeSpan _normalSyncInterval = TimeSpan.FromMinutes(15);     // Normal 
        private readonly TimeSpan _offseasonSyncInterval = TimeSpan.FromHours(1);     // Off-season
        private int _syncCounter = 0;

        public DataSyncBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<DataSyncBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Data Sync Background Service started");

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            var hasLiveGames = await PerformSyncAsync("Initial");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _syncCounter++;

                    var interval = hasLiveGames ? _liveGamesSyncInterval : _normalSyncInterval;

                    _logger.LogDebug(
                        "Next sync in {Interval} | Live games: {HasLive} | Counter: {Counter}",
                        interval, hasLiveGames, _syncCounter);

                    await Task.Delay(interval, stoppingToken);
                    hasLiveGames = await PerformSyncAsync("Scheduled");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Data Sync Background Service is stopping");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Data Sync Background Service");
                    // Aguardar 5 minutos antes de tentar novamente após erro
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }
        }

        private async Task<bool> PerformSyncAsync(string syncType)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();

                var teamService = scope.ServiceProvider.GetRequiredService<ITeamService>();
                var gameService = scope.ServiceProvider.GetRequiredService<IGameService>();

                _logger.LogInformation("Starting {SyncType} data synchronization (#{Counter})",
                    syncType, _syncCounter);

                var teams = await teamService.GetAllTeamsAsync();
                if (teams.Count < 30)
                {
                    _logger.LogWarning("Syncing teams ({Count} found, expected 30)", teams.Count);
                    await teamService.SyncAllTeamsAsync();
                }

                if (_syncCounter == 1 || _syncCounter % 12 == 0) 
                {
                    _logger.LogInformation("Syncing yesterday's games");
                    await gameService.SyncGamesByDateAsync(DateTime.Today.AddDays(-1));
                }

                _logger.LogInformation("Syncing today's games");
                await gameService.SyncTodayGamesAsync();
                if (_syncCounter == 1 || _syncCounter % 4 == 0) 
                {
                    _logger.LogInformation("Syncing future games (next 7 days)");
                    await gameService.SyncFutureGamesAsync(days: 7);
                }

                var todayGames = await gameService.GetTodayGamesAsync();
                var liveGames = todayGames.Count(g => g.IsLive);
                var hasLiveGames = liveGames > 0;

                _logger.LogInformation(
                    "{SyncType} sync completed | Today: {TotalGames} jogos, Live: {LiveGames}",
                    syncType, todayGames.Count, liveGames);

                return hasLiveGames;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during {SyncType} data synchronization", syncType);
                return false;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Data Sync Background Service is stopping...");
            await base.StopAsync(cancellationToken);
        }
    }
}