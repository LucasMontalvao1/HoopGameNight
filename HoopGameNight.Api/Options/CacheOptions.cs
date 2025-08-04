namespace HoopGameNight.Api.Options
{
    public class CacheOptions
    {
        public int DefaultExpirationMinutes { get; set; } = 30;
        public int SizeLimit { get; set; } = 1024;
        public double CompactionPercentage { get; set; } = 0.25;
        public int ExpirationScanFrequencyMinutes { get; set; } = 5;
    }
}