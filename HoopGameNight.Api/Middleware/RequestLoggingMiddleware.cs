using System;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Api.Middleware
{
    /// <summary>
    /// Middleware para logging automático de todas as requisições HTTP
    /// Registra método, path, status code e duração
    /// </summary>
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next,ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var method = context.Request.Method;
            var path = context.Request.Path;
            var queryString = context.Request.QueryString.ToString();

            try
            {
                _logger.LogInformation(
                    "{Method} {Path}{QueryString}",
                    method, path, queryString);

                await _next(context);

                stopwatch.Stop();

                var statusCode = context.Response.StatusCode;
                var logLevel = GetLogLevel(statusCode);

                _logger.Log(
                    logLevel,
                    "{Method} {Path} responded {StatusCode} in {Duration}ms",
                    method, path, statusCode, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger.LogError(
                    ex,
                    "{Method} {Path} failed after {Duration}ms",
                    method, path, stopwatch.ElapsedMilliseconds);

                throw; // Re-throw para GlobalExceptionHandler processar
            }
        }

        /// <summary>
        /// Determina log level baseado no status code
        /// </summary>
        private static LogLevel GetLogLevel(int statusCode)
        {
            return statusCode switch
            {
                >= 500 => LogLevel.Error,
                >= 400 => LogLevel.Warning,
                >= 300 => LogLevel.Information,
                _ => LogLevel.Information
            };
        }
    }
}
