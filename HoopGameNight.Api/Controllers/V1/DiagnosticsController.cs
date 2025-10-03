using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Core.Interfaces.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace HoopGameNight.Api.Controllers.V1
{
    [Route("api/v1/diagnostics")]
    public class DiagnosticsController : BaseApiController
    {
        private readonly IDatabaseConnection _databaseConnection;
        private readonly IBallDontLieService _ballDontLieService;
        private readonly IEspnApiService _espnApiService;

        public DiagnosticsController(
            IDatabaseConnection databaseConnection,
            IBallDontLieService ballDontLieService,
            IEspnApiService espnApiService,
            ILogger<DiagnosticsController> logger) : base(logger)
        {
            _databaseConnection = databaseConnection;
            _ballDontLieService = ballDontLieService;
            _espnApiService = espnApiService;
        }

        /// <summary>
        /// Verifica a conectividade com todos os serviços
        /// </summary>
        [HttpGet("connectivity")]
        public async Task<ActionResult<ApiResponse<object>>> CheckConnectivity()
        {
            try
            {
                var diagnostics = new
                {
                    Database = await CheckDatabaseAsync(),
                    BallDontLieApi = await CheckBallDontLieAsync(),
                    EspnApi = await CheckEspnAsync(),
                    Timestamp = DateTime.UtcNow
                };

                return base.Ok((object)diagnostics);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro no diagnóstico de conectividade");
                return StatusCode(500, new { Success = false, Message = "Erro interno do servidor", Error = ex.Message });
            }
        }

        /// <summary>
        /// Testa apenas a conexão com o banco de dados
        /// </summary>
        [HttpGet("database")]
        public async Task<ActionResult<ApiResponse<object>>> CheckDatabase()
        {
            try
            {
                var result = await CheckDatabaseAsync();
                return base.Ok(result);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro no teste do banco");
                return StatusCode(500, new { Success = false, Message = "Erro interno do servidor", Error = ex.Message });
            }
        }

        /// <summary>
        /// Testa as APIs externas
        /// </summary>
        [HttpGet("external-apis")]
        public async Task<ActionResult<ApiResponse<object>>> CheckExternalApis()
        {
            try
            {
                var result = new
                {
                    BallDontLie = await CheckBallDontLieAsync(),
                    Espn = await CheckEspnAsync()
                };

                return base.Ok((object)result);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro no teste de APIs externas");
                return StatusCode(500, new { Success = false, Message = "Erro interno do servidor", Error = ex.Message });
            }
        }

        private async Task<object> CheckDatabaseAsync()
        {
            try
            {
                var isConnected = await _databaseConnection.TestConnectionAsync();

                if (isConnected)
                {
                    using var connection = _databaseConnection.CreateConnection();
                    if (connection is MySqlConnector.MySqlConnection mySqlConnection)
                    {
                        await mySqlConnection.OpenAsync();

                        using var command = mySqlConnection.CreateCommand();
                        command.CommandText = "SELECT 1";
                        var result = await command.ExecuteScalarAsync();

                    return new
                    {
                            Status = "Connected",
                            Message = "Conexão com banco de dados bem-sucedida",
                            ConnectionState = connection.State.ToString(),
                            TestQuery = "SELECT 1",
                            Result = result?.ToString()
                        };
                    }
                    else
                    {
                        return new
                        {
                            Status = "Failed",
                            Message = "Tipo de conexão não suportado",
                            Error = "Expected MySqlConnection"
                        };
                    }
                }
                else
                {
                    return new
                    {
                        Status = "Failed",
                        Message = "Teste de conectividade falhou",
                        Error = "Connection test returned false"
                    };
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Falha na conexão com banco de dados");
                return new
                {
                    Status = "Failed",
                    Message = "Falha na conexão com banco de dados",
                    Error = ex.Message,
                    ErrorType = ex.GetType().Name
                };
            }
        }

        private async Task<object> CheckBallDontLieAsync()
        {
            try
            {
                var teams = await _ballDontLieService.GetAllTeamsAsync();
                var teamCount = teams.Count();

                return new
                {
                    Status = "Connected",
                    Message = "API Ball Don't Lie funcionando",
                    TeamsCount = teamCount,
                    SampleData = teamCount > 0 ? teams.Take(2).Select(t => t.FullName) : null
                };
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Falha na conexão com Ball Don't Lie API");
                return new
                {
                    Status = "Failed",
                    Message = "Falha na API Ball Don't Lie",
                    Error = ex.Message,
                    ErrorType = ex.GetType().Name
                };
            }
        }

        private async Task<object> CheckEspnAsync()
        {
            try
            {
                // Teste básico do ESPN - buscar jogos de hoje
                var games = await _espnApiService.GetGamesByDateAsync(DateTime.Today);
                var gameCount = games.Count();

                return new
                {
                    Status = "Connected",
                    Message = "API ESPN funcionando",
                    TodayGamesCount = gameCount,
                    SampleData = gameCount > 0 ? games.Take(2).Select(g => new { HomeTeam = g.HomeTeamName, AwayTeam = g.AwayTeamName }) : null
                };
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Falha na conexão com ESPN API");
                return new
                {
                    Status = "Failed",
                    Message = "Falha na API ESPN",
                    Error = ex.Message,
                    ErrorType = ex.GetType().Name
                };
            }
        }
    }
}