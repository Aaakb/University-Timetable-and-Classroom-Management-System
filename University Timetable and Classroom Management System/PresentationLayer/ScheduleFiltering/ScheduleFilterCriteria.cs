namespace University_Timetable_and_Classroom_Management_System
{
    internal sealed class ScheduleFilterCriteria
    {
        public int? FacultyId { get; init; }

        public int? SectionId { get; init; }

        public int? StudyYearId { get; init; }

        public int? SemesterNumber { get; init; }

        public string? LectureType { get; init; }

        public string? GroupName { get; init; }

        public string? DayOfWeek { get; init; }
    }
}
