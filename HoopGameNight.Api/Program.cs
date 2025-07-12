using HoopGameNight.Api.Extensions;
using HoopGameNight.Api.Services;
using HoopGameNight.Api.Mappings;
using HoopGameNight.Core.Interfaces.Infrastructure;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Core.Services;
using HoopGameNight.Infrastructure.Data;
using HoopGameNight.Infrastructure.ExternalServices;
using HoopGameNight.Infrastructure.Repositories;
using MySqlConnector;
using System.Data;
using Serilog;
using MySqlConnection = MySqlConnector.MySqlConnection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using System.Reflection;
using Polly;
using Polly.Extensions.Http;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

try
{
    Log.Information("🚀 Starting Hoop Game Night API Configuration");

    var builder = WebApplication.CreateBuilder(args);

    // CONFIGURAR SERILOG
    builder.Host.UseSerilog((context, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration));

    // CONFIGURAR SERVIÇOS BÁSICOS
    builder.Services.AddControllers(options =>
    {
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    });

    builder.Services.AddEndpointsApiExplorer();

    // CONFIGURAR DATABASE - 
    var connectionString = builder.Configuration.GetConnectionString("MySqlConnection")
        ?? throw new InvalidOperationException("MySQL connection string not found");

    // Database abstrações na ordem correta
    builder.Services.AddScoped<IDbConnection>(sp => new MySqlConnection(connectionString));
    builder.Services.AddSingleton<IDatabaseConnection>(sp => new HoopGameNight.Infrastructure.Data.MySqlConnection(connectionString));

    //  Query Executor abstração
    builder.Services.AddScoped<IDatabaseQueryExecutor, DapperQueryExecutor>();

    builder.Services.AddSingleton<ISqlLoader, SqlLoader>();
    builder.Services.AddScoped<DatabaseInitializer>();

    // CONFIGURAR REPOSITORIES
    builder.Services.AddScoped<IGameRepository, GameRepository>();
    builder.Services.AddScoped<ITeamRepository, TeamRepository>();
    builder.Services.AddScoped<IPlayerRepository, PlayerRepository>();

    // CONFIGURAR SERVICES
    builder.Services.AddScoped<IGameService, GameService>();
    builder.Services.AddScoped<ITeamService, TeamService>();
    builder.Services.AddScoped<IPlayerService, PlayerService>();

    // CONFIGURAR EXTERNAL SERVICES (BALL DON'T LIE) - ✅ CORRIGIDO
    ConfigureBallDontLieHttpClient(builder.Services, builder.Configuration);

    // CONFIGURAR AUTOMAPPER
    builder.Services.AddAutoMapper(typeof(AutoMapperProfile));

    // CONFIGURAR CACHE
    builder.Services.AddMemoryCache();

    // ✅ HEALTH CHECKS BÁSICOS (sem custom health check por enquanto)
    ConfigureBasicHealthChecks(builder.Services, connectionString);

    // ✅ RATE LIMITING CORRIGIDO
    ConfigureRateLimiting(builder.Services, builder.Configuration);

    // CONFIGURAR SWAGGER - 
    ConfigureSwagger(builder.Services);

    // CONFIGURAR CORS
    ConfigureCors(builder.Services, builder.Configuration);

    // CONFIGURAR BACKGROUND SERVICES (OPCIONAL)
    try
    {
        builder.Services.AddHostedService<DataSyncBackgroundService>();
        Log.Information("✅ Background services configured");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "⚠️ Error configuring background services");
    }

    // BUILD APPLICATION
    var app = builder.Build();
    Log.Information("✅ Application built successfully");

    // CONFIGURAR MIDDLEWARE PIPELINE - 

    // 1. Error Handling (sempre primeiro)
    app.UseMiddleware<HoopGameNight.Api.Middleware.ErrorHandlingMiddleware>();

    // 2. Security Headers (cedo no pipeline)
    app.UseSecurityHeaders();

    // 3. Development middleware
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Hoop Game Night API V1");
            c.RoutePrefix = string.Empty;
            c.DisplayRequestDuration();
            c.EnableTryItOutByDefault();
        });
        Log.Information("✅ Swagger UI available at root path");
    }

    // 4. HTTPS Redirection
    app.UseHttpsRedirection();

    // 5. Rate Limiting (antes de routing)
    app.UseRateLimiter();

    // 6. CORS
    var corsPolicy = app.Configuration.GetSection("Cors")["PolicyName"] ?? "HoopGameNightPolicy";
    app.UseCors(corsPolicy);

    // 7. Routing
    app.UseRouting();

    // 8. Health Checks BÁSICOS
    app.MapHealthChecks("/health");

    // 9. Controllers
    app.MapControllers();

    // 10. Info endpoints
    app.MapGet("/info", () => Results.Ok(new
    {
        name = "Hoop Game Night API",
        version = "1.0.0",
        environment = app.Environment.EnvironmentName,
        timestamp = DateTime.UtcNow
    }));

    // INICIALIZAR BANCO DE DADOS -
    await InitializeDatabase(app);

    // INICIAR APLICAÇÃO
    Log.Information("🚀 Starting Hoop Game Night API");
    Log.Information("📊 Environment: {Environment}", app.Environment.EnvironmentName);
    Log.Information("🌐 Swagger available at: https://localhost:7000 (or configured port)");
    Log.Information("🏥 Health checks available at: /health");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ Application terminated unexpectedly");
    throw;
}
finally
{
    Log.Information("🏁 Shutting down Hoop Game Night API");
    await Log.CloseAndFlushAsync();
}

