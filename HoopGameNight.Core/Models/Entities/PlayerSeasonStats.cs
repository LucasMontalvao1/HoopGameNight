
namespace HoopGameNight.Core.Models.Entities
{
    public class PlayerSeasonStats : BaseEntity
    {
        public int PlayerId { get; set; }
        public int Season { get; set; }
        public int? TeamId { get; set; }
        public int GamesPlayed { get; set; }
        public int GamesStarted { get; set; }
        public decimal MinutesPlayed { get; set; }
        public int Points { get; set; }
        public int FieldGoalsMade { get; set; }
        public int FieldGoalsAttempted { get; set; }
        public decimal? FieldGoalPercentage { get; set; }
        public int ThreePointersMade { get; set; }
        public int ThreePointersAttempted { get; set; }
        public decimal? ThreePointPercentage { get; set; }
        public int FreeThrowsMade { get; set; }
        public int FreeThrowsAttempted { get; set; }
        public decimal? FreeThrowPercentage { get; set; }
        public int OffensiveRebounds { get; set; }
        public int DefensiveRebounds { get; set; }
        public int TotalRebounds { get; set; }
        public int Assists { get; set; }
        public int Steals { get; set; }
        public int Blocks { get; set; }
        public int Turnovers { get; set; }
        public int PersonalFouls { get; set; }
        public decimal AvgPoints { get; set; }
        public decimal AvgRebounds { get; set; }
        public decimal AvgAssists { get; set; }
        public decimal AvgMinutes { get; set; }
        public Player? Player { get; set; }
        public Team? Team { get; set; }
        public decimal PPG => GamesPlayed > 0 ? Math.Round((decimal)Points / GamesPlayed, 1) : 0;
        public decimal RPG => GamesPlayed > 0 ? Math.Round((decimal)TotalRebounds / GamesPlayed, 1) : 0;
        public decimal APG => GamesPlayed > 0 ? Math.Round((decimal)Assists / GamesPlayed, 1) : 0;
        public decimal MPG => GamesPlayed > 0 ? Math.Round(MinutesPlayed / GamesPlayed, 1) : 0;
        public decimal SPG => GamesPlayed > 0 ? Math.Round((decimal)Steals / GamesPlayed, 1) : 0;
        public decimal BPG => GamesPlayed > 0 ? Math.Round((decimal)Blocks / GamesPlayed, 1) : 0;

        public override string ToString() => $"{Player?.FullName} - {Season} Season ({PPG} PPG, {RPG} RPG, {APG} APG)";
    }
}