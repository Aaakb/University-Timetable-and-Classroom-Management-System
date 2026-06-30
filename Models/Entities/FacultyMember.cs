namespace University_Timetable_and_Classroom_Management_System.Models
{
    public class FacultyMember
    {
        public int FacultyMemberID { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string? AcademicTitle { get; set; }

        public ICollection<FacultyMemberSubject> FacultyMemberSubjects { get; set; } = new List<FacultyMemberSubject>();

        public ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
    }
}
