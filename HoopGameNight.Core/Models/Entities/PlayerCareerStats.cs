
namespace HoopGameNight.Core.Models.Entities
{
    public class PlayerCareerStats : BaseEntity
    {
        public int PlayerId { get; set; }

        public int TotalSeasons { get; set; }
        public int TotalGames { get; set; }
        public int TotalGamesStarted { get; set; }
        public decimal TotalMinutes { get; set; }
        public int TotalPoints { get; set; }
        public int TotalFieldGoalsMade { get; set; }
        public int TotalFieldGoalsAttempted { get; set; }
        public int TotalThreePointersMade { get; set; }
        public int TotalThreePointersAttempted { get; set; }
        public int TotalFreeThrowsMade { get; set; }
        public int TotalFreeThrowsAttempted { get; set; }
        public int TotalRebounds { get; set; }
        public int TotalAssists { get; set; }
        public int TotalSteals { get; set; }
        public int TotalBlocks { get; set; }
        public int TotalTurnovers { get; set; }

        public decimal CareerPPG { get; set; } 
        public decimal CareerRPG { get; set; }
        public decimal CareerAPG { get; set; } 
        public decimal CareerFgPercentage { get; set; }
        public decimal Career3PtPercentage { get; set; }
        public decimal CareerFtPercentage { get; set; }

        public int HighestPointsGame { get; set; }
        public int HighestReboundsGame { get; set; }
        public int HighestAssistsGame { get; set; }

        public DateTime? LastGameDate { get; set; }

        public Player? Player { get; set; }

        public decimal AverageMinutesPerGame => TotalGames > 0
            ? Math.Round(TotalMinutes / TotalGames, 1)
            : 0;

        public decimal FieldGoalPercentage => TotalFieldGoalsAttempted > 0
            ? Math.Round((decimal)TotalFieldGoalsMade / TotalFieldGoalsAttempted * 100, 1)
            : 0;

        public decimal ThreePointPercentage => TotalThreePointersAttempted > 0
            ? Math.Round((decimal)TotalThreePointersMade / TotalThreePointersAttempted * 100, 1)
            : 0;

        public decimal FreeThrowPercentage => TotalFreeThrowsAttempted > 0
            ? Math.Round((decimal)TotalFreeThrowsMade / TotalFreeThrowsAttempted * 100, 1)
            : 0;

        public int YearsPlayed => TotalSeasons;

        public bool IsRookie => TotalSeasons <= 1;

        public bool IsVeteran => TotalSeasons >= 10;

        public string CareerSummary => TotalGames > 0
            ? $"{CareerPPG} PPG, {CareerRPG} RPG, {CareerAPG} APG over {TotalGames} games"
            : "No career data available";

        public override string ToString() => $"{Player?.FullName} Career: {CareerSummary}";
    }
}