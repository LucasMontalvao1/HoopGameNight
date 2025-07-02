namespace HoopGameNight.Api.Configurations
{
    public class CorsConfiguration
    {
        public string PolicyName { get; set; } = string.Empty;
        public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
        public string[] AllowedMethods { get; set; } = Array.Empty<string>();
        public string[] AllowedHeaders { get; set; } = Array.Empty<string>();
        public bool AllowCredentials { get; set; }
    }
}