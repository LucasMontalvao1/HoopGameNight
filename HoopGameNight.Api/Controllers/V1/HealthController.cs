using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using HoopGameNight.Core.Interfaces.Infrastructure;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace HoopGameNight.Api.Controllers
{
    /// <summary>
    /// Controller para health checks e status da API
    /// </summary>
    [ApiController]
    [Route("api/health")]
    [AllowAnonymous]
    [ApiExplorerSettings(GroupName = "Health")]
    public class HealthController : ControllerBase
    {
        private static readonly DateTime _startTime = DateTime.UtcNow;
        private readonly ILogger<HealthController> _logger;
        private readonly HealthCheckService _healthCheckService;
        private readonly IDatabaseConnection _databaseConnection;
        private readonly IEspnApiService _espnApiService;
        private readonly IConfiguration _configuration;

        public HealthController(
            ILogger<HealthController> logger,
            HealthCheckService healthCheckService,
            IDatabaseConnection databaseConnection,
            IEspnApiService espnApiService,
            IConfiguration configuration)
        {
            _logger = logger;
            _healthCheckService = healthCheckService;
            _databaseConnection = databaseConnection;
            _espnApiService = espnApiService;
            _configuration = configuration;
        }

        /// <summary>
        /// Health check simples - sempre retorna 200 se a API está rodando
        /// </summary>
        [HttpGet("live")]
        [ProducesResponseType(typeof(LivenessCheck), StatusCodes.Status200OK)]
        public IActionResult GetLiveness()
        {
            var response = new LivenessCheck
            {
                Status = "alive",
                Timestamp = DateTime.UtcNow,
                Service = "HoopGameNight API"
            };

            return Ok(response);
        }

        /// <summary>
        /// Health check de prontidão - verifica dependências críticas
        /// </summary>
        [HttpGet("ready")]
        [ProducesResponseType(typeof(ReadinessCheck), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ReadinessCheck), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GetReadiness()
        {
            var checks = new List<DependencyCheck>();
            var overallHealthy = true;

            // Check Database
            var dbCheck = await CheckDatabaseAsync();
            checks.Add(dbCheck);
            if (!dbCheck.IsHealthy) overallHealthy = false;

            // Check External API
            var apiCheck = await CheckExternalApiAsync();
            checks.Add(apiCheck);

            // Check Configuration
            var configCheck = CheckConfiguration();
            checks.Add(configCheck);
            if (!configCheck.IsHealthy) overallHealthy = false;

            var response = new ReadinessCheck
            {
                Status = overallHealthy ? "ready" : "not_ready",
                IsReady = overallHealthy,
                Timestamp = DateTime.UtcNow,
                Checks = checks
            };

            return overallHealthy ? Ok(response) : StatusCode(503, response);
        }

        /// <summary>
        /// Health check completo - informações detalhadas do sistema
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GetHealth()
        {
            try
            {
                var report = await _healthCheckService.CheckHealthAsync();
                var response = BuildHealthResponse(report);

                _logger.LogInformation("Health check completed: {Status}", response.Status);

                return report.Status == HealthStatus.Healthy
                    ? Ok(response)
                    : StatusCode(503, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during health check");

                var errorResponse = new HealthCheckResponse
                {
                    Status = "unhealthy",
                    Timestamp = DateTime.UtcNow,
                    Error = ex.Message
                };

                return StatusCode(503, errorResponse);
            }
        }

        /// <summary>
        /// Informações detalhadas sobre o sistema
        /// </summary>
        [HttpGet("info")]
        [ProducesResponseType(typeof(SystemInfo), StatusCodes.Status200OK)]
        public IActionResult GetSystemInfo()
        {
            var uptime = DateTime.UtcNow - _startTime;
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version?.ToString() ?? "1.0.0";

            var info = new SystemInfo
            {
                Application = new ApplicationInfo
                {
                    Name = "HoopGameNight API",
                    Version = version,
                    Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                    StartTime = _startTime,
                    Uptime = new UptimeInfo
                    {
                        Days = (int)uptime.TotalDays,
                        Hours = uptime.Hours,
                        Minutes = uptime.Minutes,
                        Seconds = uptime.Seconds,
                        TotalSeconds = (long)uptime.TotalSeconds,
                        Formatted = FormatUptime(uptime)
                    }
                },
                System = new SystemDetails
                {
                    MachineName = Environment.MachineName,
                    OSVersion = RuntimeInformation.OSDescription,
                    ProcessorCount = Environment.ProcessorCount,
                    Is64BitProcess = Environment.Is64BitProcess,
                    Is64BitOS = RuntimeInformation.OSArchitecture == Architecture.X64 ||
                               RuntimeInformation.OSArchitecture == Architecture.Arm64,
                    DotNetVersion = RuntimeInformation.FrameworkDescription,
                    TimeZone = TimeZoneInfo.Local.DisplayName,
                    CurrentTime = DateTime.Now,
                    UtcTime = DateTime.UtcNow
                },
                Process = GetProcessInfo(),
                Features = new FeaturesInfo
                {
                    SwaggerEnabled = _configuration.GetValue<bool>("Features:Swagger", false),
                    RateLimitingEnabled = _configuration.GetValue<bool>("Features:RateLimiting", true),
                    CachingEnabled = _configuration.GetValue<bool>("Features:Caching", true),
                    BackgroundSyncEnabled = _configuration.GetValue<bool>("Sync:EnableAutoSync", true)
                }
            };

            return Ok(info);
        }

        /// <summary>
        /// Diagnóstico detalhado do sistema
        /// </summary>
        [HttpGet("diagnostics")]
        [ProducesResponseType(typeof(DiagnosticsInfo), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDiagnostics()
        {
            var diagnostics = new DiagnosticsInfo
            {
                Timestamp = DateTime.UtcNow,
                Memory = GetMemoryDiagnostics(),
                Performance = GetPerformanceDiagnostics(),
                Dependencies = await GetDependenciesDiagnosticsAsync(),
                Configuration = GetConfigurationDiagnostics()
            };

            return Ok(diagnostics);
        }

        #region Private Methods

        private async Task<DependencyCheck> CheckDatabaseAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var connection = _databaseConnection.CreateConnection();
                connection.Open(); 

                stopwatch.Stop();

                return new DependencyCheck
                {
                    Name = "MySQL Database",
                    IsHealthy = true,
                    ResponseTime = stopwatch.ElapsedMilliseconds,
                    Message = "Connection successful"
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Database health check failed");

                return new DependencyCheck
                {
                    Name = "MySQL Database",
                    IsHealthy = false,
                    ResponseTime = stopwatch.ElapsedMilliseconds,
                    Message = $"Connection failed: {ex.Message}"
                };
            }
        }

        private async Task<DependencyCheck> CheckExternalApiAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var games = await _espnApiService.GetGamesByDateAsync(DateTime.Today);
                stopwatch.Stop();

                var isHealthy = games != null;

                return new DependencyCheck
                {
                    Name = "ESPN API",
                    IsHealthy = isHealthy,
                    ResponseTime = stopwatch.ElapsedMilliseconds,
                    Message = isHealthy ? $"ESPN API responding, {games?.Count() ?? 0} games found for today" : "ESPN API returned no data"
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogWarning(ex, "ESPN API health check failed");

                return new DependencyCheck
                {
                    Name = "ESPN API",
                    IsHealthy = false,
                    ResponseTime = stopwatch.ElapsedMilliseconds,
                    Message = $"ESPN API call failed: {ex.Message}"
                };
            }
        }

        private DependencyCheck CheckConfiguration()
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(_configuration.GetConnectionString("MySqlConnection")))
                errors.Add("Database connection string missing");

            var espnBaseUrl = _configuration["ExternalApis:ESPN:BaseUrl"];
            if (string.IsNullOrEmpty(espnBaseUrl))
                errors.Add("ESPN API base URL missing");

            return new DependencyCheck
            {
                Name = "Configuration",
                IsHealthy = errors.Count == 0,
                Message = errors.Count == 0 ? "All required configurations present" : string.Join("; ", errors)
            };
        }

        private HealthCheckResponse BuildHealthResponse(HealthReport report)
        {
            var checks = report.Entries.Select(entry => new HealthCheckResult
            {
                Name = entry.Key,
                Status = entry.Value.Status.ToString().ToLower(),
                Description = entry.Value.Description,
                Duration = entry.Value.Duration.TotalMilliseconds,
                Tags = entry.Value.Tags.ToList(),
                Error = entry.Value.Exception?.Message
            }).ToList();

            var response = new HealthCheckResponse
            {
                Status = report.Status.ToString().ToLower(),
                Timestamp = DateTime.UtcNow,
                Duration = report.TotalDuration.TotalMilliseconds,
                Checks = checks,
                Summary = new HealthCheckSummary
                {
                    Healthy = checks.Count(c => c.Status == "healthy"),
                    Degraded = checks.Count(c => c.Status == "degraded"),
                    Unhealthy = checks.Count(c => c.Status == "unhealthy"),
                    Total = checks.Count
                }
            };

            return response;
        }

        private ProcessInfo GetProcessInfo()
        {
            var process = Process.GetCurrentProcess();

            return new ProcessInfo
            {
                Id = process.Id,
                Name = process.ProcessName,
                StartTime = process.StartTime,
                TotalProcessorTime = process.TotalProcessorTime.TotalSeconds,
                UserProcessorTime = process.UserProcessorTime.TotalSeconds,
                WorkingSetMB = process.WorkingSet64 / (1024 * 1024),
                PrivateMemoryMB = process.PrivateMemorySize64 / (1024 * 1024),
                VirtualMemoryMB = process.VirtualMemorySize64 / (1024 * 1024),
                ThreadCount = process.Threads.Count,
                HandleCount = process.HandleCount
            };
        }

        private MemoryDiagnostics GetMemoryDiagnostics()
        {
            var process = Process.GetCurrentProcess();

            return new MemoryDiagnostics
            {
                WorkingSetMB = process.WorkingSet64 / (1024 * 1024),
                PrivateMemoryMB = process.PrivateMemorySize64 / (1024 * 1024),
                VirtualMemoryMB = process.VirtualMemorySize64 / (1024 * 1024),
                GCMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024),
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2),
                TotalAllocatedBytes = GC.GetTotalAllocatedBytes(),
                LargeObjectHeapSize = GC.GetTotalMemory(false)
            };
        }

        private PerformanceDiagnostics GetPerformanceDiagnostics()
        {
            var process = Process.GetCurrentProcess();

            return new PerformanceDiagnostics
            {
                TotalProcessorTimeSeconds = process.TotalProcessorTime.TotalSeconds,
                UserProcessorTimeSeconds = process.UserProcessorTime.TotalSeconds,
                PrivilegedProcessorTimeSeconds = process.PrivilegedProcessorTime.TotalSeconds,
                ThreadCount = process.Threads.Count,
                HandleCount = process.HandleCount,
                PeakWorkingSetMB = process.PeakWorkingSet64 / (1024 * 1024),
                PeakVirtualMemoryMB = process.PeakVirtualMemorySize64 / (1024 * 1024)
            };
        }

        private async Task<List<DependencyDiagnostic>> GetDependenciesDiagnosticsAsync()
        {
            var dependencies = new List<DependencyDiagnostic>();

            // Database
            dependencies.Add(new DependencyDiagnostic
            {
                Name = "MySQL Database",
                Type = "Database",
                ConnectionString = MaskConnectionString(_configuration.GetConnectionString("MySqlConnection")),
                Status = (await CheckDatabaseAsync()).IsHealthy ? "Connected" : "Disconnected"
            });

            // External API
            dependencies.Add(new DependencyDiagnostic
            {
                Name = "ESPN API",
                Type = "External API",
                ConnectionString = _configuration["ExternalApis:ESPN:BaseUrl"] ?? "Not configured",
                Status = (await CheckExternalApiAsync()).IsHealthy ? "Available" : "Unavailable"
            });

            return dependencies;
        }

        private ConfigurationDiagnostics GetConfigurationDiagnostics()
        {
            return new ConfigurationDiagnostics
            {
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                ConfigurationSources = _configuration.AsEnumerable()
                    .Take(10)
                    .Select(kvp => $"{kvp.Key}={MaskSensitiveValue(kvp.Key, kvp.Value)}")
                    .ToList(),
                EnvironmentVariables = Environment.GetEnvironmentVariables()
                    .Cast<System.Collections.DictionaryEntry>()
                    .Where(e => e.Key.ToString()?.StartsWith("ASPNETCORE_") == true)
                    .Select(e => $"{e.Key}={e.Value}")
                    .ToList()
            };
        }

        private static string FormatUptime(TimeSpan uptime)
        {
            if (uptime.TotalDays >= 1)
                return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
            if (uptime.TotalHours >= 1)
                return $"{uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
            if (uptime.TotalMinutes >= 1)
                return $"{uptime.Minutes}m {uptime.Seconds}s";
            return $"{uptime.Seconds}s";
        }

        private static string MaskConnectionString(string? connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                return "Not configured";

            var pattern = @"(password|pwd)=([^;]+)";
            return System.Text.RegularExpressions.Regex.Replace(
                connectionString,
                pattern,
                "$1=****",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        private static string MaskSensitiveValue(string key, string? value)
        {
            if (string.IsNullOrEmpty(value))
                return "null";

            var sensitiveKeys = new[] { "password", "pwd", "key", "secret", "token", "connectionstring" };

            if (sensitiveKeys.Any(k => key.ToLower().Contains(k)))
                return "****";

            return value.Length > 50 ? value.Substring(0, 47) + "..." : value;
        }

        #endregion
    }

    #region DTOs

    public class LivenessCheck
    {
        public string Status { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string Service { get; set; } = "";
    }

    public class ReadinessCheck
    {
        public string Status { get; set; } = "";
        public bool IsReady { get; set; }
        public DateTime Timestamp { get; set; }
        public List<DependencyCheck> Checks { get; set; } = new();
    }

    public class DependencyCheck
    {
        public string Name { get; set; } = "";
        public bool IsHealthy { get; set; }
        public long ResponseTime { get; set; }
        public string Message { get; set; } = "";
    }

    public class HealthCheckResponse
    {
        public string Status { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public double Duration { get; set; }
        public string? Error { get; set; }
        public List<HealthCheckResult> Checks { get; set; } = new();
        public HealthCheckSummary Summary { get; set; } = new();
    }

    public class HealthCheckResult
    {
        public string Name { get; set; } = "";
        public string Status { get; set; } = "";
        public string? Description { get; set; }
        public double Duration { get; set; }
        public List<string> Tags { get; set; } = new();
        public string? Error { get; set; }
    }

    public class HealthCheckSummary
    {
        public int Healthy { get; set; }
        public int Degraded { get; set; }
        public int Unhealthy { get; set; }
        public int Total { get; set; }
    }

    public class SystemInfo
    {
        public ApplicationInfo Application { get; set; } = new();
        public SystemDetails System { get; set; } = new();
        public ProcessInfo Process { get; set; } = new();
        public FeaturesInfo Features { get; set; } = new();
    }

    public class ApplicationInfo
    {
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string Environment { get; set; } = "";
        public DateTime StartTime { get; set; }
        public UptimeInfo Uptime { get; set; } = new();
    }

    public class UptimeInfo
    {
        public int Days { get; set; }
        public int Hours { get; set; }
        public int Minutes { get; set; }
        public int Seconds { get; set; }
        public long TotalSeconds { get; set; }
        public string Formatted { get; set; } = "";
    }

    public class SystemDetails
    {
        public string MachineName { get; set; } = "";
        public string OSVersion { get; set; } = "";
        public int ProcessorCount { get; set; }
        public bool Is64BitProcess { get; set; }
        public bool Is64BitOS { get; set; }
        public string DotNetVersion { get; set; } = "";
        public string TimeZone { get; set; } = "";
        public DateTime CurrentTime { get; set; }
        public DateTime UtcTime { get; set; }
    }

    public class ProcessInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public DateTime StartTime { get; set; }
        public double TotalProcessorTime { get; set; }
        public double UserProcessorTime { get; set; }
        public long WorkingSetMB { get; set; }
        public long PrivateMemoryMB { get; set; }
        public long VirtualMemoryMB { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
    }

    public class FeaturesInfo
    {
        public bool SwaggerEnabled { get; set; }
        public bool RateLimitingEnabled { get; set; }
        public bool CachingEnabled { get; set; }
        public bool BackgroundSyncEnabled { get; set; }
    }

    public class DiagnosticsInfo
    {
        public DateTime Timestamp { get; set; }
        public MemoryDiagnostics Memory { get; set; } = new();
        public PerformanceDiagnostics Performance { get; set; } = new();
        public List<DependencyDiagnostic> Dependencies { get; set; } = new();
        public ConfigurationDiagnostics Configuration { get; set; } = new();
    }

    public class MemoryDiagnostics
    {
        public long WorkingSetMB { get; set; }
        public long PrivateMemoryMB { get; set; }
        public long VirtualMemoryMB { get; set; }
        public long GCMemoryMB { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
        public long TotalAllocatedBytes { get; set; }
        public long LargeObjectHeapSize { get; set; }
    }

    public class PerformanceDiagnostics
    {
        public double TotalProcessorTimeSeconds { get; set; }
        public double UserProcessorTimeSeconds { get; set; }
        public double PrivilegedProcessorTimeSeconds { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public long PeakWorkingSetMB { get; set; }
        public long PeakVirtualMemoryMB { get; set; }
    }

    public class DependencyDiagnostic
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string ConnectionString { get; set; } = "";
        public string Status { get; set; } = "";
    }

    public class ConfigurationDiagnostics
    {
        public string Environment { get; set; } = "";
        public List<string> ConfigurationSources { get; set; } = new();
        public List<string> EnvironmentVariables { get; set; } = new();
    }

    #endregion
}