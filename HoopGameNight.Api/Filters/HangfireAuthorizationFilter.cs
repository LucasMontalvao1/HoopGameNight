using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace HoopGameNight.Api.Filters
{
    public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
    {
        private readonly IConfiguration _configuration;

        public HangfireAuthorizationFilter(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();

            // Permitir acesso local sem autenticação 
            var isLocal = httpContext.Connection.RemoteIpAddress != null && 
                         (httpContext.Connection.RemoteIpAddress.Equals(httpContext.Connection.LocalIpAddress) || 
                          IPAddress.IsLoopback(httpContext.Connection.RemoteIpAddress));
            
            if (isLocal) return true;

            // Autenticação Básica
            string authHeader = httpContext.Request.Headers["Authorization"];

            if (string.IsNullOrEmpty(authHeader))
            {
                SetChallengeResponse(httpContext);
                return false;
            }

            try
            {
                var authHeaderValue = AuthenticationHeaderValue.Parse(authHeader);

                if (!"Basic".Equals(authHeaderValue.Scheme, StringComparison.OrdinalIgnoreCase))
                {
                    SetChallengeResponse(httpContext);
                    return false;
                }

                var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(authHeaderValue.Parameter ?? "")).Split(':');

                if (credentials.Length != 2)
                {
                    SetChallengeResponse(httpContext);
                    return false;
                }

                var user = credentials[0];
                var pass = credentials[1];

                var configUser = _configuration["Hangfire:User"] ?? "admin";
                var configPass = _configuration["Hangfire:Password"] ?? "hoopadmin123";

                if (user == configUser && pass == configPass)
                {
                    return true;
                }
            }
            catch
            {
                // Fall through to challenge
            }

            SetChallengeResponse(httpContext);
            return false;
        }

        private void SetChallengeResponse(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = 401;
            httpContext.Response.Headers.Append("WWW-Authenticate", "Basic realm=\"Hangfire Dashboard\"");
        }
    }
}
