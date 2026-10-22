namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    internal static class ScheduleDayRules
    {
        public const int MaximumSectionTeachingDays = 4;

        public static void EnsureSectionDayLimit(
            IEnumerable<Models.Schedule> schedules,
            IEnumerable<Models.Section> sections,
            IEnumerable<Models.StudyYear>? studyYears = null,
            IEnumerable<Models.Branch>? branches = null)
        {
            var sectionLookup = sections.ToDictionary(s => s.SectionID);
            var yearLookup = studyYears?.ToDictionary(y => y.StudyYearID, y => y.YearName);
            var branchLookup = branches?.ToDictionary(b => b.BranchID, b => b.BranchName);
            foreach (var section in schedules.GroupBy(s => (s.SemesterNumber, s.SectionID)))
            {
                var days = section.Select(s => NormalizeDayName(s.DayOfWeek)).Distinct().OrderBy(GetDayOrder).ToList();
                if (days.Count <= MaximumSectionTeachingDays) continue;

                var details = section.Key.SectionID.HasValue ? sectionLookup.GetValueOrDefault(section.Key.SectionID.Value) : null;
                string? year = details?.StudyYear?.YearName ?? (details is null ? null : yearLookup?.GetValueOrDefault(details.StudyYearID));
                string? branch = details?.Branch?.BranchName ?? (details?.BranchID is int branchId ? branchLookup?.GetValueOrDefault(branchId) : null);
                year = year?.Trim().ToLowerInvariant() switch
                {
                    "first year" => "المرحلة الأولى",
                    "second year" => "المرحلة الثانية",
                    "third year" => "المرحلة الثالثة",
                    "fourth year" => "المرحلة الرابعة",
                    _ => year
                };
                string label = details is null ? $"رقم الشعبة: {section.Key.SectionID}" :
                    string.Join(" / ", new[] { $"الشعبة «{details.SectionName}»", year, branch }.Where(x => !string.IsNullOrWhiteSpace(x)));
                string dayNames = string.Join("، ", days.Select(day => day switch
                {
                    "Sunday" => "الأحد", "Monday" => "الاثنين", "Tuesday" => "الثلاثاء",
                    "Wednesday" => "الأربعاء", "Thursday" => "الخميس", _ => day
                }));
                throw new ArgumentException(
                    $"{label}{Environment.NewLine}" +
                    $"الفصل الدراسي: {section.Key.SemesterNumber}. عدد أيام التدريس: {days.Count}؛ الحد الأقصى: {MaximumSectionTeachingDays}.{Environment.NewLine}" +
                    $"أيام المحاضرات: {dayNames}.{Environment.NewLine}{Environment.NewLine}" +
                    $"انقل محاضرات أحد هذه الأيام إلى بقية الأيام ليصبح مجموع أيام الشعبة {MaximumSectionTeachingDays} أو أقل، وراجع تفضيلات التدريسيين عند الحاجة، ثم أعد المحاولة.{Environment.NewLine}" +
                    "لم يُحفظ أي تغيير في الجدول.");
            }
        }

        public static IReadOnlyList<string> AllDays { get; } =
        [
            "Sunday",
            "Monday",
            "Tuesday",
            "Wednesday",
            "Thursday"
        ];

        public static IReadOnlyList<string> GetSchedulingDays(int studyYearId)
        {
            return studyYearId switch
            {
                1 => ["Sunday", "Monday", "Tuesday", "Wednesday"],
                2 => ["Sunday", "Monday", "Tuesday", "Thursday"],
                3 => ["Sunday", "Monday", "Wednesday", "Thursday"],
                4 => ["Sunday", "Tuesday", "Wednesday", "Thursday"],
                _ => AllDays.Take(4).ToList()
            };
        }

        public static IReadOnlyList<string> GetSchedulingDays(int studyYearId, int dayCount)
        {
            if (dayCount >= AllDays.Count)
            {
                return AllDays;
            }

            return GetSchedulingDays(studyYearId);
        }

        public static IReadOnlyList<string> GetSchedulingDays(string studyYearName)
        {
            return studyYearName.Trim().ToLowerInvariant() switch
            {
                "first year" => GetSchedulingDays(1),
                "second year" => GetSchedulingDays(2),
                "third year" => GetSchedulingDays(3),
                "fourth year" => GetSchedulingDays(4),
                _ => AllDays.Take(4).ToList()
            };
        }

        public static string NormalizeDayName(string dayOfWeek)
        {
            return AllDays.FirstOrDefault(day =>
                string.Equals(day, dayOfWeek.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException("Schedule day must be Sunday, Monday, Tuesday, Wednesday, or Thursday.");
        }

        public static int GetDayOrder(string day)
        {
            return day.Trim().ToLowerInvariant() switch
            {
                "sunday" => 1,
                "monday" => 2,
                "tuesday" => 3,
                "wednesday" => 4,
                "thursday" => 5,
                _ => 99
            };
        }
    }
}
