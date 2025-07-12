using FluentAssertions;
using HoopGameNight.Api.Middleware;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace HoopGameNight.Tests.Unit.Api.Middleware
{
    public class ErrorHandlingMiddlewareTests : IDisposable
    {
        private readonly Mock<ILogger<ErrorHandlingMiddleware>> _mockLogger;
        private readonly ErrorHandlingMiddleware _middleware;
        private readonly DefaultHttpContext _httpContext;
        private readonly MemoryStream _responseBody;

        public ErrorHandlingMiddlewareTests()
        {
            _mockLogger = new Mock<ILogger<ErrorHandlingMiddleware>>();
            var mockRequestDelegate = new Mock<RequestDelegate>();
            _middleware = new ErrorHandlingMiddleware(mockRequestDelegate.Object, _mockLogger.Object);

            _responseBody = new MemoryStream();
            _httpContext = new DefaultHttpContext
            {
                Response = { Body = _responseBody },
                Request =
                {
                    Path = "/api/teams",
                    Method = "GET"
                }
            };
        }

        #region Testes de Sucesso

        [Fact(DisplayName = "Deve chamar próximo middleware quando não há exceção")]
        public async Task DeveProcessarRequisicao_QuandoNaoHaExcecao()
        {
            // Arrange
            var proximoMiddlewareFoiChamado = false;
            RequestDelegate next = (context) =>
            {
                proximoMiddlewareFoiChamado = true;
                return Task.CompletedTask;
            };

            var middleware = new ErrorHandlingMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            proximoMiddlewareFoiChamado.Should().BeTrue("o próximo middleware deve ser executado");
            _httpContext.Response.StatusCode.Should().Be(200);
        }

        #endregion

        #region Testes de Exceções de Domínio

        [Fact(DisplayName = "Deve retornar 404 quando EntityNotFoundException é lançada")]
        public async Task DeveTratarEntityNotFoundException_ComStatus404()
        {
            // Arrange
            var exception = new EntityNotFoundException("Time", 123);
            RequestDelegate next = (context) => throw exception;
            var middleware = new ErrorHandlingMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            _httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.NotFound);

            var resposta = await ObterRespostaDeserializada();
            resposta.Should().NotBeNull();
            resposta!.Success.Should().BeFalse();
            // A mensagem real da EntityNotFoundException
            resposta.Message.Should().Contain("123");
            resposta.Message.Should().Contain("not found");
        }

        [Fact(DisplayName = "Deve retornar 400 quando BusinessException é lançada")]
        public async Task DeveTratarBusinessException_ComStatus400()
        {
            // Arrange
            var mensagemErro = "Operação inválida";
            var exception = new BusinessException(mensagemErro);
            RequestDelegate next = (context) => throw exception;
            var middleware = new ErrorHandlingMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            _httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);

            var resposta = await ObterRespostaDeserializada();
            resposta!.Message.Should().Be(mensagemErro);
        }

        [Fact(DisplayName = "Deve retornar 503 quando ExternalApiException é lançada")]
        public async Task DeveTratarExternalApiException_ComStatus503()
        {
            // Arrange
            var exception = new ExternalApiException("Ball Don't Lie API", "Rate limit exceeded", 429);
            RequestDelegate next = (context) => throw exception;
            var middleware = new ErrorHandlingMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            _httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.ServiceUnavailable);

            var resposta = await ObterRespostaDeserializada();
            resposta!.Message.Should().Be("External service temporarily unavailable");
        }

        #endregion

        #region Testes de Exceções do Sistema

        [Fact(DisplayName = "Deve retornar 400 quando ArgumentException é lançada")]
        public async Task DeveTratarArgumentException_ComStatus400()
        {
            // Arrange
            var mensagem = "O parâmetro teamId deve ser maior que zero";
            var exception = new ArgumentException(mensagem);
            RequestDelegate next = (context) => throw exception;
            var middleware = new ErrorHandlingMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            _httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);

            var resposta = await ObterRespostaDeserializada();
            resposta!.Message.Should().Be(mensagem);
        }

        [Fact(DisplayName = "Deve retornar 401 quando UnauthorizedAccessException é lançada")]
        public async Task DeveTratarUnauthorizedAccessException_ComStatus401()
        {
            // Arrange
            var exception = new UnauthorizedAccessException();
            RequestDelegate next = (context) => throw exception;
            var middleware = new ErrorHandlingMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            _httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.Unauthorized);

            var resposta = await ObterRespostaDeserializada();
            resposta!.Message.Should().Be("Access denied");
        }

        [Fact(DisplayName = "Deve retornar 500 para exceções não tratadas")]
        public async Task DeveTratarExcecoesGenericas_ComStatus500()
        {
            // Arrange
            var exception = new InvalidOperationException("Erro inesperado");
            RequestDelegate next = (context) => throw exception;
            var middleware = new ErrorHandlingMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            _httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);

            var resposta = await ObterRespostaDeserializada();
            resposta!.Message.Should().Be("An internal server error occurred");
        }

        #endregion

        #region Testes de Validação de Resposta

        [Fact(DisplayName = "Deve definir Content-Type como application/json")]
        public async Task DeveDefinirContentTypeCorreto_QuandoTratarExcecao()
        {
            // Arrange
            var exception = new BusinessException("Erro de teste");
            RequestDelegate next = (context) => throw exception;
            var middleware = new ErrorHandlingMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            _httpContext.Response.ContentType.Should().Be("application/json");
        }

        [Fact(DisplayName = "Deve incluir timestamp atual na resposta de erro")]
        public async Task DeveIncluirTimestampAtual_NaRespostaDeErro()
        {
            // Arrange
            var exception = new BusinessException("Erro de teste");
            RequestDelegate next = (context) => throw exception;
            var middleware = new ErrorHandlingMiddleware(next, _mockLogger.Object);
            var tempoAntes = DateTime.UtcNow;

            // Act
            await middleware.InvokeAsync(_httpContext);
            var tempoDepois = DateTime.UtcNow;

            // Assert
            var resposta = await ObterRespostaDeserializada();
            resposta!.Timestamp.Should().BeAfter(tempoAntes.AddSeconds(-1));
            resposta.Timestamp.Should().BeBefore(tempoDepois.AddSeconds(1));
        }

        [Fact(DisplayName = "Deve garantir que response body pode ser lido após erro")]
        public async Task DevePermitirLeituraDoBody_AposErro()
        {
            // Arrange
            var exception = new BusinessException("Erro de teste");
            RequestDelegate next = (context) => throw exception;
            var middleware = new ErrorHandlingMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            _httpContext.Response.Body.Should().BeReadable();
            _httpContext.Response.Body.Position.Should().BeGreaterThan(0);

            // Deve ser possível ler novamente
            _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
            var conteudo = await new StreamReader(_httpContext.Response.Body).ReadToEndAsync();
            conteudo.Should().NotBeNullOrEmpty();
        }

        #endregion

        #region Testes de Logging

        [Fact(DisplayName = "Deve logar erro quando ocorre exceção")]
        public async Task DeveLogarErro_QuandoOcorreExcecao()
        {
            // Arrange
            var exception = new InvalidOperationException("Erro de teste");
            RequestDelegate next = (context) => throw exception;
            var middleware = new ErrorHandlingMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region Testes de Cenários Especiais

        [Fact(DisplayName = "Deve tratar exceção com inner exception")]
        public async Task DeveTratarExcecao_ComInnerException()
        {
            // Arrange
            var innerException = new InvalidOperationException("Erro interno");
            var exception = new BusinessException("Erro principal", innerException);
            RequestDelegate next = (context) => throw exception;
            var middleware = new ErrorHandlingMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            _httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);

            var resposta = await ObterRespostaDeserializada();
            resposta!.Message.Should().Be("Erro principal");
        }

        [Fact(DisplayName = "Deve preservar estrutura da resposta ApiResponse")]
        public async Task DevePreservarEstruturaApiResponse()
        {
            // Arrange
            var exception = new BusinessException("Teste de estrutura");
            RequestDelegate next = (context) => throw exception;
            var middleware = new ErrorHandlingMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            var conteudo = await ObterConteudoResposta();
            var resposta = JsonSerializer.Deserialize<ApiResponse<object>>(conteudo, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            resposta.Should().NotBeNull();
            resposta!.Success.Should().BeFalse();
            resposta.Message.Should().NotBeNullOrEmpty();
            resposta.Data.Should().BeNull();
            resposta.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        #endregion

        #region Métodos Auxiliares

        private async Task<string> ObterConteudoResposta()
        {
            _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(_httpContext.Response.Body);
            return await reader.ReadToEndAsync();
        }

        private async Task<ApiResponse<object>?> ObterRespostaDeserializada()
        {
            var conteudo = await ObterConteudoResposta();
            return JsonSerializer.Deserialize<ApiResponse<object>>(conteudo, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        public void Dispose()
        {
            _responseBody?.Dispose();
        }

        #endregion
    }
}