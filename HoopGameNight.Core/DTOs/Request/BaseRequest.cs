namespace HoopGameNight.Core.DTOs.Request
{
    public abstract class BaseRequest  
    {
        public virtual bool IsValid() => true;
    }

    public class PaginatedRequest : BaseRequest  
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;

        public int Skip => (Page - 1) * PageSize;
        public int Take => PageSize;

        public override bool IsValid()
        {
            return Page >= 1 && PageSize >= 1 && PageSize <= 100;
        }
    }
}