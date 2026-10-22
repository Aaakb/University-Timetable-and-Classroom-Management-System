using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

internal static class ScheduleQualityAnalyzer
{
    public static ScheduleQuality Analyze(ImportCatalog data, IReadOnlyList<Schedule> full,
        FacultyScheduleRequest request, IReadOnlyList<Schedule>? originals = null)
    {
        var periods = data.Rows<TimeSlot>().ToDictionary(p => p.TimeSlotID);
        var primary = full.Where(s => s.FacultyMemberID == request.FacultyId && s.SemesterNumber == request.Semester).ToList();
        var semester = full.Where(s => s.SemesterNumber == request.Semester).ToList();
        var students = new List<List<Schedule>>();
        foreach (var section in semester.GroupBy(s => s.SectionID))
        {
            var groups = section.Where(s => !string.IsNullOrWhiteSpace(s.GroupName)).Select(s => s.GroupName!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (groups.Count == 0) students.Add(section.ToList());
            else foreach (string group in groups) students.Add(section.Where(s => string.IsNullOrWhiteSpace(s.GroupName) || string.Equals(s.GroupName, group, StringComparison.OrdinalIgnoreCase)).ToList());
        }
        int Gaps(IEnumerable<Schedule> rows) => rows.GroupBy(s => s.DayOfWeek).Sum(day =>
        {
            var ordered = day.Select(s => periods[s.TimeSlotID]).OrderBy(p => p.StartTime).ToList();
            return ordered.Zip(ordered.Skip(1), (a, b) => Math.Max(0, (int)(b.StartTime - a.EndTime).TotalMinutes - 10)).Sum();
        });
        var old = (originals ?? full).ToDictionary(s => s.ScheduleID);
        var changed = full.Where(s => old.TryGetValue(s.ScheduleID, out var before) && !ImportCatalog.Same(before, s)).ToList();
        var rooms = data.Rows<Classroom>().ToDictionary(r => r.ClassroomID);
        var sections = data.Rows<Section>().ToDictionary(s => s.SectionID);
        double seats = 0, capacity = 0;
        foreach (var row in semester)
        {
            int minutes = FacultyPreferenceRules.Duration(periods[row.TimeSlotID]);
            int count = sections[row.SectionID!.Value].StudentCount;
            if (!AcademicStructureRules.AllowsWholeSectionPractical(sections[row.SectionID.Value].StudyYearID) &&
                !string.IsNullOrWhiteSpace(row.GroupName)) count = (count + 1) / 2;
            seats += count * (double)minutes; capacity += rooms[row.ClassroomID].Capacity * (double)minutes;
        }
        return new(primary.Count, changed.Count, changed.Count(s => s.FacultyMemberID != request.FacultyId),
            primary.Select(s => s.DayOfWeek).Distinct().Count(), Gaps(primary), students.Sum(Gaps),
            primary.Count(s => !request.SelectedDays.Contains(s.DayOfWeek)),
            primary.Count(s => request.PreferredPeriodIds!.Count > 0 && !request.PreferredPeriodIds.Contains(s.TimeSlotID)),
            students.SelectMany(s => s.GroupBy(r => r.DayOfWeek).Select(day => day.Sum(r => FacultyPreferenceRules.Duration(periods[r.TimeSlotID])))).DefaultIfEmpty().Max(),
            capacity == 0 ? 0 : Math.Round(100 * seats / capacity, 1));
    }
}
