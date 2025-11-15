using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace HoopGameNight.Api.Middleware
{
    /// <summary>
    /// Middleware global para capturar e tratar todas as exceções não tratadas
    /// </summary>
    public class GlobalExceptionHandlerMiddleware : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

        public GlobalExceptionHandlerMiddleware(ILogger<GlobalExceptionHandlerMiddleware> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            var traceId = httpContext.TraceIdentifier;
            var path = httpContext.Request.Path;
            var method = httpContext.Request.Method;

            _logger.LogError(
                exception,
                "Exceção não tratada | TraceId: {TraceId} | {Method} {Path}",
                traceId, method, path);

            var (statusCode, title) = MapExceptionToResponse(exception);

            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = exception.Message,
                Instance = path,
                Extensions =
                {
                    ["traceId"] = traceId,
                    ["timestamp"] = DateTime.UtcNow
                }
            };

            if (httpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
            {
                problemDetails.Extensions["stackTrace"] = exception.StackTrace;
            }

            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.ContentType = "application/problem+json";

            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

            return true; 
        }

        /// <summary>
        /// Mapeia tipos de exceção para códigos HTTP apropriados
        /// </summary>
        private static (int StatusCode, string Title) MapExceptionToResponse(Exception exception)
        {
            return exception switch
            {
                ArgumentException => (StatusCodes.Status400BadRequest, "Requisição Inválida"),
                KeyNotFoundException => (StatusCodes.Status404NotFound, "Recurso Não Encontrado"),
                UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Não Autorizado"),
                InvalidOperationException => (StatusCodes.Status409Conflict, "Conflito"),
                TimeoutException => (StatusCodes.Status408RequestTimeout, "Tempo Esgotado"),
                _ => (StatusCodes.Status500InternalServerError, "Erro Interno do Servidor")
            };
        }
    }
}
