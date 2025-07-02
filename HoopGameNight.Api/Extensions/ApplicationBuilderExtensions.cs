using HoopGameNight.Api.Middleware;
using HoopGameNight.Infrastructure.Data;
using AspNetCoreRateLimit;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace HoopGameNight.Api.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseApplicationMiddleware(this IApplicationBuilder app, IWebHostEnvironment env)
        {
            // Error Handling (deve ser o primeiro)
            app.UseMiddleware<ErrorHandlingMiddleware>();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // Request/Response Logging
            app.UseMiddleware<RequestResponseLoggingMiddleware>();

            // Security Headers
            app.UseCustomSecurityHeaders(); // ✅ MUDOU AQUI - para evitar conflito

            // Rate Limiting
            app.UseIpRateLimiting();

            return app;
        }

        // ✅ RENOMEADO para UseCustomSecurityHeaders para evitar conflito
        public static IApplicationBuilder UseCustomSecurityHeaders(this IApplicationBuilder app)
        {
            app.Use(async (context, next) =>
            {
                context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                context.Response.Headers.Add("X-Frame-Options", "DENY");
                context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
                context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
                await next();
            });

            return app;
        }

        public static IApplicationBuilder UseHealthChecksEndpoints(this IApplicationBuilder app)
        {
            app.UseHealthChecks("/health", new HealthCheckOptions
            {
                Predicate = _ => true,
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });

            app.UseHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready")
            });

            app.UseHealthChecks("/health/live", new HealthCheckOptions
            {
                Predicate = _ => false
            });

            return app;
        }

        public static async Task<IApplicationBuilder> InitializeDatabaseAsync(this IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();
            var dbInitializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();

            try
            {
                await dbInitializer.InitializeAsync();
            }
            catch (Exception ex)
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "Failed to initialize database - continuing without it"); // ✅ MUDOU AQUI - não quebra a app
                // ✅ REMOVIDO: throw; para não quebrar a aplicação se DB não estiver disponível
            }

            return app;
        }
    }
}