using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    internal static class ScheduleTimingRules
    {
        public static readonly TimeSpan LectureGap = TimeSpan.FromMinutes(10);
        public static readonly TimeSpan BreakStart = TimeSpan.Zero;
        public static readonly TimeSpan BreakEnd = TimeSpan.Zero;

        public static IReadOnlyList<(TimeSpan Start, TimeSpan End)> DefaultLectureSlots { get; } =
        [
            (new TimeSpan(8, 30, 0), new TimeSpan(9, 50, 0)),
            (new TimeSpan(10, 0, 0), new TimeSpan(11, 20, 0)),
            (new TimeSpan(11, 30, 0), new TimeSpan(12, 50, 0))
        ];

        public static bool IsValidLectureRange(TimeSpan startTime, TimeSpan endTime)
        {
            return startTime >= TimeSpan.Zero && endTime < TimeSpan.FromDays(1) &&
                endTime > startTime && startTime.Ticks % TimeSpan.TicksPerMinute == 0 &&
                endTime.Ticks % TimeSpan.TicksPerMinute == 0;
        }

        public static void EnsureCompatibleLectureSlots(IReadOnlyList<TimeSlot> timeSlots)
        {
            if (timeSlots.Count == 0)
                throw new ArgumentException("Add lecture periods in Time Slots before scheduling.");
            var ordered = timeSlots.OrderBy(slot => slot.StartTime).ToArray();
            for (int i = 0; i < ordered.Length; i++)
            {
                if (ordered[i].IsBreak || !IsValidLectureRange(ordered[i].StartTime, ordered[i].EndTime))
                    throw new ArgumentException("Correct invalid lecture periods in Time Slots before scheduling.");
                if (i > 0 && ordered[i].StartTime < ordered[i - 1].EndTime + LectureGap)
                    throw new ArgumentException("Lecture periods must not overlap and must leave a 10-minute gap. Adjust Time Slots before scheduling.");
            }
        }
    }
}
