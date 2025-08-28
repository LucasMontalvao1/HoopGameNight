using HoopGameNight.Api.Extensions;
using Serilog;

// ===== CONFIGURAR SERILOG =====
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Iniciando Hoop Game Night API");
    Log.Information("Ambiente: {Environment}", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production");

    // ===== BUILDER =====
    var builder = WebApplication.CreateBuilder(args);

    // Configurar Serilog do appsettings.json
    builder.Host.UseSerilog((context, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration));

    // ===== ADICIONAROS SERVIÇOS =====
    builder.Services.AddHoopGameNightServices(builder.Configuration);

    // ===== BUILD =====
    var app = builder.Build();
    Log.Information("Aplicação construída com sucesso");

    // ===== CONFIGURAR PIPELINE =====
    await app.ConfigureHoopGameNightPipeline();

    // ===== EXECUTAR =====
    Log.Information("🚀 Hoop Game Night API rodando em: {Urls}", string.Join(", ", app.Urls));
    await app.RunAsync();

    return 0;
}
catch (Exception ex) when (ex.GetType().Name != "HostAbortedException")
{
    Log.Fatal(ex, "Aplicação falhou ao iniciar");
    return 1;
}
finally
{
    Log.Information("Desligando Hoop Game Night API");
    await Log.CloseAndFlushAsync();
}