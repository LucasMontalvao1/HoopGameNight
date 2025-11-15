using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.DTOs.Response;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HoopGameNight.Api.Options;
using HoopGameNight.Core.Enums;

namespace HoopGameNight.Api.Services
{
    public class PlayerStatsSyncBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PlayerStatsSyncBackgroundService> _logger;
        private readonly PlayerStatsSyncOptions _syncOptions;
        private Timer? _timer;

        public PlayerStatsSyncBackgroundService(
            IServiceProvider serviceProvider,
            IOptions<PlayerStatsSyncOptions> syncOptions,
            ILogger<PlayerStatsSyncBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _syncOptions = syncOptions.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_syncOptions.Enabled)
            {
                _logger.LogInformation("Auto sync is disabled");
                return;
            }

            _logger.LogInformation("Player Stats Sync Background Service is starting");
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SyncPlayerStatsAsync(stoppingToken);

                    var delayHours = _syncOptions.SyncIntervalHours;
                    _logger.LogInformation("Next sync scheduled in {Hours} hours", delayHours);
                    await Task.Delay(TimeSpan.FromHours(delayHours), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Player Stats Sync Background Service");
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                }
            }

            _logger.LogInformation("Player Stats Sync Background Service is stopping");
        }

        private async Task SyncTodayGamesStatsAsync(
            IPlayerStatsSyncService syncService,
            IGameService gameService,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Syncing stats for today's games");

                var todayGames = await gameService.GetTodayGamesAsync();
                foreach (var game in todayGames)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    if (game.Status == "Final" || game.Status == GameStatus.Final.ToString())
                    {
                        _logger.LogDebug("Syncing stats for completed game {GameId}", game.Id);
                        await syncService.SyncGameStatsForAllPlayersInGameAsync(game.Id);
                        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing today's game stats");
            }
        }

        private async Task SyncRecentPlayerStatsAsync(
            IPlayerStatsSyncService syncService,
            IPlayerService playerService,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Syncing recent stats for active players");

                var searchRequest = new SearchPlayerRequest
                {
                    Search = "", 
                    Page = 1,
                    PageSize = _syncOptions.MaxPlayersPerSync
                };

                var result = await playerService.SearchPlayersAsync(searchRequest);

                var playersToSync = result.Players.Take(_syncOptions.MaxPlayersPerSync);

                foreach (var player in playersToSync)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    try
                    {
                        await syncService.SyncPlayerRecentGamesAsync(player.Id, 5);
                        var currentSeason = DateTime.Now.Year;
                        await syncService.SyncPlayerSeasonStatsAsync(player.Id, currentSeason);
                        await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to sync stats for player {PlayerId}", player.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing recent player stats");
            }
        }

        private async Task UpdateCareerStatsAsync(
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Updating career stats for all players");

                var statsService = serviceProvider.GetRequiredService<IPlayerStatsService>();
                var playerService = serviceProvider.GetRequiredService<IPlayerService>();

                var searchRequest = new SearchPlayerRequest
                {
                    Search = "", 
                    Page = 1,
                    PageSize = 1000
                };

                var result = await playerService.SearchPlayersAsync(searchRequest);

                var playersToProcess = result.Players;
                int totalProcessed = 0;

                foreach (var player in playersToProcess)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    try
                    {
                        await statsService.UpdatePlayerCareerStatsAsync(player.Id);
                        totalProcessed++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to update career stats for player {PlayerId}", player.Id);
                    }
                }

                _logger.LogInformation("Career stats update completed for {Count} players", totalProcessed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating career stats");
            }
        }

        private async Task SyncPlayerStatsAsync(CancellationToken cancellationToken)
        {
            _logger.LogWarning("Player stats sync is currently disabled - service not implemented");
            await Task.CompletedTask;
            return;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Player Stats Sync Background Service is stopping");
            _timer?.Change(Timeout.Infinite, 0);
            await base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _timer?.Dispose();
            base.Dispose();
        }
    }
}