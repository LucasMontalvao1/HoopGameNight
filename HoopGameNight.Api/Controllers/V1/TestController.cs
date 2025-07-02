using HoopGameNight.Api.Constants;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace HoopGameNight.Api.Controllers.V1
{
    [Route(ApiConstants.API_PREFIX + "/" + ApiConstants.API_VERSION + "/test")]
    public class TestController : BaseApiController
    {
        private readonly IBallDontLieService _ballDontLieService;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public TestController(
            IBallDontLieService ballDontLieService,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<TestController> logger) : base(logger)
        {
            _ballDontLieService = ballDontLieService;
            _httpClient = httpClientFactory.CreateClient();
            _configuration = configuration;
        }

        /// <summary>
        /// Teste completo da API externa Ball Don't Lie
        /// </summary>
        [HttpGet("external/full")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<object>>> TestExternalApiComplete()
        {
            try
            {
                Logger.LogInformation("🧪 Starting complete external API test");

                var stopwatch = Stopwatch.StartNew();
                var testResults = new List<object>();

                // ✅ EXTRAIR DADOS DOS ActionResult ANTES DE ADICIONAR

                // 1. Teste de configuração
                var configTest = TestConfiguration();
                var configData = configTest.Value?.Data; // ✅ Extrair dados aqui
                testResults.Add(configData);

                // 2. Teste básico de conectividade
                var connectivityResult = await TestConnectivityInternal();
                testResults.Add(connectivityResult); // ✅ Já é objeto direto

                // 3. Teste de teams
                var teamsResult = await TestTeamsEndpointInternal();
                testResults.Add(teamsResult); // ✅ Já é objeto direto

                // 4. Teste de games
                var gamesResult = await TestGamesEndpointInternal();
                testResults.Add(gamesResult); // ✅ Já é objeto direto

                // 5. Teste de players
                var playersResult = await TestPlayersEndpointInternal();
                testResults.Add(playersResult); // ✅ Já é objeto direto

                stopwatch.Stop();

                // ✅ AGORA FUNCIONA - todos são objetos diretos
                var allTestsPassed = testResults.All(t =>
                {
                    var testObj = t as dynamic;
                    return testObj?.success == true;
                });

                var passedTests = testResults.Count(t =>
                {
                    var testObj = t as dynamic;
                    return testObj?.success == true;
                });

                var summary = (object)new
                {
                    totalDuration = $"{stopwatch.ElapsedMilliseconds}ms",
                    allTestsPassed,
                    totalTests = testResults.Count,
                    passedTests, // ✅ Usar a variável calculada corretamente
                    timestamp = DateTime.UtcNow,
                    tests = testResults
                };

                Logger.LogInformation("🧪 External API test completed in {Duration}ms - {Passed}/{Total} tests passed",
                    stopwatch.ElapsedMilliseconds, passedTests, testResults.Count);

                return Ok(summary, "External API test completed");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "❌ Error during external API test");

                var errorResult = (object)new
                {
                    success = false,
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                };

                return Ok(errorResult, "External API test failed");
            }
        }

        /// <summary>
        /// ✅ NOVO: Teste de diagnóstico do HttpClient
        /// </summary>
        [HttpGet("external/httpclient-debug")]
        public async Task<ActionResult<ApiResponse<object>>> DebugHttpClient()
        {
            try
            {
                Logger.LogInformation("🔧 Debugging HttpClient configuration");

                // Obter HttpClient diretamente do DI
                using var scope = HttpContext.RequestServices.CreateScope();
                var ballDontLieService = scope.ServiceProvider.GetRequiredService<IBallDontLieService>();

                // Verificar se conseguimos acessar propriedades internas (reflection)
                var serviceType = ballDontLieService.GetType();
                var httpClientField = serviceType.GetField("_httpClient",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                var httpClient = httpClientField?.GetValue(ballDontLieService) as HttpClient;

                var diagnosis = new
                {
                    success = true,
                    httpClientBaseAddress = httpClient?.BaseAddress?.ToString() ?? "NULL",
                    httpClientTimeout = httpClient?.Timeout.TotalSeconds ?? 0,
                    ballDontLieServiceType = serviceType.Name,
                    configuredBaseUrl = _configuration["ExternalApis:BallDontLie:BaseUrl"],
                    hasApiKey = !string.IsNullOrEmpty(_configuration["ExternalApis:BallDontLie:ApiKey"]),
                    recommendation = httpClient?.BaseAddress == null ?
                        "❌ HttpClient BaseAddress is NULL - check Program.cs configuration" :
                        "✅ HttpClient appears to be configured correctly",
                    timestamp = DateTime.UtcNow
                };

                return Ok((object)diagnosis, "HttpClient debug completed");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "❌ HttpClient debug failed");

                var errorResult = new
                {
                    success = false,
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                };

                return Ok((object)errorResult, "HttpClient debug failed");
            }
        }

        /// <summary>
        /// Teste de configuração da API
        /// </summary>
        [HttpGet("external/config")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public ActionResult<ApiResponse<object>> TestConfiguration()
        {
            try
            {
                Logger.LogInformation("⚙️ Testing API configuration");

                var ballDontLieConfig = _configuration.GetSection("ExternalApis:BallDontLie");
                var baseUrl = ballDontLieConfig["BaseUrl"];
                var apiKey = ballDontLieConfig["ApiKey"];

                var result = (object)new
                {
                    success = !string.IsNullOrEmpty(baseUrl) && !string.IsNullOrEmpty(apiKey),
                    baseUrl = baseUrl ?? "NOT CONFIGURED",
                    hasApiKey = !string.IsNullOrEmpty(apiKey),
                    apiKeyLength = apiKey?.Length ?? 0,
                    timeout = ballDontLieConfig.GetValue<int>("Timeout", 30),
                    recommendations = GetConfigRecommendations(baseUrl, apiKey)
                };

                Logger.LogInformation("⚙️ Config test: BaseUrl={BaseUrl}, HasApiKey={HasApiKey}",
                    !string.IsNullOrEmpty(baseUrl), !string.IsNullOrEmpty(apiKey));

                return Ok(result, "Configuration checked");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "❌ Configuration test failed");

                var result = (object)new
                {
                    success = false,
                    error = ex.Message
                };

                return Ok(result, "Configuration test failed");
            }
        }

        /// <summary>
        /// Teste direto sem autenticação
        /// </summary>
        [HttpGet("external/raw")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<object>>> TestRawApi()
        {
            try
            {
                Logger.LogInformation("🔧 Testing raw API without authentication");

                var tests = new List<object>();

                // Teste 1: URL base sem auth
                var stopwatch1 = Stopwatch.StartNew();
                var response1 = await _httpClient.GetAsync("https://api.balldontlie.io/v1/teams?per_page=1");
                stopwatch1.Stop();

                tests.Add(new
                {
                    test = "Basic endpoint (no auth)",
                    url = "https://api.balldontlie.io/v1/teams?per_page=1",
                    success = response1.IsSuccessStatusCode,
                    statusCode = (int)response1.StatusCode,
                    statusDescription = response1.StatusCode.ToString(),
                    responseTime = $"{stopwatch1.ElapsedMilliseconds}ms",
                    contentLength = response1.Content.Headers.ContentLength
                });

                // Teste 2: Com API Key no header
                var apiKey = _configuration["ExternalApis:BallDontLie:ApiKey"];
                if (!string.IsNullOrEmpty(apiKey))
                {
                    var stopwatch2 = Stopwatch.StartNew();
                    var request = new HttpRequestMessage(HttpMethod.Get, "https://api.balldontlie.io/v1/teams?per_page=1");
                    request.Headers.Add("Authorization", $"Bearer {apiKey}");

                    var response2 = await _httpClient.SendAsync(request);
                    stopwatch2.Stop();

                    tests.Add(new
                    {
                        test = "With API Key (Bearer)",
                        url = "https://api.balldontlie.io/v1/teams?per_page=1",
                        success = response2.IsSuccessStatusCode,
                        statusCode = (int)response2.StatusCode,
                        statusDescription = response2.StatusCode.ToString(),
                        responseTime = $"{stopwatch2.ElapsedMilliseconds}ms",
                        contentLength = response2.Content.Headers.ContentLength
                    });

                    // Se response for 401, teste com API key como query param
                    if (response2.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        var stopwatch3 = Stopwatch.StartNew();
                        var response3 = await _httpClient.GetAsync($"https://api.balldontlie.io/v1/teams?per_page=1&api_key={apiKey}");
                        stopwatch3.Stop();

                        tests.Add(new
                        {
                            test = "With API Key (Query param)",
                            url = $"https://api.balldontlie.io/v1/teams?per_page=1&api_key={apiKey[..8]}...",
                            success = response3.IsSuccessStatusCode,
                            statusCode = (int)response3.StatusCode,
                            statusDescription = response3.StatusCode.ToString(),
                            responseTime = $"{stopwatch3.ElapsedMilliseconds}ms"
                        });
                    }
                }

                var result = (object)new
                {
                    success = tests.Any(t => ((dynamic)t).success),
                    totalTests = tests.Count,
                    successfulTests = tests.Count(t => ((dynamic)t).success),
                    tests,
                    timestamp = DateTime.UtcNow
                };

                return Ok(result, "Raw API test completed");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "❌ Raw API test failed");

                var result = (object)new
                {
                    success = false,
                    error = ex.Message
                };

                return Ok(result, "Raw API test failed");
            }
        }

        /// <summary>
        /// Teste de conectividade (método interno)
        /// </summary>
        private async Task<object> TestConnectivityInternal()
        {
            try
            {
                Logger.LogInformation("🔌 Testing connectivity to Ball Don't Lie API");

                var stopwatch = Stopwatch.StartNew();
                var response = await _httpClient.GetAsync("https://api.balldontlie.io/v1/teams?per_page=1");
                stopwatch.Stop();

                return new
                {
                    test = "Connectivity",
                    success = response.IsSuccessStatusCode,
                    statusCode = (int)response.StatusCode,
                    statusDescription = response.StatusCode.ToString(),
                    responseTime = $"{stopwatch.ElapsedMilliseconds}ms",
                    timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    test = "Connectivity",
                    success = false,
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Teste de teams (método interno)
        /// </summary>
        private async Task<object> TestTeamsEndpointInternal()
        {
            try
            {
                Logger.LogInformation("🏀 Testing teams endpoint");

                var stopwatch = Stopwatch.StartNew();
                var teams = await _ballDontLieService.GetAllTeamsAsync();
                stopwatch.Stop();

                return new
                {
                    test = "Teams",
                    success = teams.Any(),
                    totalTeams = teams.Count(),
                    responseTime = $"{stopwatch.ElapsedMilliseconds}ms",
                    timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    test = "Teams",
                    success = false,
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Teste de games (método interno)
        /// </summary>
        private async Task<object> TestGamesEndpointInternal()
        {
            try
            {
                Logger.LogInformation("🎮 Testing games endpoint");

                var stopwatch = Stopwatch.StartNew();
                var games = await _ballDontLieService.GetTodaysGamesAsync();
                stopwatch.Stop();

                return new
                {
                    test = "Games",
                    success = true, // Mesmo sem jogos, endpoint funcionando é sucesso
                    todayGames = games.Count(),
                    responseTime = $"{stopwatch.ElapsedMilliseconds}ms",
                    timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    test = "Games",
                    success = false,
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Teste de players (método interno)
        /// </summary>
        private async Task<object> TestPlayersEndpointInternal()
        {
            try
            {
                Logger.LogInformation("👤 Testing players endpoint");

                var stopwatch = Stopwatch.StartNew();
                var players = await _ballDontLieService.SearchPlayersAsync("lebron", 1);
                stopwatch.Stop();

                return new
                {
                    test = "Players",
                    success = true, // Endpoint funcionando é sucesso
                    playersFound = players.Count(),
                    searchTerm = "lebron",
                    responseTime = $"{stopwatch.ElapsedMilliseconds}ms",
                    timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    test = "Players",
                    success = false,
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// ✅ NOVO: Teste específico para verificar se API mudou
        /// </summary>
        [HttpGet("external/api-version-check")]
        public async Task<ActionResult<ApiResponse<object>>> CheckApiVersionAndEndpoints()
        {
            try
            {
                Logger.LogInformation("🔍 Checking API version and endpoint changes");

                var tests = new List<object>();

                // 1. Testar URL antiga vs nova
                var oldApiTest = await TestSpecificUrl("https://www.balldontlie.io/api/v1/teams?per_page=1", "Old API URL");
                tests.Add(oldApiTest);

                var newApiTest = await TestSpecificUrl("https://api.balldontlie.io/v1/teams?per_page=1", "New API URL");
                tests.Add(newApiTest);

                // 2. Testar diferentes métodos de autenticação
                var apiKey = _configuration["ExternalApis:BallDontLie:ApiKey"];
                if (!string.IsNullOrEmpty(apiKey))
                {
                    var bearerAuthTest = await TestWithBearerAuth("https://api.balldontlie.io/v1/teams?per_page=1", apiKey);
                    tests.Add(bearerAuthTest);

                    var queryParamTest = await TestWithQueryParam("https://api.balldontlie.io/v1/teams", apiKey);
                    tests.Add(queryParamTest);
                }

                var result = new
                {
                    success = tests.Any(t => ((dynamic)t).success),
                    recommendation = GetRecommendationBasedOnResults(tests),
                    tests,
                    timestamp = DateTime.UtcNow
                };

                return Ok((object)result, "API version check completed");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "❌ API version check failed");
                throw;
            }
        }

        private async Task<object> TestSpecificUrl(string url, string testName)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var response = await _httpClient.GetAsync(url);
                stopwatch.Stop();

                var content = response.IsSuccessStatusCode ?
                    await response.Content.ReadAsStringAsync() :
                    "Not available";

                return new
                {
                    test = testName,
                    url,
                    success = response.IsSuccessStatusCode,
                    statusCode = (int)response.StatusCode,
                    statusDescription = response.StatusCode.ToString(),
                    responseTime = $"{stopwatch.ElapsedMilliseconds}ms",
                    contentPreview = content.Length > 100 ? content[..100] + "..." : content,
                    timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    test = testName,
                    url,
                    success = false,
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                };
            }
        }

        private async Task<object> TestWithBearerAuth(string url, string apiKey)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

                var response = await _httpClient.SendAsync(request);
                stopwatch.Stop();

                return new
                {
                    test = "Bearer Authentication",
                    url,
                    success = response.IsSuccessStatusCode,
                    statusCode = (int)response.StatusCode,
                    statusDescription = response.StatusCode.ToString(),
                    responseTime = $"{stopwatch.ElapsedMilliseconds}ms",
                    authMethod = "Bearer Token",
                    timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    test = "Bearer Authentication",
                    url,
                    success = false,
                    error = ex.Message,
                    authMethod = "Bearer Token",
                    timestamp = DateTime.UtcNow
                };
            }
        }

        private async Task<object> TestWithQueryParam(string baseUrl, string apiKey)
        {
            try
            {
                var url = $"{baseUrl}?per_page=1&api_key={apiKey}";

                var stopwatch = Stopwatch.StartNew();
                var response = await _httpClient.GetAsync(url);
                stopwatch.Stop();

                return new
                {
                    test = "Query Parameter Auth",
                    success = response.IsSuccessStatusCode,
                    statusCode = (int)response.StatusCode,
                    statusDescription = response.StatusCode.ToString(),
                    responseTime = $"{stopwatch.ElapsedMilliseconds}ms",
                    authMethod = "Query Parameter",
                    timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    test = "Query Parameter Auth",
                    success = false,
                    error = ex.Message,
                    authMethod = "Query Parameter",
                    timestamp = DateTime.UtcNow
                };
            }
        }

        private static string GetRecommendationBasedOnResults(List<object> tests)
        {
            var hasSuccessfulTest = tests.Any(t => ((dynamic)t).success);

            if (!hasSuccessfulTest)
            {
                return "🚨 AÇÃO NECESSÁRIA: Nenhum teste passou. Verifique se você tem uma API key válida em balldontlie.io e se está usando o endpoint correto.";
            }

            var bearerAuthWorked = tests.Any(t =>
            {
                var test = (dynamic)t;
                return test.test?.ToString() == "Bearer Authentication" && test.success == true;
            });

            if (bearerAuthWorked)
            {
                return "✅ RECOMENDAÇÃO: Use Bearer Authentication com https://api.balldontlie.io/v1/";
            }

            var newApiWorked = tests.Any(t =>
            {
                var test = (dynamic)t;
                return test.test?.ToString() == "New API URL" && test.success == true;
            });

            if (newApiWorked)
            {
                return "✅ RECOMENDAÇÃO: Use https://api.balldontlie.io/v1/ como base URL";
            }

            return "⚠️ INVESTIGAÇÃO NECESSÁRIA: Resultados mistos. Verifique logs detalhados.";
        }

        /// <summary>
        /// Diagnóstico completo da situação
        /// </summary>
        [HttpGet("external/diagnosis")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<object>>> DiagnoseApiIssues()
        {
            try
            {
                Logger.LogInformation("🔬 Running complete API diagnosis");

                var diagnosis = new
                {
                    configuration = TestConfiguration().Value?.Data,
                    rawApiTest = (await TestRawApi()).Value?.Data,
                    networkTest = await TestNetworkConnectivity(),
                    recommendations = GetDetailedRecommendations()
                };

                return Ok((object)diagnosis, "API diagnosis completed");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "❌ Diagnosis failed");
                throw;
            }
        }

        // ============================================
        // HELPER METHODS
        // ============================================

        private async Task<object> TestNetworkConnectivity()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var response = await _httpClient.GetAsync("https://httpbin.org/status/200");
                stopwatch.Stop();

                return new
                {
                    internetConnectivity = response.IsSuccessStatusCode,
                    responseTime = $"{stopwatch.ElapsedMilliseconds}ms",
                    statusCode = (int)response.StatusCode
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    internetConnectivity = false,
                    error = ex.Message
                };
            }
        }

        private static List<string> GetConfigRecommendations(string? baseUrl, string? apiKey)
        {
            var recommendations = new List<string>();

            if (string.IsNullOrEmpty(baseUrl))
                recommendations.Add("❌ Configure BaseUrl in appsettings.json");

            if (string.IsNullOrEmpty(apiKey))
                recommendations.Add("❌ Configure ApiKey in appsettings.json or User Secrets");

            if (!string.IsNullOrEmpty(apiKey) && apiKey.Length < 20)
                recommendations.Add("⚠️ API Key seems too short - verify it's correct");

            if (recommendations.Count == 0)
                recommendations.Add("✅ Configuration looks good");

            return recommendations;
        }

        private static List<string> GetDetailedRecommendations()
        {
            return new List<string>
            {
                "1. Verify your Ball Don't Lie API key is valid",
                "2. Check if you need to activate your API key",
                "3. Try testing the API directly in browser: https://api.balldontlie.io/v1/teams?per_page=1",
                "4. Check Ball Don't Lie documentation for authentication changes",
                "5. Verify your internet connection allows HTTPS requests",
                "6. Check if you're hitting rate limits (60 requests per minute)"
            };
        }
    }
}