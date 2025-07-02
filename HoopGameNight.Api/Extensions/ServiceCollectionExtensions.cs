using HoopGameNight.Api.Configurations;
using HoopGameNight.Api.Mappings;
using HoopGameNight.Core.Interfaces.Infrastructure;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Core.Services;
using HoopGameNight.Infrastructure.Data;
using HoopGameNight.Infrastructure.ExternalServices;
using HoopGameNight.Infrastructure.Repositories;
using AspNetCoreRateLimit;
using Polly;
using Polly.Extensions.Http;
using Asp.Versioning;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Data;
using MySqlConnection = MySqlConnector.MySqlConnection;

namespace HoopGameNight.Api.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Configurações
            services.AddConfigurations(configuration);

            // Database
            services.AddDatabaseServices(configuration);

            // Repositories
            services.AddRepositories();

            // Business Services
            services.AddBusinessServices();

            // External Services
            services.AddExternalServices(configuration);

            // Caching
            services.AddMemoryCache();

            // AutoMapper - CORRIGIDO
            services.AddAutoMapper(typeof(AutoMapperProfile));

            return services;
        }

        private static IServiceCollection AddConfigurations(this IServiceCollection services, IConfiguration configuration)
        {
            // Registrar apenas as configurações que existem
            services.Configure<ApiConfiguration>(configuration.GetSection("ApiSettings"));
            services.Configure<ExternalApiConfiguration>(configuration.GetSection("ExternalApis"));
            services.Configure<CorsConfiguration>(configuration.GetSection("Cors"));

            return services;
        }

        private static IServiceCollection AddDatabaseServices(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("MySqlConnection")
                ?? throw new InvalidOperationException("MySQL connection string not found");

            // Registrar IDbConnection para Dapper
            services.AddScoped<IDbConnection>(sp => new MySqlConnection(connectionString));

            // Registrar IDatabaseConnection customizado
            services.AddSingleton<IDatabaseConnection>(provider =>
                new HoopGameNight.Infrastructure.Data.MySqlConnection(connectionString));

            services.AddSingleton<ISqlLoader, SqlLoader>();
            services.AddScoped<DatabaseInitializer>();

            return services;
        }

        private static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            services.AddScoped<IGameRepository, GameRepository>();
            services.AddScoped<ITeamRepository, TeamRepository>();
            services.AddScoped<IPlayerRepository, PlayerRepository>();

            return services;
        }

        private static IServiceCollection AddBusinessServices(this IServiceCollection services)
        {
            services.AddScoped<IGameService, GameService>();
            services.AddScoped<ITeamService, TeamService>();
            services.AddScoped<IPlayerService, PlayerService>();

            return services;
        }

        private static IServiceCollection AddExternalServices(this IServiceCollection services, IConfiguration configuration)
        {
            var ballDontLieConfig = configuration.GetSection("ExternalApis:BallDontLie");
            var baseUrl = ballDontLieConfig["BaseUrl"] ?? "https://api.balldontlie.io/v1";
            var apiKey = ballDontLieConfig["ApiKey"];

            // ✅ REMOVIDO: Validação que quebrava quando API Key estava no appsettings
            // Agora funciona com API Key tanto no appsettings quanto em User Secrets

            // Configurar HttpClient com Polly (resilience)
            services.AddHttpClient<IBallDontLieService, BallDontLieService>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                if (!string.IsNullOrEmpty(apiKey))
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                }
                client.Timeout = TimeSpan.FromSeconds(ballDontLieConfig.GetValue<int>("Timeout", 30));
            })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

            return services;
        }

        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        }

        private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30));
        }

        public static IServiceCollection AddRateLimiting(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddMemoryCache();
            services.Configure<IpRateLimitOptions>(configuration.GetSection("RateLimiting"));
            services.AddInMemoryRateLimiting();
            services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

            return services;
        }

        public static IServiceCollection AddCustomApiVersioning(this IServiceCollection services)
        {
            services.AddApiVersioning(opt =>
            {
                opt.DefaultApiVersion = new ApiVersion(1, 0);
                opt.AssumeDefaultVersionWhenUnspecified = true;
                opt.ApiVersionReader = ApiVersionReader.Combine(
                    new HeaderApiVersionReader("X-API-Version"),
                    new QueryStringApiVersionReader("version")
                );
            });

            return services;
        }

        public static IServiceCollection AddCustomHealthChecks(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("MySqlConnection");
            var ballDontLieBaseUrl = configuration["ExternalApis:BallDontLie:BaseUrl"] ?? "https://api.balldontlie.io/v1";

            services.AddHealthChecks()
                .AddMySql(connectionString!, "mysql", tags: new[] { "database", "mysql" })
                .AddUrlGroup(new Uri($"{ballDontLieBaseUrl}/teams"), "balldontlie-api", tags: new[] { "external", "api" });

            return services;
        }
    }
}