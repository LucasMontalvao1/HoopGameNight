namespace HoopGameNight.Core.Models.Configuration
{
    public class ApiSettings
    {
        public string Title { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ContactSettings Contact { get; set; } = new();
    }

    public class ContactSettings
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}