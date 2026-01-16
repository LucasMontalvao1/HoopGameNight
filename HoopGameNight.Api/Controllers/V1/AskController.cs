using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace HoopGameNight.Api.Controllers
{
    /// <summary>
    /// Interface de consulta em linguagem natural utilizando IA local para processamento de dados históricos e futuros da NBA.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class AskController : ControllerBase
    {
        private readonly INbaAiService _aiService;
        private readonly ILogger<AskController> _logger;

        public AskController(INbaAiService aiService, ILogger<AskController> logger)
        {
            _aiService = aiService;
            _logger = logger;
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
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Ask([FromBody] AskRequest request)
        {
            // Validação detalhada
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                return BadRequest(new
                {
                    error = "Question is required",
                    message = "A pergunta não pode estar vazia",
                    example = "Quais jogos aconteceram ontem?"
                });
            }

            if (request.Question.Length < 5)
            {
                return BadRequest(new
                {
                    error = "Question too short",
                    message = "A pergunta deve ter pelo menos 5 caracteres",
                    example = "Jogos de hoje?"
                });
            }

            if (request.Question.Length > 500)
            {
                return BadRequest(new
                {
                    error = "Question too long",
                    message = "A pergunta não pode ter mais de 500 caracteres",
                    hint = "Seja mais específico, por exemplo: 'Qual foi o resultado do jogo do Lakers ontem?'"
                });
            }

            // Bloquear perguntas sobre estatísticas (não temos no banco)
            var forbiddenKeywords = new[] { "ponto", "assist", "rebound", "estatística", "stat" };
            if (forbiddenKeywords.Any(k => request.Question.ToLower().Contains(k)))
            {
                return BadRequest(new
                {
                    error = "Unsupported question type",
                    message = "No momento, não temos estatísticas de jogadores no banco de dados.",
                    supported = "Perguntas sobre resultados e horários de jogos"
                });
            }

            try
            {
                _logger.LogInformation("Pergunta de {IP}: {Question}",
                    HttpContext.Connection.RemoteIpAddress,
                    request.Question);

                var response = await _aiService.AskAsync(request);

                _logger.LogInformation("Resposta gerada: {Games} jogos analisados, cache: {Cache}",
                    response.GamesAnalyzed,
                    response.FromCache);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar: {Question}", request.Question);

                return StatusCode(500, new
                {
                    error = "Internal server error",
                    message = "Erro ao processar pergunta. Verifique se o Ollama está rodando.",
                    details = ex.Message
                });
            }
        }
    }
}