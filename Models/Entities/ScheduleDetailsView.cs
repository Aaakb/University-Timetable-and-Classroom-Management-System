namespace University_Timetable_and_Classroom_Management_System.Models
{
    public class ScheduleDetailsView
    {
        public int ScheduleID { get; set; }

        public int SubjectID { get; set; }

        public int FacultyMemberID { get; set; }

        public int ClassroomID { get; set; }

        public int TimeSlotID { get; set; }

        public int SemesterNumber { get; set; }

        public int? StudyYearID { get; set; }

        public int? BranchID { get; set; }

        public int? SectionID { get; set; }

        public string YearName { get; set; } = string.Empty;

        public string? BranchName { get; set; }

        public string SectionName { get; set; } = string.Empty;

        public string? GroupName { get; set; }

        public string LectureType { get; set; } = "Theory";

        public string SubjectName { get; set; } = string.Empty;

        public string FacultyMemberName { get; set; } = string.Empty;

        public string ClassroomNumber { get; set; } = string.Empty;

        public int Capacity { get; set; }

        public TimeSpan StartTime { get; set; }

        public TimeSpan EndTime { get; set; }

        public string DayOfWeek { get; set; } = string.Empty;
    }
}
