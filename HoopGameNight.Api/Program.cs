using HoopGameNight.Api.Extensions;
using HoopGameNight.Api.Options;
using HoopGameNight.Api.Services;
using HoopGameNight.Api.Mappings;
using HoopGameNight.Infrastructure.Data;
using HoopGameNight.Infrastructure.Services;
using Serilog;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// ===== CONFIGURAR SERILOG =====
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

try
{
    Log.Information("Iniciando a Configuração da API do Hoop Game Night");

    // ===== CONFIGURAR SERVIÇOS =====
    ConfigureServices(builder.Services, builder.Configuration);

    // ===== BUILD APPLICATION =====
    Log.Information("Aplicação Versão: {Version}", builder.Configuration["Application:Version"] ?? "1.0.0");
    var app = builder.Build();
    Log.Information("Aplicação construída com sucesso");

    // ===== CONFIGURAR PIPELINE =====
    ConfigurePipeline(app);

    // ===== INICIALIZAR BANCO DE DADOS =====
    await InitializeDatabaseWithRetryAsync(app);

    // ===== LOG DE STARTUP =====
    LogStartupInformation(app);

    // ===== EXECUTAR APLICAÇÃO =====
    Log.Information("A API do Hoop Game Night já está em execução");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "O aplicativo foi encerrado inesperadamente");
    return 1; 
}
finally
{
    Log.Information("Desligando a API do Hoop Game Night");
    await Log.CloseAndFlushAsync();
}

return 0;

// ========== MÉTODOS DE CONFIGURAÇÃO ==========

static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    Log.Information("Configurando serviços...");

    services.AddControllers(options =>
    {
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    });

    services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.SerializerOptions.WriteIndented = false; 
        options.SerializerOptions.PropertyNameCaseInsensitive = true;
    });

    services.AddEndpointsApiExplorer();

    // Validação de configuração crítica
    ValidateCriticalConfiguration(configuration);

    // Cache e Métricas 
    services.AddCacheAndMetrics();

    // Serviços da aplicação
    services.AddApplicationServices(configuration);

    // Options Pattern
    services.AddApplicationOptions(configuration);

    // Health Checks
    services.AddCustomHealthChecks(configuration);

    // Rate Limiting
    services.AddRateLimiting(configuration);

    // Swagger
    services.AddSwaggerDocumentation(configuration);

    // CORS
    services.AddCustomCors(configuration);

    // Background Services
    services.AddHostedService<DataSyncBackgroundService>();

    Log.Information("Serviços configurados com sucesso");
}

static void ConfigurePipeline(WebApplication app)
{
    Log.Information("Configurando o pipeline de solicitação...");

    // Middleware de segurança
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts(); 
        app.UseHttpsRedirection();
    }

    // Middleware customizado
    app.UseApplicationMiddleware(app.Environment);

    // Development middleware
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Hoop Game Night API V1");
            c.RoutePrefix = string.Empty;
            c.DisplayRequestDuration();
            c.EnableTryItOutByDefault();
            c.EnableDeepLinking();
            c.ShowExtensions();
            c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
        });
    }

    // 4. CORS 
    var corsPolicy = app.Configuration.GetSection("Cors")["PolicyName"] ?? "HoopGameNightPolicy";
    app.UseCors(corsPolicy);

    // 5. Routing
    app.UseRouting();

    // 6. Rate Limiting 
   // app.UseRateLimiter();

    // 7. Authentication/Authorization 
    // app.UseAuthentication();
    // app.UseAuthorization();

    // 8. Controllers
    app.MapControllers();

    // 9. Health Checks
    app.UseHealthChecksEndpoints();

    // 10. Endpoints customizados
    MapCustomEndpoints(app);

    Log.Information("O pipeline de solicitação foi configurado com sucesso");
}