// ✅ MÉTODOS DE CONFIGURAÇÃO

static void ConfigureBallDontLieHttpClient(IServiceCollection services, IConfiguration configuration)
{
    services.AddHttpClient<IBallDontLieService, BallDontLieService>((serviceProvider, client) =>
    {
        var config = serviceProvider.GetRequiredService<IConfiguration>();
        var section = config.GetSection("ExternalApis:BallDontLie");

        var baseUrl = section["BaseUrl"];
        var apiKey = section["ApiKey"];

        if (string.IsNullOrEmpty(baseUrl))
            throw new InvalidOperationException("BallDontLie BaseUrl not configured");

        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("BallDontLie ApiKey not configured");

        client.BaseAddress = new Uri(baseUrl!);
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Add("User-Agent", "HoopGameNight/1.0");
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    })
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());
}

// ✅ POLLY POLICIES 
static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryCount, context) => 
            {
                Log.Warning("Retry {RetryCount} after {Delay}ms. Reason: {Reason}",
                    retryCount, timespan.TotalMilliseconds, outcome.Exception?.Message ?? "Unknown");
            });
}

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 3,
            durationOfBreak: TimeSpan.FromSeconds(30),
            onBreak: (exception, duration) => 
            {
                Log.Warning("Circuit breaker opened for {Duration}. Reason: {Reason}",
                    duration, exception.Exception?.Message ?? "Unknown");
            },
            onReset: () => Log.Information("Circuit breaker closed"));
}

// ✅ HEALTH CHECKS BÁSICOS (sem custom health check)
static void ConfigureBasicHealthChecks(IServiceCollection services, string connectionString)
{
    services.AddHealthChecks()
        .AddMySql(connectionString, name: "mysql", tags: new[] { "database", "mysql" });
    
}

// ✅ RATE LIMITING 
static void ConfigureRateLimiting(IServiceCollection services, IConfiguration configuration)
{
    services.AddRateLimiter(options =>
    {
        options.AddFixedWindowLimiter("ApiPolicy", limiterOptions =>
        {
            limiterOptions.PermitLimit = 100;
            limiterOptions.Window = TimeSpan.FromMinutes(1);
            // ✅ REMOVIDO QueueProcessingOrder - não existe no .NET 8
            limiterOptions.QueueLimit = 10;
        });

        options.OnRejected = async (context, token) =>
        {
            context.HttpContext.Response.StatusCode = 429;
            await context.HttpContext.Response.WriteAsync("Rate limit exceeded", token);
        };
    });
}

static void ConfigureSwagger(IServiceCollection services)
{
    services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Hoop Game Night API",
            Version = "v1",
            Description = "API completa para acompanhamento de jogos da NBA",
            Contact = new OpenApiContact
            {
                Name = "Support Team",
                Email = "support@hoopgamenight.com"
            }
        });

        // Include XML comments if available
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            c.IncludeXmlComments(xmlPath);
        }
    });
}

static void ConfigureCors(IServiceCollection services, IConfiguration configuration)
{
    var corsConfig = configuration.GetSection("Cors");

    services.AddCors(options =>
    {
        options.AddPolicy(corsConfig["PolicyName"] ?? "HoopGameNightPolicy", policy =>
        {
            var allowedOrigins = corsConfig.GetSection("AllowedOrigins").Get<string[]>()
                ?? new[] { "http://localhost:3000", "https://localhost:3000" };

            var allowedMethods = corsConfig.GetSection("AllowedMethods").Get<string[]>()
                ?? new[] { "GET", "POST", "PUT", "DELETE", "OPTIONS" };

            var allowedHeaders = corsConfig.GetSection("AllowedHeaders").Get<string[]>()
                ?? new[] { "*" };

            var allowCredentials = corsConfig.GetValue<bool>("AllowCredentials");

            if (allowedOrigins.Contains("*"))
            {
                policy.AllowAnyOrigin();
            }
            else
            {
                policy.WithOrigins(allowedOrigins);
            }

            policy.WithMethods(allowedMethods)
                  .WithHeaders(allowedHeaders);

            if (allowCredentials && !allowedOrigins.Contains("*"))
            {
                policy.AllowCredentials();
            }
        });
    });
}

// ✅ DATABASE INITIALIZATION SIMPLES
static async Task InitializeDatabase(WebApplication app)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var dbInitializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await dbInitializer.InitializeAsync();
        Log.Information("✅ Database initialized successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "❌ Error initializing database - continuing without it");
    }
}

// EXTENSION METHOD PARA SECURITY HEADERS
public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Add("X-Frame-Options", "DENY");
            context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
            context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
            context.Response.Headers.Add("Content-Security-Policy", "default-src 'self'");
            await next();
        });
    }
}