using System.Collections.Generic;

namespace HoopGameNight.Api.Options
{
    public class RateLimitOptions
    {
        public int PermitLimit { get; set; } = 100;
        public int WindowMinutes { get; set; } = 1;
        public int QueueLimit { get; set; } = 10;
        public Dictionary<string, PolicyOptions> Policies { get; set; } = new();
    }

    public class PolicyOptions
    {
        public int PermitLimit { get; set; }
        public int WindowMinutes { get; set; }
        public int QueueLimit { get; set; }
    }
}