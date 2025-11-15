namespace HoopGameNight.Core.Configuration
{
    public class RedisSettings
    {
        public string ConnectionString { get; set; } = "localhost:6379";
        public string InstanceName { get; set; } = "HoopGameNight:";
        public bool Enabled { get; set; } = true;
        public bool AbortOnConnectFail { get; set; } = false; 
        public int ConnectTimeout { get; set; } = 5000;
        public int SyncTimeout { get; set; } = 5000;
        public int DefaultDatabase { get; set; } = 0;
        public bool Ssl { get; set; } = false;
        public CacheExpirationSettings CacheExpiration { get; set; } = new();
    }

    public class CacheExpirationSettings
    {
        public int TodayGames { get; set; } = 300; // 5 minutos
        public int GamesByDate { get; set; } = 900; // 15 minutos
        public int FutureGames { get; set; } = 21600; // 6 horas
        public int Teams { get; set; } = 86400; // 24 horas
        public int Players { get; set; } = 3600; // 1 hora
    }
}
