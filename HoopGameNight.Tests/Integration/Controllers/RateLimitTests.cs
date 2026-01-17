using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace HoopGameNight.Tests.Integration.Controllers
{
    public class RateLimitTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public RateLimitTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact(Skip = "Necessita configuração de Rate Limit no TestServer ainda não isolada")]
        public async Task Ask_ShouldReturnTooManyRequests_AfterLimitExceeded()
        {
            // O Rate Limit requer configuração específica de Client IP ou Auth para contar corretamente.
            // Em testes de integração in-memory, o IP pode ser sempre loopback, disparando o limite compartilhado.
            
            // Arrange
            var client = _factory.CreateClient();
            var url = "/api/Ask";
            var content = new StringContent("{\"question\": \"teste de rate limit\"}", System.Text.Encoding.UTF8, "application/json");

            // Act & Assert
            // Limite configurado é 5 por minuto. Vamos fazer 6 chamadas.
            for (int i = 0; i < 5; i++)
            {
                var response = await client.PostAsync(url, content);
                // Pode falhar se o ambiente de teste não tiver o serviço de IA mockado e retornar 500,
                // mas não deve retornar 429 ainda.
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    Assert.Fail($"Bloqueado prematuramente na requisição {i + 1}");
                }
            }

            // A 6ª chamada deve ser bloqueada
            var blockedResponse = await client.PostAsync(url, content);
            
            // Assert
            blockedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        }
    }
}
