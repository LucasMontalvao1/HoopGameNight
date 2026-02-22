namespace HoopGameNight.Core.DTOs.Request
{
    public class PlayerStatsRequest
    {
        public int PlayerId { get; set; }
        public int? Season { get; set; }
        public int LastGames { get; set; } = 5;
        public bool IncludeCareer { get; set; } = true;
        public bool IncludeCurrentSeason { get; set; } = true;
    }
}
