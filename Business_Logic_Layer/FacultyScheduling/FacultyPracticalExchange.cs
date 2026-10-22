using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

// A unit is the two subgroup sessions of one practical subject occurrence.
// Whole-section practicals deliberately stay outside the subgroup exchange rule.
internal static class FacultyPracticalExchange
{
    internal sealed record Unit(Schedule First, Schedule Second);
    public static List<List<Unit>> Scopes(IReadOnlyList<Schedule> rows, IReadOnlySet<int> affectedSections)
    {
        var result = new List<List<Unit>>();
        foreach (var scope in rows.Where(s => s.LectureType == "Practical" && !string.IsNullOrWhiteSpace(s.GroupName) &&
                     s.SectionID.HasValue && affectedSections.Contains(s.SectionID.Value))
                 .GroupBy(s => (s.SemesterNumber, s.SectionID)))
        {
            var units = new List<Unit>();
            foreach (var subject in scope.GroupBy(s => s.SubjectID))
            {
                var groups = subject.GroupBy(s => s.GroupName).OrderBy(g => g.Key, StringComparer.Ordinal).ToList();
                if (groups.Count != 2 || groups[0].Count() != groups[1].Count())
                    throw new ArgumentException($"العملي للشعبة {scope.Key.SectionID}: يلزم وجود محاضرات المجموعتين قبل إعادة التنظيم.");
                var a = groups[0].OrderBy(s => ScheduleDayRules.GetDayOrder(s.DayOfWeek)).ThenBy(s => s.TimeSlotID).ThenBy(s => s.ScheduleID).ToList();
                var b = groups[1].OrderBy(s => ScheduleDayRules.GetDayOrder(s.DayOfWeek)).ThenBy(s => s.TimeSlotID).ThenBy(s => s.ScheduleID).ToList();
                for (int i = 0; i < a.Count; i++) units.Add(new(a[i], b[i]));
            }
            result.Add(units);
        }
        return result;
    }
    public static bool CanExchange(Unit a, Unit b, IReadOnlyList<TimeSlot> periods)
    {
        if (a.First.SubjectID == b.First.SubjectID || a.First.FacultyMemberID != a.Second.FacultyMemberID ||
            b.First.FacultyMemberID != b.Second.FacultyMemberID) return false;
        int x = periods.ToList().FindIndex(p => p.TimeSlotID == a.First.TimeSlotID);
        int y = periods.ToList().FindIndex(p => p.TimeSlotID == a.Second.TimeSlotID);
        return x >= 0 && y >= 0 && Math.Abs(x - y) == 1 &&
            a.First.DayOfWeek == a.Second.DayOfWeek && a.First.DayOfWeek == b.First.DayOfWeek &&
            a.First.DayOfWeek == b.Second.DayOfWeek && a.First.TimeSlotID == b.Second.TimeSlotID &&
            a.Second.TimeSlotID == b.First.TimeSlotID &&
            a.First.ClassroomID == a.Second.ClassroomID && b.First.ClassroomID == b.Second.ClassroomID;
    }
    public static void Validate(IReadOnlyList<Schedule> rows, IReadOnlySet<int> affectedSections, IReadOnlyList<TimeSlot> periods)
    {
        foreach (var units in Scopes(rows, affectedSections))
            if (!Match(units, UnpairedCount(units)))
                throw new ArgumentException("تعذّر الحفاظ على تبادل العملي في اليوم نفسه بفترتين متتاليتين مع ثبات محاضرات الآخرين.");
        bool Match(List<Unit> remaining, int singles)
        {
            if (remaining.Count == 0) return singles == 0;
            var first = remaining[0];
            for (int i = 1; i < remaining.Count; i++)
                if (CanExchange(first, remaining[i], periods) && Match(remaining.Where((_, n) => n != 0 && n != i).ToList(), singles)) return true;
            return singles > 0 && Match(remaining.Skip(1).ToList(), singles - 1);
        }
    }

    public static int UnpairedCount(IReadOnlyList<Unit> units)
    {
        // The generator permits one unpaired subject in each occurrence with
        // an odd subject count. Unequal weekly counts can create several such
        // occurrences; retain all valid exchange pairings when checking them.
        var counts = units.GroupBy(u => u.First.SubjectID).Select(g => g.Count()).ToArray();
        return counts.Length == 0 ? 0 : Enumerable.Range(1, counts.Max())
            .Sum(occurrence => counts.Count(count => count >= occurrence) % 2);
    }
}
