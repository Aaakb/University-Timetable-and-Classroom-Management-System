namespace University_Timetable_and_Classroom_Management_System.Models
{
    public class Schedule
    {
        public int ScheduleID { get; set; }

        public int SubjectID { get; set; }

        public int FacultyMemberID { get; set; }

        public int ClassroomID { get; set; }

        public int TimeSlotID { get; set; }

        public int SemesterNumber { get; set; }

        public string LectureType { get; set; } = "Theory";

        public string? GroupName { get; set; }

        public string DayOfWeek { get; set; } = string.Empty;

        public int? StudyYearID { get; set; }

        public int? BranchID { get; set; }

        public int? SectionID { get; set; }

        public Subject Subject { get; set; } = null!;

        public FacultyMember FacultyMember { get; set; } = null!;

        public Classroom Classroom { get; set; } = null!;

        public TimeSlot TimeSlot { get; set; } = null!;

        public StudyYear? StudyYear { get; set; }

        public Branch? Branch { get; set; }

        public Section? Section { get; set; }
    }
}
