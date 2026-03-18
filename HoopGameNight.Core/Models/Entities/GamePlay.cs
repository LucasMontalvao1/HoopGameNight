using System;

namespace HoopGameNight.Core.Models.Entities
{
    public class GamePlay : BaseEntity
    {
        public int GameId { get; set; }
        public string? ExternalId { get; set; }
        public int Sequence { get; set; }
        public int Period { get; set; }
        public string? Clock { get; set; }
        public string Text { get; set; } = string.Empty;
        public string? Type { get; set; }
        public int? TeamId { get; set; }
        public int? PlayerId { get; set; }
        public int ScoreValue { get; set; }
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Game? Game { get; set; }
    }
}
