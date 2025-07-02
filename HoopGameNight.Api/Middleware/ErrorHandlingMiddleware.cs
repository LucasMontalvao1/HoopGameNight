using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Exceptions;
using System.Net;
using System.Text.Json;

namespace HoopGameNight.Api.Middleware
{
    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlingMiddleware> _logger;

        public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred");
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var response = context.Response;
            response.ContentType = "application/json";

            var errorResponse = new ApiResponse<object>
            {
                Success = false,
                Message = GetErrorMessage(exception),
                Data = null
            };

            response.StatusCode = exception switch
            {
                EntityNotFoundException => (int)HttpStatusCode.NotFound,
                BusinessException => (int)HttpStatusCode.BadRequest,
                ExternalApiException => (int)HttpStatusCode.ServiceUnavailable,
                ArgumentException => (int)HttpStatusCode.BadRequest,
                UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                _ => (int)HttpStatusCode.InternalServerError
            };

            var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await response.WriteAsync(jsonResponse);
        }

        private static string GetErrorMessage(Exception exception)
        {
            return exception switch
            {
                EntityNotFoundException ex => ex.Message,
                BusinessException ex => ex.Message,
                ExternalApiException ex => "External service temporarily unavailable",
                ArgumentException ex => ex.Message,
                UnauthorizedAccessException => "Access denied",
                _ => "An internal server error occurred"
            };
        }
    }
}