using System;

namespace Swiddler.Utils
{
    public static class DateTimeExtensions
    {
        public static DateTime LastDayOfWeek(this DateTime date, DayOfWeek dayofweek)
        {
            int adjustment = (int)date.DayOfWeek < (int)dayofweek ? 7 : 0;
            return date.Date.AddDays(0 - ((int)date.DayOfWeek + adjustment - (int)dayofweek));
        }

        public static DateTime NextDayOfWeek(this DateTime date, DayOfWeek dayofweek)
        {
            return date.LastDayOfWeek(dayofweek).AddDays(7).Date;
        }

        public static long GetUnixTimeSeconds(this DateTime utcDate)
        {
            const long UnixEpochSeconds = 62_135_596_800;

            long seconds = utcDate.Ticks / TimeSpan.TicksPerSecond;
            return seconds - UnixEpochSeconds;
        }

        public static ulong GetUnixMicroSeconds(this DateTime utcDate)
        {
            const decimal UnixEpochMicroSeconds = 62_135_596_800_000_000;

            decimal us = utcDate.Ticks / (decimal)(TimeSpan.TicksPerMillisecond / 1000.0);
            return (ulong)(us - UnixEpochMicroSeconds);
        }

        public static int GetMicroSeconds(this DateTime utcDate)
        {
            return utcDate.Millisecond * 1_000;
        }
    }
}
