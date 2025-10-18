using HoopGameNight.Api.Configurations;
using HoopGameNight.Api.Constants;
using HoopGameNight.Api.Controllers.V1.Admin;
using HoopGameNight.Api.Filters;
using HoopGameNight.Api.HealthChecks;
using HoopGameNight.Api.Mappings;
using HoopGameNight.Api.Options;
using HoopGameNight.Api.Services;
using HoopGameNight.Core.Interfaces.Infrastructure;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Core.Services;
using HoopGameNight.Infrastructure.Data;
using HoopGameNight.Infrastructure.ExternalServices;
using HoopGameNight.Infrastructure.Repositories;
using HoopGameNight.Infrastructure.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using MySqlConnector;
using Polly;
using Polly.Extensions.Http;
using System.Data;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.OpenApi.Any;

namespace HoopGameNight.Api.Extensions
{
    /// <summary>
    /// Extension methods para configuração de serviços
    /// </summary>
    public static class ServiceExtensions
    {
        /// <summary>
        /// Adiciona os serviços 
        /// </summary>
        public static IServiceCollection AddHoopGameNightServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Validar configurações críticas primeiro
            ValidateCriticalConfiguration(configuration);

            // 1. Core API Setup
            services.AddApiCore();

            // 2. Configurations & Options
            services.AddApplicationConfiguration(configuration);

            // 3. Database & Repositories
            services.AddDataLayer(configuration);

            // 4. Business Services
            services.AddBusinessServices();

            // 5. External Services & HTTP Clients
            services.AddExternalServices(configuration);

            // 6. Caching
            services.AddCaching(configuration);

            // 7. Security (CORS & Rate Limiting)
            services.AddSecurity(configuration);

            // 8. Health Checks & Monitoring
            services.AddHealthAndMonitoring(configuration);

            // 9. API Documentation (Swagger/OpenAPI)
            services.AddApiDocumentation(configuration);

            // 10. Background Services
            services.AddBackgroundServices();

            // 11. API Versioning
            services.AddApiVersioning();

            return services;
        }

        #region Core API Configuration

