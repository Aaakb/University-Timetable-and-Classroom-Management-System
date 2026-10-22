using System.ComponentModel.DataAnnotations;
using University_Timetable_and_Classroom_Management_System.Models;
namespace UniversityTimetable.API;

public sealed class BranchDto
{
     public int BranchID { get; set; }
    [Required, StringLength(100)] public string BranchName { get; set; } = string.Empty;
    public Branch ToEntity(int id) => new() { BranchID = id, BranchName = BranchName };
    public static BranchDto From(Branch x) => new() { BranchID = x.BranchID, BranchName = x.BranchName };
}

public sealed class StudyYearDto
{
     public int StudyYearID { get; set; }
    [Required, StringLength(100)] public string YearName { get; set; } = string.Empty;
    public StudyYear ToEntity(int id) => new() { StudyYearID = id, YearName = YearName };
    public static StudyYearDto From(StudyYear x) => new() { StudyYearID = x.StudyYearID, YearName = x.YearName };
}

public sealed class ClassroomDto
{
     public int ClassroomID { get; set; }
    [Required, StringLength(100)] public string ClassroomNumber { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int Capacity { get; set; }
    [StringLength(50)] public string? RoomType { get; set; }
    public Classroom ToEntity(int id) => new() { ClassroomID = id, ClassroomNumber = ClassroomNumber, Capacity = Capacity, RoomType = RoomType };
    public static ClassroomDto From(Classroom x) => new() { ClassroomID = x.ClassroomID, ClassroomNumber = x.ClassroomNumber, Capacity = x.Capacity, RoomType = x.RoomType };
}

public sealed class FacultyMemberDto
{
     public int FacultyMemberID { get; set; }
    [Required, StringLength(150)] public string FullName { get; set; } = string.Empty;
    [StringLength(100)] public string? AcademicTitle { get; set; }
    public FacultyMember ToEntity(int id) => new() { FacultyMemberID = id, FullName = FullName, AcademicTitle = AcademicTitle };
    public static FacultyMemberDto From(FacultyMember x) => new() { FacultyMemberID = x.FacultyMemberID, FullName = x.FullName, AcademicTitle = x.AcademicTitle };
}

public sealed class SectionDto
{
     public int SectionID { get; set; }
    [Required, StringLength(50)] public string SectionName { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int StudentCount { get; set; }
    [Range(1, int.MaxValue)] public int StudyYearID { get; set; }
    [Range(1, int.MaxValue)] public int? BranchID { get; set; }
    public Section ToEntity(int id) => new() { SectionID = id, SectionName = SectionName, StudentCount = StudentCount, StudyYearID = StudyYearID, BranchID = BranchID };
    public static SectionDto From(Section x) => new() { SectionID = x.SectionID, SectionName = x.SectionName, StudentCount = x.StudentCount, StudyYearID = x.StudyYearID, BranchID = x.BranchID };
}

public sealed class SubjectDto
{
     public int SubjectID { get; set; }
    [Required, StringLength(100)] public string SubjectName { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int StudyYearID { get; set; }
    [Range(1, int.MaxValue)] public int? BranchID { get; set; }
    [Range(1,2)] public int SemesterNumber { get; set; }
    [Range(0, double.MaxValue)] public double TheoreticalHours { get; set; }
    [Range(0, double.MaxValue)] public double PracticalHours { get; set; }
    [Range(0, double.MaxValue)] public double CreditUnits { get; set; }
    [Range(0, Subject.MaximumWeeklyLectureCount)] public int? TheoreticalLectureCount { get; set; }
    [Range(0, Subject.MaximumWeeklyLectureCount)] public int? PracticalLectureCount { get; set; }
    // Accept legacy clients without exposing or requiring this retired input.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? RequirementType { get; set; }
    public Subject ToEntity(int id) => new() { SubjectID = id, SubjectName = SubjectName, StudyYearID = StudyYearID, BranchID = BranchID, SemesterNumber = SemesterNumber, TheoreticalHours = TheoreticalHours, PracticalHours = PracticalHours, CreditUnits = CreditUnits, TheoreticalLectureCount = TheoreticalLectureCount, PracticalLectureCount = PracticalLectureCount };
    public static SubjectDto From(Subject x) => new() { SubjectID = x.SubjectID, SubjectName = x.SubjectName, StudyYearID = x.StudyYearID, BranchID = x.BranchID, SemesterNumber = x.SemesterNumber, TheoreticalHours = x.TheoreticalHours, PracticalHours = x.PracticalHours, CreditUnits = x.CreditUnits, TheoreticalLectureCount = x.TheoreticalLectureCount, PracticalLectureCount = x.PracticalLectureCount };
}

public sealed class TimeSlotDto
{
     public int TimeSlotID { get; set; }
     public TimeSpan StartTime { get; set; }
     public TimeSpan EndTime { get; set; }
    public TimeSlot ToEntity(int id) => new() { TimeSlotID = id, StartTime = StartTime, EndTime = EndTime };
    public static TimeSlotDto From(TimeSlot x) => new() { TimeSlotID = x.TimeSlotID, StartTime = x.StartTime, EndTime = x.EndTime };
}

public sealed class ScheduleDto
{
    public bool IsLocked { get; set; }
     public int ScheduleID { get; set; }
    [Range(1,int.MaxValue)] public int SubjectID { get; set; }
    [Range(1,int.MaxValue)] public int FacultyMemberID { get; set; }
    [Range(1,int.MaxValue)] public int ClassroomID { get; set; }
    [Range(1,int.MaxValue)] public int TimeSlotID { get; set; }
    [Required, Range(1,int.MaxValue)] public int? SectionID { get; set; }
    [Required, StringLength(20)] public string DayOfWeek { get; set; } = string.Empty;
    [Required, RegularExpression("^(Theory|Practical)$")] public string LectureType { get; set; } = string.Empty;
    [StringLength(20)] public string? GroupName { get; set; }
    public Schedule ToEntity(int id) => new() { ScheduleID = id, SubjectID = SubjectID, FacultyMemberID = FacultyMemberID, ClassroomID = ClassroomID, TimeSlotID = TimeSlotID, SectionID = SectionID, DayOfWeek = DayOfWeek, LectureType = LectureType, GroupName = GroupName };
    public static ScheduleDto From(Schedule x) => new() { ScheduleID = x.ScheduleID, SubjectID = x.SubjectID, FacultyMemberID = x.FacultyMemberID, ClassroomID = x.ClassroomID, TimeSlotID = x.TimeSlotID, SectionID = x.SectionID, DayOfWeek = x.DayOfWeek, LectureType = x.LectureType, GroupName = x.GroupName, IsLocked = x.IsLocked };
}
