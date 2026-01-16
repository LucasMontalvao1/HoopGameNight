using System;

namespace HoopGameNight.Core.Models.Entities
{
    public abstract class BaseEntity
    {
        private static DateTime GetCurrentTimestamp() => DateTime.UtcNow;

        public int Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        protected BaseEntity()
        {
            var timestamp = GetCurrentTimestamp();
            CreatedAt = timestamp;
            UpdatedAt = timestamp;
        }

        public virtual void UpdateTimestamp()
        {
            UpdatedAt = DateTime.UtcNow;
        }
    }
}