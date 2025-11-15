
namespace HoopGameNight.Core.Models.Entities
{
    public class PlayerGameStats : BaseEntity
    {
        public int PlayerId { get; set; }
        public int GameId { get; set; }
        public int TeamId { get; set; }

        public bool DidNotPlay { get; set; } = false;
        public bool IsStarter { get; set; } = false;

        public int MinutesPlayed { get; set; }
        public int SecondsPlayed { get; set; }

        public int Points { get; set; }
        public int FieldGoalsMade { get; set; }
        public int FieldGoalsAttempted { get; set; }

        public int ThreePointersMade { get; set; }
        public int ThreePointersAttempted { get; set; }

        public int FreeThrowsMade { get; set; }
        public int FreeThrowsAttempted { get; set; }

        public int OffensiveRebounds { get; set; }
        public int DefensiveRebounds { get; set; }
        public int TotalRebounds { get; set; }

        public int Assists { get; set; }
        public int Steals { get; set; }
        public int Blocks { get; set; }
        public int Turnovers { get; set; }
        public int PersonalFouls { get; set; }

        public int PlusMinus { get; set; }

        public Player? Player { get; set; }
        public Game? Game { get; set; }
        public Team? Team { get; set; }

        public string MinutesFormatted => $"{MinutesPlayed}:{SecondsPlayed:D2}";

        public string FieldGoalsFormatted => $"{FieldGoalsMade}/{FieldGoalsAttempted}";

        public string ThreePointersFormatted => $"{ThreePointersMade}/{ThreePointersAttempted}";

        public string FreeThrowsFormatted => $"{FreeThrowsMade}/{FreeThrowsAttempted}";

        public decimal FieldGoalPercentage => FieldGoalsAttempted > 0
            ? Math.Round((decimal)FieldGoalsMade / FieldGoalsAttempted * 100, 1)
            : 0;

        public decimal ThreePointPercentage => ThreePointersAttempted > 0
            ? Math.Round((decimal)ThreePointersMade / ThreePointersAttempted * 100, 1)
            : 0;

        public decimal FreeThrowPercentage => FreeThrowsAttempted > 0
            ? Math.Round((decimal)FreeThrowsMade / FreeThrowsAttempted * 100, 1)
            : 0;

        public bool DoubleDouble => GetStatsAboveTen() >= 2;

        public bool TripleDouble => GetStatsAboveTen() >= 3;

        private int GetStatsAboveTen()
        {
            int count = 0;
            if (Points >= 10) count++;
            if (TotalRebounds >= 10) count++;
            if (Assists >= 10) count++;
            if (Steals >= 10) count++;
            if (Blocks >= 10) count++;
            return count;
        }

        public override string ToString() => $"{Player?.FullName} vs {Game?.VisitorTeam?.Abbreviation ?? "N/A"} - {Points}pts, {TotalRebounds}reb, {Assists}ast";
    }
}