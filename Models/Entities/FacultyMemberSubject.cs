namespace University_Timetable_and_Classroom_Management_System.Models
{
    public class FacultyMemberSubject
    {
        public int FacultyMemberID { get; set; }

        public int SubjectID { get; set; }

        // Null retains legacy assignments covering every matching section.
        public int? SectionID { get; set; }

        // Legacy assignments cover both components until explicitly classified.
        public string LectureType { get; set; } = "Both";

        public Section? Section { get; set; }

        public FacultyMember FacultyMember { get; set; } = null!;

        public Subject Subject { get; set; } = null!;
    }
}
