namespace HoopGameNight.Api.Configurations
{
    public class ApiConfiguration
    {
        public string Title { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ContactConfiguration Contact { get; set; } = new();
    }

    public class ContactConfiguration
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}