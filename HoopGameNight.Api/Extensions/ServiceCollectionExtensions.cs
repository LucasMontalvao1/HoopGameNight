using HoopGameNight.Api.Configurations;
using HoopGameNight.Api.Mappings;
using HoopGameNight.Core.Interfaces.Infrastructure;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Core.Services;
using HoopGameNight.Infrastructure.Data;
using HoopGameNight.Infrastructure.ExternalServices;
using HoopGameNight.Infrastructure.Repositories;
using HoopGameNight.Infrastructure.Services;
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

            // AutoMapper 
            services.AddAutoMapper(typeof(AutoMapperProfile));

            return services;
        }

        private static IServiceCollection AddConfigurations(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<ApiConfiguration>(configuration.GetSection("ApiSettings"));
            services.Configure<ExternalApiConfiguration>(configuration.GetSection("ExternalApis"));
            services.Configure<CorsConfiguration>(configuration.GetSection("Cors"));

            return services;
        }

        private static IServiceCollection AddDatabaseServices(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("MySqlConnection")
                ?? throw new InvalidOperationException("MySQL connection string not found");

            services.AddScoped<IDbConnection>(sp => new MySqlConnection(connectionString));

            services.AddSingleton<IDatabaseConnection>(provider =>
                new HoopGameNight.Infrastructure.Data.MySqlConnection(connectionString));

            services.AddScoped<IDatabaseQueryExecutor, DapperQueryExecutor>();

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
            // BALL DON'T LIE API SERVICE
            var ballDontLieConfig = configuration.GetSection("ExternalApis:BallDontLie");
            var baseUrl = ballDontLieConfig["BaseUrl"] ?? "https://api.balldontlie.io/v1";
            var apiKey = ballDontLieConfig["ApiKey"];

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

            // ESPN API SERVICE
            services.AddHttpClient<IEspnApiService, EspnApiService>(client =>
            {
                client.BaseAddress = new Uri("https://site.api.espn.com/");
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("User-Agent", "HoopGameNight/1.0");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

            return services;
        }

        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => !msg.IsSuccessStatusCode)
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        Console.WriteLine($"Tentar novamente {retryCount} após {timespan.TotalMilliseconds}ms");
                    });
        }

        private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (result, duration) =>
                    {
                        Console.WriteLine($"Disjuntor aberto por {duration.TotalSeconds}s");
                    },
                    onReset: () =>
                    {
                        Console.WriteLine("Reinicialização do disjuntor");
                    });
        }

        public static IServiceCollection AddRateLimiting(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<IpRateLimitOptions>(configuration.GetSection("RateLimiting"));
            services.AddInMemoryRateLimiting();
            services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
            services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();

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