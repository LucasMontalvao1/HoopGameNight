using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using HoopGameNight.Core.DTOs.Response;

namespace HoopGameNight.Api.Controllers.V1
{
    /// <summary>
    /// Interface de consulta em linguagem natural utilizando IA local para processamento de dados históricos e futuros da NBA.
    /// </summary>
    [ApiController]
    [Route("api/v1/[controller]")]
    [Produces("application/json")]
    public class AskController : BaseApiController
    {
        private readonly INbaAiService _aiService;

        public AskController(INbaAiService aiService, ILogger<AskController> logger) : base(logger)
        {
            _aiService = aiService;
        }

        /// <summary>
        /// Processa uma consulta em linguagem natural utilizando o modelo local (Ollama).
        /// </summary>
        /// <param name="request">Payload contendo a pergunta do usuário.</param>
        /// <returns>Resposta estruturada baseada exclusivamente nos registros de jogos persistidos no banco de dados.</returns>
        /// <response code="200">Pergunta processada com sucesso</response>
        /// <response code="400">Pergunta inválida</response>
        /// <response code="500">Erro interno</response>
        /// <remarks>
        /// **DADOS EXCLUSIVOS DO BANCO:**
        /// - Jogos passados, presentes e futuros salvos no MySQL
        /// - NUNCA usa dados da internet ou conhecimento prévio da IA
        /// - APENAS placares e horários salvos no banco
        /// 
        /// **Exemplos de perguntas válidas:**
        /// - "Quais jogos aconteceram ontem?"
        /// - "Qual foi o resultado do jogo do Lakers?"
        /// - "Lakers joga hoje?"
        /// - "Próximos jogos do Warriors"
        /// - "Quanto ficou SAC x IND de ontem?"
        /// 
        /// **Perguntas NÃO suportadas:**
        /// - Estatísticas de jogadores (não temos no banco)
        /// - Análises táticas
        /// - Previsões de jogos
        /// - Informações sobre lesões/transferências
        /// </remarks>
        [HttpPost]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("AskPolicy")]
        [ProducesResponseType(typeof(ApiResponse<AskResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<AskResponse>>> Ask([FromBody] AskRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                return BadRequest<AskResponse>("A pergunta não pode estar vazia");
            }

            if (request.Question.Length < 5)
            {
                return BadRequest<AskResponse>("A pergunta deve ter pelo menos 5 caracteres");
            }

            if (request.Question.Length > 500)
            {
                return BadRequest<AskResponse>("A pergunta não pode ter mais de 500 caracteres");
            }

            try
            {
                Logger.LogInformation("Pergunta de {IP}: {Question}",
                    HttpContext.Connection.RemoteIpAddress,
                    request.Question);

                var response = await _aiService.AskAsync(request);

                Logger.LogInformation("Resposta gerada: {Games} jogos analisados, cache: {Cache}",
                    response.GamesAnalyzed,
                    response.FromCache);

                return Ok(response);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao processar: {Question}", request.Question);

                return StatusCode(500, ApiResponse<AskResponse>.ErrorResult(
                    "Erro ao processar pergunta. Verifique se o Ollama está rodando."));
            }
        }

        [HttpGet("game/{gameId}")]
        [ProducesResponseType(typeof(ApiResponse<AskResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<AskResponse>>> GetGameSummary(int gameId)
        {
            try
            {
                var response = await _aiService.GetGameSummaryAsync(gameId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao gerar resumo para jogo {GameId}", gameId);
                return StatusCode(500, ApiResponse<AskResponse>.ErrorResult("Erro ao gerar resumo do jogo."));
            }
        }
    }
}