using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using HoopGameNight.Core.DTOs.Response;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace HoopGameNight.Api.Controllers
{
    [ApiController]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public abstract class BaseApiController : ControllerBase
    {
        protected readonly ILogger Logger;
        private readonly Stopwatch _stopwatch = new();

        protected BaseApiController(ILogger logger)
        {
            Logger = logger;
        }

        protected async Task<ActionResult<ApiResponse<T>>> ExecuteAsync<T>(
            Func<Task<ActionResult<ApiResponse<T>>>> operation,
            [System.Runtime.CompilerServices.CallerMemberName] string operationName = "")
        {
            _stopwatch.Restart();
            var requestId = HttpContext.TraceIdentifier;

            try
            {
                Logger.LogDebug("Starting {Operation} - RequestId: {RequestId}",
                    operationName, requestId);

                var result = await operation();

                Logger.LogDebug("Completed {Operation} in {ElapsedMs}ms - RequestId: {RequestId}",
                    operationName, _stopwatch.ElapsedMilliseconds, requestId);

                if (!Response.Headers.ContainsKey("X-Request-ID"))
                {
                    Response.Headers.Add("X-Request-ID", requestId);
                }
                Response.Headers.Add("X-Response-Time", $"{_stopwatch.ElapsedMilliseconds}ms");

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex,
                    "Error in {Operation} after {ElapsedMs}ms - RequestId: {RequestId}",
                    operationName, _stopwatch.ElapsedMilliseconds, requestId);
                throw;
            }
            finally
            {
                _stopwatch.Stop();
            }
        }

        /// <summary>
        /// Encapsula os dados em um objeto <see cref="ApiResponse{T}"/> com status HTTP 200 (OK).
        /// </summary>
        protected new ActionResult<ApiResponse<T>> Ok<T>(T data, string message = "Success")
        {
            var response = ApiResponse<T>.SuccessResult(data, message);
            response.RequestId = HttpContext.TraceIdentifier;
            return base.Ok(response);
        }

        /// <summary>
        /// Retorna um erro de validação ou regra de negócio com status HTTP 400 (Bad Request).
        /// </summary>
        protected new ActionResult<ApiResponse<T>> BadRequest<T>(string message)
        {
            var response = ApiResponse<T>.ErrorResult(message);
            response.RequestId = HttpContext.TraceIdentifier;
            return base.BadRequest(response);
        }

        /// <summary>
        /// Indica que o recurso solicitado não foi encontrado, retornando status HTTP 404 (Not Found).
        /// </summary>
        protected new ActionResult<ApiResponse<T>> NotFound<T>(string message)
        {
            var response = ApiResponse<T>.ErrorResult(message);
            response.RequestId = HttpContext.TraceIdentifier;
            return base.NotFound(response);
        }

        /// <summary>
        /// Formata a resposta para coleções paginadas, incluindo metadados nos headers da resposta HTTP.
        /// </summary>
        protected ActionResult<PaginatedResponse<T>> OkPaginated<T>(
            IEnumerable<T> data,
            int page,
            int pageSize,
            int totalRecords,
            string message = "Success")
        {
            var response = PaginatedResponse<T>.Create(data.ToList(), page, pageSize, totalRecords, message);
            response.RequestId = HttpContext.TraceIdentifier;

            Response.Headers.Add("X-Total-Count", totalRecords.ToString());
            Response.Headers.Add("X-Page", page.ToString());
            Response.Headers.Add("X-Page-Size", pageSize.ToString());

            return base.Ok(response);
        }

        /// <summary>
        /// Valida se os parâmetros de paginação (página e tamanho) estão dentro dos limites aceitáveis.
        /// </summary>
        protected bool IsValidPagination(int page, int pageSize, out string? errorMessage)
        {
            errorMessage = null;

            if (page < 1)
            {
                errorMessage = "Page must be greater than 0";
                return false;
            }

            if (pageSize < 1 || pageSize > 100)
            {
                errorMessage = "Page size must be between 1 and 100";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Tenta extrair o endereço IP original do cliente, considerando headers de proxy (X-Real-IP / X-Forwarded-For).
        /// </summary>
        protected string GetClientIpAddress()
        {
            return HttpContext.Request.Headers["X-Real-IP"].FirstOrDefault() ??
                   HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault() ??
                   HttpContext.Connection.RemoteIpAddress?.ToString() ??
                   "unknown";
        }

        /// <summary>
        /// Adiciona headers de controle de cache (Cache-Control e Expires) à resposta HTTP.
        /// </summary>
        protected void SetCacheHeaders(int seconds)
        {
            Response.Headers.Add("Cache-Control", $"public, max-age={seconds}");
            Response.Headers.Add("Expires", DateTime.UtcNow.AddSeconds(seconds).ToString("R"));
        }
    }
}