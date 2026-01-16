using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HoopGameNight.Api.Filters
{
    /// <summary>
    /// Filtro de validação automática de paginação
    /// </summary>
    public class ValidatePaginationAttribute : ActionFilterAttribute
    {
        private const int MIN_PAGE = 1;
        private const int MAX_PAGE = 1000;
        private const int MIN_PAGE_SIZE = 1;
        private const int MAX_PAGE_SIZE = 100;

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.HttpContext.Request;

            if (request.Query.TryGetValue("page", out var pageValue))
            {
                if (!int.TryParse(pageValue, out var page) || page < MIN_PAGE || page > MAX_PAGE)
                {
                    context.Result = new BadRequestObjectResult(new
                    {
                        error = "Invalid pagination",
                        message = $"'page' must be between {MIN_PAGE} and {MAX_PAGE}",
                        received = pageValue.ToString()
                    });
                    return;
                }
            }

            if (request.Query.TryGetValue("pageSize", out var pageSizeValue))
            {
                if (!int.TryParse(pageSizeValue, out var pageSize) || pageSize < MIN_PAGE_SIZE || pageSize > MAX_PAGE_SIZE)
                {
                    context.Result = new BadRequestObjectResult(new
                    {
                        error = "Invalid pagination",
                        message = $"'pageSize' must be between {MIN_PAGE_SIZE} and {MAX_PAGE_SIZE}",
                        received = pageSizeValue.ToString()
                    });
                    return;
                }
            }

            base.OnActionExecuting(context);
        }
    }
}
