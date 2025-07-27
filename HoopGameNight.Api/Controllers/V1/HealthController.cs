using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace HoopGameNight.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private static readonly DateTime _startTime = DateTime.UtcNow;
        private readonly ILogger<HealthController> _logger;

        public HealthController(ILogger<HealthController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Verifica o status geral da API
        /// </summary>
        /// <returns>Dados como uptime, versão e ambiente</returns>
        [HttpGet]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(typeof(object), 500)]
        public IActionResult Get()
        {
            try
            {
                var now = DateTime.UtcNow;
                var uptimeSeconds = Math.Round((now - _startTime).TotalSeconds);

                var version = Assembly.GetExecutingAssembly()
                                      .GetName()
                                      .Version?
                                      .ToString() ?? "1.0.0";

                var response = new
                {
                    status = "healthy",
                    uptime = uptimeSeconds,
                    timestamp = now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    version,
                    environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                    server = Environment.MachineName
                };

                _logger.LogInformation("Health check OK: {@Response}", response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar a saúde da API");
                return StatusCode(500, new { status = "unhealthy", error = ex.Message });
            }
        }

        /// <summary>
        /// Verificação simples da API
        /// </summary>
        /// <returns>Confirmação textual</returns>
        [HttpGet("simple")]
        [ProducesResponseType(200)]
        public IActionResult Simple()
        {
            return Ok("API is running!");
        }
    }
}
