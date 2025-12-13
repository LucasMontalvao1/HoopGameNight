using HoopGameNight.Api.Constants;
using HoopGameNight.Api.Middleware;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Infrastructure.Data;
using HoopGameNight.Infrastructure.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using System.Diagnostics;
using System.Text.Json;

namespace HoopGameNight.Api.Extensions
{
    /// <summary>
    /// Extension methods para configuração do pipeline de requisições
    /// </summary>
    public static class PipelineExtensions
    {
        /// <summary>
        /// Configura o pipeline completo da aplicação Hoop Game Night
        /// </summary>
        public static async Task<WebApplication> ConfigureHoopGameNightPipeline(
            this WebApplication app)
        {
            // 1. Initialize Database
            await app.InitializeDatabaseAsync();

            // 2. Configure Middleware Pipeline
            app.ConfigureMiddleware();

            // 3. Configure Endpoints
            app.ConfigureEndpoints();

            // 4. Log Startup Information
            app.LogStartupInformation();

            return app;
        }

        #region Middleware Configuration

        private static void ConfigureMiddleware(this WebApplication app)
        {
            // 1. Exception Handler (ASP.NET Core 8+) 
            app.UseExceptionHandler();

            // 2. Request Logging (sempre ativo)
            app.UseMiddleware<RequestLoggingMiddleware>();

            // 3. Security Headers
            app.UseSecurityHeaders();

            // 4. HTTPS Redirection (Production only)
            if (!app.Environment.IsDevelopment())
            {
                app.UseHsts();
                app.UseHttpsRedirection();
            }

            // 5. Swagger (Development only)
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Main API");
                    c.SwaggerEndpoint("/swagger/admin/swagger.json", "Admin");
                    c.SwaggerEndpoint("/swagger/monitoring/swagger.json", "Monitoring");
                    c.RoutePrefix = string.Empty;
                    c.DisplayRequestDuration();
                    c.EnableTryItOutByDefault();
                    c.EnableDeepLinking();
                    c.ShowExtensions();
                    c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
                    c.DefaultModelsExpandDepth(2);
                    c.DefaultModelRendering(Swashbuckle.AspNetCore.SwaggerUI.ModelRendering.Model);
                });

