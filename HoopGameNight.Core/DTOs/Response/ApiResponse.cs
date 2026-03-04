using System;

namespace HoopGameNight.Core.DTOs.Response
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; } = true;
        public string Message { get; set; } = "Success";
        public T? Data { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? RequestId { get; set; }
        public bool IsSyncing { get; set; } = false;

        public static ApiResponse<T> SuccessResult(T data, string message = "Success", bool isSyncing = false)
        {
            return new ApiResponse<T>
            {
                Success = true,
                Message = message,
                Data = data,
                IsSyncing = isSyncing
            };
        }

        public static ApiResponse<T> ErrorResult(string message, T? data = default)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                Data = data
            };
        }
    }
}