using HoopGameNight.Core.Enums;

namespace HoopGameNight.Core.DTOs.Request
{
    public class GetGamesRequest : PaginatedRequest  // PÚBLICO
    {
        public DateTime? Date { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? TeamId { get; set; }
        public GameStatus? Status { get; set; }
        public bool? PostSeason { get; set; }
        public int? Season { get; set; }

        public override bool IsValid()
        {
            if (!base.IsValid()) return false;

            if (StartDate.HasValue && EndDate.HasValue && StartDate > EndDate)
                return false;

            if (Season.HasValue && Season < 2000)
                return false;

            return true;
        }
    }
}