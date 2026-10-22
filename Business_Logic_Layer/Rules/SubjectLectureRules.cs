using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

public static class SubjectLectureRules
{
    public static void Validate(Subject subject)
    {
        if (subject.TheoreticalLectureCount is < 0 or > Subject.MaximumWeeklyLectureCount ||
            subject.PracticalLectureCount is < 0 or > Subject.MaximumWeeklyLectureCount)
            throw new ArgumentException($"عدد المحاضرات الأسبوعية يجب أن يكون عدداً صحيحاً من 0 إلى {Subject.MaximumWeeklyLectureCount}.");
    }

    public static void ValidateSavedLectures(Subject subject, IEnumerable<Schedule> saved)
    {
        foreach (var group in saved.Where(s => s.SubjectID == subject.SubjectID)
                     .GroupBy(s => new { s.SemesterNumber, s.SectionID, s.LectureType, s.GroupName }))
        {
            int required = group.Key.LectureType == "Practical"
                ? subject.EffectivePracticalLectureCount : subject.EffectiveTheoreticalLectureCount;
            if (group.Count() > required)
                throw new ArgumentException("عدد المحاضرات المحدد أقل من المحاضرات المحفوظة للشعبة أو المجموعة. احذف المحاضرات الزائدة من الجدول قبل تقليل العدد.");
        }
    }
}
