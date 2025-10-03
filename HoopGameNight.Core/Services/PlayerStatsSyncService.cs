using HoopGameNight.Core.DTOs.External.BallDontLie;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Core.Models.Entities;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Core.Services
{
    public class PlayerStatsSyncService : IPlayerStatsSyncService
    {
        private readonly IBallDontLieService _ballDontLieService;
        private readonly IPlayerStatsRepository _statsRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IGameRepository _gameRepository;
        private readonly ITeamRepository _teamRepository;
        private readonly ILogger<PlayerStatsSyncService> _logger;

        public PlayerStatsSyncService(
            IBallDontLieService ballDontLieService,
            IPlayerStatsRepository statsRepository,
            IPlayerRepository playerRepository,
            IGameRepository gameRepository,
            ITeamRepository teamRepository,
            ILogger<PlayerStatsSyncService> logger)
        {
            _ballDontLieService = ballDontLieService;
            _statsRepository = statsRepository;
            _playerRepository = playerRepository;
            _gameRepository = gameRepository;
            _teamRepository = teamRepository;
            _logger = logger;
        }

        public async Task<bool> SyncPlayerSeasonStatsAsync(int playerId, int season)
        {
            try
            {
                _logger.LogInformation("Starting sync of season {Season} stats for player {PlayerId}", season, playerId);

                // Buscar o player no banco local
                var player = await _playerRepository.GetByIdAsync(playerId);
                if (player == null)
                {
                    _logger.LogWarning("Player {PlayerId} not found in local database", playerId);
                    return false;
                }

                // Buscar stats da API externa
                var apiStats = await _ballDontLieService.GetPlayerSeasonStatsAsync(player.ExternalId, season);
                if (apiStats == null)
                {
                    _logger.LogWarning("No season stats found for player {PlayerId} in season {Season}", playerId, season);
                    return false;
                }

                // Converter e salvar no banco
                var seasonStats = await ConvertToSeasonStats(apiStats, playerId, season);
                await _statsRepository.UpsertSeasonStatsAsync(seasonStats);

                _logger.LogInformation("Successfully synced season {Season} stats for player {PlayerId}", season, playerId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing season stats for player {PlayerId}", playerId);
                return false;
            }
        }

        public async Task<bool> SyncPlayerGameStatsAsync(int playerId, int gameId)
        {
            try
            {
                _logger.LogInformation("Starting sync of game {GameId} stats for player {PlayerId}", gameId, playerId);

                var player = await _playerRepository.GetByIdAsync(playerId);
                var game = await _gameRepository.GetByIdAsync(gameId);

                if (player == null || game == null)
                {
                    _logger.LogWarning("Player or game not found in local database");
                    return false;
                }

                // Buscar stats do jogo na API
                var apiStats = await _ballDontLieService.GetPlayerGameStatsAsync(player.ExternalId, game.ExternalId);
                if (apiStats == null)
                {
                    _logger.LogWarning("No game stats found for player {PlayerId} in game {GameId}", playerId, gameId);
                    return false;
                }

                // Converter e salvar
                var gameStats = await ConvertToGameStats(apiStats, playerId, gameId);
                await _statsRepository.UpsertGameStatsAsync(gameStats);

                _logger.LogInformation("Successfully synced game {GameId} stats for player {PlayerId}", gameId, playerId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing game stats for player {PlayerId} in game {GameId}", playerId, gameId);
                return false;
            }
        }

        public async Task<bool> SyncPlayerRecentGamesAsync(int playerId, int numberOfGames = 10)
        {
            try
            {
                _logger.LogInformation("Starting sync of {NumberOfGames} recent games for player {PlayerId}", numberOfGames, playerId);

                var player = await _playerRepository.GetByIdAsync(playerId);
                if (player == null)
                {
                    _logger.LogWarning("Player {PlayerId} not found in local database", playerId);
                    return false;
                }

                // Buscar jogos recentes da API
                var recentGames = await _ballDontLieService.GetPlayerRecentGamesAsync(player.ExternalId, numberOfGames);
                if (!recentGames.Any())
                {
                    _logger.LogWarning("No recent games found for player {PlayerId}", playerId);
                    return false;
                }

                int syncedCount = 0;
                foreach (var apiGameStats in recentGames)
                {
                    try
                    {
                        // Verificar se o jogo existe no banco local
                        var localGame = await _gameRepository.GetByExternalIdAsync(apiGameStats.Game.Id);
                        if (localGame == null)
                        {
                            // Criar o jogo se não existir
                            localGame = await CreateGameFromApi(apiGameStats.Game);
                            if (localGame == null) continue;
                        }

                        // Converter e salvar stats
                        var gameStats = await ConvertToGameStats(apiGameStats, playerId, localGame.Id);
                        await _statsRepository.UpsertGameStatsAsync(gameStats);
                        syncedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error syncing individual game stats for player {PlayerId}", playerId);
                    }
                }

                _logger.LogInformation("Successfully synced {SyncedCount} of {Total} recent games for player {PlayerId}",
                    syncedCount, recentGames.Count(), playerId);

                return syncedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing recent games for player {PlayerId}", playerId);
                return false;
            }
        }

        public async Task<bool> SyncAllPlayersSeasonStatsAsync(int season)
        {
            try
            {
                _logger.LogInformation("Starting sync of season {Season} stats for all active players", season);

                // Buscar todos os jogadores ativos
                var activePlayers = await _playerRepository.GetActivePlayersAsync();
                if (!activePlayers.Any())
                {
                    _logger.LogWarning("No active players found");
                    return false;
                }

                int successCount = 0;
                int totalPlayers = activePlayers.Count();

                foreach (var player in activePlayers)
                {
                    try
                    {
                        var success = await SyncPlayerSeasonStatsAsync(player.Id, season);
                        if (success) successCount++;

                        // Adicionar delay para respeitar rate limiting
                        await Task.Delay(2000); // 2 segundos entre requisições
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error syncing season stats for player {PlayerId}", player.Id);
                    }
                }

                _logger.LogInformation("Successfully synced season {Season} stats for {SuccessCount} of {TotalPlayers} players",
                    season, successCount, totalPlayers);

                return successCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing season {Season} stats for all players", season);
                return false;
            }
        }

        public async Task<bool> SyncGameStatsForAllPlayersInGameAsync(int gameId)
        {
            try
            {
                _logger.LogInformation("Starting sync of stats for all players in game {GameId}", gameId);

                var game = await _gameRepository.GetByIdAsync(gameId);
                if (game == null)
                {
                    _logger.LogWarning("Game {GameId} not found", gameId);
                    return false;
                }

                // Buscar jogadores dos dois times
                var homeTeamPlayers = await _playerRepository.GetByTeamIdAsync(game.HomeTeamId);
                var visitorTeamPlayers = await _playerRepository.GetByTeamIdAsync(game.VisitorTeamId);
                var allPlayers = homeTeamPlayers.Concat(visitorTeamPlayers);

                int successCount = 0;
                foreach (var player in allPlayers)
                {
                    try
                    {
                        var success = await SyncPlayerGameStatsAsync(player.Id, gameId);
                        if (success) successCount++;

                        await Task.Delay(2000); // Rate limiting
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error syncing game stats for player {PlayerId}", player.Id);
                    }
                }

                _logger.LogInformation("Successfully synced game {GameId} stats for {SuccessCount} players",
                    gameId, successCount);

                return successCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing game {GameId} stats for all players", gameId);
                return false;
            }
        }

        private async Task<PlayerSeasonStats> ConvertToSeasonStats(BallDontLiePlayerSeasonStatsDto apiStats, int playerId, int season)
        {
            // Buscar o time atual do jogador
            var player = await _playerRepository.GetByIdAsync(playerId);

            return new PlayerSeasonStats
            {
                PlayerId = playerId,
                Season = season,
                TeamId = player?.TeamId,
                GamesPlayed = apiStats.GamesPlayed,
                AvgPoints = Convert.ToDecimal(apiStats.Pts),
                AvgRebounds = Convert.ToDecimal(apiStats.Reb),
                AvgAssists = Convert.ToDecimal(apiStats.Ast),
                Points = Convert.ToInt32(apiStats.Pts * apiStats.GamesPlayed),
                TotalRebounds = Convert.ToInt32(apiStats.Reb * apiStats.GamesPlayed),
                Assists = Convert.ToInt32(apiStats.Ast * apiStats.GamesPlayed),
                Steals = Convert.ToInt32(apiStats.Stl * apiStats.GamesPlayed),
                Blocks = Convert.ToInt32(apiStats.Blk * apiStats.GamesPlayed),
                Turnovers = Convert.ToInt32(apiStats.Turnover * apiStats.GamesPlayed),
                FieldGoalPercentage = apiStats.FgPct.HasValue ? Convert.ToDecimal(apiStats.FgPct.Value) : null,
                ThreePointPercentage = apiStats.Fg3Pct.HasValue ? Convert.ToDecimal(apiStats.Fg3Pct.Value) : null,
                FreeThrowPercentage = apiStats.FtPct.HasValue ? Convert.ToDecimal(apiStats.FtPct.Value) : null,
                AvgMinutes = ParseMinutesString(apiStats.Min),
                MinutesPlayed = ParseMinutesString(apiStats.Min) * apiStats.GamesPlayed,
                UpdatedAt = DateTime.UtcNow
            };
        }

        private async Task<PlayerGameStats> ConvertToGameStats(BallDontLiePlayerGameStatsDto apiStats, int playerId, int gameId)
        {
            var (minutes, seconds) = ParseMinutesAndSeconds(apiStats.Min);

            // Buscar o team ID local baseado no external ID
            var team = await _teamRepository.GetByExternalIdAsync(apiStats.Team.Id);

            return new PlayerGameStats
            {
                PlayerId = playerId,
                GameId = gameId,
                TeamId = team?.Id ?? 0,
                Points = apiStats.Pts,
                TotalRebounds = apiStats.Reb,
                Assists = apiStats.Ast,
                Steals = apiStats.Stl,
                Blocks = apiStats.Blk,
                Turnovers = apiStats.Turnover,
                MinutesPlayed = minutes,
                SecondsPlayed = seconds,
                DidNotPlay = string.IsNullOrEmpty(apiStats.Min) || apiStats.Min == "00:00",
                UpdatedAt = DateTime.UtcNow
            };
        }

        private async Task<Game?> CreateGameFromApi(BallDontLieGameDto apiGame)
        {
            try
            {
                // Verificar se os times existem localmente
                var homeTeam = await _teamRepository.GetByExternalIdAsync(apiGame.HomeTeam.Id);
                var visitorTeam = await _teamRepository.GetByExternalIdAsync(apiGame.VisitorTeam.Id);

                if (homeTeam == null || visitorTeam == null)
                {
                    _logger.LogWarning("Teams not found for game {ExternalGameId}", apiGame.Id);
                    return null;
                }

                var game = new Game
                {
                    ExternalId = apiGame.Id,
                    Date = DateTime.Parse(apiGame.Date),
                    DateTime = DateTime.Parse(apiGame.Date),
                    HomeTeamId = homeTeam.Id,
                    VisitorTeamId = visitorTeam.Id,
                    HomeTeamScore = apiGame.HomeTeamScore,
                    VisitorTeamScore = apiGame.VisitorTeamScore,
                    Status = ParseGameStatus(apiGame.Status),
                    Season = apiGame.Season,
                    PostSeason = apiGame.Postseason,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _gameRepository.InsertAsync(game);
                return game;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating game from API data");
                return null;
            }
        }

        private decimal ParseMinutesString(string? minutesStr)
        {
            if (string.IsNullOrEmpty(minutesStr)) return 0;

            var parts = minutesStr.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out int minutes) && int.TryParse(parts[1], out int seconds))
            {
                return minutes + (decimal)seconds / 60;
            }

            return 0;
        }

        private (int minutes, int seconds) ParseMinutesAndSeconds(string? minutesStr)
        {
            if (string.IsNullOrEmpty(minutesStr)) return (0, 0);

            var parts = minutesStr.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out int minutes) && int.TryParse(parts[1], out int seconds))
            {
                return (minutes, seconds);
            }

            return (0, 0);
        }

        private Core.Enums.GameStatus ParseGameStatus(string status)
        {
            return status?.ToLower() switch
            {
                "scheduled" => Core.Enums.GameStatus.Scheduled,
                "live" => Core.Enums.GameStatus.Live,
                "final" => Core.Enums.GameStatus.Final,
                "postponed" => Core.Enums.GameStatus.Postponed,
                "cancelled" => Core.Enums.GameStatus.Cancelled,
                _ => Core.Enums.GameStatus.Scheduled
            };
        }
    }
}