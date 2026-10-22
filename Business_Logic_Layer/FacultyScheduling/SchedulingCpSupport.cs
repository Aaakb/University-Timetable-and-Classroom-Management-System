using Google.OrTools.Sat;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

internal sealed record SchedulingOption(Schedule Row, int Day, int Period, BoolVar On);

internal static class SchedulingCpSupport
{
    public static void AddSectionDayLimits(CpModel model, IReadOnlyList<SchedulingOption> choices, IReadOnlyList<Schedule> fixedRows,
        Action<BoolVar, (int Semester, int? Section)>? diagnose = null)
    {
        var semesters = choices.Select(c => c.Row.SemesterNumber).ToHashSet();
        var fixedScope = fixedRows.Where(s => semesters.Contains(s.SemesterNumber)).ToList();
        var sections = choices.Select(c => (c.Row.SemesterNumber, c.Row.SectionID))
            .Concat(fixedScope.Select(s => (s.SemesterNumber, s.SectionID))).Distinct();
        foreach (var section in sections)
        {
            var own = choices.Where(c => (c.Row.SemesterNumber, c.Row.SectionID) == section).ToList();
            var fixedDays = fixedScope.Where(s => (s.SemesterNumber, s.SectionID) == section)
                .Select(s => s.DayOfWeek).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var usedDays = new List<BoolVar>();
            for (int d = 0; d < ScheduleDayRules.AllDays.Count; d++)
            {
                var used = model.NewBoolVar($"sectionDay_{section}_{d}");
                if (fixedDays.Contains(ScheduleDayRules.AllDays[d])) model.Add(used == 1);
                else
                {
                    var onDay = own.Where(c => c.Day == d).Select(c => c.On).ToList();
                    if (onDay.Count == 0) model.Add(used == 0);
                    else model.AddMaxEquality(used, onDay);
                }
                usedDays.Add(used);
            }
            // Count the entire section, including every practical subgroup,
            // every faculty member and all fixed/locked lectures.
            var limit = model.Add(LinearExpr.Sum(usedDays) <= ScheduleDayRules.MaximumSectionTeachingDays);
            if (diagnose is not null)
            {
                var assumption = model.NewBoolVar($"requireSectionLimit_{section}");
                limit.OnlyEnforceIf(assumption);
                model.AddAssumption(assumption);
                diagnose(assumption, section);
            }
        }
    }

    public static void AddResourceConstraints(CpModel model, IReadOnlyList<SchedulingOption> choices)
    {
        foreach (var simultaneous in choices.GroupBy(c => (c.Row.SemesterNumber, c.Day, c.Period)))
        {
            foreach (var group in simultaneous.GroupBy(c => c.Row.ClassroomID)) model.AddAtMostOne(group.Select(c => (ILiteral)c.On));
            foreach (var group in simultaneous.GroupBy(c => c.Row.FacultyMemberID)) model.AddAtMostOne(group.Select(c => (ILiteral)c.On));
            foreach (var section in simultaneous.GroupBy(c => c.Row.SectionID))
            {
                var whole = section.Where(c => string.IsNullOrWhiteSpace(c.Row.GroupName)).ToList();
                var groups = section.Where(c => !string.IsNullOrWhiteSpace(c.Row.GroupName)).GroupBy(c => c.Row.GroupName, StringComparer.OrdinalIgnoreCase).ToList();
                if (groups.Count == 0) model.AddAtMostOne(whole.Select(c => (ILiteral)c.On));
                else foreach (var group in groups) model.AddAtMostOne(whole.Concat(group).Select(c => (ILiteral)c.On));
            }
        }
    }

