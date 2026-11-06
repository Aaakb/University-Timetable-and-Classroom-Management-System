namespace University_Timetable_and_Classroom_Management_System.Models;

public sealed class Student
{
    public int StudentID { get; set; }
    public string FullName { get; set; } = "";
    public int StudyYearID { get; set; }
    public int? SectionID { get; set; }
    public int? PracticalGroupNumber { get; set; }
}
