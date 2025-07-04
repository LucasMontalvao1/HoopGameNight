using HoopGameNight.Core.Enums;

namespace HoopGameNight.Core.Models.Entities
{
    public class Game : BaseEntity
    {
        public int ExternalId { get; set; }
        public DateTime Date { get; set; }
        public DateTime DateTime { get; set; }
        public int HomeTeamId { get; set; }
        public int VisitorTeamId { get; set; }
        public int? HomeTeamScore { get; set; }
        public int? VisitorTeamScore { get; set; }
        public GameStatus Status { get; set; } = GameStatus.Scheduled;
        public int? Period { get; set; }
        public string? TimeRemaining { get; set; }
        public bool PostSeason { get; set; }
        public int Season { get; set; }

        // Navigation Properties 
        public Team? HomeTeam { get; set; }
        public Team? VisitorTeam { get; set; }

        // Computed Properties
        public bool IsToday => Date.Date == DateTime.Today;
        public bool IsTomorrow => Date.Date == DateTime.Today.AddDays(1);
        public bool IsLive => Status == GameStatus.Live;
        public bool IsCompleted => Status == GameStatus.Final;
        public bool IsScheduled => Status == GameStatus.Scheduled;

        public string Score => HomeTeamScore.HasValue && VisitorTeamScore.HasValue
            ? $"{HomeTeamScore} - {VisitorTeamScore}"
            : "0 - 0";

        public string GameTitle => HomeTeam != null && VisitorTeam != null
            ? $"{VisitorTeam.Abbreviation} @ {HomeTeam.Abbreviation}"
            : $"Team {VisitorTeamId} @ Team {HomeTeamId}";

        public Team? WinningTeam
        {
            get
            {
                if (!IsCompleted || !HomeTeamScore.HasValue || !VisitorTeamScore.HasValue)
                    return null;

                if (HomeTeamScore > VisitorTeamScore)
                    return HomeTeam;
                else if (VisitorTeamScore > HomeTeamScore)
                    return VisitorTeam;

                return null; 
            }
        }

        public int? WinningScore => WinningTeam?.Id == HomeTeamId ? HomeTeamScore : VisitorTeamScore;
        public int? LosingScore => WinningTeam?.Id == HomeTeamId ? VisitorTeamScore : HomeTeamScore;

        // Validation
        public bool IsValid()
        {
            return ExternalId > 0 &&
                   HomeTeamId > 0 &&
                   VisitorTeamId > 0 &&
                   HomeTeamId != VisitorTeamId &&
                   Season > 2000;
        }

        public override string ToString() => GameTitle;
    }
}