namespace HoopGameNight.Core.DTOs.Request
{
    public class SearchPlayerRequest : PaginatedRequest  // PÚBLICO
    {
        public string? Search { get; set; }
        public int? TeamId { get; set; }
        public string? Position { get; set; }

        public override bool IsValid()
        {
            if (!base.IsValid()) return false;

            if (string.IsNullOrWhiteSpace(Search) && !TeamId.HasValue && string.IsNullOrWhiteSpace(Position))
                return false;

            if (!string.IsNullOrWhiteSpace(Search) && Search.Length < 2)
                return false;

            return true;
        }
    }
}