using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using HoopGameNight.Api.Constants;
using HoopGameNight.Core.DTOs.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HoopGameNight.Api.Filters
{
    public class ValidateModelFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.ModelState.IsValid)
            {
                var errors = context.ModelState
                    .SelectMany(x => x.Value?.Errors ?? [])
                    .Select(x => x.ErrorMessage)
                    .ToList();

                var response = new ApiResponse<object>
                {
                    Success = false,
                    Message = string.Join("; ", errors),
                    Data = errors,
                    RequestId = context.HttpContext.TraceIdentifier
                };

                context.Result = new BadRequestObjectResult(response);
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}