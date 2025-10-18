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
        private readonly INbaStatsApiService _nbaStatsService;
        private readonly IEspnApiService _espnService;
        private readonly IPlayerStatsRepository _statsRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IGameRepository _gameRepository;
        private readonly ITeamRepository _teamRepository;
        private readonly ILogger<PlayerStatsSyncService> _logger;

        public PlayerStatsSyncService(
            IBallDontLieService ballDontLieService,
            INbaStatsApiService nbaStatsService,
            IEspnApiService espnService,
            IPlayerStatsRepository statsRepository,
            IPlayerRepository playerRepository,
            IGameRepository gameRepository,
            ITeamRepository teamRepository,
            ILogger<PlayerStatsSyncService> logger)
        {
            _ballDontLieService = ballDontLieService;
            _nbaStatsService = nbaStatsService;
            _espnService = espnService;
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

                PlayerSeasonStats? seasonStats = null;

                // ESTRATÉGIA 1: Tentar NBA Stats API primeiro (usa Ball Don't Lie v1 diretamente com external_id)
                try
                {
                    _logger.LogInformation("Trying NBA Stats API (Ball Don't Lie v1) with external_id {ExternalId}...", player.ExternalId);
                    var nbaStats = await _nbaStatsService.GetPlayerSeasonStatsAsync(player.ExternalId.ToString(), season);
                    if (nbaStats != null)
                    {
                        _logger.LogInformation("✓ Found stats in NBA Stats API: {Points} PPG, {Games} games", nbaStats.Points, nbaStats.GamesPlayed);
                        seasonStats = await ConvertFromNbaStatsDto(nbaStats, playerId, season);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "NBA Stats API failed for player {PlayerId} (external_id: {ExternalId}) season {Season}", playerId, player.ExternalId, season);
                }

                // ESTRATÉGIA 2: Fallback para NBA Stats API usando nba_stats_id se disponível
                if (seasonStats == null && !string.IsNullOrEmpty(player.NbaStatsId))
                {
                    try
                    {
                        _logger.LogInformation("Trying NBA Stats API with nba_stats_id...");
                        var nbaStats = await _nbaStatsService.GetPlayerSeasonStatsAsync(player.NbaStatsId, season);
                        if (nbaStats != null)
                        {
                            _logger.LogInformation("✓ Found stats in NBA Stats API");
                            seasonStats = await ConvertFromNbaStatsDto(nbaStats, playerId, season);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "NBA Stats API failed for player {PlayerId} season {Season}", playerId, season);
                    }
                }

                // ESTRATÉGIA 3: Fallback para ESPN API
                if (seasonStats == null && !string.IsNullOrEmpty(player.EspnId))
                {
                    try
                    {
                        _logger.LogInformation("Trying ESPN API...");
                        var espnStats = await _espnService.GetPlayerSeasonStatsAsync(player.EspnId, season);
                        if (espnStats != null)
                        {
                            _logger.LogInformation("✓ Found stats in ESPN API");
                            seasonStats = await ConvertFromEspnStatsDto(espnStats, playerId, season);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "ESPN API failed for player {PlayerId} season {Season}", playerId, season);
                    }
                }

                if (seasonStats == null)
                {
                    _logger.LogWarning("✗ No season stats found for player {PlayerId} in season {Season} in any API", playerId, season);
                    return false;
                }

                // Salvar no banco
                await _statsRepository.UpsertSeasonStatsAsync(seasonStats);

                _logger.LogInformation("✓ Successfully synced season {Season} stats for player {PlayerId}", season, playerId);
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

        private async Task<PlayerSeasonStats> ConvertFromNbaStatsDto(DTOs.External.NBAStats.NbaStatsSeasonStatsDto nbaStats, int playerId, int season)
        {
            var player = await _playerRepository.GetByIdAsync(playerId);

            return new PlayerSeasonStats
            {
                PlayerId = playerId,
                Season = season,
                TeamId = player?.TeamId,
                GamesPlayed = nbaStats.GamesPlayed,
                AvgPoints = nbaStats.Points,
                AvgRebounds = nbaStats.Rebounds,
                AvgAssists = nbaStats.Assists,
                Points = Convert.ToInt32(nbaStats.Points * nbaStats.GamesPlayed),
                TotalRebounds = Convert.ToInt32(nbaStats.Rebounds * nbaStats.GamesPlayed),
                Assists = Convert.ToInt32(nbaStats.Assists * nbaStats.GamesPlayed),
                Steals = Convert.ToInt32(nbaStats.Steals * nbaStats.GamesPlayed),
                Blocks = Convert.ToInt32(nbaStats.Blocks * nbaStats.GamesPlayed),
                Turnovers = Convert.ToInt32(nbaStats.Turnovers * nbaStats.GamesPlayed),
                FieldGoalPercentage = nbaStats.FieldGoalPercentage,
                ThreePointPercentage = nbaStats.ThreePointPercentage,
                FreeThrowPercentage = nbaStats.FreeThrowPercentage,
                AvgMinutes = ParseMinutesString(nbaStats.Minutes),
                MinutesPlayed = ParseMinutesString(nbaStats.Minutes) * nbaStats.GamesPlayed,
                UpdatedAt = DateTime.UtcNow
            };
        }

        private async Task<PlayerSeasonStats> ConvertFromEspnStatsDto(DTOs.External.ESPN.EspnPlayerStatsDto espnStats, int playerId, int season)
        {
            var player = await _playerRepository.GetByIdAsync(playerId);

            // ESPN API retorna stats em categorias complexas
            var stats = new PlayerSeasonStats
            {
                PlayerId = playerId,
                Season = season,
                TeamId = player?.TeamId,
                UpdatedAt = DateTime.UtcNow
            };

            // Extrair estatísticas das categorias ESPN
            if (espnStats.Splits?.Categories != null)
            {
                foreach (var category in espnStats.Splits.Categories)
                {
                    foreach (var stat in category.Stats)
                    {
                        switch (stat.Abbreviation.ToUpper())
                        {
                            case "GP":
                                stats.GamesPlayed = (int)stat.Value;
                                break;
                            case "PPG":
                                stats.AvgPoints = (decimal)stat.Value;
                                stats.Points = Convert.ToInt32(stat.Value * stats.GamesPlayed);
                                break;
                            case "RPG":
                                stats.AvgRebounds = (decimal)stat.Value;
                                stats.TotalRebounds = Convert.ToInt32(stat.Value * stats.GamesPlayed);
                                break;
                            case "APG":
                                stats.AvgAssists = (decimal)stat.Value;
                                stats.Assists = Convert.ToInt32(stat.Value * stats.GamesPlayed);
                                break;
                            case "SPG":
                                stats.Steals = Convert.ToInt32(stat.Value * stats.GamesPlayed);
                                break;
                            case "BPG":
                                stats.Blocks = Convert.ToInt32(stat.Value * stats.GamesPlayed);
                                break;
                            case "TOPG":
                            case "TO":
                                stats.Turnovers = Convert.ToInt32(stat.Value * stats.GamesPlayed);
                                break;
                            case "FG%":
                                stats.FieldGoalPercentage = (decimal)stat.Value;
                                break;
                            case "3P%":
                                stats.ThreePointPercentage = (decimal)stat.Value;
                                break;
                            case "FT%":
                                stats.FreeThrowPercentage = (decimal)stat.Value;
                                break;
                            case "MPG":
                            case "MIN":
                                stats.AvgMinutes = (decimal)stat.Value;
                                stats.MinutesPlayed = (decimal)stat.Value * stats.GamesPlayed;
                                break;
                        }
                    }
                }
            }

            return stats;
        }

        public async Task<int> SyncPlayerCareerHistoryAsync(int playerId, int startYear, int endYear)
        {
            try
            {
                _logger.LogInformation("Starting sync of career history for player {PlayerId} from {StartYear} to {EndYear}",
                    playerId, startYear, endYear);

                var player = await _playerRepository.GetByIdAsync(playerId);
                if (player == null)
                {
                    _logger.LogWarning("Player {PlayerId} not found in local database", playerId);
                    return 0;
                }

                int syncedSeasons = 0;

                // Iterar de trás para frente (da temporada mais recente para a mais antiga)
                for (int year = endYear; year >= startYear; year--)
                {
                    try
                    {
                        _logger.LogInformation("Syncing season {Year} for player {PlayerId} ({PlayerName})",
                            year, playerId, $"{player.FirstName} {player.LastName}");

                        var success = await SyncPlayerSeasonStatsAsync(playerId, year);
                        if (success)
                        {
                            syncedSeasons++;
                            _logger.LogInformation("✓ Successfully synced season {Year}", year);
                        }
                        else
                        {
                            _logger.LogWarning("✗ No data found for season {Year}", year);
                        }

                        // Rate limiting - 2 segundos entre cada temporada
                        await Task.Delay(2000);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error syncing season {Year} for player {PlayerId}", year, playerId);
                    }
                }

                _logger.LogInformation("Career history sync completed for player {PlayerId}: {SyncedSeasons} of {TotalSeasons} seasons synced",
                    playerId, syncedSeasons, (endYear - startYear + 1));

                return syncedSeasons;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing career history for player {PlayerId}", playerId);
                return 0;
            }
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