    public static void AddProfiles(CpModel model, IReadOnlyList<SchedulingOption> choices, IReadOnlyList<Schedule> fixedRows,
        IReadOnlyList<TimeSlot> periods, IReadOnlyDictionary<(int Faculty, int Semester), FacultyScheduleRequest> profiles)
    {
        foreach (var entry in profiles)
        {
            var profile = entry.Value;
            var options = choices.Where(c => c.Row.FacultyMemberID == entry.Key.Faculty && c.Row.SemesterNumber == entry.Key.Semester).ToList();
            var fixedTeacher = fixedRows.Where(s => s.FacultyMemberID == entry.Key.Faculty && s.SemesterNumber == entry.Key.Semester).ToList();
            foreach (var choice in options.Where(c => !FacultyPreferenceRules.Allows(profile, c.Row, periods[c.Period]))) model.Add(choice.On == 0);
            if (FacultyPreferenceRules.Violation(profile, fixedTeacher, periods.ToDictionary(p => p.TimeSlotID)) is not null)
                model.AddBoolOr(Array.Empty<ILiteral>());
            var usedDays = new List<BoolVar>();
            for (int d = 0; d < ScheduleDayRules.AllDays.Count; d++)
            {
                var busy = new List<BoolVar>();
                for (int p = 0; p < periods.Count; p++)
                {
                    var on = model.NewBoolVar($"profile_{entry.Key}_{d}_{p}");
                    int count = fixedTeacher.Count(s => s.DayOfWeek == ScheduleDayRules.AllDays[d] && s.TimeSlotID == periods[p].TimeSlotID);
                    model.Add(on == LinearExpr.Sum(options.Where(c => c.Day == d && c.Period == p).Select(c => c.On)) + count);
                    busy.Add(on);
                }
                var used = model.NewBoolVar($"day_{entry.Key}_{d}"); model.AddMaxEquality(used, busy); usedDays.Add(used);
                if (profile.UsePreferenceMatrix && (options.Count > 0 || fixedTeacher.Count > 0))
                    model.Add(used == (profile.SelectedDays.Contains(ScheduleDayRules.AllDays[d]) ? 1 : 0));
                if (profile.MaxDailyMinutes.HasValue)
                    model.Add(LinearExpr.WeightedSum(busy, periods.Select(p => (long)FacultyPreferenceRules.Duration(p))) <= profile.MaxDailyMinutes.Value);
                if (profile.MaxConsecutiveMinutes.HasValue)
                {
                    IntVar lastEnd = model.NewConstant(-1440), running = model.NewConstant(0);
                    for (int p = 0; p < periods.Count; p++)
                    {
                        int start = (int)periods[p].StartTime.TotalMinutes, end = (int)periods[p].EndTime.TotalMinutes;
                        var rested = model.NewBoolVar($"rest_{entry.Key}_{d}_{p}");
                        model.Add(lastEnd <= start - profile.MinimumRestMinutes).OnlyEnforceIf(rested);
                        model.Add(lastEnd > start - profile.MinimumRestMinutes).OnlyEnforceIf(rested.Not());
                        var next = model.NewIntVar(0, profile.MaxConsecutiveMinutes.Value, $"run_{entry.Key}_{d}_{p}");
                        model.Add(next == end - start).OnlyEnforceIf([busy[p], rested]);
                        model.Add(next == running + end - start).OnlyEnforceIf([busy[p], rested.Not()]);
                        model.Add(next == running).OnlyEnforceIf(busy[p].Not());
                        var nextEnd = model.NewIntVar(-1440, 1440, $"end_{entry.Key}_{d}_{p}");
                        model.Add(nextEnd == end).OnlyEnforceIf(busy[p]); model.Add(nextEnd == lastEnd).OnlyEnforceIf(busy[p].Not());
                        lastEnd = nextEnd; running = next;
                    }
                }
            }
            if (profile.MaxTeachingDays.HasValue) model.Add(LinearExpr.Sum(usedDays) <= profile.MaxTeachingDays.Value);
        }
    }

