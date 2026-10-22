namespace University_Timetable_and_Classroom_Management_System.Models
{
    public class Subject
    {
        public int SubjectID { get; set; }

        public string SubjectName { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }

        public int StudyYearID { get; set; }

        public int SemesterNumber { get; set; }

        public double TheoreticalHours { get; set; }

        public double PracticalHours { get; set; }

        public double CreditUnits { get; set; }

        // Null keeps the automatic weekly count; a value is a user override.
        public int? TheoreticalLectureCount { get; set; }
        public int? PracticalLectureCount { get; set; }
        public const int MaximumWeeklyLectureCount = 100;

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public int DefaultTheoreticalLectureCount => TheoreticalHours > 0 || PracticalHours <= 0 ? 1 : 0;
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public int DefaultPracticalLectureCount => PracticalHours > 0 ? 1 : 0;
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public int EffectiveTheoreticalLectureCount => TheoreticalLectureCount ?? DefaultTheoreticalLectureCount;
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public int EffectivePracticalLectureCount => PracticalLectureCount ?? DefaultPracticalLectureCount;
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public bool HasWeeklyLectures => !IsDeleted && (EffectiveTheoreticalLectureCount > 0 || EffectivePracticalLectureCount > 0);

        // Retained only for compatibility with existing database constraints.
        public const string DefaultRequirementType = "University";
        public string RequirementType { get; set; } = DefaultRequirementType;

        public int? BranchID { get; set; }

        public StudyYear StudyYear { get; set; } = null!;

        public Branch? Branch { get; set; }

        public ICollection<FacultyMemberSubject> FacultyMemberSubjects { get; set; } = new List<FacultyMemberSubject>();

        public ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
    }
}
