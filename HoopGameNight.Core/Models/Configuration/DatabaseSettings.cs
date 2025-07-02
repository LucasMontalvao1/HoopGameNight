namespace HoopGameNight.Core.Models.Configuration
{
    public class DatabaseSettings
    {
        public string MySqlConnection { get; set; } = string.Empty;
        public int CommandTimeout { get; set; } = 30;
        public bool EnableRetryOnFailure { get; set; } = true;
        public int MaxRetryCount { get; set; } = 3;
    }
}