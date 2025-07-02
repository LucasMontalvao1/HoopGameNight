namespace HoopGameNight.Core.DTOs.Response
{
    public class PlayerResponse
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Position { get; set; }
        public string? PositionDisplay { get; set; }
        public string Height { get; set; } = string.Empty;
        public string Weight { get; set; } = string.Empty;
        public TeamSummaryResponse? Team { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }

    public class PlayerSummaryResponse
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Position { get; set; }
        public string? Team { get; set; }
    }
}