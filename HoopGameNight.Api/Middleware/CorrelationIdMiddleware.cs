using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace HoopGameNight.Api.Middleware
{
    public class CorrelationIdMiddleware
    {
        private const string CorrelationIdHeader = "X-Correlation-ID";
        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Request.Headers.TryGetValue(CorrelationIdHeader, out StringValues correlationId))
            {
                correlationId = Guid.NewGuid().ToString();
            }

            // Adicionar ao contexto de logs
            context.Items[CorrelationIdHeader] = correlationId.ToString();

            // Adicionar à resposta
            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.ContainsKey(CorrelationIdHeader))
                {
                    context.Response.Headers.Add(CorrelationIdHeader, correlationId);
                }
                return Task.CompletedTask;
            });

            using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId.ToString()))
            {
                await _next(context);
            }
        }
    }
}