        private static IServiceCollection AddApiCore(this IServiceCollection services)
        {
            // Controllers com Filters globais
            services.AddControllers(options =>
            {
                options.Filters.Add<ApiExceptionFilter>();
                options.Filters.Add<ValidateModelFilter>();
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            })
            .ConfigureApiBehaviorOptions(options =>
            {
                options.SuppressModelStateInvalidFilter = false;
            });

            // Configuração JSON
            services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.SerializerOptions.WriteIndented = false;
                options.SerializerOptions.PropertyNameCaseInsensitive = true;
            });

            services.AddEndpointsApiExplorer();

            // AutoMapper
            services.AddAutoMapper(typeof(AutoMapperProfile));

            return services;
        }

        #endregion

        #region Configuration & Options

        private static IServiceCollection AddApplicationConfiguration(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // API Configuration
            services.Configure<ApiConfiguration>(
                configuration.GetSection("ApiSettings"));

            // External APIs Configuration
            services.Configure<ExternalApiConfiguration>(
                configuration.GetSection("ExternalApis"));

            // CORS Configuration
            services.Configure<CorsConfiguration>(
                configuration.GetSection("Cors"));

            // Database Configuration
            services.Configure<DatabaseConfiguration>(
                configuration.GetSection("Database"));

            // Cache Options
            services.Configure<Options.CacheOptions>(
                configuration.GetSection("Cache"));

            // Sync Options
            services.Configure<SyncOptions>(
                configuration.GetSection("Sync"));

            // Rate Limit Options
            services.Configure<RateLimitOptions>(
                configuration.GetSection("RateLimit"));

            return services;
        }

        #endregion

        #region Data Layer

        private static IServiceCollection AddDataLayer(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("MySqlConnection")
                ?? throw new InvalidOperationException("MySQL connection string not found");

            // Database connections
            services.AddScoped<IDbConnection>(sp =>
                new MySqlConnector.MySqlConnection(connectionString));

            services.AddSingleton<IDatabaseConnection>(provider =>
                new Infrastructure.Data.MySqlConnection(connectionString));

            services.AddScoped<IDatabaseQueryExecutor, DapperQueryExecutor>();
            services.AddSingleton<ISqlLoader, SqlLoader>();
            services.AddScoped<DatabaseInitializer>();

            // Repositories
            services.AddScoped<IGameRepository, GameRepository>();
            services.AddScoped<ITeamRepository, TeamRepository>();
            services.AddScoped<IPlayerRepository, PlayerRepository>();
            services.AddScoped<IPlayerStatsRepository, PlayerStatsRepository>();

            return services;
        }

        #endregion

        #region Business Services

        private static IServiceCollection AddBusinessServices(this IServiceCollection services)
        {
            services.AddScoped<IGameService, GameService>();
            services.AddScoped<ITeamService, TeamService>();
            services.AddScoped<IPlayerService, PlayerService>();
            services.AddScoped<IPlayerStatsService, PlayerStatsService>();
            services.AddScoped<IPlayerStatsSyncService, PlayerStatsSyncService>();
            services.AddSingleton<ISyncHealthService, SyncHealthService>();

            return services;
        }

        #endregion

        #region External Services

        private static IServiceCollection AddExternalServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var externalApisConfig = configuration.GetSection("ExternalApis");

            // Ball Don't Lie API
            var ballDontLieConfig = externalApisConfig.GetSection("BallDontLie");
            var ballDontLieBaseUrl = ballDontLieConfig["BaseUrl"] ?? "https://api.balldontlie.io/v1";
            var ballDontLieApiKey = ballDontLieConfig["ApiKey"];
            var ballDontLieTimeout = ballDontLieConfig.GetValue<int>("Timeout", 30);

            services.AddHttpClient<IBallDontLieService, BallDontLieService>(client =>
            {
                client.BaseAddress = new Uri(ballDontLieBaseUrl);
                if (!string.IsNullOrEmpty(ballDontLieApiKey))
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {ballDontLieApiKey}");
                }
                client.Timeout = TimeSpan.FromSeconds(ballDontLieTimeout);
                client.DefaultRequestHeaders.Add("User-Agent", "HoopGameNight/1.0");
            })
            .AddPolicyHandler(GetRetryPolicy(ballDontLieConfig))
            .AddPolicyHandler(GetCircuitBreakerPolicy());

            // ESPN API
            services.AddHttpClient<IEspnApiService, EspnApiService>(client =>
            {
                client.BaseAddress = new Uri("https://site.api.espn.com/");
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("User-Agent", "HoopGameNight/1.0");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

            // NBA Stats API (usando Ball Don't Lie v1 para stats oficiais da NBA)
            services.AddHttpClient<INbaStatsApiService, NbaStatsApiService>(client =>
            {
                client.BaseAddress = new Uri(ballDontLieBaseUrl);
                if (!string.IsNullOrEmpty(ballDontLieApiKey))
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {ballDontLieApiKey}");
                }
                client.Timeout = TimeSpan.FromSeconds(ballDontLieTimeout);
                client.DefaultRequestHeaders.Add("User-Agent", "HoopGameNight/1.0");
            })
            .AddPolicyHandler(GetRetryPolicy(ballDontLieConfig))
            .AddPolicyHandler(GetCircuitBreakerPolicy());

            return services;
        }

        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(IConfigurationSection? config = null)
        {
            var retryCount = config?.GetValue<int>("RetryPolicy:RetryCount") ?? 3;

            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => !msg.IsSuccessStatusCode)
                .WaitAndRetryAsync(
                    retryCount,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        Console.WriteLine($"Retry {retryCount} after {timespan.TotalMilliseconds}ms");
                    });
        }

        private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30));
        }

        #endregion

        #region Caching

        private static IServiceCollection AddCaching(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var cacheOptions = configuration.GetSection("Cache").Get<Options.CacheOptions>()
                ?? new Options.CacheOptions();

            services.AddMemoryCache(options =>
            {
                options.CompactionPercentage = cacheOptions.CompactionPercentage;
                options.ExpirationScanFrequency = TimeSpan.FromMinutes(
                    cacheOptions.ExpirationScanFrequencyMinutes);
            });

            services.AddSingleton<ICacheService, CacheService>();

            return services;
        }

        #endregion

        #region Security

        private static IServiceCollection AddSecurity(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // CORS
            var corsConfig = configuration.GetSection("Cors").Get<CorsConfiguration>()
                ?? new CorsConfiguration();

            services.AddCors(options =>
            {
                // Main policy
                options.AddPolicy(corsConfig.PolicyName ?? "HoopGameNightPolicy", policy =>
                {
                    if (corsConfig.AllowedOrigins?.Contains("*") == true)
                    {
                        policy.AllowAnyOrigin();
                    }
                    else
                    {
                        policy.WithOrigins(corsConfig.AllowedOrigins ??
                            new[] { "http://localhost:4200", "http://localhost:3000" });
                    }

                    policy.WithMethods(corsConfig.AllowedMethods ??
                            new[] { "GET", "POST", "PUT", "DELETE", "OPTIONS", "PATCH" })
                          .WithHeaders(corsConfig.AllowedHeaders ??
                            new[] { "Content-Type", "Authorization", "X-Requested-With", "X-API-Key" })
                          .WithExposedHeaders("X-Total-Count", "X-Page", "X-Page-Size", "X-API-Version", "X-Rate-Limit-Remaining")
                          .SetPreflightMaxAge(TimeSpan.FromSeconds(3600));

                    if (corsConfig.AllowCredentials && corsConfig.AllowedOrigins?.Contains("*") != true)
                    {
                        policy.AllowCredentials();
                    }
                });

                // Development policy
                options.AddPolicy("DevelopmentPolicy", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            // Rate Limiting (usando o nativo do .NET 7+)
            var rateLimitOptions = configuration.GetSection("RateLimit").Get<RateLimitOptions>()
                ?? new RateLimitOptions();

            services.AddRateLimiter(options =>
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: GetRateLimitKey(context),
                        factory: partition => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = rateLimitOptions.PermitLimit,
                            QueueLimit = rateLimitOptions.QueueLimit,
                            Window = TimeSpan.FromMinutes(rateLimitOptions.WindowMinutes)
                        }));

                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.OnRejected = async (context, token) =>
                {
                    context.HttpContext.Response.Headers.Add("X-Rate-Limit-Retry-After",
                        TimeSpan.FromMinutes(rateLimitOptions.WindowMinutes).TotalSeconds.ToString());

                    await context.HttpContext.Response.WriteAsJsonAsync(new
                    {
                        error = "Rate limit exceeded",
                        retryAfter = TimeSpan.FromMinutes(rateLimitOptions.WindowMinutes).TotalSeconds
                    }, cancellationToken: token);
                };
            });

            return services;
        }

        private static string GetRateLimitKey(HttpContext context)
        {
            // Prioridade: API Key > User ID > IP Address
            var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
            if (!string.IsNullOrEmpty(apiKey))
                return $"api_key_{apiKey}";

            if (context.User?.Identity?.IsAuthenticated == true)
                return $"user_{context.User.Identity.Name}";

            var ip = context.Request.Headers["X-Real-IP"].FirstOrDefault() ??
                    context.Request.Headers["X-Forwarded-For"].FirstOrDefault() ??
                    context.Connection.RemoteIpAddress?.ToString() ??
                    "unknown";

            return $"ip_{ip}";
        }

        #endregion

        #region Health & Monitoring

        private static IServiceCollection AddHealthAndMonitoring(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("MySqlConnection");
            var ballDontLieUrl = configuration["ExternalApis:BallDontLie:BaseUrl"] ?? "https://api.balldontlie.io/v1";

            services.AddHealthChecks()
                .AddMySql(
                    connectionString!,
                    name: "mysql-database",
                    tags: new[] { "database", "mysql", "ready" },
                    timeout: TimeSpan.FromSeconds(3))
                .AddCheck<CacheHealthCheck>(
                    "cache-memory",
                    tags: new[] { "cache", "performance" })
                .AddCheck<SyncHealthCheck>(
                    "background-sync",
                    tags: new[] { "background", "sync" })
                .AddCheck<ExternalApiHealthCheck>(
                    "external-apis",
                    tags: new[] { "external", "api" })
                .AddCheck<BallDontLieHealthCheck>(
                    "balldontlie-api",
                    tags: new[] { "external", "api", "nba-data" },
                    timeout: TimeSpan.FromSeconds(5))
                .AddCheck<EspnApiHealthCheck>(
                    "espn-api",
                    tags: new[] { "external", "api", "nba-schedule" },
                    timeout: TimeSpan.FromSeconds(5));

            // Metrics service
            services.AddSingleton<ISyncMetricsService, SyncMetricsService>();

            return services;
        }

        #endregion

        #region API Documentation 

        private static IServiceCollection AddApiDocumentation(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var apiConfig = configuration.GetSection("ApiSettings").Get<ApiConfiguration>()
                ?? new ApiConfiguration();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = apiConfig.Title ?? "Hoop Game Night API",
                    Version = "v1",
                    Description = apiConfig.Description ?? "NBA games tracking API",
                    Contact = new OpenApiContact
                    {
                        Name = "Hoop Game Night Team",
                        Email = "support@hoopgamenight.com"
                    },
                    License = new OpenApiLicense
                    {
                        Name = "MIT",
                        Url = new Uri("https://opensource.org/licenses/MIT")
                    }
                });

                // FIX: Simplified schema naming for generics
                c.CustomSchemaIds(type =>
                {
                    if (type.IsGenericType)
                    {
                        var name = type.Name.Split('`')[0];
                        var args = type.GetGenericArguments();

                        // Special handling for ApiResponse<T>
                        if (name == "ApiResponse" && args.Length == 1)
                        {
                            var argType = args[0];
                            if (argType.IsGenericType && argType.Name.StartsWith("List"))
                            {
                                var listType = argType.GetGenericArguments()[0];
                                return $"ApiResponseListOf{listType.Name}";
                            }
                            return $"ApiResponseOf{argType.Name}";
                        }

                        // Special handling for PaginatedResponse<T>
                        if (name == "PaginatedResponse" && args.Length == 1)
                        {
                            return $"PaginatedResponseOf{args[0].Name}";
                        }

                        // Generic handling
                        var argNames = string.Join("And", args.Select(t => t.Name));
                        return $"{name}Of{argNames}";
                    }

                    // Non-generic types - use simple name without namespace
                    return type.Name;
                });

                // Add XML comments if available
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
                }

                // Simplified example values
                c.MapType<DateTime>(() => new OpenApiSchema
                {
                    Type = "string",
                    Format = "date-time",
                    Example = new OpenApiString(DateTime.Today.ToString("yyyy-MM-dd"))
                });

                // Add operation filter to simplify responses
                c.OperationFilter<SimplifyResponsesOperationFilter>();

                // Add JWT Authentication (quando implementar)
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });

                // Support for nullable reference types
                c.SupportNonNullableReferenceTypes();

                // Ignore obsolete
                c.IgnoreObsoleteActions();
                c.IgnoreObsoleteProperties();
            });

            return services;
        }

        #endregion

        #region Background Services

        private static IServiceCollection AddBackgroundServices(this IServiceCollection services)
        {
            services.AddHostedService<DataSyncBackgroundService>();
            services.AddHostedService<PlayerStatsSyncBackgroundService>();
            return services;
        }

        #endregion

        #region API Versioning 

        private static IServiceCollection AddApiVersioning(this IServiceCollection services)
        {
            services.AddApiVersioning(opt =>
            {
                opt.DefaultApiVersion = new ApiVersion(1, 0);
                opt.AssumeDefaultVersionWhenUnspecified = true; 
                opt.ReportApiVersions = true;

                opt.ApiVersionReader = new HeaderApiVersionReader("X-API-Version");
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

            return services;
        }

        #endregion

        #region Validation

        private static void ValidateCriticalConfiguration(IConfiguration configuration)
        {
            var errors = new List<string>();

            // Database connection
            if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("MySqlConnection")))
            {
                errors.Add("MySQL connection string is missing");
            }

            // API Key for sync
            var syncEnabled = configuration.GetValue<bool>("Sync:EnableAutoSync");
            var apiKey = configuration["ExternalApis:BallDontLie:ApiKey"];

            if (syncEnabled && string.IsNullOrWhiteSpace(apiKey))
            {
                Console.WriteLine("WARNING: Sync is enabled but BallDontLie API key is missing");
            }

            if (errors.Any())
            {
                throw new InvalidOperationException(
                    $"Critical configuration missing: {string.Join(", ", errors)}");
            }
        }

        #endregion
    }

    public class SimplifyResponsesOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            foreach (var response in operation.Responses)
            {
                if (response.Value.Content.TryGetValue("application/json", out var mediaType))
                {
                    if (mediaType.Schema?.Reference?.Id?.Contains("[[") == true)
                    {
                        var originalId = mediaType.Schema.Reference.Id;
                        var simplifiedId = originalId
                            .Replace("[[", "<")
                            .Replace("]]", ">")
                            .Replace(",", ", ");

                        mediaType.Schema.Reference.Id = simplifiedId;
                    }
                }

                if (string.IsNullOrWhiteSpace(response.Value.Description))
                {
                    response.Value.Description = response.Key switch
                    {
                        "200" => "Operação concluída com sucesso",
                        "400" => "Requisição inválida",
                        "401" => "Não autorizado",
                        "403" => "Acesso negado",
                        "404" => "Recurso não encontrado",
                        "500" => "Erro interno do servidor",
                        _ => "Resposta não documentada"
                    };
                }
            }

            foreach (var response in operation.Responses.Values)
            {
                response.Headers ??= new Dictionary<string, OpenApiHeader>();

                response.Headers["X-Request-ID"] = new OpenApiHeader
                {
                    Description = "Unique request identifier",
                    Schema = new OpenApiSchema { Type = "string" }
                };

                response.Headers["X-Correlation-ID"] = new OpenApiHeader
                {
                    Description = "Correlation ID for tracing requests across services",
                    Schema = new OpenApiSchema { Type = "string" }
                };
            }
        }
    }
}