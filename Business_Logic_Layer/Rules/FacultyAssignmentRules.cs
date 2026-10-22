using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

public static class FacultyAssignmentRules
{
    public static string NormalizeLectureType(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "both" or "كلاهما" => "Both",
        "theory" or "نظري" => "Theory",
        "practical" or "عملي" => "Practical",
        _ => throw new ArgumentException("اختر نوع التكليف: نظري أو عملي أو كلاهما.")
    };

    public static bool Covers(FacultyMemberSubject assignment, int subjectId, int? sectionId, string? lectureType = null) =>
        assignment.SubjectID == subjectId && (!assignment.SectionID.HasValue || assignment.SectionID == sectionId) &&
        (lectureType is null ||
            string.Equals(assignment.LectureType, "Both", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(assignment.LectureType, lectureType, StringComparison.OrdinalIgnoreCase));

    public static List<int> FacultyIds(IEnumerable<FacultyMemberSubject> assignments, int subjectId, int? sectionId, string? lectureType = null) =>
        assignments.Where(a => Covers(a, subjectId, sectionId, lectureType)).Select(a => a.FacultyMemberID).Distinct().Order().ToList();

    public static bool Matches(Subject subject, Section section) =>
        subject.StudyYearID == section.StudyYearID && AcademicStructureRules.SectionMatchesSubject(
            subject.StudyYearID, subject.BranchID, section.BranchID, section.SectionName);
}
