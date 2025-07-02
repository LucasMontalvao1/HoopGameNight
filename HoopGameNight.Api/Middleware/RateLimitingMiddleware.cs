using System.Collections.Concurrent;
using System.Net;

namespace HoopGameNight.Api.Middleware
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RateLimitingMiddleware> _logger;
        private readonly ConcurrentDictionary<string, List<DateTime>> _requests = new();
        private readonly int _requestLimit = 100; // requests per minute
        private readonly TimeSpan _timeWindow = TimeSpan.FromMinutes(1);

        public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var clientId = GetClientId(context);
            var now = DateTime.UtcNow;

            // Get or create request list for this client
            var requestTimes = _requests.GetOrAdd(clientId, _ => new List<DateTime>());

            lock (requestTimes)
            {
                // Remove old requests outside the time window
                requestTimes.RemoveAll(time => now - time > _timeWindow);

                // Check if limit exceeded
                if (requestTimes.Count >= _requestLimit)
                {
                    _logger.LogWarning("Rate limit exceeded for client {ClientId}", clientId);
                    context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                    context.Response.Headers.Add("Retry-After", _timeWindow.TotalSeconds.ToString());
                    return;
                }

                // Add current request
                requestTimes.Add(now);
            }

            // Add rate limit headers
            context.Response.Headers.Add("X-RateLimit-Limit", _requestLimit.ToString());
            context.Response.Headers.Add("X-RateLimit-Remaining", (_requestLimit - requestTimes.Count).ToString());
            context.Response.Headers.Add("X-RateLimit-Reset", DateTimeOffset.UtcNow.Add(_timeWindow).ToUnixTimeSeconds().ToString());

            await _next(context);
        }

        private string GetClientId(HttpContext context)
        {
            // Try to get real IP from headers (for load balancers)
            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault() ??
                        context.Request.Headers["X-Forwarded-For"].FirstOrDefault() ??
                        context.Connection.RemoteIpAddress?.ToString() ??
                        "unknown";

            return realIp;
        }
    }
}