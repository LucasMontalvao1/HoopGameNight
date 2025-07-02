using HoopGameNight.Api.Constants;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Net;

namespace HoopGameNight.Api.Filters
{
    public class ApiExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<ApiExceptionFilter> _logger;

        public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger)
        {
            _logger = logger;
        }

        public void OnException(ExceptionContext context)
        {
            var exception = context.Exception;
            var response = new ApiResponse<object>
            {
                Success = false,
                RequestId = context.HttpContext.TraceIdentifier,
                Data = null
            };

            switch (exception)
            {
                case EntityNotFoundException ex:
                    response.Message = ex.Message;
                    context.Result = new NotFoundObjectResult(response);
                    context.HttpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    break;

                case BusinessException ex:
                    response.Message = ex.Message;
                    context.Result = new BadRequestObjectResult(response);
                    context.HttpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    break;

                case ExternalApiException ex:
                    response.Message = ErrorMessages.EXTERNAL_API_ERROR;
                    context.Result = new ObjectResult(response);
                    context.HttpContext.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                    _logger.LogError(ex, "External API error: {ApiName}", ex.ApiName);
                    break;

                case ArgumentException ex:
                    response.Message = ex.Message;
                    context.Result = new BadRequestObjectResult(response);
                    context.HttpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    break;

                case UnauthorizedAccessException:
                    response.Message = "Access denied";
                    context.Result = new UnauthorizedObjectResult(response);
                    context.HttpContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    break;

                default:
                    response.Message = ErrorMessages.INTERNAL_SERVER_ERROR;
                    context.Result = new ObjectResult(response);
                    context.HttpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    _logger.LogError(exception, "Unhandled exception occurred");
                    break;
            }

            context.ExceptionHandled = true;
        }
    }
}