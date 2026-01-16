using HoopGameNight.Core.Enums;
using System;
using System.Collections.Generic;

namespace HoopGameNight.Core.Models.Entities
{
    public class Player : BaseEntity
    {
        public int ExternalId { get; set; } 
        public string? EspnId { get; set; } 
        public string? NbaStatsId { get; set; } 

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
        public Team? Team { get; set; }
        public ICollection<PlayerSeasonStats> SeasonStats { get; set; } = new List<PlayerSeasonStats>();
        public ICollection<PlayerGameStats> GameStats { get; set; } = new List<PlayerGameStats>();
        public PlayerCareerStats? CareerStats { get; set; }
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

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(FirstName) &&
                   !string.IsNullOrWhiteSpace(LastName) &&
                   ExternalId > 0;
        }

        public override string ToString() => FullName;
    }
}