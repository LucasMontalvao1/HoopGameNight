using Microsoft.AspNetCore.Mvc;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Core.Interfaces.Repositories;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace HoopGameNight.Api.Controllers.V1.Admin
{
    /// <summary>
    /// Endpoint de diagnóstico para validar integração com APIs externas e cache
    /// </summary>
    [ApiController]
    [Route("api/v1/admin/diagnostics")]
    public class DiagnosticsController : ControllerBase
    {
        private readonly IEspnApiService _espnService;
        private readonly IGameService _gameService;
        private readonly ITeamRepository _teamRepository;
        private readonly IDistributedCache? _distributedCache;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<DiagnosticsController> _logger;

        public DiagnosticsController(
            IEspnApiService espnService,
            IGameService gameService,
            ITeamRepository teamRepository,
            IMemoryCache memoryCache,
            ILogger<DiagnosticsController> logger,
            IDistributedCache? distributedCache = null)
        {
            _espnService = espnService;
            _gameService = gameService;
            _teamRepository = teamRepository;
            _distributedCache = distributedCache;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        /// <summary>
        /// Busca dados RAW da ESPN para hoje e mostra mapeamento de times
        /// GET /api/v1/admin/diagnostics/espn/today
        /// </summary>
        [HttpGet("espn/today")]
        public async Task<IActionResult> GetEspnRawDataToday()
        {
            try
            {
                var espnGames = await _espnService.GetGamesByDateAsync(DateTime.Today);
                var teams = await _teamRepository.GetAllAsync();

                var result = new
                {
                    Date = DateTime.Today.ToString("yyyy-MM-dd"),
                    TotalGames = espnGames.Count,
                    Games = await Task.WhenAll(espnGames.Select(async eg => new
                    {
                        EspnGameId = eg.Id,
                        GameDate = eg.Date.ToString("yyyy-MM-dd HH:mm"),
                        Status = eg.Status,
                        HomeTeam = new
                        {
                            EspnId = eg.HomeTeamId,
                            Name = eg.HomeTeamName,
                            Abbreviation = eg.HomeTeamAbbreviation,
                            Score = eg.HomeTeamScore,
                            MappedTeam = teams.FirstOrDefault(t => t.Abbreviation == eg.HomeTeamAbbreviation) != null
                                ? new
                                {
                                    SystemId = teams.First(t => t.Abbreviation == eg.HomeTeamAbbreviation).Id,
                                    Name = teams.First(t => t.Abbreviation == eg.HomeTeamAbbreviation).Name,
                                    Abbreviation = teams.First(t => t.Abbreviation == eg.HomeTeamAbbreviation).Abbreviation
                                }
                                : null,
                            MappingStatus = teams.Any(t => t.Abbreviation == eg.HomeTeamAbbreviation) ? "✅ OK" : "❌ NOT FOUND"
                        },
                        AwayTeam = new
                        {
                            EspnId = eg.AwayTeamId,
                            Name = eg.AwayTeamName,
                            Abbreviation = eg.AwayTeamAbbreviation,
                            Score = eg.AwayTeamScore,
                            MappedTeam = teams.FirstOrDefault(t => t.Abbreviation == eg.AwayTeamAbbreviation) != null
                                ? new
                                {
                                    SystemId = teams.First(t => t.Abbreviation == eg.AwayTeamAbbreviation).Id,
                                    Name = teams.First(t => t.Abbreviation == eg.AwayTeamAbbreviation).Name,
                                    Abbreviation = teams.First(t => t.Abbreviation == eg.AwayTeamAbbreviation).Abbreviation
                                }
                                : null,
                            MappingStatus = teams.Any(t => t.Abbreviation == eg.AwayTeamAbbreviation) ? "✅ OK" : "❌ NOT FOUND"
                        }
                    }))
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN diagnostic data");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Busca dados RAW da ESPN para uma data específica
        /// GET /api/v1/admin/diagnostics/espn/date?date=2025-01-15
        /// </summary>
        [HttpGet("espn/date")]
        public async Task<IActionResult> GetEspnRawDataByDate([FromQuery] DateTime date)
        {
            try
            {
                var espnGames = await _espnService.GetGamesByDateAsync(date);
                var teams = await _teamRepository.GetAllAsync();

                var result = new
                {
                    Date = date.ToString("yyyy-MM-dd"),
                    TotalGames = espnGames.Count,
                    Games = espnGames.Select(eg => new
                    {
                        EspnGameId = eg.Id,
                        GameDate = eg.Date.ToString("yyyy-MM-dd HH:mm"),
                        Status = eg.Status,
                        HomeTeam = new
                        {
                            EspnId = eg.HomeTeamId,
                            Name = eg.HomeTeamName,
                            Abbreviation = eg.HomeTeamAbbreviation,
                            Score = eg.HomeTeamScore,
                            MappedSystemId = teams.FirstOrDefault(t => t.Abbreviation == eg.HomeTeamAbbreviation)?.Id,
                            MappingStatus = teams.Any(t => t.Abbreviation == eg.HomeTeamAbbreviation) ? "✅" : "❌"
                        },
                        AwayTeam = new
                        {
                            EspnId = eg.AwayTeamId,
                            Name = eg.AwayTeamName,
                            Abbreviation = eg.AwayTeamAbbreviation,
                            Score = eg.AwayTeamScore,
                            MappedSystemId = teams.FirstOrDefault(t => t.Abbreviation == eg.AwayTeamAbbreviation)?.Id,
                            MappingStatus = teams.Any(t => t.Abbreviation == eg.AwayTeamAbbreviation) ? "✅" : "❌"
                        }
                    })
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ESPN diagnostic data for date {Date}", date);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Compara dados ESPN vs Banco de Dados
        /// GET /api/v1/admin/diagnostics/compare/today
        /// </summary>
        [HttpGet("compare/today")]
        public async Task<IActionResult> CompareEspnVsDatabase()
        {
            try
            {
                var espnGames = await _espnService.GetGamesByDateAsync(DateTime.Today);
                var dbGames = await _gameService.GetGamesByDateAsync(DateTime.Today);

                var comparison = new
                {
                    Date = DateTime.Today.ToString("yyyy-MM-dd"),
                    EspnCount = espnGames.Count,
                    DatabaseCount = dbGames.Count,
                    Match = espnGames.Count == dbGames.Count ? "OK" : "MISMATCH",
                    EspnGames = espnGames.Select(eg => $"{eg.AwayTeamAbbreviation} @ {eg.HomeTeamAbbreviation}"),
                    DatabaseGames = dbGames.Select(dg => $"{dg.VisitorTeam.Abbreviation} @ {dg.HomeTeam.Abbreviation}")
                };

                return Ok(comparison);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error comparing ESPN vs Database");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Limpa todos os caches (Redis + Memory)
        /// POST /api/v1/admin/diagnostics/cache/clear
        /// </summary>
        [HttpPost("cache/clear")]
        public async Task<IActionResult> ClearAllCache()
        {
            try
            {
                var clearedKeys = new List<string>();

                // Limpar cache de hoje e próximos 7 dias
                for (int i = -1; i <= 7; i++)
                {
                    var date = DateTime.Today.AddDays(i);
                    var keys = new[]
                    {
                        $"games_today_{date:yyyy-MM-dd}",
                        $"games_date_{date:yyyy-MM-dd}"
                    };

                    foreach (var key in keys)
                    {
                        // Memory Cache
                        _memoryCache.Remove(key);

                        // Redis
                        if (_distributedCache != null)
                        {
                            await _distributedCache.RemoveAsync(key);
                        }

                        clearedKeys.Add(key);
                    }
                }

                _logger.LogInformation("Cache cleared: {Count} keys", clearedKeys.Count);

                return Ok(new
                {
                    Message = "Cache cleared successfully",
                    ClearedKeys = clearedKeys,
                    RedisAvailable = _distributedCache != null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Verifica status do cache
        /// GET /api/v1/admin/diagnostics/cache/status
        /// </summary>
        [HttpGet("cache/status")]
        public IActionResult GetCacheStatus()
        {
            return Ok(new
            {
                MemoryCache = "Active",
                Redis = _distributedCache != null ? "Active" : "Not configured",
                Recommendation = _distributedCache == null
                    ? "Configure Redis for distributed caching"
                    : "Both caches are active"
            });
        }

        /// <summary>
        /// Mostra todos os times do banco com seus IDs
        /// GET /api/v1/admin/diagnostics/teams
        /// </summary>
        [HttpGet("teams")]
        public async Task<IActionResult> GetAllTeams()
        {
            try
            {
                var teams = (await _teamRepository.GetAllAsync()).ToList();

                return Ok(new
                {
                    TotalTeams = teams.Count,
                    Teams = teams.OrderBy(t => t.Id).Select(t => new
                    {
                        SystemId = t.Id,
                        ExternalId = t.ExternalId,
                        Name = t.Name,
                        Abbreviation = t.Abbreviation,
                        City = t.City,
                        Conference = t.Conference.ToString()
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching teams");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Testa conectividade com ESPN API
        /// GET /api/v1/admin/diagnostics/espn/health
        /// </summary>
        [HttpGet("espn/health")]
        public async Task<IActionResult> CheckEspnHealth()
        {
            try
            {
                var isAvailable = await _espnService.IsApiAvailableAsync();

                return Ok(new
                {
                    EspnApiStatus = isAvailable ? "Online" : "Offline",
                    TestedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    EspnApiStatus = "Error",
                    Error = ex.Message,
                    TestedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
                });
            }
        }

        /// <summary>
        /// Verifica duplicatas de times
        /// GET /api/v1/admin/diagnostics/teams/duplicates
        /// </summary>
        [HttpGet("teams/duplicates")]
        public async Task<IActionResult> CheckTeamDuplicates()
        {
            try
            {
                var teams = (await _teamRepository.GetAllAsync()).ToList();

                var duplicatesByAbbr = teams
                    .GroupBy(t => t.Abbreviation)
                    .Where(g => g.Count() > 1)
                    .Select(g => new
                    {
                        Abbreviation = g.Key,
                        Count = g.Count(),
                        Teams = g.Select(t => new
                        {
                            Id = t.Id,
                            Name = t.DisplayName,
                            ExternalId = t.ExternalId,
                            EspnId = t.EspnId
                        })
                    }).ToList();

                var duplicatesByName = teams
                    .GroupBy(t => t.Name)
                    .Where(g => g.Count() > 1)
                    .Select(g => new
                    {
                        Name = g.Key,
                        Count = g.Count(),
                        Teams = g.Select(t => new
                        {
                            Id = t.Id,
                            Abbreviation = t.Abbreviation,
                            DisplayName = t.DisplayName
                        })
                    }).ToList();

                return Ok(new
                {
                    HasDuplicates = duplicatesByAbbr.Any() || duplicatesByName.Any(),
                    DuplicatesByAbbreviation = duplicatesByAbbr,
                    DuplicatesByName = duplicatesByName,
                    TotalTeams = teams.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking team duplicates");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Mostra jogos recentes de um time específico com detalhes
        /// GET /api/v1/admin/diagnostics/team/{teamId}/recent-games
        /// </summary>
        [HttpGet("team/{teamId}/recent-games")]
        public async Task<IActionResult> GetTeamRecentGamesDebug(int teamId)
        {
            try
            {
                var team = await _teamRepository.GetByIdAsync(teamId);
                if (team == null)
                {
                    return NotFound(new { error = $"Time {teamId} não encontrado" });
                }

                var games = await _gameService.GetRecentGamesForTeamAsync(teamId, 30);

                return Ok(new
                {
                    Team = new
                    {
                        Id = team.Id,
                        Name = team.DisplayName,
                        Abbreviation = team.Abbreviation
                    },
                    TotalGames = games.Count,
                    Games = games.Select(g => new
                    {
                        GameId = g.Id,
                        Date = g.Date,
                        HomeTeam = new
                        {
                            Id = g.HomeTeam.Id,
                            Name = g.HomeTeam.DisplayName,
                            Abbreviation = g.HomeTeam.Abbreviation,
                            Score = g.HomeTeamScore
                        },
                        VisitorTeam = new
                        {
                            Id = g.VisitorTeam.Id,
                            Name = g.VisitorTeam.DisplayName,
                            Abbreviation = g.VisitorTeam.Abbreviation,
                            Score = g.VisitorTeamScore
                        },
                        Status = g.Status,
                        Score = g.Score,
                        IsTeamHome = g.HomeTeam.Id == teamId,
                        IsTeamVisitor = g.VisitorTeam.Id == teamId
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting team recent games");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