                Log.Information("Swagger UI disponível em: /");
            }

            // 6. CORS
            var corsPolicy = app.Configuration["Cors:PolicyName"] ?? "HoopGameNightPolicy";
            if (app.Environment.IsDevelopment())
            {
                app.UseCors("DevelopmentPolicy");
                Log.Information("CORS: Development Policy (Allow All)");
            }
            else
            {
                app.UseCors(corsPolicy);
                Log.Information("CORS: Production Policy ({Policy})", corsPolicy);
            }

            // 7. Routing
            app.UseRouting();

            // 8. Rate Limiting
            app.UseRateLimiter();

            // 9. Authentication & Authorization 
            // app.UseAuthentication();
            // app.UseAuthorization();

            // 10. Response Compression 
            if (app.Configuration.GetValue<bool>("Performance:EnableResponseCompression", false))
            {
                app.UseResponseCompression();
            }
        }

        #endregion

        #region Endpoints Configuration

        private static void ConfigureEndpoints(this WebApplication app)
        {
            // Controllers
            app.MapControllers();

            // Health Checks
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
                ResponseWriter = async (context, report) =>
                {
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new
                    {
                        status = "Healthy",
                        timestamp = DateTime.UtcNow
                    }));
                }
            });

            // System/Management Endpoints
            app.MapSystemEndpoints();
        }

        private static void MapSystemEndpoints(this WebApplication app)
        {
            var apiGroup = app.MapGroup("/api")
                .WithTags("System")
                .AllowAnonymous();
                //.ExcludeFromDescription(); // Não mostrar no Swagger os endpoint de metrica e info

            // Info endpoint
            apiGroup.MapGet("/info", (IConfiguration config) =>
            {
                var version = config["Application:Version"] ?? "1.0.0";
                var buildDate = config["Application:BuildDate"] ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
                var uptime = GetUptime();

                return Results.Ok(new
                {
                    name = "Hoop Game Night API",
                    version,
                    buildDate,
                    environment = app.Environment.EnvironmentName,
                    timestamp = DateTime.UtcNow,
                    uptime,
                    features = new
                    {
                        healthChecks = true,
                        rateLimiting = true,
                        caching = true,
                        backgroundSync = config.GetValue<bool>("Sync:EnableAutoSync"),
                        swagger = app.Environment.IsDevelopment()
                    }
                });
            })
            .WithName("GetApiInfo")
            .Produces<object>(200);

            // Metrics endpoint
            apiGroup.MapGet("/metrics", async (
                ICacheService cache,
                ISyncMetricsService sync) =>
            {
                try
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
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Erro ao recuperar métricas");
                    return Results.Problem("Erro ao recuperar métricas", statusCode: 500);
                }
            })
            .WithName("GetMetrics")
            .Produces<object>(200)
            .Produces(500);

            // Status endpoint 
            apiGroup.MapGet("/status", () => Results.Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                uptime = GetUptime()
            }))
            .WithName("GetStatus")
            .Produces<object>(200);

            // Cache management endpoints
            if (app.Environment.IsDevelopment())
            {
                apiGroup.MapDelete("/cache/clear", (ICacheService cache) =>
                {
                    cache.Clear();
                    return Results.Ok(new { message = "Cache cleared successfully" });
                })
                .WithName("ClearCache")
                .Produces<object>(200);
            }
        }

        #endregion

        #region Database Initialization

        private static async Task InitializeDatabaseAsync(this WebApplication app)
        {
            var config = app.Configuration.GetSection("Database");
            var maxRetries = config.GetValue<int>("MaxRetryCount", 3);
            var retryDelayMs = config.GetValue<int>("RetryDelayMs", 2000);

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    Log.Information("Inicializando banco de dados (tentativa {Attempt}/{MaxRetries})",
                        attempt, maxRetries);

                    using var scope = app.Services.CreateScope();
                    var dbInitializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
                    await dbInitializer.InitializeAsync();

                    Log.Information("Banco de dados inicializado com sucesso");
                    return;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Falha na inicialização do banco (tentativa {Attempt}/{MaxRetries})",
                        attempt, maxRetries);

                    if (attempt == maxRetries)
                    {
                        var requireDb = config.GetValue<bool>("RequiredForStartup", true);

                        if (requireDb)
                        {
                            Log.Fatal("Banco de dados é obrigatório mas não pôde ser inicializado após {MaxRetries} tentativas. " +
                                     "Aplicação será encerrada.", maxRetries);
                            throw new InvalidOperationException("Falha crítica na inicialização do banco de dados");
                        }
                        else
                        {
                            Log.Warning("Banco de dados não pôde ser inicializado após {MaxRetries} tentativas. " +
                                       "Aplicação continuará em MODO SOMENTE-APIs (sem persistência local).", maxRetries);
                            Log.Information("Use /api/v1/diagnostics/connectivity para verificar status dos serviços");
                        }
                        return;
                    }

                    Log.Information("Aguardando {Delay}ms antes da próxima tentativa...", retryDelayMs);
                    await Task.Delay(retryDelayMs);
                }
            }
        }

        #endregion

        #region Security Headers

        private static void UseSecurityHeaders(this IApplicationBuilder app)
        {
            app.Use(async (context, next) =>
            {
                // Security headers baseados em OWASP recommendations
                context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                context.Response.Headers.Add("X-Frame-Options", "DENY");
                context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
                context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
                context.Response.Headers.Add("Permissions-Policy", "camera=(), microphone=(), geolocation=()");

                // API Version header
                context.Response.Headers.Add("X-API-Version", ApiConstants.API_VERSION);

                // Custom headers
                context.Response.Headers.Add("X-Service-Name", "HoopGameNight-API");

                // Request ID for tracking
                if (!context.Response.Headers.ContainsKey("X-Request-ID"))
                {
                    context.Response.Headers.Add("X-Request-ID", context.TraceIdentifier);
                }

                await next();
            });
        }

        #endregion

        #region Health Check Response

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
                    data = x.Value.Data,
                    exception = x.Value.Exception?.Message
                }).OrderBy(x => x.name),
                summary = new
                {
                    healthy = report.Entries.Count(x => x.Value.Status == HealthStatus.Healthy),
                    degraded = report.Entries.Count(x => x.Value.Status == HealthStatus.Degraded),
                    unhealthy = report.Entries.Count(x => x.Value.Status == HealthStatus.Unhealthy),
                    total = report.Entries.Count
                }
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
        }

        #endregion

        #region Startup Logging

        private static void LogStartupInformation(this WebApplication app)
        {
            var env = app.Environment;
            var urls = string.Join(", ", app.Urls);
            var config = app.Configuration;

            Log.Information("╔══════════════════════════════════════════════════════════╗");
            Log.Information("║         HOOP GAME NIGHT API - INICIADA COM SUCESSO!!!    ║");
            Log.Information("╠══════════════════════════════════════════════════════════╣");
            Log.Information("║ Environment: {Environment,-44}║", env.EnvironmentName);
            Log.Information("║ URLs: {Urls,-51}║", urls.Length > 51 ? urls.Substring(0, 48) + "..." : urls);
            Log.Information("║ Version: {Version,-48}║", config["Application:Version"] ?? "1.0.0");
            Log.Information("╠══════════════════════════════════════════════════════════╣");
            Log.Information("║ FEATURES:                                                ║");
            Log.Information("║ • Swagger UI: {SwaggerEnabled,-43} ║", env.IsDevelopment() ? "/ (root)" : "Disabled");
            Log.Information("║ • Health Checks: {HealthUrl,-40} ║", "/health");
            Log.Information("║ • Metrics: {MetricsUrl,-46} ║", "/api/metrics");
            Log.Information("║ • API Info: {InfoUrl,-45} ║", "/api/info");
            Log.Information("║ • Rate Limiting: {RateLimitEnabled,-40} ║", "Enabled");
            Log.Information("║ • CORS Policy: {CorsPolicy,-42} ║", env.IsDevelopment() ? "Development" : "Production");
            Log.Information("║ • Background Sync: {SyncEnabled,-38} ║", config.GetValue<bool>("Sync:EnableAutoSync") ? "Enabled" : "Disabled");
            Log.Information("╠══════════════════════════════════════════════════════════╣");
            Log.Information("║ DATABASE:                                                ║");
            Log.Information("║ • Provider: {Provider,-45} ║", "MySQL");
            Log.Information("║ • Connection: {Connection,-43} ║", "Configured");
            Log.Information("╠══════════════════════════════════════════════════════════╣");
            Log.Information("║ EXTERNAL APIS:                                           ║");
            Log.Information("║ • ESPN API: {ESPN,-45} ║", "Configured (Free)");
            Log.Information("╠══════════════════════════════════════════════════════════╣");
            Log.Information("║ STATUS: {Status,-48} ║", "ONLINEEEE");
            Log.Information("╚══════════════════════════════════════════════════════════╝");

            if (env.IsDevelopment())
            {
                Log.Information("");
                Log.Information("Links:");
                Log.Information("   • Swagger UI: {SwaggerUrl}", $"{urls.Split(',')[0]}/");
                Log.Information("   • Health Check: {HealthUrl}", $"{urls.Split(',')[0]}/health");
                Log.Information("   • API Metrics: {MetricsUrl}", $"{urls.Split(',')[0]}/api/metrics");
            }
        }

        #endregion

        #region Helper Methods

        private static string GetUptime()
        {
            var uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();

            if (uptime.TotalDays >= 1)
                return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";
            else if (uptime.TotalHours >= 1)
                return $"{uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
            else
                return $"{uptime.Minutes}m {uptime.Seconds}s";
        }

        #endregion
    }
}