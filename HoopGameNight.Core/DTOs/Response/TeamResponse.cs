namespace HoopGameNight.Core.DTOs.Response
{
    public class TeamResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Abbreviation { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Conference { get; set; } = string.Empty;
        public string ConferenceDisplay { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public class TeamSummaryResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Abbreviation { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
}