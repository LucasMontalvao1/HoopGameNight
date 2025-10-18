using HoopGameNight.Core.Enums;

namespace HoopGameNight.Core.Models.Entities
{
    public class Player : BaseEntity
    {
        // IDs de APIs externas (para evitar duplicidades e mapear entre APIs)
        public int ExternalId { get; set; } // Ball Don't Lie ID
        public string? EspnId { get; set; } // ESPN API ID
        public string? NbaStatsId { get; set; } // NBA Stats API ID (PERSON_ID)

        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public int? JerseyNumber { get; set; }
        public PlayerPosition? Position { get; set; }
        public int? HeightFeet { get; set; }
        public int? HeightInches { get; set; }
        public int? WeightPounds { get; set; }
        public DateTime? BirthDate { get; set; }
        public string? BirthCity { get; set; }
        public string? BirthCountry { get; set; }
        public string? College { get; set; }
        public int? DraftYear { get; set; }
        public int? DraftRound { get; set; }
        public int? DraftPick { get; set; }
        public int? TeamId { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation Properties
        public Team? Team { get; set; }
        public ICollection<PlayerSeasonStats> SeasonStats { get; set; } = new List<PlayerSeasonStats>();
        public ICollection<PlayerGameStats> GameStats { get; set; } = new List<PlayerGameStats>();
        public PlayerCareerStats? CareerStats { get; set; }

        // Computed Properties
        public string FullName => $"{FirstName} {LastName}".Trim();

        public string DisplayName => $"{FullName} ({Position?.ToString() ?? "N/A"})";

        public string Height => HeightFeet.HasValue && HeightInches.HasValue
            ? $"{HeightFeet}'{HeightInches}\""
            : "N/A";

        public string Weight => WeightPounds.HasValue
            ? $"{WeightPounds} lbs"
            : "N/A";

        public int? Age => BirthDate.HasValue
            ? DateTime.Today.Year - BirthDate.Value.Year
            : null;

        public int? HeightInInches => HeightFeet.HasValue && HeightInches.HasValue
            ? (HeightFeet * 12) + HeightInches
            : null;

        // Validation
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(FirstName) &&
                   !string.IsNullOrWhiteSpace(LastName) &&
                   ExternalId > 0;
        }

        public override string ToString() => FullName;
    }
}