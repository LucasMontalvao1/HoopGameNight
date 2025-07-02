namespace HoopGameNight.Api.Configurations
{
    public class ExternalApiConfiguration
    {
        public BallDontLieConfiguration BallDontLie { get; set; } = new();
    }

    public class BallDontLieConfiguration
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public int Timeout { get; set; } = 30;
        public int RateLimit { get; set; } = 60;
        public RetryPolicyConfiguration RetryPolicy { get; set; } = new();
    }

    public class RetryPolicyConfiguration
    {
        public int RetryCount { get; set; } = 3;
        public int[] WaitAndRetry { get; set; } = { 1, 2, 4 };
    }
}