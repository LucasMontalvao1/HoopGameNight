namespace HoopGameNight.Core.Exceptions
{
    public class BusinessException : Exception
    {
        public string ErrorCode { get; }

        public BusinessException(string message, string errorCode = "BUSINESS_ERROR")
            : base(message)
        {
            ErrorCode = errorCode;
        }

        public BusinessException(string message, Exception innerException, string errorCode = "BUSINESS_ERROR")
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }

    public class EntityNotFoundException : BusinessException
    {
        public EntityNotFoundException(string entityName, object id)
            : base($"{entityName} com ID '{id}' não foi encontrado", "ENTITY_NOT_FOUND")
        {
        }

        public EntityNotFoundException(string entityName, string property, object value)
            : base($"{entityName} com {property} '{value}' não foi encontrado", "ENTITY_NOT_FOUND")
        {
        }
    }

    public class ExternalApiException : Exception
    {
        public string ApiName { get; }
        public int? StatusCode { get; }

        public ExternalApiException(string apiName, string message, int? statusCode = null)
            : base(message)
        {
            ApiName = apiName;
            StatusCode = statusCode;
        }

        public ExternalApiException(string apiName, string message, Exception innerException, int? statusCode = null)
            : base(message, innerException)
        {
            ApiName = apiName;
            StatusCode = statusCode;
        }
    }
}