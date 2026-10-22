using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    internal sealed record ScheduleBuildContext(
        IReadOnlyCollection<Classroom> Classrooms,
        IReadOnlyCollection<TimeSlot> TimeSlots)
    {
        public bool RequirePracticalExchange { get; init; } = true;
        public IReadOnlyList<Schedule> FixedSchedules { get; init; } = [];
        // Retry incremental completion on the full configured teaching week.
        public bool UseAllTeachingDays { get; init; }
        public int VariationSeed { get; init; }
        public bool EnforceSectionDayLimit { get; init; }
        public CancellationToken CancellationToken { get; init; }
        public IReadOnlyDictionary<(int Faculty, int Semester), FacultyScheduleRequest> Preferences { get; init; }
            = new Dictionary<(int, int), FacultyScheduleRequest>();
        public double RepairTimeLimitSeconds { get; init; } = 20;
        public int RepairAttemptLimit { get; init; } = 20000;
    }
}
