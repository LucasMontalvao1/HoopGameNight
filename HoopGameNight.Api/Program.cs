using System;
using HoopGameNight.Api.Extensions;
using HoopGameNight.Core.Enums;
using HoopGameNight.Infrastructure.TypeHandlers;
using Serilog;
using Dapper;

// ===== CARREGAR VARIÁVEIS DE AMBIENTE PRIMEIRO =====
DotNetEnv.Env.Load("../.env");

// ===== CONFIGURAR DAPPER PARA MAPEAR SNAKE_CASE → PASCALCASE =====
DefaultTypeMap.MatchNamesWithUnderscores = true;

// ===== REGISTRAR TYPE HANDLERS PARA ENUMS =====
SqlMapper.AddTypeHandler(new PlayerPositionTypeHandler());
SqlMapper.AddTypeHandler(new PlayerPositionNonNullableTypeHandler());

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

    // Configurar connection string com variáveis de ambiente
    var dbServer = Environment.GetEnvironmentVariable("DB_SERVER");
    var dbPort = Environment.GetEnvironmentVariable("DB_PORT");
    var dbName = Environment.GetEnvironmentVariable("DB_NAME");
    var dbUser = Environment.GetEnvironmentVariable("DB_USER");
    var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");

    Log.Information("DB Config - Server: {Server}, Port: {Port}, Database: {Database}, User: {User}, HasPassword: {HasPassword}",
        dbServer ?? "NULL", dbPort ?? "NULL", dbName ?? "NULL", dbUser ?? "NULL", !string.IsNullOrEmpty(dbPassword));

    var connectionString = $"Server={dbServer};Port={dbPort};Database={dbName};Uid={dbUser};Pwd={dbPassword};CharSet=utf8mb4;AllowUserVariables=true;";
    builder.Configuration.GetSection("ConnectionStrings")["MySqlConnection"] = connectionString;

    // Configurar Serilog do appsettings.json
    builder.Host.UseSerilog((context, configuration) =>
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext());

    // ===== ADICIONAROS SERVIÇOS =====
    builder.Services.AddHoopGameNightServices(builder.Configuration);

    // ===== BUILD =====
    var app = builder.Build();
    Log.Information("Aplicação construída com sucesso");

    // ===== CONFIGURAR PIPELINE =====
    await app.ConfigureHoopGameNightPipeline();

    // ===== EXECUTAR =====
    Log.Information("Hoop Game Night API rodando em: {Urls}", string.Join(", ", app.Urls));
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