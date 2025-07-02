namespace HoopGameNight.Core.Extensions
{
    public static class DateTimeExtensions
    {
        public static bool IsToday(this DateTime date)
        {
            return date.Date == DateTime.Today;
        }

        public static bool IsTomorrow(this DateTime date)
        {
            return date.Date == DateTime.Today.AddDays(1);
        }

        public static bool IsYesterday(this DateTime date)
        {
            return date.Date == DateTime.Today.AddDays(-1);
        }

        public static string ToGameTimeString(this DateTime dateTime)
        {
            return dateTime.ToString("HH:mm");
        }

        public static string ToGameDateString(this DateTime date)
        {
            return date.ToString("yyyy-MM-dd");
        }
    }
}