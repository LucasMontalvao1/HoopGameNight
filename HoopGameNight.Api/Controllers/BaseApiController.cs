using HoopGameNight.Core.DTOs.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace HoopGameNight.Api.Controllers
{
    [ApiController]
    public abstract class BaseApiController : ControllerBase
    {
        protected readonly ILogger Logger;

        protected BaseApiController(ILogger logger)
        {
            Logger = logger;
        }

        /// <summary>
        /// Retorna uma resposta de sucesso com dados
        /// </summary>
        protected new ActionResult<ApiResponse<T>> Ok<T>(T data, string message = "Success")
        {
            var response = ApiResponse<T>.SuccessResult(data, message);
            return base.Ok(response);
        }

        /// <summary>
        /// Retorna uma resposta de erro
        /// </summary>
        protected new ActionResult<ApiResponse<T>> BadRequest<T>(string message)
        {
            var response = ApiResponse<T>.ErrorResult(message);
            return base.BadRequest(response);
        }

        /// <summary>
        /// Retorna uma resposta de não encontrado
        /// </summary>
        protected new ActionResult<ApiResponse<T>> NotFound<T>(string message)
        {
            var response = ApiResponse<T>.ErrorResult(message);
            return base.NotFound(response);
        }

        /// <summary>
        /// Retorna uma resposta paginada
        /// </summary>
        protected ActionResult<PaginatedResponse<T>> OkPaginated<T>(
            IEnumerable<T> data,
            int page,
            int pageSize,
            int totalRecords,
            string message = "Success")
        {
            var dataList = data.ToList();
            var response = PaginatedResponse<T>.Create(dataList, page, pageSize, totalRecords, message);
            return base.Ok(response);
        }
    }
}