namespace HoopGameNight.Api.Options
{
    public class PlayerStatsSyncOptions
    {
        public bool Enabled { get; set; } = true;
        public int SyncIntervalHours { get; set; } = 6;
        public int MaxPlayersPerSync { get; set; } = 50; 
        public int DelayBetweenRequestsMs { get; set; } = 3000;
        public bool SyncTodayGames { get; set; } = true; 
        public bool SyncRecentGames { get; set; } = true; 
        public bool UpdateCareerStats { get; set; } = true; 
        public int RecentGamesToSync { get; set; } = 5;
    }
}
