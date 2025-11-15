using HoopGameNight.Core.Enums;

namespace HoopGameNight.Core.Models.Entities
{
    public class Team : BaseEntity
    {
        public int ExternalId { get; set; }
        public string? EspnId { get; set; } 
        public string Name { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Abbreviation { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public Conference Conference { get; set; }
        public string Division { get; set; } = string.Empty;
        public string DisplayName => $"{City} {Name}";
        public string ConferenceDisplay => Conference.ToString();

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Name) &&
                   !string.IsNullOrWhiteSpace(City) &&
                   !string.IsNullOrWhiteSpace(Abbreviation) &&
                   ExternalId > 0;
        }

        public override string ToString() => DisplayName;
    }
}