static void MapCustomEndpoints(WebApplication app)
{
    // Info endpoint com mais detalhes
    app.MapGet("/info", (IConfiguration config) =>
    {
        var version = config["Application:Version"] ?? "1.0.0";
        var buildDate = config["Application:BuildDate"] ?? DateTime.UtcNow.ToString("yyyy-MM-dd");

        return Results.Ok(new
        {
            name = "Hoop Game Night API",
            version,
            buildDate,
            environment = app.Environment.EnvironmentName,
            timestamp = DateTime.UtcNow,
            uptime = GetUptime()
        });
    })
    .WithName("GetApiInfo")
    .WithTags("System")
    .AllowAnonymous()
    .Produces<object>(200);

    app.MapGet("/metrics", async (ICacheService cache, ISyncMetricsService sync) =>
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
                    lastSync = syncMetrics.LastSyncTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never"
                },
                timestamp = DateTime.UtcNow,
                uptime = GetUptime()
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Erro ao recuperar métricas");
            return Results.Problem("Erro ao recuperar métricas", statusCode: 500);
        }
    })
    .WithName("GetMetrics")
    .WithTags("Monitoring")
    .AllowAnonymous()
    .Produces<object>(200)
    .Produces(500);

    app.MapGet("/status", () => Results.Ok(new
    {
        status = "healthy",
        timestamp = DateTime.UtcNow,
        uptime = GetUptime()
    }))
    .WithName("GetStatus")
    .WithTags("System")
    .AllowAnonymous()
    .Produces<object>(200);
}

static async Task InitializeDatabaseWithRetryAsync(WebApplication app)
{
    const int maxRetries = 3;
    const int delayMs = 2000;

    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            Log.Information("Inicializando banco de dados (attempt {Attempt}/{MaxRetries})", attempt, maxRetries);
            await app.InitializeDatabaseAsync();
            Log.Information("Banco de dados inicializado com sucesso");
            return;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "A inicialização do banco de dados falhou na tentativa {Attempt}/{MaxRetries}", attempt, maxRetries);

            if (attempt == maxRetries)
            {
                Log.Fatal("A inicialização do banco de dados falhou após {MaxRetries} tentativas", maxRetries);
                throw;
            }

            Log.Information("Aguardando {DelayMs}ms antes da próxima tentativa...", delayMs);
            await Task.Delay(delayMs);
        }
    }
}

static void ValidateCriticalConfiguration(IConfiguration configuration)
{
    Log.Information("Validando configuração crítica...");

    var criticalSettings = new[]
    {
        "ConnectionStrings:DefaultConnection",
        "SyncOptions:ApiKey",
    };

    var missingSettings = criticalSettings
        .Where(setting => string.IsNullOrWhiteSpace(configuration[setting]))
        .ToList();

    if (missingSettings.Any())
    {
        var missing = string.Join(", ", missingSettings);
        Log.Fatal("Definições de configuração críticas ausentes: {MissingSettings}", missing);
        throw new InvalidOperationException($"Configurações críticas ausentes: {missing}");
    }

    Log.Information("Configuração crítica validada com sucesso");
}

static void LogStartupInformation(WebApplication app)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    var configuration = app.Services.GetRequiredService<IConfiguration>();
    var syncOptions = configuration.GetSection("SyncOptions").Get<SyncOptions>() ?? new SyncOptions();

    // Informações básicas
    logger.LogInformation("API do Hoop Game Night iniciada com sucesso");
    logger.LogInformation("Ambiente: {Environment}", app.Environment.EnvironmentName);
    logger.LogInformation("URLs: {Urls}", string.Join(", ", app.Urls));

    // Endpoints importantes
    logger.LogInformation("Health check: /health");
    logger.LogInformation("Metrics: /metrics");
    logger.LogInformation("Info: /info");
    logger.LogInformation("Status: /status");

    // Swagger apenas em desenvolvimento
    if (app.Environment.IsDevelopment())
    {
        logger.LogInformation("Swagger UI: / (root)");
        logger.LogInformation("Recursos do modo de desenvolvimento habilitados");
    }

    // Informações de sincronização
    logger.LogInformation("Background sync: {Status}",
        syncOptions.EnableAutoSync ? "Enabled" : "Disabled");

    if (syncOptions.EnableAutoSync)
    {
        logger.LogInformation("Sync interval: {Interval} minutes", syncOptions.SyncIntervalMinutes);
    }

    // Informações de performance
    var gcMemory = GC.GetTotalMemory(false);
    logger.LogInformation("Initial memory usage: {Memory:N0} bytes", gcMemory);

    // Timezone info
    logger.LogInformation("Server timezone: {Timezone}", TimeZoneInfo.Local.DisplayName);
    logger.LogInformation("Server time: {Time}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
    logger.LogInformation("UTC time: {Time}", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
}

static string GetUptime()
{
    var uptime = DateTime.UtcNow.Subtract(Process.GetCurrentProcess().StartTime.ToUniversalTime());
    return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
}