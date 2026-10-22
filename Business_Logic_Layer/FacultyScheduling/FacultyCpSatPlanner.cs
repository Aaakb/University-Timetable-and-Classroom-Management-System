using System.Diagnostics;
using System.Globalization;
using Google.OrTools.Sat;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

internal static class FacultyCpSatPlanner
{
    private sealed class SearchLimitException : Exception;
    internal sealed record Candidate(Schedule Lecture, int DayIndex, int PeriodIndex);
    internal sealed record Result(string Status, List<Schedule>? Plan, List<FacultySlotSummary> Slots, List<string> Suggestions)
    { public IReadOnlyList<FacultyAvailableSlot> Options { get; init; } = []; }
    internal static Schedule Copy(Schedule s) => new()
    {
        ScheduleID = s.ScheduleID, SubjectID = s.SubjectID, FacultyMemberID = s.FacultyMemberID,
        SectionID = s.SectionID, StudyYearID = s.StudyYearID, BranchID = s.BranchID,
        SemesterNumber = s.SemesterNumber, LectureType = s.LectureType, GroupName = s.GroupName,
        DayOfWeek = s.DayOfWeek, TimeSlotID = s.TimeSlotID, ClassroomID = s.ClassroomID, IsLocked = s.IsLocked
    };
    public static Result Solve(ImportCatalog catalog, FacultyScheduleRequest request, bool inspectOnly, CancellationToken token)
    {
        try { return SolveCore(catalog, request, inspectOnly, token); }
        catch (SearchLimitException) { return new("Unknown", null, [], ["بلغ البحث حد الوقت أو حجم الخيارات؛ لم تثبت استحالة الحل."]); }
    }
    private static Result SolveCore(ImportCatalog catalog, FacultyScheduleRequest request, bool inspectOnly, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!inspectOnly) FacultySchedulingRuntime.EnsureAvailable();
        var clock = Stopwatch.StartNew();
        var all = catalog.Rows<Schedule>();
        void CheckBudget()
        {
            token.ThrowIfCancellationRequested();
            if (clock.Elapsed.TotalSeconds >= request.TimeLimitSeconds) throw new SearchLimitException();
        }
        var primary = all.Where(s => s.FacultyMemberID == request.FacultyId && s.SemesterNumber == request.Semester).OrderBy(s => s.ScheduleID).ToList();
        if (primary.Count == 0) throw new ArgumentException("لا توجد محاضرات للتدريسي في الفصل المحدد.");
        var target = primary.Concat(request.MaxOtherLectureChanges > 0
            ? all.Where(s => s.SemesterNumber == request.Semester && s.FacultyMemberID != request.FacultyId && !s.IsLocked) : []).ToList();
        var profiles = FacultyPreferenceRules.FromCatalog(catalog);
        profiles[(request.FacultyId, request.Semester)] = request;
        var targetIds = target.Select(s => s.ScheduleID).ToHashSet();
        var fixedRows = all.Where(s => s.SemesterNumber == request.Semester && !targetIds.Contains(s.ScheduleID)).ToList();
        var periods = catalog.Rows<TimeSlot>().Where(p => !p.IsBreak).OrderBy(p => p.StartTime).ToList();
        ScheduleTimingRules.EnsureCompatibleLectureSlots(periods);
        var days = ScheduleDayRules.AllDays;
        var sections = catalog.Rows<Section>().ToDictionary(s => s.SectionID);
        var subjects = catalog.Rows<Subject>().ToDictionary(s => s.SubjectID);
        var candidates = new Dictionary<int, List<Candidate>>();
        int optionCount = 0;
        foreach (var row in target)
        {
            CheckBudget();
            var originalPeriod = periods.SingleOrDefault(p => p.TimeSlotID == row.TimeSlotID)
                ?? throw new ArgumentException("إحدى المحاضرات مرتبطة بفترة غير صالحة؛ صححها قبل إعادة التنظيم.");
            var choices = new List<Candidate>();
            for (int d = 0; d < days.Count; d++)
            {
                for (int p = 0; p < periods.Count; p++)
                {
                    CheckBudget();
                    if (periods[p].EndTime - periods[p].StartTime != originalPeriod.EndTime - originalPeriod.StartTime) continue;
                    if (row.IsLocked && (days[d] != row.DayOfWeek || periods[p].TimeSlotID != row.TimeSlotID)) continue;
                    foreach (var room in catalog.Rows<Classroom>().Where(room =>
                        ScheduleRoomSelector.MatchesLectureType(room, row.LectureType) &&
                        ScheduleRoomSelector.HasCapacity(room, sections[row.SectionID!.Value], row.GroupName)))
                    {
                        if (row.IsLocked && room.ClassroomID != row.ClassroomID) continue;
                        var candidate = Copy(row);
                        candidate.DayOfWeek = days[d]; candidate.TimeSlotID = periods[p].TimeSlotID; candidate.ClassroomID = room.ClassroomID;
                        if (profiles.TryGetValue((row.FacultyMemberID, row.SemesterNumber), out var profile) && !FacultyPreferenceRules.Allows(profile, candidate, periods[p])) continue;
                        if (!fixedRows.Any(other => ScheduleConflictChecker.HasPairConflict(candidate, other)))
                        {
                            if (++optionCount > 300000) throw new SearchLimitException();
                            choices.Add(new(candidate, d, p));
                        }
                    }
                }
            }
            candidates.Add(row.ScheduleID, choices);
        }
        var summary = primary.Select(row => new FacultySlotSummary(row.ScheduleID, subjects[row.SubjectID].SubjectName,
            candidates[row.ScheduleID].Count(c => request.SelectedDays.Contains(c.Lecture.DayOfWeek)),
            candidates[row.ScheduleID].Count(c => !request.SelectedDays.Contains(c.Lecture.DayOfWeek)))).ToList();
        var suggestions = new List<string>();
        foreach (var row in summary.Where(s => s.PreferredSlots == 0))
            suggestions.Add($"المحاضرة #{row.ScheduleID} ({row.Subject}): لا توجد خيارات في الأيام المختارة بعد فحص الحجز والسعة ونوع القاعة والمدة.");
        var extras = candidates.Values.SelectMany(x => x).Where(c => !request.SelectedDays.Contains(c.Lecture.DayOfWeek))
            .Select(c => c.Lecture.DayOfWeek).Distinct().ToArray();
        if (extras.Length > 0) suggestions.Add("أيام إضافية لها خيارات فردية تحتاج فحصًا جماعيًا: " + string.Join("، ", extras));
        if (primary.Any(s => s.LectureType == "Practical" && !string.IsNullOrEmpty(s.GroupName)))
            suggestions.Add("تبادل العملي إلزامي؛ تُفحص المحاضرات المقابلة مع كل مقترح.");
        suggestions.Add(request.MaxOtherLectureChanges == 0 ? "محاضرات التدريسيين الآخرين ثابتة." : $"يسمح بتحريك {request.MaxOtherLectureChanges} محاضرة للآخرين كحد أقصى، مع احترام تثبيت المحاضرات وتفضيلاتهم المحفوظة.");
        var byPeriod = periods.ToDictionary(p => p.TimeSlotID);
        int minutes = primary.Sum(s => FacultyPreferenceRules.Duration(byPeriod[s.TimeSlotID]));
        if (request.MaxDailyMinutes.HasValue && request.MaxDailyMinutes.Value * request.SelectedDays.Count < minutes)
            suggestions.Add($"المحاضرات تحتاج {minutes} دقيقة أسبوعيًا، بينما سعة الأيام المختارة وفق الحد اليومي {request.MaxDailyMinutes.Value * request.SelectedDays.Count} دقيقة فقط.");
        var roomNames = catalog.Rows<Classroom>().ToDictionary(r => r.ClassroomID, r => r.ClassroomNumber);
        var detail = candidates.Values.SelectMany(x => x).Where(c => c.Lecture.FacultyMemberID == request.FacultyId).Select(c => new FacultyAvailableSlot(c.Lecture.ScheduleID,
            subjects[c.Lecture.SubjectID].SubjectName, c.Lecture.DayOfWeek,
            periods[c.PeriodIndex].StartTime.ToString(@"hh\:mm") + " – " + periods[c.PeriodIndex].EndTime.ToString(@"hh\:mm"),
            roomNames[c.Lecture.ClassroomID], request.SelectedDays.Contains(c.Lecture.DayOfWeek))).ToArray();
        Result Pack(string status, List<Schedule>? plan) => new(status, plan, summary, suggestions) { Options = detail };
        if (inspectOnly) return Pack("Inspected", null);
        CheckBudget();
        double remaining = request.TimeLimitSeconds - clock.Elapsed.TotalSeconds;
        var first = Run(true, Math.Max(0.001, remaining / (request.Mode == FacultyAvailabilityMode.Preferred ? 2 : 1)));
        if (first.Plan is not null || request.Mode == FacultyAvailabilityMode.Strict) return first;
        token.ThrowIfCancellationRequested();
        suggestions.Add(first.Status == "Infeasible"
            ? "ثبت عدم وجود توزيع كامل داخل الأيام المختارة مع القيود الحالية؛ جرى البحث في بقية الأيام المسموحة."
            : "لم يُعثر على حل في الأيام المفضلة ضمن مهلة المرحلة الأولى؛ يجري فحص البدائل دون اعتبار ذلك إثباتًا للاستحالة.");
        CheckBudget();
        return Run(false, Math.Max(0.001, request.TimeLimitSeconds - clock.Elapsed.TotalSeconds));

