namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

public sealed record ImportSheetSchema(string Name, string[] Required, string[] Optional)
{
    public IEnumerable<string> Columns => Required.Concat(Optional);
}
public static class ExcelImportSchema
{
    public static ImportSheetSchema NamedAssignments { get; } = new("NamedAssignments",
        [], ["FacultyName", "SubjectName", "StudyYear", "Branch", "StudySession", "SemesterNumber", "TheoreticalHours", "PracticalHours", "CreditUnits", "AcademicTitle"]);
    public static IEnumerable<ImportSheetSchema> AcademicInputs => Academic.Where(s => s.Name is "FacultyMembers" or "Subjects").Append(NamedAssignments);
    public static IReadOnlyList<ImportSheetSchema> Academic { get; } =
    [
        new("StudyYears", ["StudyYearID", "YearName"], []),
        new("Branches", ["BranchID", "BranchName"], []),
        new("FacultyMembers", ["FacultyMemberID", "FullName"], ["AcademicTitle"]),
        new("Classrooms", ["ClassroomID", "ClassroomNumber", "Capacity", "RoomType"], []),
        new("TimeSlots", ["TimeSlotID", "StartTime", "EndTime"], []),
        new("Sections", ["SectionID", "SectionName", "StudentCount", "StudyYearID"], ["BranchID"]),
        new("Subjects", ["SubjectID", "SubjectName", "StudyYearID", "SemesterNumber", "TheoreticalHours", "PracticalHours"], ["CreditUnits", "BranchID", "TheoreticalLectureCount", "PracticalLectureCount"]),
        new("FacultyMemberSubjects", ["FacultyMemberID", "SubjectID"], ["SectionID", "LectureType"])
    ];
    public static ImportSheetSchema Timetable { get; } = new("Timetable",
        ["Day", "TimeSlot", "Subject", "Faculty", "Classroom", "Section", "LectureType", "SemesterNumber"],
        ["GroupName", "StudyYear", "Branch", "Locked", "ScheduleID", "EndTime", "StudySession", "SourceText", "ImportNote"]);
    public static IEnumerable<ImportSheetSchema> For(ExcelImportMode mode)
        => mode == ExcelImportMode.AcademicData ? AcademicInputs.Where(s => s.Name != NamedAssignments.Name) : [Timetable];
}
