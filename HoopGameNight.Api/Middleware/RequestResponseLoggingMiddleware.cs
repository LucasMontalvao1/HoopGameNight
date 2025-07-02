using System.Diagnostics;
using System.Text;

namespace HoopGameNight.Api.Middleware
{
    public class RequestResponseLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

        public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var requestId = context.TraceIdentifier;

            // Log request
            await LogRequestAsync(context, requestId);

            // Capture response
            var originalResponseBodyStream = context.Response.Body;
            using var responseBodyStream = new MemoryStream();
            context.Response.Body = responseBodyStream;

            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();

                // Log response
                await LogResponseAsync(context, requestId, stopwatch.ElapsedMilliseconds);

                // Copy response back
                responseBodyStream.Seek(0, SeekOrigin.Begin);
                await responseBodyStream.CopyToAsync(originalResponseBodyStream);
            }
        }

        private async Task LogRequestAsync(HttpContext context, string requestId)
        {
            try
            {
                var request = context.Request;
                var requestBody = string.Empty;

                if (request.ContentLength > 0 && request.ContentType?.Contains("application/json") == true)
                {
                    request.EnableBuffering();
                    using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
                    requestBody = await reader.ReadToEndAsync();
                    request.Body.Position = 0;
                }

                _logger.LogInformation(
                    "Request {RequestId}: {Method} {Path} {QueryString} - Body: {RequestBody}",
                    requestId,
                    request.Method,
                    request.Path,
                    request.QueryString,
                    string.IsNullOrEmpty(requestBody) ? "Empty" : requestBody);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to log request {RequestId}", requestId);
            }
        }

        private async Task LogResponseAsync(HttpContext context, string requestId, long elapsedMs)
        {
            try
            {
                var response = context.Response;
                var responseBody = string.Empty;

                if (response.Body.CanSeek && response.ContentType?.Contains("application/json") == true)
                {
                    response.Body.Seek(0, SeekOrigin.Begin);
                    using var reader = new StreamReader(response.Body, Encoding.UTF8, leaveOpen: true);
                    responseBody = await reader.ReadToEndAsync();
                    response.Body.Seek(0, SeekOrigin.Begin);
                }

                _logger.LogInformation(
                    "Response {RequestId}: {StatusCode} in {ElapsedMs}ms - Body: {ResponseBody}",
                    requestId,
                    response.StatusCode,
                    elapsedMs,
                    string.IsNullOrEmpty(responseBody) ? "Empty" : responseBody);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to log response {RequestId}", requestId);
            }
        }
    }
}