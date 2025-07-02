using HoopGameNight.Core.Enums;

namespace HoopGameNight.Core.Models.Entities
{
    public class Player : BaseEntity
    {
        public int ExternalId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public PlayerPosition? Position { get; set; }
        public int? HeightFeet { get; set; }
        public int? HeightInches { get; set; }
        public int? WeightPounds { get; set; }
        public int? TeamId { get; set; }

        // Navigation Properties (não persistidas no banco)
        public Team? Team { get; set; }

        // Computed Properties
        public string FullName => $"{FirstName} {LastName}".Trim();
        public string DisplayName => $"{FullName} ({Position?.ToString() ?? "N/A"})";

        public string Height => HeightFeet.HasValue && HeightInches.HasValue
            ? $"{HeightFeet}'{HeightInches}\""
            : "N/A";

        public string Weight => WeightPounds.HasValue
            ? $"{WeightPounds} lbs"
            : "N/A";

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