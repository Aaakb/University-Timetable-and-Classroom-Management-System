using System.Data;
using System.Diagnostics;
using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

public sealed class TimetableAlternativesService
{
    private readonly Func<AppDbContext> factory;
    public TimetableAlternativesService() : this(() => new AppDbContext()) { }
    public TimetableAlternativesService(Func<AppDbContext> factory) => this.factory = factory;
    public IReadOnlyList<string> TeachingDays => ScheduleDayRules.AllDays.ToArray();
    private async Task<ImportCatalog> SnapshotAsync(CancellationToken token)
    {
        await using var db = factory(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var data = await ImportCatalog.LoadAsync(db, token); await tx.CommitAsync(token); return data;
    }
    public async Task<TimetableAlternative> GetCurrentAsync(int semester, CancellationToken token = default)
    {
        if (semester is < 1 or > 2) throw new ArgumentException("اختر الفصل الدراسي.");
        var data = await SnapshotAsync(token);
        return Describe(data, data.Rows<Schedule>().Where(s => s.SemesterNumber == semester).ToList(), semester, "الجدول الحالي", "Current", 0);
    }
    public async Task<TimetableAlternativesPreview> GenerateAsync(TimetableAlternativesRequest request, CancellationToken token = default)
    {
        if (request.Semester is < 1 or > 2 || request.Count is < 1 or > 3 || !double.IsFinite(request.TimeLimitSeconds) || request.TimeLimitSeconds is < .01 or > 120)
            throw new ArgumentException("اختر الفصل، وعدد بدائل من 1 إلى 3، ومهلة بحث حتى 120 ثانية.");
        var data = await SnapshotAsync(token); string version = data.Version();
        var rows = data.Rows<Schedule>().Where(s => s.SemesterNumber == request.Semester).ToList();
        var current = Describe(data, rows, request.Semester, "الجدول الحالي", "Current", 0);
        if (rows.Count == 0) return new(current, [], "لا توجد محاضرات محفوظة لهذا الفصل. أكمل إدخالها أو استيرادها أولًا.", version);
        var movable = rows.Where(s => !s.IsLocked).ToList(); var fixedRows = rows.Where(s => s.IsLocked).ToList();
        if (movable.Count == 0) return new(current, [], "كل المحاضرات مثبتة. اختر المحاضرات التي تسمح بتحريكها وألغِ تثبيتها صراحةً؛ لن يغيّر البحث الأقفال تلقائيًا.", version);
        var profiles = FacultyPreferenceRules.FromCatalog(data).Where(p => p.Key.Semester == request.Semester).ToDictionary(p => p.Key, p => p.Value);
        var periods = data.Rows<TimeSlot>().ToDictionary(p => p.TimeSlotID);
        foreach (var profile in profiles.Values) FacultyPreferenceRules.ValidateMatrix(profile, periods.Values);
        bool baselineValid = profiles.Values.All(p => FacultyPreferenceRules.Violation(p, rows, periods, requireAttendance: true) is null);
        long? cap = baselineValid ? rows.Where(s => profiles.TryGetValue((s.FacultyMemberID, s.SemesterNumber), out var p) && p.HasScoredPreferences &&
            FacultyPreferenceRules.Level(p, s.DayOfWeek, s.TimeSlotID) == FacultyPreferenceLevel.Preferred).Sum(s => 2L * FacultyPreferenceRules.Duration(periods[s.TimeSlotID])) : null;
        var excluded = new List<IReadOnlyList<Schedule>> { rows }; var alternatives = new List<TimetableAlternative>();
        var clock = Stopwatch.StartNew(); string lastStatus = "Unknown";
        for (int i = 0; i < request.Count; i++)
        {
            token.ThrowIfCancellationRequested(); double remaining = request.TimeLimitSeconds - clock.Elapsed.TotalSeconds;
            if (remaining < .01) break;
            var goal = (TimetableAlternativeGoal)i; var search = Stopwatch.StartNew();
            var result = await Task.Run(() => ImportedTimetableRepairPlanner.Solve(data,
                movable.Select(s => new TimetableImportRow("Saved", 0, s, false)).ToList(), fixedRows,
                Math.Max(.01, remaining / (request.Count - i)), token,
                alternativeSearch: new(goal, excluded, cap)), token);
            search.Stop(); lastStatus = result.Status;
            if (result.Plan is null) { if (result.Status == "Infeasible") break; continue; }
            var full = fixedRows.Concat(result.Plan).ToList();
            await ValidateAsync(data, full, request.Semester, token);
            alternatives.Add(Describe(data, full, request.Semester, goal switch
            { TimetableAlternativeGoal.FewerFacultyGaps => "بديل لتقليل فراغات التدريسيين", TimetableAlternativeGoal.BalancedStudentLoad => "بديل لتوازن العبء اليومي للطلبة", _ => "بديل بأقل تغييرات" }, result.Status, search.Elapsed.TotalSeconds));
            excluded.Add(full);
        }
        string message = alternatives.Count > 0
            ? $"عُثر على {alternatives.Count} بدائل مختلفة في الأيام أو الفترات. قارنها بالجدول الحالي؛ لم يُحفظ أي تغيير. لا تتراجع البدائل عن عقوبة التفضيل الحالية عندما يكون الجدول الحالي موافقًا للتفضيلات."
            : lastStatus == "Infeasible" ? "لا يوجد بديل مختلف في الأيام أو الفترات ضمن الأقفال والتفضيلات الحالية دون تراجع في التفضيلات. راجع القيود أو المحاضرات المسموح تحريكها."
            : "لم يُعثر على بديل مختلف ضمن المهلة؛ هذا لا يثبت استحالة البدائل. جرّب مهلة أطول.";
        return new(current, alternatives, message, version);
    }
    public async Task<int> SaveAsync(TimetableAlternative alternative, bool confirmed, CancellationToken token = default)
    {
        if (!confirmed || alternative.Status is not ("Optimal" or "Feasible")) throw new ArgumentException("اختر بديلًا صالحًا وأكد اعتماده.");
        await using var db = factory(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var data = await ImportCatalog.LoadAsync(db, token);
        if (data.Version() != alternative.Version) throw new InvalidOperationException("تغيرت البيانات أو التفضيلات أو الأقفال؛ أعد إنشاء البدائل.");
        await ValidateAsync(data, alternative.Plan, alternative.Semester, token);
        var original = data.Rows<Schedule>().ToDictionary(s => s.ScheduleID);
        var changed = alternative.Plan.Where(s => !ImportCatalog.Same(s, original[s.ScheduleID])).Select(FacultyCpSatPlanner.Copy).ToList();
        foreach (var row in changed) data.Put(row, original[row.ScheduleID]);
        await FacultyScheduleWriter.ReplacePlacementsAsync(db, changed, token);
        if ((await ImportCatalog.LoadAsync(db, token)).Version() != data.Version()) throw new InvalidOperationException("لم تتطابق نتيجة الحفظ؛ أُلغيت العملية.");
        await tx.CommitAsync(token); return changed.Count;
    }
    public async Task<int> SetLocksAsync(int semester, IReadOnlyList<int> ids, bool locked, string expectedVersion, CancellationToken token = default)
    {
        if (ids.Count == 0 || ids.Distinct().Count() != ids.Count) throw new ArgumentException("اختر المحاضرات المطلوبة.");
        await using var db = factory(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var data = await ImportCatalog.LoadAsync(db, token);
        if (data.Version() != expectedVersion) throw new InvalidOperationException("تغيرت البيانات؛ حدّث الجدول قبل تعديل التثبيت.");
        if (data.Rows<Schedule>().Count(s => s.SemesterNumber == semester && ids.Contains(s.ScheduleID)) != ids.Count) throw new ArgumentException("المحاضرات المحددة لا تخص الفصل الحالي.");
        int count = await db.Schedules.Where(s => s.SemesterNumber == semester && ids.Contains(s.ScheduleID) && s.IsLocked != locked)
            .ExecuteUpdateAsync(update => update.SetProperty(s => s.IsLocked, locked), token);
        await tx.CommitAsync(token); return count;
    }
    private async Task ValidateAsync(ImportCatalog data, IReadOnlyList<Schedule> plan, int semester, CancellationToken token)
    {
        var original = data.Rows<Schedule>().Where(s => s.SemesterNumber == semester).ToDictionary(s => s.ScheduleID);
        if (plan.Count != original.Count || plan.Select(s => s.ScheduleID).Distinct().Count() != plan.Count || plan.Any(s => !original.ContainsKey(s.ScheduleID))) throw new ArgumentException("البديل يضيف أو يحذف محاضرات.");
        var periods = data.Rows<TimeSlot>().Where(p => !p.IsBreak).OrderBy(p => p.StartTime).ToList();
        await using var db = factory();
        foreach (var row in plan)
        {
            token.ThrowIfCancellationRequested(); var before = original[row.ScheduleID]; var expected = FacultyCpSatPlanner.Copy(before);
            expected.DayOfWeek = row.DayOfWeek; expected.TimeSlotID = row.TimeSlotID; expected.ClassroomID = row.ClassroomID;
            if (!ImportCatalog.Same(expected, row) || before.IsLocked && !ImportCatalog.Same(before, row)) throw new ArgumentException("البديل يغير هوية محاضرة أو محاضرة مثبتة.");
            if (FacultyPreferenceRules.Duration(periods.Single(p => p.TimeSlotID == before.TimeSlotID)) != FacultyPreferenceRules.Duration(periods.Single(p => p.TimeSlotID == row.TimeSlotID))) throw new ArgumentException("البديل يغير مدة محاضرة.");
            var copy = FacultyCpSatPlanner.Copy(row); await ScheduleService.ValidateAsync(db, copy, true, checkConflicts: false);
            if (!ImportCatalog.Same(copy, row)) throw new ArgumentException("إحدى المحاضرات تحتاج تصحيحًا.");
        }
        if (ScheduleConflictChecker.HasAnyPairConflict(plan)) throw new ArgumentException("يوجد تعارض في البديل.");
        ScheduleDayRules.EnsureSectionDayLimit(plan, data.Rows<Section>(), data.Rows<StudyYear>(), data.Rows<Branch>());
        FacultyPreferenceRules.Validate(plan, periods.ToDictionary(p => p.TimeSlotID), FacultyPreferenceRules.FromCatalog(data).Where(p => p.Key.Semester == semester).ToDictionary(p => p.Key, p => p.Value));
        FacultyPracticalExchange.Validate(plan, plan.Where(s => s.LectureType == "Practical" && !string.IsNullOrWhiteSpace(s.GroupName)).Select(s => s.SectionID!.Value).ToHashSet(), periods);
    }
    private static TimetableAlternative Describe(ImportCatalog data, IReadOnlyList<Schedule> rows, int semester, string title, string status, double seconds)
    {
        var original = data.Rows<Schedule>().ToDictionary(s => s.ScheduleID); var periods = data.Rows<TimeSlot>().ToDictionary(p => p.TimeSlotID);
        var faculty = data.Rows<FacultyMember>().ToDictionary(f => f.FacultyMemberID, f => f.FullName); var subjects = data.Rows<Subject>().ToDictionary(s => s.SubjectID, s => s.SubjectName);
        var rooms = data.Rows<Classroom>().ToDictionary(r => r.ClassroomID, r => r.ClassroomNumber);
        var years = data.Rows<StudyYear>().ToDictionary(y => y.StudyYearID, y => y.YearName); var branches = data.Rows<Branch>().ToDictionary(b => b.BranchID, b => b.BranchName);
        var sections = data.Rows<Section>().ToDictionary(s => s.SectionID, s => string.Join(" / ", new[] { years[s.StudyYearID], s.BranchID.HasValue ? branches.GetValueOrDefault(s.BranchID.Value) : null, s.SectionName }.Where(x => !string.IsNullOrWhiteSpace(x))));
        var quality = rows.Select(s => s.FacultyMemberID).Distinct().Select(id => ScheduleQualityAnalyzer.Analyze(data, rows,
            new FacultyScheduleRequest(id, semester, FacultyAvailabilityMode.Preferred, ScheduleDayRules.AllDays, PreferredPeriodIds: []), data.Rows<Schedule>())).ToList();
        var profiles = FacultyPreferenceRules.FromCatalog(data); decimal penalty = 0;
        foreach (var row in rows)
            if (profiles.TryGetValue((row.FacultyMemberID, semester), out var profile) && profile.HasScoredPreferences && FacultyPreferenceRules.Level(profile, row.DayOfWeek, row.TimeSlotID) == FacultyPreferenceLevel.Preferred)
                penalty += FacultyPreferenceRules.Duration(periods[row.TimeSlotID]) / 30m;
        var changed = rows.Where(s => !ImportCatalog.Same(s, original[s.ScheduleID])).ToList();
        return new()
        {
            Title = title, Status = status, Version = data.Version(), Semester = semester, Plan = rows.Select(FacultyCpSatPlanner.Copy).ToArray(), SearchSeconds = seconds,
            Metrics = new(Math.Round(penalty, 2), quality.Sum(q => q.FacultyGapMinutes), quality.FirstOrDefault()?.StudentGapMinutes ?? 0,
                quality.FirstOrDefault()?.PeakStudentDailyMinutes ?? 0, changed.Count, changed.Select(s => s.FacultyMemberID).Distinct().Count(), quality.Sum(q => q.TeachingDays)),
            Lectures = rows.OrderBy(s => ScheduleDayRules.GetDayOrder(s.DayOfWeek)).ThenBy(s => periods[s.TimeSlotID].StartTime).Select(s => new TimetableAlternativeLecture(s.ScheduleID,
                faculty[s.FacultyMemberID], subjects[s.SubjectID], sections[s.SectionID!.Value], s.DayOfWeek,
                periods[s.TimeSlotID].StartTime.ToString(@"hh\:mm") + " – " + periods[s.TimeSlotID].EndTime.ToString(@"hh\:mm"), rooms[s.ClassroomID], s.IsLocked, !ImportCatalog.Same(s, original[s.ScheduleID]), s.LectureType, s.GroupName)).ToArray()
        };
    }
}
