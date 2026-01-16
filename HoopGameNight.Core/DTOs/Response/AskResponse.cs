using System.Collections.Generic;

namespace HoopGameNight.Core.DTOs.Response
{
    public class AskResponse
    {
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public int GamesAnalyzed { get; set; }
        public bool FromCache { get; set; }
        public string DataSource { get; set; } = "Database";
        public string Period { get; set; } = string.Empty;
        public List<string> DetectedTeams { get; set; } = new();
    }
}