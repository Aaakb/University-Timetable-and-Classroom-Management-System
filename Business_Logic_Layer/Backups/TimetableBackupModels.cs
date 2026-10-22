using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

public sealed record TimetableBackupInfo(string Path, DateTimeOffset CreatedAtUtc, int LectureCount, int SemesterOneCount, int SemesterTwoCount);
public sealed record TimetableRestoreResult(int RestoredCount, string SafetyBackupPath);
public sealed class TimetableRestorePreview
{
    public DateTimeOffset CreatedAtUtc { get; internal init; }
    public int LectureCount { get; internal init; }
    public int CurrentLectureCount { get; internal init; }
    public int SemesterOneCount { get; internal init; }
    public int SemesterTwoCount { get; internal init; }
    public IReadOnlyList<string> Issues { get; internal init; } = [];
    public bool CanRestore => Document is not null && Issues.Count == 0;
    internal TimetableBackupDocument? Document { get; init; }
    internal string CurrentVersion { get; init; } = "";
}
internal sealed record TimetableBackupLecture(int ScheduleID, int SubjectID, int FacultyMemberID, int ClassroomID,
    int TimeSlotID, int SemesterNumber, string LectureType, string? GroupName, string DayOfWeek,
    int? StudyYearID, int? BranchID, int? SectionID, bool IsLocked)
{
    public static TimetableBackupLecture From(Schedule s) => new(s.ScheduleID,s.SubjectID,s.FacultyMemberID,s.ClassroomID,
        s.TimeSlotID,s.SemesterNumber,s.LectureType,s.GroupName,s.DayOfWeek,s.StudyYearID,s.BranchID,s.SectionID,s.IsLocked);
    public Schedule ToEntity() => new() { ScheduleID=ScheduleID,SubjectID=SubjectID,FacultyMemberID=FacultyMemberID,
        ClassroomID=ClassroomID,TimeSlotID=TimeSlotID,SemesterNumber=SemesterNumber,LectureType=LectureType,
        GroupName=GroupName,DayOfWeek=DayOfWeek,StudyYearID=StudyYearID,BranchID=BranchID,SectionID=SectionID,IsLocked=IsLocked };
}
internal sealed record TimetableBackupReference(string Table, string Key, string Values);
internal sealed record TimetableBackupPayload(int Version, DateTimeOffset CreatedAtUtc, string Database,
    List<TimetableBackupLecture> Lectures, List<TimetableBackupReference> References);
internal sealed record TimetableBackupDocument(string Format, TimetableBackupPayload Payload, string Checksum);
