namespace HoopGameNight.Core.DTOs.Response
{
    public class PaginatedResponse<T> : ApiResponse<List<T>>
    {
        public PaginationMetadata Pagination { get; set; } = new();

        public static PaginatedResponse<T> Create(
            List<T> data,
            int page,
            int pageSize,
            int totalCount,
            string message = "Success")
        {
            return new PaginatedResponse<T>
            {
                Success = true,
                Message = message,
                Data = data,
                Pagination = new PaginationMetadata
                {
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                }
            };
        }
    }

    public class PaginationMetadata
    {
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public bool HasPrevious => CurrentPage > 1;
        public bool HasNext => CurrentPage < TotalPages;
    }
}