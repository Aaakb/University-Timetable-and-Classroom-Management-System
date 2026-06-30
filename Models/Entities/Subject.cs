namespace University_Timetable_and_Classroom_Management_System.Models
{
    public class Subject
    {
        public int SubjectID { get; set; }

        public string SubjectName { get; set; } = string.Empty;

        public int StudyYearID { get; set; }

        public int SemesterNumber { get; set; }

        public double TheoreticalHours { get; set; }

        public double PracticalHours { get; set; }

        public double CreditUnits { get; set; }

        public string RequirementType { get; set; } = string.Empty;

        public int? BranchID { get; set; }

        public StudyYear StudyYear { get; set; } = null!;

        public Branch? Branch { get; set; }

        public ICollection<FacultyMemberSubject> FacultyMemberSubjects { get; set; } = new List<FacultyMemberSubject>();

        public ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
    }
}
