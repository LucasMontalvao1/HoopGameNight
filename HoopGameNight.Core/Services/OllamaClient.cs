using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace HoopGameNight.Core.Services
{
    public class OllamaClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OllamaClient> _logger;
        private const string DefaultModel = "llama3.2";
        private const int MaxPromptLength = 10000;

        public OllamaClient(HttpClient httpClient, ILogger<OllamaClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<string> GenerateAsync(string prompt)
        {
            try
            {
                // Validar tamanho
                if (prompt.Length > MaxPromptLength)
                {
                    _logger.LogWarning("Prompt muito grande: {Length} chars (max: {Max})",
                        prompt.Length, MaxPromptLength);
                    return "A pergunta gerou um contexto muito grande. Tente ser mais específico (ex: 'jogos do Lakers hoje').";
                }

                // Log do prompt (debug) 
                _logger.LogDebug("PROMPT ENVIADO:\n{Prompt}", prompt);

                var request = new
                {
                    model = DefaultModel,
                    prompt = prompt,
                    stream = false,
                    options = new
                    {
                        temperature = 0.1,      // Muito baixo = mais factual
                        top_p = 0.9,
                        top_k = 20,             // Reduzido para menos criatividade
                        num_predict = 500,      // Resposta curta
                        repeat_penalty = 1.2,   // Evita repetição
                        stop = new[] { "\n\n\n", "###", "---" } // Para se fugir do assunto
                    }
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogInformation("Enviando para Ollama: {Size} chars, modelo: {Model}",
                    prompt.Length, DefaultModel);

                var response = await _httpClient.PostAsync("/api/generate", content);

                // Tratamento de erros
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Ollama erro {Status}: {Error}", response.StatusCode, error);

                    return response.StatusCode switch
                    {
                        System.Net.HttpStatusCode.NotFound =>
                            $"Modelo '{DefaultModel}' não encontrado.\n\nSolução:\n1. ollama pull {DefaultModel}\n2. ollama list (para verificar)",
                        System.Net.HttpStatusCode.ServiceUnavailable =>
                            "Ollama indisponível. Execute: ollama serve",
                        _ => $"Erro ao processar (Status: {response.StatusCode})"
                    };
                }

                var responseBody = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(responseBody))
                {
                    _logger.LogError("Ollama retornou vazio");
                    return "Resposta vazia do Ollama.";
                }

                var result = JsonSerializer.Deserialize<OllamaResponse>(responseBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result?.Response == null)
                {
                    _logger.LogError("Falha ao deserializar: {Body}",
                        responseBody.Substring(0, Math.Min(500, responseBody.Length)));
                    return "Erro ao processar resposta do Ollama.";
                }

                // Validar se resposta está completa
                if (!result.Done)
                {
                    _logger.LogWarning("Resposta do Ollama foi truncada");
                }

                _logger.LogInformation("Resposta recebida: {Length} chars (completa: {Done})",
                    result.Response.Length, result.Done);

                // Validar se resposta parece inventada
                var suspiciousWords = new[] { "provavelmente", "acho que", "talvez", "possivelmente", "pode ser" };
                var lowerResponse = result.Response.ToLower();

                if (suspiciousWords.Any(word => lowerResponse.Contains(word)))
                {
                    _logger.LogWarning("Resposta contém palavras suspeitas (especulação)");
                }

                return result.Response.Trim();
            }
            catch (TaskCanceledException)
            {
                _logger.LogError("Timeout ao chamar Ollama ({Timeout}s)", _httpClient.Timeout.TotalSeconds);
                return "Timeout. Tente uma pergunta mais simples.";
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Erro de conexão com Ollama");
                return "Não foi possível conectar ao Ollama.\n\nVerifique:\n1. ollama serve está rodando?\n2. OLLAMA_URL está correto no .env?";
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Erro ao processar JSON");
                return "Erro ao processar JSON do Ollama.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado");
                return "Erro inesperado. Verifique os logs.";
            }
        }

        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/tags");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ollama indisponível");
                return false;
            }
        }

        private class OllamaResponse
        {
            public string Response { get; set; } = string.Empty;
            public string Model { get; set; } = string.Empty;
            public bool Done { get; set; } = true;
        }
    }
}