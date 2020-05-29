using System;

namespace Swiddler.Utils
{
    public static class DateTimeExtensions
    {
        public static DateTime LastDayOfWeek(this DateTime date, DayOfWeek dayofweek)
        {
            int adjustment = (int)date.DayOfWeek < (int)dayofweek ? 7 : 0;
            return date.Date.AddDays(0 - (((int)(date.DayOfWeek) + adjustment) - (int)dayofweek));
        }

        public static DateTime NextDayOfWeek(this DateTime date, DayOfWeek dayofweek)
        {
            return date.LastDayOfWeek(dayofweek).AddDays(7).Date;
        }
    }
}
