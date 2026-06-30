using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    internal sealed record SubjectTeachingAssignment(
        Subject Subject,
        IReadOnlyList<int> FacultyMemberIds);

    internal sealed record ScheduleGenerationRequest(
        Subject Subject,
        IReadOnlyList<int> FacultyMemberIds,
        Section Section,
        int LessonNumber,
        int RequiredLessons,
        string LectureType,
        string? GroupName);

    internal sealed record SchedulePlanningResult(
        List<Schedule> Schedules,
        int NoClassroomCount,
        int ConflictCount);

    internal sealed record ScheduleRequestPlan(
        ScheduleGenerationRequest Request,
        List<SchedulePlacementCandidate> Candidates,
        int DifficultyScore);

    internal sealed record ScheduleDayPlanKey(
        int StudyYearID,
        int SectionID,
        int SemesterNumber);

    internal sealed record ScheduleDayPlan(
        IReadOnlyList<string> Days,
        int TargetDayCount);

    internal sealed record ScheduleRequestIdentity(
        int SubjectID,
        int SectionID,
        string LectureType,
        string? GroupName,
        int LessonNumber)
    {
        public static ScheduleRequestIdentity From(ScheduleGenerationRequest request)
        {
            return new ScheduleRequestIdentity(
                request.Subject.SubjectID,
                request.Section.SectionID,
                request.LectureType,
                request.GroupName,
                request.LessonNumber);
        }
    }

    internal sealed record SchedulePlacementCandidate(
        string DayOfWeek,
        TimeSlot TimeSlot,
        Classroom Classroom,
        int RequiredCapacity);

    internal sealed record SchedulePlacementOption(
        Schedule Schedule,
        int Score);
}
