namespace University_Timetable_and_Classroom_Management_System.Models
{
    public class Section
    {
        public int SectionID { get; set; }

        public string SectionName { get; set; } = string.Empty;

        public int StudentCount { get; set; }

        public int StudyYearID { get; set; }

        public int? BranchID { get; set; }

        public StudyYear StudyYear { get; set; } = null!;

        public Branch? Branch { get; set; }

        public ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
    }
}