        Result Run(bool selectedOnly, double seconds)
        {
            token.ThrowIfCancellationRequested();
            var model = new CpModel();
            var options = new Dictionary<int, List<(Candidate Choice, BoolVar On)>>();
            var slotVars = new Dictionary<int, IntVar>();
            var roomVars = new Dictionary<int, IntVar>();
            var outside = new List<BoolVar>(); var moved = new List<BoolVar>(); var otherMoved = new List<BoolVar>();
            var secondary = new List<IntVar>(); var secondaryCosts = new List<long>();
            foreach (var row in target)
            {
                CheckBudget();
                var choices = candidates[row.ScheduleID].Where(c => row.FacultyMemberID != request.FacultyId || !selectedOnly || request.SelectedDays.Contains(c.Lecture.DayOfWeek))
                    .Select((c, i) => (Choice: c, On: model.NewBoolVar($"l{row.ScheduleID}_{i}"))).ToList();
                if (choices.Count == 0) return Pack(CpSolverStatus.Infeasible.ToString(), null);
                options.Add(row.ScheduleID, choices);
                model.AddExactlyOne(choices.Select(x => (ILiteral)x.On));
                var slot = model.NewIntVar(0, days.Count * periods.Count - 1, $"slot{row.ScheduleID}");
                model.Add(slot == LinearExpr.WeightedSum(choices.Select(x => x.On), choices.Select(x => (long)(x.Choice.DayIndex * periods.Count + x.Choice.PeriodIndex))));
                slotVars.Add(row.ScheduleID, slot);
                var room = model.NewIntVar(0, catalog.Rows<Classroom>().Max(r => r.ClassroomID), $"room{row.ScheduleID}");
                model.Add(room == LinearExpr.WeightedSum(choices.Select(x => x.On), choices.Select(x => (long)x.Choice.Lecture.ClassroomID)));
                roomVars.Add(row.ScheduleID, room);
                foreach (var choice in choices)
                {
                    bool timeChanged = choice.Choice.Lecture.DayOfWeek != row.DayOfWeek || choice.Choice.Lecture.TimeSlotID != row.TimeSlotID;
                    bool roomChanged = choice.Choice.Lecture.ClassroomID != row.ClassroomID;
                    bool own = row.FacultyMemberID == request.FacultyId;
                    if (own && !request.SelectedDays.Contains(choice.Choice.Lecture.DayOfWeek)) outside.Add(choice.On);
                    if (timeChanged || roomChanged) { if (own) moved.Add(choice.On); else otherMoved.Add(choice.On); }
                    long cost = own ? (timeChanged ? 2 : 0) + (roomChanged ? 1 : 0) : 0;
                    if (cost > 0) { secondary.Add(choice.On); secondaryCosts.Add(cost * (primary.Count + 7201L)); }
                    if (own && request.PreferredPeriodIds!.Count > 0 && !request.PreferredPeriodIds.Contains(choice.Choice.Lecture.TimeSlotID))
                    { secondary.Add(choice.On); secondaryCosts.Add(1); }
                    if (!timeChanged && !roomChanged) model.AddHint(choice.On, 1);
                }
            }
            var allOptions = options.Values.SelectMany(x => x).ToList();
            var sharedOptions = allOptions.Select(x => new SchedulingOption(x.Choice.Lecture, x.Choice.DayIndex, x.Choice.PeriodIndex, x.On)).ToList();
            SchedulingCpSupport.AddResourceConstraints(model, sharedOptions);
            SchedulingCpSupport.AddSectionDayLimits(model, sharedOptions, fixedRows);
            SchedulingCpSupport.AddProfiles(model, sharedOptions, fixedRows, periods, profiles);
            model.Add(LinearExpr.Sum(otherMoved) <= request.MaxOtherLectureChanges);
            var quality = SchedulingCpSupport.Quality(model, sharedOptions, fixedRows, periods, request);
            IntVar Slot(Schedule s) => slotVars.TryGetValue(s.ScheduleID, out var v) ? v :
                model.NewConstant(days.ToList().IndexOf(s.DayOfWeek) * periods.Count + periods.FindIndex(p => p.TimeSlotID == s.TimeSlotID));
            IntVar Room(Schedule s) => roomVars.TryGetValue(s.ScheduleID, out var v) ? v : model.NewConstant(s.ClassroomID);
            var affected = target.Where(s => s.LectureType == "Practical" && !string.IsNullOrWhiteSpace(s.GroupName)).Select(s => s.SectionID!.Value).ToHashSet();
            foreach (var units in FacultyPracticalExchange.Scopes(all.Where(s => s.SemesterNumber == request.Semester).ToList(), affected))
            {
                int unpairedCount = FacultyPracticalExchange.UnpairedCount(units);
                var edges = units.Select(_ => new List<BoolVar>()).ToList(); var singles = new List<BoolVar>();
                for (int a = 0; a < units.Count; a++)
                {
                    if (unpairedCount > 0) { var single = model.NewBoolVar($"odd{a}_{units[a].First.ScheduleID}"); singles.Add(single); edges[a].Add(single); }
                    for (int b = a + 1; b < units.Count; b++)
                    {
                        var x = units[a]; var y = units[b];
                        if (x.First.SubjectID == y.First.SubjectID || x.First.FacultyMemberID != x.Second.FacultyMemberID || y.First.FacultyMemberID != y.Second.FacultyMemberID) continue;
                        var pair = model.NewBoolVar($"exchange{x.First.ScheduleID}_{y.First.ScheduleID}");
                        edges[a].Add(pair); edges[b].Add(pair);
                        model.Add(Slot(x.First) == Slot(y.Second)).OnlyEnforceIf(pair);
                        model.Add(Slot(x.Second) == Slot(y.First)).OnlyEnforceIf(pair);
                        model.Add(Room(x.First) == Room(x.Second)).OnlyEnforceIf(pair);
                        model.Add(Room(y.First) == Room(y.Second)).OnlyEnforceIf(pair);
                        var tuples = new List<long[]>();
                        for (int d = 0; d < days.Count; d++) for (int p = 0; p + 1 < periods.Count; p++)
                        { long v = d * periods.Count + p; tuples.Add([v,v+1]); tuples.Add([v+1,v]); }
                        var table = model.AddAllowedAssignments(new[] { Slot(x.First), Slot(x.Second) });
                        foreach (var tuple in tuples) table.AddTuple(tuple);
                        table.OnlyEnforceIf(pair);
                    }
                }
                foreach (var edge in edges) model.Add(LinearExpr.Sum(edge) == 1);
                if (singles.Count > 0) model.Add(LinearExpr.Sum(singles) == unpairedCount);
            }
            long count = primary.Count;
            var priorities = new List<(LinearExpr, long)>
            {
                (LinearExpr.Sum(outside), count), (LinearExpr.Sum(otherMoved), request.MaxOtherLectureChanges),
                (quality.TargetExcess, request.TargetTeachingDays.HasValue ? 5 : 0)
            };
            if (request.Goal == ScheduleOptimizationGoal.CompactAttendance) priorities.Add((quality.Days, 5));
            if (request.Goal == ScheduleOptimizationGoal.FewerGaps) priorities.Add((quality.Gaps, 7200));
            if (request.Goal == ScheduleOptimizationGoal.BalancedStudentLoad) priorities.Add((quality.StudentPeak, 1440));
            priorities.Add((LinearExpr.Sum(moved), count));
            priorities.Add((LinearExpr.WeightedSum(secondary, secondaryCosts) + quality.Gaps,
                3 * count * (count + 7201) + count + 7200));
            if (request.HasScoredPreferences)
            {
                var preferences = SchedulingCpSupport.PreferenceQuality(model, sharedOptions, fixedRows, periods, profiles);
                SchedulingCpSupport.MinimizePriorities(model, (preferences.Total, preferences.Bound),
                    (preferences.WorstNormalized, 1000), (LinearExpr.Sum(moved.Concat(otherMoved)), target.Count),
                    (quality.Gaps, 7200));
            }
            else SchedulingCpSupport.MinimizePriorities(model, priorities.ToArray());
            string error = model.Validate();
            if (error.Length > 0) throw new InvalidOperationException("Invalid scheduling model: " + error);
            using var solver = new CpSolver();
            CheckBudget();
            solver.StringParameters = "max_time_in_seconds:" + Math.Max(.001, Math.Min(seconds, request.TimeLimitSeconds - clock.Elapsed.TotalSeconds)).ToString(CultureInfo.InvariantCulture) + " num_search_workers:1 random_seed:1";
            using var cancel = token.Register(solver.StopSearch);
            var status = solver.Solve(model);
            token.ThrowIfCancellationRequested();
            if (status is not (CpSolverStatus.Optimal or CpSolverStatus.Feasible)) return Pack(status.ToString(), null);
            var plan = options.Values.Select(list => Copy(list.Single(x => solver.BooleanValue(x.On)).Choice.Lecture)).ToList();
            return Pack(status.ToString(), plan);
        }
    }
}
