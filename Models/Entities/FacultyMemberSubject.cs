namespace University_Timetable_and_Classroom_Management_System.Models
{
    public class FacultyMemberSubject
    {
        public int FacultyMemberID { get; set; }

        public int SubjectID { get; set; }

        public FacultyMember FacultyMember { get; set; } = null!;

        public Subject Subject { get; set; } = null!;
    }
}
