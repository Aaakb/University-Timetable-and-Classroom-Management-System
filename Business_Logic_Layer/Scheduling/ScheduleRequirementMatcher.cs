using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

internal static class ScheduleRequirementMatcher
{
    public static List<ScheduleGenerationRequest> Remaining(IReadOnlyList<ScheduleGenerationRequest> requests,
        IReadOnlyList<Schedule> lectures)
    {
        var remaining = requests.ToList();
        foreach (var lecture in lectures)
        {
            var matches = remaining.Where(r => r.Subject.SubjectID == lecture.SubjectID && r.Section.SectionID == lecture.SectionID &&
                r.Subject.SemesterNumber == lecture.SemesterNumber && r.LectureType == lecture.LectureType &&
                (string.Equals(r.GroupName ?? "", lecture.GroupName ?? "", StringComparison.OrdinalIgnoreCase) ||
                 lecture.LectureType == "Practical" && string.IsNullOrWhiteSpace(lecture.GroupName))).ToList();
            // One whole-section practical fulfills both subgroup requirements for this lesson.
            foreach (var match in matches.GroupBy(r => r.GroupName).Select(g => g.First())) remaining.Remove(match);
        }
        return remaining;
    }
}
