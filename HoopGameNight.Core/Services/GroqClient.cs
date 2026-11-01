using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Core.Services
{
    /// <summary>
    /// Cliente para comunicação com a API do Groq
    /// </summary>
    public class GroqClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GroqClient> _logger;
        private readonly string _apiKey;
        private readonly string _model;
        private const string FallbackModel = "llama-3.1-8b-instant"; 
        private const int MaxPromptLength = 32000; 

        public GroqClient(HttpClient httpClient, ILogger<GroqClient> logger, string apiKey = "", string model = "llama-3.1-8b-instant")
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = apiKey;
            _model = string.IsNullOrWhiteSpace(model) ? FallbackModel : model;
        }

        public Task<bool> IsAvailableAsync()
        {
            return Task.FromResult(!string.IsNullOrEmpty(_apiKey));
        }

        public async Task<string> GenerateAsync(string prompt)
        {
            return await GenerateWithModelAsync(prompt, _model);
        }

        private async Task<string> GenerateWithModelAsync(string prompt, string modelName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(prompt)) return string.Empty;

                if (prompt.Length > MaxPromptLength)
                {
                    _logger.LogWarning("Prompt muito grande: {Length} chars (max: {Max})",
                        prompt.Length, MaxPromptLength);
                    return "A consulta gerou um volume de dados muito alto. Tente filtrar por um período menor ou um time específico.";
                }

                var requestBody = new
                {
                    model = modelName,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.1, 
                    max_tokens = 1000
                };

                var jsonRequest = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                _logger.LogDebug("Enviando requisição para Groq API com modelo {Model}...", modelName);
                
                var response = await _httpClient.PostAsync("openai/v1/chat/completions", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Erro na API do Groq: {StatusCode} - {Error}", response.StatusCode, error);

                    // Se o modelo falhar por model_not_found e não for o fallback, tenta o fallback
                    if (error.Contains("model_not_found") && modelName != FallbackModel)
                    {
                        _logger.LogWarning("Modelo {Model} não encontrado no Groq. Tentando com fallback {Fallback}...", modelName, FallbackModel);
                        return await GenerateWithModelAsync(prompt, FallbackModel);
                    }

                    return "Ocorreu um erro ao processar sua pergunta com a IA. Por favor, tente novamente em instantes.";
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(jsonResponse);
                
                var answer = document.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                return answer ?? "Não foi possível gerar uma resposta.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro crítico ao chamar a API do Groq");
                return "O Assistente IA está temporariamente offline.";
            }
        }
    }
}
