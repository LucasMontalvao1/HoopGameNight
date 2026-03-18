using HoopGameNight.Core.Constants;
using HoopGameNight.Core.DTOs.External;
using HoopGameNight.Core.DTOs.External.ESPN;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Enums;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Core.Models.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace HoopGameNight.Core.Services
{
    /// <summary>
    /// Responsável exclusivamente por sincronizar jogos da ESPN para o banco de dados.
    /// Extraído de GameService para isolar a responsabilidade de escrita/sync.
    /// </summary>
    public class GameSyncService : IGameSyncService
    {
        private readonly IGameRepository _gameRepository;
        private readonly ITeamRepository _teamRepository;
        private readonly IEspnApiService _espnService;
        private readonly ITeamService _teamService;
        private readonly ICacheService _cacheService;
        private readonly IEspnParser _espnParser;
        private readonly IBackgroundSyncQueue _backgroundSyncQueue;
        private readonly IGameUpdateNotifier _gameUpdateNotifier;
        private readonly ILogger<GameSyncService> _logger;

        public GameSyncService(
            IGameRepository gameRepository,
            ITeamRepository teamRepository,
            IEspnApiService espnService,
            ITeamService teamService,
            ICacheService cacheService,
            IEspnParser espnParser,
            IBackgroundSyncQueue backgroundSyncQueue,
            IGameUpdateNotifier gameUpdateNotifier,
            ILogger<GameSyncService> logger)
        {
            _gameRepository = gameRepository;
            _teamRepository = teamRepository;
            _espnService = espnService;
            _teamService = teamService;
            _cacheService = cacheService;
            _espnParser = espnParser;
            _backgroundSyncQueue = backgroundSyncQueue;
            _gameUpdateNotifier = gameUpdateNotifier;
            _logger = logger;
        }


        public async Task SyncTodayGamesAsync(bool bypassCache = false)
        {
            await SyncGamesByDateAsync(DateTime.Today, bypassCache);
        }

        public async Task<int> SyncGamesByDateAsync(DateTime date, bool bypassCache = false)
        {
            var syncCount = 0;
            var updateCount = 0;
            var updatedGamesToNotify = new List<Game>();

            _logger.LogInformation("Sincronizando jogos de {Date} (BypassCache: {Bypass})", date.ToShortDateString(), bypassCache);

            try
            {
                _logger.LogInformation("Sincronizando jogos para {Date}", date.ToString("yyyy-MM-dd"));

                List<EspnGameDto> espnGames;
                try
                {
                    espnGames = await _espnService.GetGamesByDateAsync(date, bypassCache);
                    _logger.LogInformation("ESPN retornou {Count} jogos para {Date}", espnGames.Count, date.ToString("yyyy-MM-dd"));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao buscar da ESPN para {Date}", date);
                    return 0;
                }

                if (!espnGames.Any())
                {
                    _logger.LogInformation("Nenhum jogo encontrado para {Date}", date.ToString("yyyy-MM-dd"));
                    return 0;
                }

                var affectedTeamIds = new HashSet<int>();
                foreach (var espnGame in espnGames)
                {
                    try
                    {
                        var homeTeamId = await _teamService.MapEspnTeamToSystemIdAsync(espnGame.HomeTeamId, espnGame.HomeTeamAbbreviation);
                        var visitorTeamId = await _teamService.MapEspnTeamToSystemIdAsync(espnGame.AwayTeamId, espnGame.AwayTeamAbbreviation);

                        if (homeTeamId == 0 || visitorTeamId == 0) continue;

                        var gameDate = espnGame.Date.Date;
                        var existingGames = await _gameRepository.GetGamesByDateAsync(gameDate);
                        var existingGame = existingGames.FirstOrDefault(g =>
                            g.HomeTeamId == homeTeamId &&
                            g.VisitorTeamId == visitorTeamId &&
                            g.Date.Date == gameDate);

                        if (existingGame == null)
                        {
                            var newGame = new Game
                            {
                                ExternalId = GenerateExternalId(espnGame, gameDate),
                                Date = espnGame.Date.Date,
                                DateTime = espnGame.Date,
                                HomeTeamId = homeTeamId,
                                VisitorTeamId = visitorTeamId,
                                HomeTeamScore = espnGame.HomeTeamScore,
                                VisitorTeamScore = espnGame.AwayTeamScore,
                                Status = DetermineGameStatus(espnGame),
                                Period = espnGame.Period,
                                TimeRemaining = espnGame.TimeRemaining,
                                PostSeason = DeterminePostseason(espnGame),
                                Season = espnGame.Season ?? GetSeasonYear(espnGame.Date),
                                LineScoreJson = espnGame.LineScoreJson,
                                GameLeadersJson = espnGame.GameLeadersJson,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            };

                            await _gameRepository.InsertAsync(newGame);
                            syncCount++;
                            affectedTeamIds.Add(homeTeamId);
                            affectedTeamIds.Add(visitorTeamId);
                            updatedGamesToNotify.Add(newGame);
                            
                            if (newGame.Status == GameStatus.Final)
                            {
                                _backgroundSyncQueue.EnqueuePostGameProcessing(newGame.ExternalId);
                            }
                        }
                        else
                        {
                            var oldStatus = existingGame.Status;
                            var hasChanges = ApplyGameUpdates(existingGame, espnGame);
                            if (hasChanges)
                            {
                                existingGame.DateTime = espnGame.Date;
                                existingGame.UpdatedAt = DateTime.UtcNow;
                                await _gameRepository.UpdateAsync(existingGame);
                                updateCount++;
                                affectedTeamIds.Add(homeTeamId);
                                affectedTeamIds.Add(visitorTeamId);
                                updatedGamesToNotify.Add(existingGame);

                                if (oldStatus != GameStatus.Final && existingGame.Status == GameStatus.Final)
                                {
                                    _backgroundSyncQueue.EnqueuePostGameProcessing(existingGame.ExternalId);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao processar jogo ESPN ID {GameId}", espnGame.Id);
                    }
                }

                // Update Team Records
                await UpdateTeamRecordsAsync(espnGames);

                await InvalidateCacheForDate(date, affectedTeamIds);

                if (updatedGamesToNotify.Any())
                {
                    await _gameUpdateNotifier.NotifyGamesUpdatedAsync(updatedGamesToNotify);
                }

                _logger.LogInformation("Sincronização concluída para {Date}: {New} novos, {Updated} atualizados",
                    date.ToShortDateString(), syncCount, updateCount);

                return syncCount + updateCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao sincronizar jogos para {Date}", date);
                return syncCount + updateCount;
            }
        }

        public async Task<int> SyncGameByIdAsync(string gameId)
        {
            try
            {
                _logger.LogInformation("Sincronizando jogo individual ESPN ID: {GameId}", gameId);

                var detail = await _espnService.GetGameEventAsync(gameId);
                if (detail?.Header == null)
                {
                    _logger.LogWarning("Jogo {GameId} não encontrado na ESPN.", gameId);
                    return 0;
                }

                var gameDate = DateTime.TryParse(detail.Header.Date, out var dt) ? dt : DateTime.MinValue;
                var season = detail.Header.Season?.Year ?? GetSeasonYear(gameDate);

                string? homeEspnId = null, homeAbbr = null;
                string? awayEspnId = null, awayAbbr = null;
                int? homeScore = null, awayScore = null;

                var coreEvent = await _espnService.GetCoreEventAsync(gameId);
                if (coreEvent?.Competitions != null && coreEvent.Competitions.Any())
                {
                    var comp = coreEvent.Competitions.First();
                    var homeComp = comp.Competitors?.FirstOrDefault(c => c.HomeAway == "home");
                    var awayComp = comp.Competitors?.FirstOrDefault(c => c.HomeAway == "away");

                    if (homeComp != null && awayComp != null)
                    {
                        homeEspnId = homeComp.Team?.Id;
                        homeAbbr = homeComp.Team?.Abbreviation;
                        homeScore = _espnParser.ParseScore(homeComp.Score);

                        awayEspnId = awayComp.Team?.Id;
                        awayAbbr = awayComp.Team?.Abbreviation;
                        awayScore = _espnParser.ParseScore(awayComp.Score);
                    }
                }

                if ((string.IsNullOrEmpty(homeAbbr) || string.IsNullOrEmpty(awayAbbr))
                    && detail.Boxscore?.Teams != null && detail.Boxscore.Teams.Count >= 2)
                {
                    _logger.LogInformation("CoreEvent incompleto. Tentando fallback via Boxscore para Game {GameId}", gameId);
                    var awayTeam = detail.Boxscore.Teams[0].Team;
                    var homeTeam = detail.Boxscore.Teams[1].Team;

                    if (awayTeam != null && homeTeam != null)
                    {
                        if (string.IsNullOrEmpty(awayEspnId)) awayEspnId = awayTeam.Id;
                        if (string.IsNullOrEmpty(awayAbbr)) awayAbbr = awayTeam.Abbreviation;
                        if (string.IsNullOrEmpty(homeEspnId)) homeEspnId = homeTeam.Id;
                        if (string.IsNullOrEmpty(homeAbbr)) homeAbbr = homeTeam.Abbreviation;
                    }
                }

                if (string.IsNullOrEmpty(homeEspnId) || string.IsNullOrEmpty(awayEspnId))
                {
                    _logger.LogWarning("Não foi possível determinar os times para o jogo {GameId}.", gameId);
                    return 0;
                }

                var homeTeamId = await _teamService.MapEspnTeamToSystemIdAsync(homeEspnId, homeAbbr ?? "");
                var visitorTeamId = await _teamService.MapEspnTeamToSystemIdAsync(awayEspnId, awayAbbr ?? "");

                if (homeTeamId == 0 || visitorTeamId == 0)
                {
                    _logger.LogWarning("Times do jogo {GameId} não mapeados ({Away}@{Home})", gameId, awayAbbr, homeAbbr);
                    return 0;
                }

                var existingGames = await _gameRepository.GetGamesByDateAsync(gameDate.Date);
                var existingGame = existingGames.FirstOrDefault(g =>
                    g.HomeTeamId == homeTeamId &&
                    g.VisitorTeamId == visitorTeamId &&
                    g.Date.Date == gameDate.Date);

                var status = detail.Status != null ? _espnParser.MapGameStatus(detail.Status.State ?? "") : GameStatus.Scheduled;

                if (existingGame == null)
                {
                    var newGame = new Game
                    {
                        ExternalId = gameId,
                        Date = gameDate.Date,
                        DateTime = gameDate,
                        HomeTeamId = homeTeamId,
                        VisitorTeamId = visitorTeamId,
                        HomeTeamScore = homeScore,
                        VisitorTeamScore = awayScore,
                        Status = status,
                        Period = detail.Status?.Period,
                        TimeRemaining = detail.Status?.DisplayClock,
                        PostSeason = detail.Header.Season?.Type == 3,
                        Season = season,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await _gameRepository.InsertAsync(newGame);
                    _logger.LogInformation("Jogo {GameId} inserido com sucesso via sync individual.", gameId);
                    return 1;
                }
                else
                {
                    var hasChanges = false;
                    
                    // Unificar ID se for diferente (ex: trocando ID gerado manual por ID numérico real)
                    if (existingGame.ExternalId != gameId)
                    {
                        _logger.LogInformation("Unificando ExternalId do jogo: {Old} -> {New}", existingGame.ExternalId, gameId);
                        existingGame.ExternalId = gameId;
                        hasChanges = true;
                    }

                    if (existingGame.HomeTeamScore != homeScore) { existingGame.HomeTeamScore = homeScore; hasChanges = true; }
                    if (existingGame.VisitorTeamScore != awayScore) { existingGame.VisitorTeamScore = awayScore; hasChanges = true; }
                    if (existingGame.Status != status) { existingGame.Status = status; hasChanges = true; }
                    if (existingGame.Period != detail.Status?.Period) { existingGame.Period = detail.Status?.Period; hasChanges = true; }
                    if (existingGame.TimeRemaining != detail.Status?.DisplayClock) { existingGame.TimeRemaining = detail.Status?.DisplayClock; hasChanges = true; }

                    if (hasChanges)
                    {
                        var oldStatus = existingGame.Status;
                        existingGame.Status = status; // Already assigned in the block before, but let's be safe
                        existingGame.DateTime = gameDate;
                        existingGame.UpdatedAt = DateTime.UtcNow;
                        await _gameRepository.UpdateAsync(existingGame);
                        _logger.LogInformation("Jogo {GameId} atualizado via sync individual.", gameId);

                        // Trigger: Mudou para Final
                        if (oldStatus != GameStatus.Final && existingGame.Status == GameStatus.Final)
                        {
                            _backgroundSyncQueue.EnqueuePostGameProcessing(gameId);
                        }
                        
                        await InvalidateCacheForDate(gameDate.Date, new[] { homeTeamId, visitorTeamId });
                    }

                    return 1;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao sincronizar jogo individual {GameId}", gameId);
                return 0;
            }
        }

        public async Task<int> SyncGamesForPeriodAsync(DateTime startDate, DateTime endDate)
        {
            if (endDate > DateTime.Today)
                endDate = DateTime.Today;

            var totalSynced = 0;
            var currentDate = startDate;

            while (currentDate <= endDate)
            {
                totalSynced += await SyncGamesByDateAsync(currentDate);
                await Task.Delay(1000);
                currentDate = currentDate.AddDays(1);
            }

            return totalSynced;
        }

        public async Task<int> SyncFutureGamesAsync(int days = 10)
        {
            try
            {
                var startDate = DateTime.Today.AddDays(1);
                var endDate = DateTime.Today.AddDays(days);

                _logger.LogInformation("Syncing future games from {Start} to {End}",
                    startDate.ToShortDateString(), endDate.ToShortDateString());

                var totalSynced = 0;
                var currentDate = startDate;

                while (currentDate <= endDate)
                {
                    try
                    {
                        totalSynced += await SyncGamesByDateAsync(currentDate);
                        await Task.Delay(500);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error syncing future games for {Date}", currentDate);
                    }

                    currentDate = currentDate.AddDays(1);
                }

                _logger.LogInformation("Synced {Count} future games across {Days} days", totalSynced, days);
                return totalSynced;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing future games");
                return 0;
            }
        }

        #region Helpers Privados

        private bool ApplyGameUpdates(Game existingGame, EspnGameDto espnGame)
        {
            var hasChanges = false;

            if (existingGame.HomeTeamScore != espnGame.HomeTeamScore) { existingGame.HomeTeamScore = espnGame.HomeTeamScore; hasChanges = true; }
            if (existingGame.VisitorTeamScore != espnGame.AwayTeamScore) { existingGame.VisitorTeamScore = espnGame.AwayTeamScore; hasChanges = true; }

            var newStatus = DetermineGameStatus(espnGame);
            if (existingGame.Status != newStatus) { existingGame.Status = newStatus; hasChanges = true; }

            if (existingGame.Period != espnGame.Period) { existingGame.Period = espnGame.Period; hasChanges = true; }
            if (existingGame.TimeRemaining != espnGame.TimeRemaining) { existingGame.TimeRemaining = espnGame.TimeRemaining; hasChanges = true; }

            var isPostseason = DeterminePostseason(espnGame);
            if (existingGame.PostSeason != isPostseason) { existingGame.PostSeason = isPostseason; hasChanges = true; }

            if (existingGame.LineScoreJson != espnGame.LineScoreJson) { existingGame.LineScoreJson = espnGame.LineScoreJson; hasChanges = true; }
            if (existingGame.GameLeadersJson != espnGame.GameLeadersJson) { existingGame.GameLeadersJson = espnGame.GameLeadersJson; hasChanges = true; }

            return hasChanges;
        }

        private string GenerateExternalId(EspnGameDto espnGame, DateTime date)
            => !string.IsNullOrEmpty(espnGame.Id)
                ? espnGame.Id
                : $"ESPN_{espnGame.HomeTeamId}_{espnGame.AwayTeamId}_{date:yyyyMMdd}";

        private GameStatus DetermineGameStatus(EspnGameDto espnGame)
        {
            if (!string.IsNullOrEmpty(espnGame.Status))
                return _espnParser.MapGameStatus(espnGame.Status);

            if (espnGame.HomeTeamScore.HasValue && espnGame.AwayTeamScore.HasValue &&
                espnGame.Date < DateTime.Now.AddHours(-3)) 
            {
                return GameStatus.Final;
            }

            return espnGame.Date > DateTime.Now ? GameStatus.Scheduled : GameStatus.Final;
        }

        private bool DeterminePostseason(EspnGameDto espnGame)
        {
            if (espnGame.IsPostseason.HasValue)
                return espnGame.IsPostseason.Value;

            var month = espnGame.Date.Month;
            return month >= 4 && month <= 6;
        }

        private int GetSeasonYear(DateTime date)
            => date.Month >= 10 ? date.Year : date.Year - 1;

        private async Task InvalidateCacheForDate(DateTime date, IEnumerable<int>? teamIds = null)
        {
            // Operações atômicas/diretas (IDs fixos)
            await _cacheService.RemoveAsync(CacheKeys.TodayGames());
            await _cacheService.RemoveAsync(CacheKeys.GamesByDate(date));
            
            // Operações por padrão (SCAN no Redis) - Executar apenas uma vez por lote
            await _cacheService.RemoveByPatternAsync("games:range:*");
            
            if (date.Date == DateTime.Today)
            {
                await _cacheService.RemoveByPatternAsync("games:today:*");
            }

            if (teamIds != null && teamIds.Any())
            {
                // Invalida o padrão de times uma única vez para evitar múltiplos SCANs pesados no Redis
                await _cacheService.RemoveByPatternAsync("games:team:*");
            }
        }

        private async Task UpdateTeamRecordsAsync(List<EspnGameDto> espnGames)
        {
            foreach (var eg in espnGames)
            {
                if (!string.IsNullOrEmpty(eg.HomeTeamRecord)) await UpdateSingleTeamRecord(eg.HomeTeamId, eg.HomeTeamAbbreviation, eg.HomeTeamRecord);
                if (!string.IsNullOrEmpty(eg.AwayTeamRecord)) await UpdateSingleTeamRecord(eg.AwayTeamId, eg.AwayTeamAbbreviation, eg.AwayTeamRecord);
            }
        }

        private async Task UpdateSingleTeamRecord(string espnId, string abbr, string record)
        {
            try
            {
                var teamId = await _teamService.MapEspnTeamToSystemIdAsync(espnId, abbr);
                if (teamId == 0) return;

                var team = await _teamRepository.GetByIdAsync(teamId);
                if (team == null) return;

                var parts = record.Split('-');
                if (parts.Length >= 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int l))
                {
                    if (team.Wins != w || team.Losses != l)
                    {
                        team.Wins = w;
                        team.Losses = l;
                        await _teamRepository.UpdateAsync(team);
                        _logger.LogInformation("Record atualizado para {Team}: {W}-{L}", team.Abbreviation, w, l);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao atualizar record para time {Abbr}", abbr);
            }
        }

        #endregion
    }
}