    public sealed record PreferenceExpressions(LinearExpr Total, long Bound, IntVar WorstNormalized);
    public static PreferenceExpressions PreferenceQuality(CpModel model, IReadOnlyList<SchedulingOption> choices,
        IReadOnlyList<Schedule> fixedRows, IReadOnlyList<TimeSlot> periods,
        IReadOnlyDictionary<(int Faculty, int Semester), FacultyScheduleRequest> profiles)
    {
        var totals = new List<LinearExpr>(); var normalized = new List<IntVar>(); long bound = 0;
        foreach (var entry in profiles.Where(p => p.Value.HasScoredPreferences))
        {
            var own = choices.Where(c => c.Row.FacultyMemberID == entry.Key.Faculty && c.Row.SemesterNumber == entry.Key.Semester).ToList();
            var fixedOwn = fixedRows.Where(s => s.FacultyMemberID == entry.Key.Faculty && s.SemesterNumber == entry.Key.Semester).ToList();
            int Duration(Schedule s) => FacultyPreferenceRules.Duration(periods.Single(p => p.TimeSlotID == s.TimeSlotID));
            int totalMinutes = own.GroupBy(c => c.Row.ScheduleID).Sum(g => Duration(g.First().Row)) + fixedOwn.Sum(Duration);
            if (totalMinutes == 0) continue;
            long Penalty(Schedule row) => FacultyPreferenceRules.Level(entry.Value, row.DayOfWeek, row.TimeSlotID) == FacultyPreferenceLevel.Preferred ? 2L * Duration(row) : 0;
            long constant = fixedOwn.Sum(Penalty);
            var sum = LinearExpr.WeightedSum(own.Select(c => c.On), own.Select(c => Penalty(c.Row))) + constant;
            totals.Add(sum); bound += 2L * totalMinutes;
            var score = model.NewIntVar(0, 2L * totalMinutes, $"prefPenalty{entry.Key}"); model.Add(score == sum);
            var ratio = model.NewIntVar(0, 1000, $"prefRatio{entry.Key}");
            model.AddDivisionEquality(ratio, score * 1000, 2L * totalMinutes); normalized.Add(ratio);
        }
        var worst = model.NewIntVar(0, 1000, "worstPreference");
        if (normalized.Count > 0) model.AddMaxEquality(worst, normalized); else model.Add(worst == 0);
        return new(LinearExpr.Sum(totals), bound, worst);
    }

    // Each objective has an explicit upper bound. Mixed-radix coefficients make
    // priorities exact, without arbitrary large constants or integer overflow.
    public static void MinimizePriorities(CpModel model, params (LinearExpr Expression, long Bound)[] priorities)
    {
        var terms = new List<LinearExpr>(); long weight = 1;
        for (int i = priorities.Length - 1; i >= 0; i--)
        {
            try
            {
                checked
                {
                    terms.Add(priorities[i].Expression * weight);
                    weight *= priorities[i].Bound + 1;
                }
            }
            catch (OverflowException) { throw new ArgumentException("نطاق التحسين كبير جدًا؛ قلل عدد المحاضرات المتاحة للتحريك."); }
        }
        model.Minimize(LinearExpr.Sum(terms));
    }

