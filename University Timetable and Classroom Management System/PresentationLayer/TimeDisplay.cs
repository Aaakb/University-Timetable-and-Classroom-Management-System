using System.Globalization;

namespace University_Timetable_and_Classroom_Management_System
{
    internal static class TimeDisplay
    {
        public static string Format(TimeSpan time)
        {
            return DateTime.Today
                .Add(time)
                .ToString("hh:mm tt", CultureInfo.InvariantCulture);
        }

        public static string FormatRange(TimeSpan startTime, TimeSpan endTime)
        {
            return $"{Format(startTime)} - {Format(endTime)}";
        }
    }
}
