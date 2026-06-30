namespace University_Timetable_and_Classroom_Management_System.Models
{
    public class StudyYear
    {
        public int StudyYearID { get; set; }

        public string YearName { get; set; } = string.Empty;

        public ICollection<Subject> Subjects { get; set; } = new List<Subject>();

        public ICollection<Section> Sections { get; set; } = new List<Section>();

        public ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
    }
}
