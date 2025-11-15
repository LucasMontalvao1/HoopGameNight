namespace HoopGameNight.Core.DTOs.External.ESPN
{
    /// <summary>
    /// DTO para times da ESPN API
    /// </summary>
    public class EspnTeamDto
    {
        public string Id { get; set; } = string.Empty;
        public string Uid { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty; 
        public string Name { get; set; } = string.Empty;
        public string Abbreviation { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty; 
        public string ShortDisplayName { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string AlternateColor { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsAllStar { get; set; }

        // Logos
        public List<EspnLogoDto> Logos { get; set; } = new();
    }

    public class EspnLogoDto
    {
        public string Href { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public string Alt { get; set; } = string.Empty;
        public List<string> Rel { get; set; } = new();
    }
}
