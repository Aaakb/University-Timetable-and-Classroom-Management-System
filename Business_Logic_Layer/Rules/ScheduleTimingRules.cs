using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    internal static class ScheduleTimingRules
    {
        public static readonly TimeSpan WorkdayStart = new(8, 30, 0);
        public static readonly TimeSpan WorkdayEnd = new(12, 50, 0);
        public static readonly TimeSpan LectureDuration = TimeSpan.FromMinutes(80);
        public static readonly TimeSpan LectureGap = TimeSpan.FromMinutes(10);
        public static readonly TimeSpan BreakStart = TimeSpan.Zero;
        public static readonly TimeSpan BreakEnd = TimeSpan.Zero;

        public static IReadOnlyList<(TimeSpan Start, TimeSpan End)> OfficialLectureSlots { get; } =
        [
            (new TimeSpan(8, 30, 0), new TimeSpan(9, 50, 0)),
            (new TimeSpan(10, 0, 0), new TimeSpan(11, 20, 0)),
            (new TimeSpan(11, 30, 0), new TimeSpan(12, 50, 0))
        ];

        public static bool IsOfficialLectureSlot(TimeSlot timeSlot)
        {
            return !timeSlot.IsBreak && OfficialLectureSlots.Any(slot =>
                slot.Start == timeSlot.StartTime &&
                slot.End == timeSlot.EndTime);
        }

        public static bool IsValidLectureRange(TimeSpan startTime, TimeSpan endTime)
        {
            return endTime - startTime == LectureDuration &&
                OfficialLectureSlots.Any(slot => slot.Start == startTime && slot.End == endTime);
        }
    }
}
