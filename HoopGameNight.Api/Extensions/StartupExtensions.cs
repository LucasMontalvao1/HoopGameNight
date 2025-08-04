using HoopGameNight.Api.HealthChecks;
using HoopGameNight.Infrastructure.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;
using System.Reflection;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace HoopGameNight.Api.Extensions
{
    /// <summary>
    /// Extension methods para configurações de startup
    /// </summary>
    public static class StartupExtensions
    {
        /// <summary>
        /// Configura serviços de cache e métricas
        /// </summary>
        public static IServiceCollection AddCacheAndMetrics(this IServiceCollection services)
        {
            services.AddMemoryCache(options =>
            {
                options.CompactionPercentage = 0.25;
                options.ExpirationScanFrequency = TimeSpan.FromMinutes(5);
            });

            services.AddSingleton<ICacheService, CacheService>();
            services.AddSingleton<ISyncMetricsService, SyncMetricsService>();

            return services;
        }

        /// <summary>
        /// Configura CORS customizado
        /// </summary>
        public static IServiceCollection AddCustomCors(this IServiceCollection services, IConfiguration configuration)
        {
            var corsConfig = configuration.GetSection("Cors");

            services.AddCors(options =>
            {
                // Policy principal
                options.AddPolicy(corsConfig["PolicyName"] ?? "HoopGameNightPolicy", policy =>
                {
                    var allowedOrigins = corsConfig.GetSection("AllowedOrigins").Get<string[]>()
                        ?? new[] { "http://localhost:4200", "https://localhost:4200", "http://localhost:3000" };

                    var allowedMethods = corsConfig.GetSection("AllowedMethods").Get<string[]>()
                        ?? new[] { "GET", "POST", "PUT", "DELETE", "OPTIONS", "PATCH" };

                    var allowedHeaders = corsConfig.GetSection("AllowedHeaders").Get<string[]>()
                        ?? new[] { "Content-Type", "Authorization", "X-Requested-With", "X-API-Key" };

                    var allowCredentials = corsConfig.GetValue<bool>("AllowCredentials");
                    var maxAge = corsConfig.GetValue<int>("MaxAge", 3600);

                    if (allowedOrigins.Contains("*"))
                    {
                        policy.AllowAnyOrigin();
                    }
                    else
                    {
                        policy.WithOrigins(allowedOrigins);
                    }

                    policy.WithMethods(allowedMethods)
                          .WithHeaders(allowedHeaders)
                          .WithExposedHeaders("X-Total-Count", "X-Page", "X-Page-Size", "X-API-Warning", "X-Data-Source")
                          .SetPreflightMaxAge(TimeSpan.FromSeconds(maxAge));

                    if (allowCredentials && !allowedOrigins.Contains("*"))
                    {
                        policy.AllowCredentials();
                    }
                });

                // Policy para desenvolvimento
                options.AddPolicy("DevelopmentPolicy", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            return services;
        }

        /// <summary>
        /// Configura health checks customizados
        /// </summary>
        public static IServiceCollection AddApplicationHealthChecks(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("MySqlConnection")!;

            services.AddHealthChecks()
                .AddMySql(
                    connectionString,
                    name: "mysql",
                    tags: new[] { "database", "mysql" },
                    timeout: TimeSpan.FromSeconds(3))
                .AddCheck<CacheHealthCheck>("cache", tags: new[] { "cache" })
                .AddCheck<SyncHealthCheck>("sync", tags: new[] { "background", "sync" })
                .AddCheck<ExternalApiHealthCheck>("external-api", tags: new[] { "external", "api" });

            return services;
        }

        /// <summary>
        /// Configura options pattern
        /// </summary>
        public static IServiceCollection AddApplicationOptions(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<Options.SyncOptions>(configuration.GetSection("SyncOptions"));
            services.Configure<Options.CacheOptions>(configuration.GetSection("CacheOptions"));

            return services;
        }

        /// <summary>
        /// Adiciona endpoints customizados
        /// </summary>
        public static WebApplication MapCustomEndpoints(this WebApplication app)
        {
            app.MapGet("/info", () => Results.Ok(new
            {
                name = "Hoop Game Night API",
                version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
                environment = app.Environment.EnvironmentName,
                timestamp = DateTime.UtcNow,
                features = new
                {
                    healthChecks = true,
                    rateLimiting = true,
                    caching = true,
                    backgroundSync = true,
                    swagger = app.Environment.IsDevelopment()
                }
            })).WithName("GetApiInfo")
              .WithOpenApi()
              .AllowAnonymous();

            app.MapGet("/metrics", async (ICacheService cache, ISyncMetricsService sync) =>
            {
                var cacheStats = cache.GetStatistics();
                var syncMetrics = sync.GetMetrics();

                return Results.Ok(new
                {
                    cache = new
                    {
                        hitRate = $"{cacheStats.HitRate:P}",
                        requests = cacheStats.TotalRequests,
                        hits = cacheStats.Hits,
                        misses = cacheStats.Misses,
                        entries = cacheStats.CurrentEntries,
                        evictions = cacheStats.Evictions
                    },
                    sync = new
                    {
                        successRate = $"{syncMetrics.SuccessRate:F1}%",
                        totalSyncs = syncMetrics.TotalSyncs,
                        successful = syncMetrics.SuccessfulSyncs,
                        failed = syncMetrics.FailedSyncs,
                        lastSync = syncMetrics.LastSyncTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never",
                        uptime = syncMetrics.Uptime.ToString(@"dd\.hh\:mm\:ss")
                    },
                    timestamp = DateTime.UtcNow
                });
            }).WithName("GetMetrics")
              .WithOpenApi()
              .AllowAnonymous();

            app.MapGet("/status", () => Results.Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow
            })).WithName("GetStatus")
              .AllowAnonymous();

            return app;
        }

        /// <summary>
        /// Configura health check endpoints com formatação customizada
        /// </summary>
        public static WebApplication MapHealthCheckEndpoints(this WebApplication app)
        {
            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = WriteHealthCheckResponse,
                ResultStatusCodes =
                {
                    [HealthStatus.Healthy] = StatusCodes.Status200OK,
                    [HealthStatus.Degraded] = StatusCodes.Status200OK,
                    [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
                }
            });

            app.MapHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready"),
                ResponseWriter = WriteHealthCheckResponse
            });

            app.MapHealthChecks("/health/live", new HealthCheckOptions
            {
                Predicate = _ => false,
                ResponseWriter = WriteHealthCheckResponse
            });

            return app;
        }

        private static async Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
        {
            context.Response.ContentType = "application/json";

            var response = new
            {
                status = report.Status.ToString(),
                timestamp = DateTime.UtcNow,
                duration = report.TotalDuration.TotalMilliseconds,
                checks = report.Entries.Select(x => new
                {
                    name = x.Key,
                    status = x.Value.Status.ToString(),
                    description = x.Value.Description,
                    duration = x.Value.Duration.TotalMilliseconds,
                    tags = x.Value.Tags,
                    data = x.Value.Data
                }).OrderBy(x => x.name),
                summary = new
                {
                    healthy = report.Entries.Count(x => x.Value.Status == HealthStatus.Healthy),
                    degraded = report.Entries.Count(x => x.Value.Status == HealthStatus.Degraded),
                    unhealthy = report.Entries.Count(x => x.Value.Status == HealthStatus.Unhealthy),
                    total = report.Entries.Count
                }
            };

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
        }
    }

    /// <summary>
    /// Extension method para Security Headers
    /// </summary>
    public static class SecurityHeadersExtensions
    {
        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        {
            return app.Use(async (context, next) =>
            {
                // Security headers
                context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                context.Response.Headers.Add("X-Frame-Options", "DENY");
                context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
                context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");

                // Mais flexível para desenvolvimento
                var env = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
                var csp = env.IsDevelopment()
                    ? "default-src 'self' 'unsafe-inline' 'unsafe-eval'; img-src 'self' data: https:; font-src 'self' data:;"
                    : "default-src 'self'; img-src 'self' data: https:; font-src 'self';";

                context.Response.Headers.Add("Content-Security-Policy", csp);

                context.Response.Headers.Add("X-API-Version", "1.0");

                await next();
            });
        }
    }
}