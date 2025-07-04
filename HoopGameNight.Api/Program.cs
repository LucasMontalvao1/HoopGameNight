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

    // CONFIGURAR DATABASE
    var connectionString = builder.Configuration.GetConnectionString("MySqlConnection")
        ?? throw new InvalidOperationException("MySQL connection string not found");

    builder.Services.AddScoped<IDbConnection>(sp => new MySqlConnection(connectionString));
    builder.Services.AddSingleton<IDatabaseConnection>(sp => new HoopGameNight.Infrastructure.Data.MySqlConnection(connectionString));
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

    // CONFIGURAR EXTERNAL SERVICES (BALL DON'T LIE)
    var ballDontLieConfig = builder.Configuration.GetSection("ExternalApis:BallDontLie");
    var baseUrl = ballDontLieConfig["BaseUrl"] ?? "https://api.balldontlie.io/v1";
    var apiKey = ballDontLieConfig["ApiKey"];

    builder.Services.AddHttpClient<IBallDontLieService, BallDontLieService>((serviceProvider, client) =>
    {
        // ✅ OBTER CONFIGURAÇÃO DO SERVICE PROVIDER
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var ballDontLieConfig = configuration.GetSection("ExternalApis:BallDontLie");

        var baseUrl = ballDontLieConfig["BaseUrl"];
        var apiKey = ballDontLieConfig["ApiKey"];

        // ✅ VALIDAÇÕES
        if (string.IsNullOrEmpty(baseUrl))
            throw new InvalidOperationException("BallDontLie BaseUrl not configured in appsettings");

        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("BallDontLie ApiKey not configured");

        // ✅ CONFIGURAÇÃO DO CLIENT
        client.BaseAddress = new Uri(baseUrl!);
        client.Timeout = TimeSpan.FromSeconds(30);

        // ✅ HEADERS PADRÃO (sem Authorization aqui)
        client.DefaultRequestHeaders.Add("User-Agent", "HoopGameNight/1.0");
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    });

    // CONFIGURAR AUTOMAPPER
    builder.Services.AddAutoMapper(typeof(AutoMapperProfile));

    // CONFIGURAR CACHE
    builder.Services.AddMemoryCache();

    // CONFIGURAR SWAGGER
    try
    {
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "Hoop Game Night API",
                Version = "v1",
                Description = "API completa para acompanhamento de jogos da NBA"
            });
        });
        Log.Information("✅ Swagger documentation configured");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "⚠️ Error configuring Swagger - continuing without it");
    }

    // CONFIGURAR CORS
    try
    {
        ConfigureCors(builder.Services, builder.Configuration);
        Log.Information("✅ CORS configured");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "⚠️ Error configuring CORS - using default policy");
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
                policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        });
    }

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

    // CONFIGURAR MIDDLEWARE PIPELINE

    // Error Handling 
    app.UseMiddleware<HoopGameNight.Api.Middleware.ErrorHandlingMiddleware>();

    // Development middleware
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();

        try
        {
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
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Swagger UI not available");
        }
    }

    // Security Headers
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Add("X-Frame-Options", "DENY");
        context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
        await next();
    });

    // CORS
    var corsPolicy = app.Configuration.GetSection("Cors")["PolicyName"] ?? "HoopGameNightPolicy";
    app.UseCors(corsPolicy);

    // Standard pipeline
    app.UseHttpsRedirection();
    app.UseRouting();

    // Map controllers
    app.MapControllers();

    // HEALTH CHECKS SIMPLES
    app.MapGet("/health", () => Results.Ok(new
    {
        status = "healthy",
        timestamp = DateTime.UtcNow,
        environment = app.Environment.EnvironmentName
    }));

    app.MapGet("/info", () => Results.Ok(new
    {
        name = "Hoop Game Night API",
        version = "1.0.0",
        environment = app.Environment.EnvironmentName,
        timestamp = DateTime.UtcNow
    }));

    // INICIALIZAR BANCO DE DADOS
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

    // INICIAR APLICAÇÃO
    Log.Information("🚀 Starting Hoop Game Night API");
    Log.Information("📊 Environment: {Environment}", app.Environment.EnvironmentName);
    Log.Information("🌐 Swagger available at: https://localhost:7000 (or configured port)");

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

// MÉTODOS DE CONFIGURAÇÃO
static void ConfigureCors(IServiceCollection services, IConfiguration configuration)
{
    var corsConfig = configuration.GetSection("Cors");

    services.AddCors(options =>
    {
        options.AddPolicy(corsConfig["PolicyName"] ?? "HoopGameNightPolicy", policy =>
        {
            var allowedOrigins = corsConfig.GetSection("AllowedOrigins").Get<string[]>()
                ?? new[] { "http://localhost:3000", "https://localhost:3000", "http://localhost:4200", "https://localhost:4200" };

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