    public sealed record QualityExpressions(LinearExpr Days, LinearExpr Gaps, LinearExpr TargetExcess, IntVar StudentPeak);
    public static QualityExpressions Quality(CpModel model, IReadOnlyList<SchedulingOption> options,
        IReadOnlyList<Schedule> fixedRows, IReadOnlyList<TimeSlot> periods, FacultyScheduleRequest request, bool includeStudentLoad = true)
    {
        var days = new List<BoolVar>(); var gaps = new List<IntVar>();
        for (int d = 0; d < ScheduleDayRules.AllDays.Count; d++)
        {
            var busy = Enumerable.Range(0, periods.Count).Select(p => Occupied($"q_{d}_{p}",
                options.Where(c => c.Row.FacultyMemberID == request.FacultyId && c.Day == d && c.Period == p),
                fixedRows.Count(s => s.FacultyMemberID == request.FacultyId && s.DayOfWeek == ScheduleDayRules.AllDays[d] && s.TimeSlotID == periods[p].TimeSlotID))).ToList();
            var used = model.NewBoolVar($"qday{d}"); model.AddMaxEquality(used, busy); days.Add(used);
            var starts = new List<IntVar>(); var ends = new List<IntVar>();
            for (int p = 0; p < periods.Count; p++)
            {
                var start = model.NewIntVar(0, 1440, $"qs{d}_{p}"); var end = model.NewIntVar(0, 1440, $"qe{d}_{p}");
                model.Add(start == (int)periods[p].StartTime.TotalMinutes).OnlyEnforceIf(busy[p]); model.Add(start == 1440).OnlyEnforceIf(busy[p].Not());
                model.Add(end == (int)periods[p].EndTime.TotalMinutes).OnlyEnforceIf(busy[p]); model.Add(end == 0).OnlyEnforceIf(busy[p].Not());
                starts.Add(start); ends.Add(end);
            }
            var first = model.NewIntVar(0, 1440, $"first{d}"); var last = model.NewIntVar(0, 1440, $"last{d}");
            model.AddMinEquality(first, starts); model.AddMaxEquality(last, ends);
            var gap = model.NewIntVar(0, 1440, $"qgap{d}");
            var load = LinearExpr.WeightedSum(busy, periods.Select(p => (long)FacultyPreferenceRules.Duration(p)));
            model.Add(gap == last - first - load - 10 * (LinearExpr.Sum(busy) - 1)).OnlyEnforceIf(used);
            model.Add(gap == 0).OnlyEnforceIf(used.Not()); gaps.Add(gap);
        }
        var excess = model.NewIntVar(0, 5, "targetExcess");
        model.AddMaxEquality(excess, [LinearExpr.Sum(days) - (request.TargetTeachingDays ?? 5), LinearExpr.Constant(0)]);
        var dailyLoads = new List<IntVar>();
        var all = includeStudentLoad ? fixedRows.Concat(options.Select(c => c.Row)).ToList() : [];
        foreach (var section in all.Where(s => s.SectionID.HasValue).Select(s => s.SectionID!.Value).Distinct())
        {
            var groups = all.Where(s => s.SectionID == section && !string.IsNullOrWhiteSpace(s.GroupName)).Select(s => s.GroupName!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (groups.Count == 0) groups.Add("");
            foreach (string group in groups)
            for (int d = 0; d < ScheduleDayRules.AllDays.Count; d++)
            {
                bool Includes(Schedule s) => s.SectionID == section && (string.IsNullOrWhiteSpace(s.GroupName) || string.Equals(s.GroupName, group, StringComparison.OrdinalIgnoreCase));
                var dayOptions = options.Where(c => c.Day == d && Includes(c.Row)).ToList();
                int fixedMinutes = fixedRows.Where(s => s.DayOfWeek == ScheduleDayRules.AllDays[d] && Includes(s)).Sum(s => FacultyPreferenceRules.Duration(periods.Single(p => p.TimeSlotID == s.TimeSlotID)));
                var load = model.NewIntVar(0, 1440, $"student_{section}_{group}_{d}");
                model.Add(load == LinearExpr.WeightedSum(dayOptions.Select(c => c.On), dayOptions.Select(c => (long)FacultyPreferenceRules.Duration(periods[c.Period]))) + fixedMinutes);
                dailyLoads.Add(load);
            }
        }
        var peak = model.NewIntVar(0, 1440, "studentPeak");
        if (dailyLoads.Count > 0) model.AddMaxEquality(peak, dailyLoads); else model.Add(peak == 0);
        return new(LinearExpr.Sum(days), LinearExpr.Sum(gaps), excess, peak);

        BoolVar Occupied(string name, IEnumerable<SchedulingOption> choices, int count)
        {
            var on = model.NewBoolVar(name); model.Add(on == LinearExpr.Sum(choices.Select(c => c.On)) + count); return on;
        }
    }
}
