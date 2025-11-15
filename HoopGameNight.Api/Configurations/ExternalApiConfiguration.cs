namespace HoopGameNight.Api.Configurations
{
    // REMOVIDO: BallDontLie configuration - API deprecated
    public class ExternalApiConfiguration
    {
        // Configuration for external APIs can be added here if needed
    }

    public class RetryPolicyConfiguration
    {
        public int RetryCount { get; set; } = 3;
        public int[] WaitAndRetry { get; set; } = { 1, 2, 4 };
    }
}