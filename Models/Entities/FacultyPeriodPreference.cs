namespace University_Timetable_and_Classroom_Management_System.Models;

public sealed class FacultyPeriodPreference
{
    public int FacultyMemberID { get; set; }
    public int SemesterNumber { get; set; }
    public string DayOfWeek { get; set; } = "";
    public int TimeSlotID { get; set; }
    public int Level { get; set; }
}
