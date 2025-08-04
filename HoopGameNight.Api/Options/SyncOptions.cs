namespace HoopGameNight.Api.Options
{
    public class SyncOptions
    {
        public bool EnableAutoSync { get; set; } = true;
        public int SyncIntervalMinutes { get; set; } = 60;
        public int MaxRetries { get; set; } = 3;
        public int RetryDelaySeconds { get; set; } = 5;
        public int BatchSize { get; set; } = 100;
    }
}