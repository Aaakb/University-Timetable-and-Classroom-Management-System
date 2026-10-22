using System.Diagnostics;
using System.Globalization;
using Google.OrTools.Sat;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

internal static class ImportedTimetableRepairPlanner
{
    internal sealed record Result(string Status, List<Schedule>? Plan)
    {
        public IReadOnlyList<string> Diagnostics { get; init; } = [];
    }
    private sealed record Choice(Schedule Row, int Slot, BoolVar On);
    private sealed class SearchLimitException : Exception;

    public static Result Solve(ImportCatalog catalog, IReadOnlyList<TimetableImportRow> imported,
        IReadOnlyList<Schedule> fixedRows, double seconds, CancellationToken token, bool optimizePreferences = false,
        TimetableAlternativeSearch? alternativeSearch = null, bool allowFacultyReassignment = false, bool requirePracticalExchange = true)
    {
        var clock = Stopwatch.StartNew();
        void CheckBudget()
        {
            token.ThrowIfCancellationRequested();
            if (clock.Elapsed.TotalSeconds >= seconds) throw new SearchLimitException();
        }
        try
        {
            CheckBudget(); FacultySchedulingRuntime.EnsureAvailable();
            var days = ScheduleDayRules.AllDays;
            var periods = catalog.Rows<TimeSlot>().Where(p => !p.IsBreak).OrderBy(p => p.StartTime).ToList();
            ScheduleTimingRules.EnsureCompatibleLectureSlots(periods);
            var sections = catalog.Rows<Section>().ToDictionary(s => s.SectionID);
            var rooms = catalog.Rows<Classroom>();
            var profiles = FacultyPreferenceRules.FromCatalog(catalog);
            var semesters = imported.Select(r => r.Schedule.SemesterNumber).ToHashSet();
            profiles = profiles.Where(p => semesters.Contains(p.Key.Semester)).ToDictionary(p => p.Key, p => p.Value);
            var target = imported.Select((r, i) =>
            {
                var copy = FacultyCpSatPlanner.Copy(r.Schedule);
                // New lectures have no database ID; give each one its own model identity.
                if (copy.ScheduleID == 0) copy.ScheduleID = -i - 1;
                return copy;
            }).ToList();
            if (target.Count == 0) return new("Infeasible", null);
            var model = new CpModel();
            var diagnostics = new Dictionary<int, string>();
            var diagnosticAssumptions = new List<BoolVar>();
            string SectionLabel(int? id)
            {
                var section = sections.GetValueOrDefault(id ?? 0);
                if (section is null) return $"رقم الشعبة: {id}";
                string year = catalog.Rows<StudyYear>().Single(y => y.StudyYearID == section.StudyYearID).YearName;
                year = year.ToLowerInvariant() switch { "first year" => "المرحلة الأولى", "second year" => "المرحلة الثانية", "third year" => "المرحلة الثالثة", "fourth year" => "المرحلة الرابعة", _ => year };
                string? branch = catalog.Rows<Branch>().FirstOrDefault(b => b.BranchID == section.BranchID)?.BranchName;
                return string.Join(" / ", new[] { $"الشعبة «{section.SectionName}»", year, branch }.Where(s => !string.IsNullOrWhiteSpace(s)));
            }
            var options = new Dictionary<int, List<Choice>>();
            var slots = new Dictionary<int, IntVar>();
            var roomVars = new Dictionary<int, IntVar>();
            var facultyVars = new Dictionary<int, IntVar>();
            var assignments = catalog.Rows<FacultyMemberSubject>();
            var objective = new List<IntVar>(); var costs = new List<long>();
            int optionCount = 0;
            long minorWeight = target.Count + 1L;
            // A changed lecture costs more than every possible secondary change combined.
            long moveWeight = minorWeight * minorWeight;
            foreach (var row in target)
            {
                CheckBudget();
                var originalPeriod = periods.Single(p => p.TimeSlotID == row.TimeSlotID);
                var choices = new List<Choice>();
                var facultyIds = allowFacultyReassignment && !row.IsLocked
                    ? assignments.Where(a => a.SubjectID == row.SubjectID).Select(a => a.FacultyMemberID).Distinct().Order().ToArray()
                    : [row.FacultyMemberID];
                foreach (var day in days)
                foreach (var period in periods)
                {
                    CheckBudget();
                    if (period.EndTime - period.StartTime != originalPeriod.EndTime - originalPeriod.StartTime) continue;
                    if (row.IsLocked && (day != row.DayOfWeek || period.TimeSlotID != row.TimeSlotID)) continue;
                    foreach (var room in rooms)
                    {
                        CheckBudget();
                        if (row.IsLocked && room.ClassroomID != row.ClassroomID) continue;
                        if (!ScheduleRoomSelector.MatchesLectureType(room, row.LectureType) ||
                            !ScheduleRoomSelector.HasCapacity(room, sections[row.SectionID!.Value], row.GroupName)) continue;
                        foreach (int facultyId in facultyIds)
                        {
                            var candidate = FacultyCpSatPlanner.Copy(row);
                            candidate.DayOfWeek = day; candidate.TimeSlotID = period.TimeSlotID; candidate.ClassroomID = room.ClassroomID;
                            candidate.FacultyMemberID = facultyId;
                            if (profiles.TryGetValue((candidate.FacultyMemberID, candidate.SemesterNumber), out var profile) && !FacultyPreferenceRules.Allows(profile, candidate, period)) continue;
                            if (fixedRows.Any(other => ScheduleConflictChecker.HasPairConflict(candidate, other))) continue;
                            if (++optionCount > 300000) throw new SearchLimitException();
                            var on = model.NewBoolVar($"l{row.ScheduleID}_{choices.Count}");
                            choices.Add(new(candidate, days.ToList().IndexOf(day) * periods.Count + periods.IndexOf(period), on));
                            bool timeChanged = day != row.DayOfWeek || period.TimeSlotID != row.TimeSlotID;
                            bool roomChanged = room.ClassroomID != row.ClassroomID;
                            bool facultyChanged = facultyId != row.FacultyMemberID;
                            objective.Add(on); costs.Add((timeChanged || roomChanged || facultyChanged ? moveWeight : 0) + (timeChanged ? minorWeight : 0) + (roomChanged ? 1 : 0) + (facultyChanged ? 1 : 0));
                            if (!timeChanged && !roomChanged && !facultyChanged) model.AddHint(on, 1);
                        }
                    }
                }
                if (choices.Count == 0)
                    return new("Infeasible", null) { Diagnostics = [$"لا يوجد موعد وقاعة مسموحان لمحاضرة «{catalog.Rows<Subject>().Single(s => s.SubjectID == row.SubjectID).SubjectName}» — {SectionLabel(row.SectionID)}. راجع إتاحة التدريسي وسعة ونوع القاعة والمحاضرات المثبتة."] };
                options.Add(row.ScheduleID, choices);
                model.AddExactlyOne(choices.Select(c => (ILiteral)c.On));
                var slot = model.NewIntVar(0, days.Count * periods.Count - 1, $"slot{row.ScheduleID}");
                model.Add(slot == LinearExpr.WeightedSum(choices.Select(c => c.On), choices.Select(c => (long)c.Slot)));
                slots.Add(row.ScheduleID, slot);
                var classroom = model.NewIntVar(0, rooms.Max(r => r.ClassroomID), $"room{row.ScheduleID}");
                model.Add(classroom == LinearExpr.WeightedSum(choices.Select(c => c.On), choices.Select(c => (long)c.Row.ClassroomID)));
                roomVars.Add(row.ScheduleID, classroom);
                var faculty = model.NewIntVar(0, facultyIds.DefaultIfEmpty(0).Max(), $"faculty{row.ScheduleID}");
                model.Add(faculty == LinearExpr.WeightedSum(choices.Select(c => c.On), choices.Select(c => (long)c.Row.FacultyMemberID)));
                facultyVars.Add(row.ScheduleID, faculty);
            }
            var allChoices = options.Values.SelectMany(c => c).ToList();
            if (optimizePreferences || alternativeSearch is not null)
                SchedulingCpSupport.AddSectionDayLimits(model, allChoices.Select(c => new SchedulingOption(c.Row,
                    c.Slot / periods.Count, c.Slot % periods.Count, c.On)).ToList(), fixedRows,
                    (assumption, scope) =>
                    {
                        diagnosticAssumptions.Add(assumption);
                        diagnostics.Add(assumption.Index, $"حد 4 أيام: {SectionLabel(scope.Section)}، الفصل {scope.Semester}.");
                    });
            if (alternativeSearch is not null)
            {
                foreach (var previous in alternativeSearch.ExcludedPlans)
                {
                    CheckBudget();
                    var previousById = previous.ToDictionary(s => s.ScheduleID);
                    var matching = allChoices.Where(c => previousById.TryGetValue(c.Row.ScheduleID, out var old) &&
                        old.DayOfWeek == c.Row.DayOfWeek && old.TimeSlotID == c.Row.TimeSlotID).Select(c => c.On);
                    // Require a genuinely different time assignment, not merely a room swap.
                    model.Add(LinearExpr.Sum(matching) <= target.Count - 1);
                }
            }
            SchedulingCpSupport.AddProfiles(model, allChoices.Select(c => new SchedulingOption(c.Row,
                c.Slot / periods.Count, c.Slot % periods.Count, c.On)).ToList(), fixedRows, periods, profiles);
            foreach (var simultaneous in allChoices.GroupBy(c => c.Slot))
            {
                CheckBudget();
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
            IntVar Slot(Schedule s) => slots.TryGetValue(s.ScheduleID, out var slot) ? slot : model.NewConstant(
                days.ToList().IndexOf(s.DayOfWeek) * periods.Count + periods.FindIndex(p => p.TimeSlotID == s.TimeSlotID));
            IntVar Room(Schedule s) => roomVars.TryGetValue(s.ScheduleID, out var room) ? room : model.NewConstant(s.ClassroomID);
            IntVar Faculty(Schedule s) => facultyVars.TryGetValue(s.ScheduleID, out var faculty) ? faculty : model.NewConstant(s.FacultyMemberID);
            var affected = target.Where(s => s.LectureType == "Practical" && !string.IsNullOrWhiteSpace(s.GroupName)).Select(s => s.SectionID!.Value).ToHashSet();
            var full = fixedRows.Concat(target).Where(s => s.SemesterNumber == target[0].SemesterNumber).ToList();
            foreach (var units in requirePracticalExchange ? FacultyPracticalExchange.Scopes(full, affected) : [])
            {
                CheckBudget();
                int unpairedCount = FacultyPracticalExchange.UnpairedCount(units);
                var exchangeAssumption = model.NewBoolVar($"requireExchange_{units[0].First.SectionID}");
                model.AddAssumption(exchangeAssumption);
                diagnosticAssumptions.Add(exchangeAssumption);
                diagnostics.Add(exchangeAssumption.Index, $"تبادل العملي بفترتين متتاليتين: {SectionLabel(units[0].First.SectionID)}.");
                var edges = units.Select(_ => new List<BoolVar>()).ToList(); var singles = new List<BoolVar>();
                for (int a = 0; a < units.Count; a++)
                {
                    CheckBudget();
                    if (unpairedCount > 0)
                    {
                        var single = model.NewBoolVar($"single{units[a].First.ScheduleID}"); singles.Add(single); edges[a].Add(single);
                    }
                    for (int b = a + 1; b < units.Count; b++)
                    {
                        CheckBudget();
                        var x = units[a]; var y = units[b];
                        if (x.First.SubjectID == y.First.SubjectID || !allowFacultyReassignment &&
                            (x.First.FacultyMemberID != x.Second.FacultyMemberID || y.First.FacultyMemberID != y.Second.FacultyMemberID)) continue;
                        var pair = model.NewBoolVar($"exchange{x.First.ScheduleID}_{y.First.ScheduleID}"); edges[a].Add(pair); edges[b].Add(pair);
                        model.Add(Slot(x.First) == Slot(y.Second)).OnlyEnforceIf(pair);
                        model.Add(Slot(x.Second) == Slot(y.First)).OnlyEnforceIf(pair);
                        model.Add(Room(x.First) == Room(x.Second)).OnlyEnforceIf(pair);
                        model.Add(Room(y.First) == Room(y.Second)).OnlyEnforceIf(pair);
                        model.Add(Faculty(x.First) == Faculty(x.Second)).OnlyEnforceIf(pair);
                        model.Add(Faculty(y.First) == Faculty(y.Second)).OnlyEnforceIf(pair);
                        var table = model.AddAllowedAssignments([Slot(x.First), Slot(x.Second)]);
                        for (int d = 0; d < days.Count; d++) for (int p = 0; p + 1 < periods.Count; p++)
                        {
                            long slot = d * periods.Count + p; table.AddTuple([slot, slot + 1]); table.AddTuple([slot + 1, slot]);
                        }
                        table.OnlyEnforceIf(pair);
                    }
                }
                foreach (var edge in edges) model.Add(LinearExpr.Sum(edge) == 1).OnlyEnforceIf(exchangeAssumption);
                if (singles.Count > 0) model.Add(LinearExpr.Sum(singles) == unpairedCount).OnlyEnforceIf(exchangeAssumption);
            }
            var preferenceQuality = SchedulingCpSupport.PreferenceQuality(model, allChoices.Select(c => new SchedulingOption(c.Row,
                c.Slot / periods.Count, c.Slot % periods.Count, c.On)).ToList(), fixedRows, periods, profiles);
            if (alternativeSearch is not null)
            {
                if (alternativeSearch.MaximumPreferencePenalty.HasValue) model.Add(preferenceQuality.Total <= alternativeSearch.MaximumPreferencePenalty.Value);
                var shared = allChoices.Select(c => new SchedulingOption(c.Row, c.Slot / periods.Count, c.Slot % periods.Count, c.On)).ToList();
                var facultyIds = target.Concat(fixedRows).Select(s => s.FacultyMemberID).Distinct().ToList();
                var quality = new List<SchedulingCpSupport.QualityExpressions>();
                for (int i = 0; i < facultyIds.Count; i++)
                {
                    CheckBudget();
                    quality.Add(SchedulingCpSupport.Quality(model, shared, fixedRows, periods,
                        new FacultyScheduleRequest(facultyIds[i], target[0].SemesterNumber, FacultyAvailabilityMode.Preferred, days,
                            PreferredPeriodIds: []), includeStudentLoad: i == 0));
                }
                var gaps = LinearExpr.Sum(quality.Select(q => q.Gaps));
                var originalById = target.ToDictionary(s => s.ScheduleID);
                var movement = LinearExpr.Sum(allChoices.Where(c => !ImportCatalog.Same(c.Row, originalById[c.Row.ScheduleID])).Select(c => c.On));
                long movementBound = target.Count;
                var priorities = new List<(LinearExpr, long)> { (preferenceQuality.Total, preferenceQuality.Bound), (preferenceQuality.WorstNormalized, preferenceQuality.Bound == 0 ? 0 : 1000) };
                if (alternativeSearch.Goal == TimetableAlternativeGoal.FewerFacultyGaps) priorities.Add((gaps, 7200L * facultyIds.Count));
                if (alternativeSearch.Goal == TimetableAlternativeGoal.BalancedStudentLoad) priorities.Add((quality[0].StudentPeak, 1440));
                priorities.Add((movement, movementBound));
                if (alternativeSearch.Goal == TimetableAlternativeGoal.FewestChanges) priorities.Add((gaps, 7200L * facultyIds.Count));
                SchedulingCpSupport.MinimizePriorities(model, priorities.ToArray());
            }
            else if (optimizePreferences)
                SchedulingCpSupport.MinimizePriorities(model, (preferenceQuality.Total, preferenceQuality.Bound),
                    (preferenceQuality.WorstNormalized, 1000),
                    (LinearExpr.WeightedSum(objective, costs), target.Count * (moveWeight + minorWeight + 2)));
            else SchedulingCpSupport.MinimizePriorities(model,
                    (LinearExpr.WeightedSum(objective, costs), target.Count * (moveWeight + minorWeight + 2)),
                    (preferenceQuality.Total, preferenceQuality.Bound));
            string error = model.Validate();
            if (error.Length > 0) throw new InvalidOperationException("نموذج إصلاح الجدول غير صالح: " + error);
            // Fresh generation has no saved placement to protect. If every
            // available cell has the same preference level, feasibility suffices.
            if (allowFacultyReassignment && !profiles.Values.Any(p => p.HasScoredPreferences &&
                (p.DayPreferences.Any(c => c.Level == FacultyPreferenceLevel.Preferred) ||
                 p.PeriodPreferences.Any(c => c.Level == FacultyPreferenceLevel.Preferred))))
                model.Model.Objective = null;
            CheckBudget();
            using var solver = new CpSolver();
            int constraintCount = model.Model.Constraints.Count;
            if (allowFacultyReassignment)
            {
                // Hard constraints allow parallel feasibility search. Assumption
                // literals are restored only for explaining a proven failure.
                model.Model.Assumptions.Clear();
                foreach (var assumption in diagnosticAssumptions) model.Add(assumption == 1);
                model.Model.SolutionHint = null;
            }
            solver.StringParameters = "max_time_in_seconds:" + Math.Max(.001, seconds - clock.Elapsed.TotalSeconds).ToString(CultureInfo.InvariantCulture) +
                " num_search_workers:" + (allowFacultyReassignment ? Math.Min(8, Environment.ProcessorCount) : 1) + " random_seed:1";
            using var cancellation = token.Register(solver.StopSearch);
            Trace.WriteLine($"Timetable solver: rows={target.Count}, choices={optionCount}, buildSeconds={clock.Elapsed.TotalSeconds:F2}, facultyChoices={allowFacultyReassignment}");
            var status = solver.Solve(model); token.ThrowIfCancellationRequested();
            Trace.WriteLine($"Timetable solver result: {status}, totalSeconds={clock.Elapsed.TotalSeconds:F2}");
            if (status is not (CpSolverStatus.Feasible or CpSolverStatus.Optimal))
            {
                var core = status == CpSolverStatus.Infeasible ? solver.SufficientAssumptionsForInfeasibility().ToArray() : [];
                // Optimization may report every assumption. A short feasibility
                // pass can isolate the actual conflicting rules more precisely.
                double diagnosticSeconds = Math.Min(3, seconds - clock.Elapsed.TotalSeconds);
                if (status == CpSolverStatus.Infeasible && (core.Length > 1 || allowFacultyReassignment) && diagnosticSeconds > .01)
                {
                    if (allowFacultyReassignment)
                    {
                        while (model.Model.Constraints.Count > constraintCount) model.Model.Constraints.RemoveAt(model.Model.Constraints.Count - 1);
                        foreach (var assumption in diagnosticAssumptions) model.AddAssumption(assumption);
                    }
                    model.Model.Objective = null;
                    solver.StringParameters = "max_time_in_seconds:" + diagnosticSeconds.ToString(CultureInfo.InvariantCulture) + " num_search_workers:1";
                    if (solver.Solve(model) == CpSolverStatus.Infeasible) core = solver.SufficientAssumptionsForInfeasibility().ToArray();
                    token.ThrowIfCancellationRequested();
                }
                return new(status.ToString(), null) { Diagnostics = core.Where(diagnostics.ContainsKey).Select(i => diagnostics[i]).Distinct().ToArray() };
            }
            var plan = target.Select((row, index) =>
            {
                var selected = FacultyCpSatPlanner.Copy(options[row.ScheduleID].Single(c => solver.BooleanValue(c.On)).Row);
                selected.ScheduleID = imported[index].Schedule.ScheduleID; return selected;
            }).ToList();
            return new(status.ToString(), plan);
        }
        catch (SearchLimitException) { return new("Unknown", null); }
    }
}
