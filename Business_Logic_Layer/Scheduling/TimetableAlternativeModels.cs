using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

public enum TimetableAlternativeGoal { FewestChanges, FewerFacultyGaps, BalancedStudentLoad }
public sealed record TimetableAlternativesRequest(int Semester, int Count = 3, double TimeLimitSeconds = 30);
public sealed record TimetableAlternativeMetrics(decimal PreferencePenalty, int FacultyGapMinutes,
    int StudentGapMinutes, int PeakStudentDailyMinutes, int ChangedLectures, int AffectedFaculty, int TotalAttendanceDays);
public sealed record TimetableAlternativeLecture(int Id, string Faculty, string Subject, string Section,
    string Day, string Period, string Room, bool Locked, bool Changed, string LectureType = "", string? GroupName = null);
public sealed class TimetableAlternative
{
    public string Title { get; internal init; } = "";
    public string Status { get; internal init; } = "";
    public TimetableAlternativeMetrics Metrics { get; internal init; } = null!;
    public IReadOnlyList<TimetableAlternativeLecture> Lectures { get; internal init; } = [];
    public double SearchSeconds { get; internal init; }
    internal IReadOnlyList<Schedule> Plan { get; init; } = [];
    public string Version { get; internal init; } = "";
    internal int Semester { get; init; }
}
public sealed record TimetableAlternativesPreview(TimetableAlternative Current,
    IReadOnlyList<TimetableAlternative> Alternatives, string Message, string Version);

internal sealed record TimetableAlternativeSearch(TimetableAlternativeGoal Goal,
    IReadOnlyList<IReadOnlyList<Schedule>> ExcludedPlans, long? MaximumPreferencePenalty);
