namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    public sealed record ScheduleGenerationResult(
        int CreatedCount,
        int SkippedCount,
        int RequiredCount,
        int UnassignedSubjectsCount,
        int MissingSectionCount,
        int NoClassroomCount,
        int ConflictCount,
        int DuplicateAssignmentCount,
        int TimeSlotCount,
        int ClassroomCount,
        int SectionCount,
        int AddedTimeSlotCount,
        int AddedClassroomCount,
        int AddedFacultyMemberCount,
        int AddedFacultySubjectAssignmentCount);
}
