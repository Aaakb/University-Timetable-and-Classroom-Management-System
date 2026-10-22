using System.Data;
using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

public sealed partial class FacultyScheduleEditorService
{
    private readonly Func<AppDbContext> factory;
    public FacultyScheduleEditorService() : this(() => new AppDbContext()) { }
    public FacultyScheduleEditorService(Func<AppDbContext> factory) => this.factory = factory;
    public FacultyPreferenceService Preferences => new(factory);
    public async Task<IReadOnlySet<int>> GetConfiguredFacultyAsync(int semester, CancellationToken token = default)
    {
        await using var db = factory();
        var rows = await db.FacultySchedulingPreferences.AsNoTracking().Where(p => p.SemesterNumber == semester).ToListAsync(token);
        return rows.Where(p => System.Text.Json.JsonSerializer.Deserialize<FacultyScheduleRequest>(p.SettingsJson)?.HasScoredPreferences == true)
            .Select(p => p.FacultyMemberID).ToHashSet();
    }
    private async Task<ImportCatalog> SnapshotAsync(CancellationToken token)
    {
        await using var db = factory();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var result = await ImportCatalog.LoadAsync(db, token);
        await transaction.CommitAsync(token);
        return result;
    }
    public async Task<FacultyEditorLookups> GetLookupsAsync(CancellationToken token = default)
    {
        var data = await SnapshotAsync(token);
        return new(data.Rows<FacultyMember>().OrderBy(f => f.FullName).Select(f => new FacultyEditorOption(f.FacultyMemberID, f.FullName)).ToArray(),
            ScheduleDayRules.AllDays.ToArray(), data.Rows<TimeSlot>().Where(p => !p.IsBreak).OrderBy(p => p.StartTime)
                .Select(p => new FacultyEditorOption(p.TimeSlotID, Period(p))).ToArray());
    }
    public async Task<IReadOnlyList<FacultyLectureView>> GetCurrentAsync(int facultyId, int semester, CancellationToken token = default)
    {
        var data = await SnapshotAsync(token);
        return data.Rows<Schedule>().Where(s => s.FacultyMemberID == facultyId && s.SemesterNumber == semester)
            .OrderBy(s => ScheduleDayRules.GetDayOrder(s.DayOfWeek)).ThenBy(s => s.TimeSlotID).Select(s => View(data, s)).ToArray();
    }
    public async Task<FacultySchedulePreview> PreviewAsync(FacultyScheduleRequest request, bool inspectOnly = false, CancellationToken token = default)
    {
        request = FacultyPreferenceRules.Normalize(request);
        var data = await SnapshotAsync(token);
        return await PreviewAsync(data, request, inspectOnly, token);
    }
    private async Task<FacultySchedulePreview> PreviewAsync(ImportCatalog data, FacultyScheduleRequest request, bool inspectOnly, CancellationToken token)
    {
        FacultyPreferenceRules.ValidateMatrix(request, data.Rows<TimeSlot>());
        if (request.HasScoredPreferences && FacultyPreferenceRules.FromCatalog(data).TryGetValue((request.FacultyId, request.Semester), out var configured) && configured.HasScoredPreferences)
        {
            string Cells(FacultyScheduleRequest p) => p.UseDayPreferences + ":" + string.Join(";", p.DayPreferences.OrderBy(c => c.Day)) + ":" + string.Join(";", p.PeriodPreferences.OrderBy(c => c.Day).ThenBy(c => c.TimeSlotId));
            if (!configured.SelectedDays.ToHashSet().SetEquals(request.SelectedDays) || Cells(configured) != Cells(request))
                throw new InvalidOperationException("تغيّرت التفضيلات أو لم تُحفظ التعديلات؛ حدّثها ثم أنشئ المعاينة من جديد.");
        }
        if (!request.HasScoredPreferences && FacultyPreferenceRules.FromCatalog(data).TryGetValue((request.FacultyId, request.Semester), out var saved) && saved.HasScoredPreferences)
            throw new ArgumentException("لهذا التدريسي مصفوفة تفضيلات محفوظة؛ استخدم شاشة تفضيلات التدريسيين للحفاظ على الأيام والفترات المرفوضة.");
        if (!data.Rows<FacultyMember>().Any(f => f.FacultyMemberID == request.FacultyId)) throw new ArgumentException("التدريسي غير موجود.");
        if (request.PreferredPeriodIds!.Except(data.Rows<TimeSlot>().Where(p => !p.IsBreak).Select(p => p.TimeSlotID)).Any())
            throw new ArgumentException("إحدى الفترات المختارة غير صالحة.");
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var result = await Task.Run(() => FacultyCpSatPlanner.Solve(data, request, inspectOnly, token), token);
        clock.Stop();
        if (result.Plan is not null) await ValidatePlanAsync(data, request, result.Plan, token);
        var oldRows = data.Rows<Schedule>().ToDictionary(s => s.ScheduleID);
        var changes = (result.Plan ?? []).Select(row =>
        {
            var before = View(data, oldRows[row.ScheduleID]); var after = View(data, row);
            return new FacultyLectureChange(row.ScheduleID, before.Subject, before.Section, before.LectureType, before.GroupName,
                before.Day, before.Period, before.Room, after.Day, after.Period, after.Room,
                !ImportCatalog.Same(oldRows[row.ScheduleID], row), row.FacultyMemberID == request.FacultyId && !request.SelectedDays.Contains(row.DayOfWeek))
            {
                Faculty = data.Rows<FacultyMember>().Single(f => f.FacultyMemberID == row.FacultyMemberID).FullName,
                IsOtherFaculty = row.FacultyMemberID != request.FacultyId
            };
        }).Where(c => !c.IsOtherFaculty || c.Changed).ToArray();
        var full = data.Rows<Schedule>().Where(s => !(result.Plan ?? []).Any(p => p.ScheduleID == s.ScheduleID)).Concat(result.Plan ?? []).ToList();
        return new()
        {
            Status = result.Status, Version = data.Version(), Request = request,
            Plan = result.Plan, Changes = changes, AvailableSlots = result.Slots.ToArray(), SlotOptions = result.Options, Suggestions = result.Suggestions.ToArray(),
            BeforeQuality = ScheduleQualityAnalyzer.Analyze(data, data.Rows<Schedule>(), request),
            AfterQuality = result.Plan is null ? null : ScheduleQualityAnalyzer.Analyze(data, full, request, data.Rows<Schedule>()),
            PreferenceMeasurements = FacultyPreferenceMeasurements.Compare(data, data.Rows<Schedule>(), full, request),
            SearchSeconds = clock.Elapsed.TotalSeconds,
            AffectedFacultyCount = (result.Plan ?? []).Where(s => !ImportCatalog.Same(oldRows[s.ScheduleID], s)).Select(s => s.FacultyMemberID).Distinct().Count(),
            Message = inspectOnly ? "الخيارات المعروضة فردية؛ لا تؤكد وجود توزيع كامل أو توافق تبادل العملي حتى توليد المقترح." :
                result.Plan is not null ? $"اقتراح كامل: {changes.Length} محاضرة، {changes.Count(c => c.Changed)} تغيير، {changes.Count(c => c.OutsidePreferredDays)} خارج الأيام المفضلة. لم يُحفظ شيء." :
                result.Status == "Infeasible" ? "لا يوجد توزيع يحقق القيود الحالية ضمن نطاق التعديلات المسموح. بقي الجدول محفوظًا." :
                    "انتهت مهلة البحث دون إيجاد توزيع كامل؛ هذا لا يثبت استحالة الحل. بقي الجدول محفوظًا."
        };
    }
    public async Task<IReadOnlyList<FacultyScheduleAlternative>> GetAlternativesAsync(FacultyScheduleRequest request, CancellationToken token = default)
    {
        request = FacultyPreferenceRules.Normalize(request);
        var data = await SnapshotAsync(token);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var results = new List<FacultyScheduleAlternative>(); var seen = new HashSet<string>();
        var goals = new[] { request.Goal, ScheduleOptimizationGoal.MinimumChanges, ScheduleOptimizationGoal.CompactAttendance,
            ScheduleOptimizationGoal.FewerGaps, ScheduleOptimizationGoal.BalancedStudentLoad }.Distinct().ToArray();
        for (int i = 0; i < goals.Length; i++)
        {
            double remaining = request.TimeLimitSeconds - clock.Elapsed.TotalSeconds;
            if (remaining < .01) break;
            var preview = await PreviewAsync(data, request with { Goal = goals[i], TimeLimitSeconds = Math.Max(.01, remaining / (goals.Length - i)) }, false, token);
            if (!preview.CanSave) continue;
            string key = string.Join(";", preview.Plan!.OrderBy(s => s.ScheduleID).Select(ImportCatalog.ScalarJson));
            if (seen.Add(key)) results.Add(new(GoalTitle(goals[i]), preview));
        }
        return results;
    }

    public async Task<IReadOnlyList<FacultyScheduleAlternative>> FindFeasibleAdjustmentsAsync(FacultyScheduleRequest request, CancellationToken token = default)
    {
        request = FacultyPreferenceRules.Normalize(request);
        var data = await SnapshotAsync(token);
        var trials = new List<(string Title, FacultyScheduleRequest Request)>();
        if (request.Mode == FacultyAvailabilityMode.Strict)
            foreach (string day in ScheduleDayRules.AllDays.Except(request.SelectedDays).Except(request.UnavailableDays!))
                trials.Add(($"إتاحة يوم إضافي: {ArabicDay(day)}", request with { SelectedDays = request.SelectedDays.Append(day).ToArray() }));
        if (request.MaxTeachingDays is < 5) trials.Add(("زيادة الحد الأقصى للحضور يومًا واحدًا", request with { MaxTeachingDays = request.MaxTeachingDays + 1 }));
        if (request.MaxDailyMinutes is > 0 and < 1440) trials.Add(("زيادة الحد اليومي 60 دقيقة", request with { MaxDailyMinutes = Math.Min(1440, request.MaxDailyMinutes.Value + 60) }));
        if (request.MaxConsecutiveMinutes is > 0 and < 1440) trials.Add(("زيادة التدريس المتتالي 60 دقيقة", request with { MaxConsecutiveMinutes = Math.Min(1440, request.MaxConsecutiveMinutes.Value + 60) }));
        var clock = System.Diagnostics.Stopwatch.StartNew(); var results = new List<FacultyScheduleAlternative>();
        for (int i = 0; i < trials.Count; i++)
        {
            double remaining = request.TimeLimitSeconds - clock.Elapsed.TotalSeconds;
            if (remaining < .01) break;
            var trial = trials[i];
            var preview = await PreviewAsync(data, trial.Request with { TimeLimitSeconds = Math.Max(.01, remaining / (trials.Count - i)) }, false, token);
            if (preview.CanSave) results.Add(new(trial.Title + " — حل قابل للتنفيذ", preview));
        }
        return results;
    }
    public static string GoalTitle(ScheduleOptimizationGoal goal) => goal switch
    {
        ScheduleOptimizationGoal.CompactAttendance => "أقل أيام حضور", ScheduleOptimizationGoal.FewerGaps => "أقل فراغات",
        ScheduleOptimizationGoal.BalancedStudentLoad => "عبء يومي متوازن للطلبة", _ => "أقل تغييرات"
    };
    internal static string ArabicDay(string day) => day switch { "Sunday" => "الأحد", "Monday" => "الاثنين", "Tuesday" => "الثلاثاء", "Wednesday" => "الأربعاء", "Thursday" => "الخميس", _ => day };
    public async Task<int> SaveAsync(FacultySchedulePreview preview, bool confirmed, CancellationToken token = default)
    {
        if (!confirmed) throw new InvalidOperationException("يلزم تأكيد حفظ المقترح.");
        if (!preview.CanSave) throw new ArgumentException("لا يوجد اقتراح كامل صالح للحفظ.");
        await using var db = factory();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var data = await ImportCatalog.LoadAsync(db, token);
        if (data.Version() != preview.Version) throw new InvalidOperationException("تغيرت بيانات الجدولة منذ المعاينة؛ أعد تحميل الجدول وتوليد المقترح.");
        await ValidatePlanAsync(data, preview.Request, preview.Plan!, token);
        var old = data.Rows<Schedule>().ToDictionary(s => s.ScheduleID);
        var changed = preview.Plan!.Where(s => !ImportCatalog.Same(s, old[s.ScheduleID])).Select(FacultyCpSatPlanner.Copy).ToList();
        foreach (var row in changed) data.Put(row, old[row.ScheduleID]);
        await FacultyScheduleWriter.ReplacePlacementsAsync(db, changed, token);
        if ((await ImportCatalog.LoadAsync(db, token)).Version() != data.Version())
            throw new InvalidOperationException("لم تتطابق نتيجة الحفظ مع المقترح المعتمد؛ أُلغيت العملية.");
        await transaction.CommitAsync(token);
        return changed.Count;
    }
    private async Task ValidatePlanAsync(ImportCatalog data, FacultyScheduleRequest request, IReadOnlyList<Schedule> plan, CancellationToken token, bool wholeSemester = false, AppDbContext? validationDb = null)
    {
        var originals = data.Rows<Schedule>().Where(s => s.SemesterNumber == request.Semester &&
            (wholeSemester || s.FacultyMemberID == request.FacultyId || request.MaxOtherLectureChanges > 0 && !s.IsLocked)).ToDictionary(s => s.ScheduleID);
        if (plan.Count != originals.Count || plan.Select(s => s.ScheduleID).Distinct().Count() != plan.Count || plan.Any(s => !originals.ContainsKey(s.ScheduleID)))
            throw new ArgumentException("المقترح يضيف أو يحذف أو يكرر محاضرات.");
        var periods = data.Rows<TimeSlot>().Where(p => !p.IsBreak).OrderBy(p => p.StartTime).ToList();
        await using var ownedDb = validationDb is null ? factory() : null;
        var db = validationDb ?? ownedDb!;
        foreach (var row in plan)
        {
            token.ThrowIfCancellationRequested();
            var expected = FacultyCpSatPlanner.Copy(originals[row.ScheduleID]);
            expected.DayOfWeek = row.DayOfWeek; expected.TimeSlotID = row.TimeSlotID; expected.ClassroomID = row.ClassroomID;
            if (!ImportCatalog.Same(expected, row)) throw new ArgumentException("لا يجوز تغيير هوية المحاضرة أو المدرّس أو المادة أو المجموعة.");
            if (originals[row.ScheduleID].IsLocked && !ImportCatalog.Same(originals[row.ScheduleID], row))
                throw new ArgumentException("لا يجوز تحريك محاضرة مثبتة.");
            var oldPeriod = periods.Single(p => p.TimeSlotID == originals[row.ScheduleID].TimeSlotID);
            var newPeriod = periods.Single(p => p.TimeSlotID == row.TimeSlotID);
            if (newPeriod.EndTime - newPeriod.StartTime != oldPeriod.EndTime - oldPeriod.StartTime)
                throw new ArgumentException("لا يجوز تغيير مدة المحاضرة.");
            var copy = FacultyCpSatPlanner.Copy(row);
            await ScheduleService.ValidateAsync(db, copy, true, checkConflicts: false);
            if (!ImportCatalog.Same(copy, row)) throw new ArgumentException("تحتاج بيانات المحاضرة تصحيحًا قبل إعادة التنظيم.");
        }
        var full = data.Rows<Schedule>().Where(s => !originals.ContainsKey(s.ScheduleID)).Concat(plan).ToList();
        ScheduleDayRules.EnsureSectionDayLimit(full.Where(s => s.SemesterNumber == request.Semester),
            data.Rows<Section>(), data.Rows<StudyYear>(), data.Rows<Branch>());
        if (!wholeSemester && plan.Count(s => s.FacultyMemberID != request.FacultyId && !ImportCatalog.Same(originals[s.ScheduleID], s)) > request.MaxOtherLectureChanges)
            throw new ArgumentException("تجاوز المقترح الحد المسموح لتحريك محاضرات الآخرين.");
        var profiles = FacultyPreferenceRules.FromCatalog(data); profiles[(request.FacultyId, request.Semester)] = request;
        FacultyPreferenceRules.Validate(full, periods.ToDictionary(p => p.TimeSlotID), profiles);
        if (ScheduleConflictChecker.HasAnyPairConflict(full)) throw new ArgumentException("يوجد تعارض في الجدول الكامل؛ لن يُحفظ المقترح.");
        var affected = plan.Where(s => s.LectureType == "Practical" && !string.IsNullOrWhiteSpace(s.GroupName)).Select(s => s.SectionID!.Value).ToHashSet();
        FacultyPracticalExchange.Validate(full.Where(s => s.SemesterNumber == request.Semester).ToList(), affected, periods);
    }
    private static string Period(TimeSlot p) => p.StartTime.ToString(@"hh\:mm") + " – " + p.EndTime.ToString(@"hh\:mm");
    private static FacultyLectureView View(ImportCatalog data, Schedule s) => new(s.ScheduleID,
        data.Rows<Subject>().Single(x => x.SubjectID == s.SubjectID).SubjectName,
        string.Join(" / ", new[] { data.Rows<StudyYear>().Single(x => x.StudyYearID == s.StudyYearID).YearName,
            data.Rows<Branch>().FirstOrDefault(x => x.BranchID == s.BranchID)?.BranchName,
            data.Rows<Section>().Single(x => x.SectionID == s.SectionID).SectionName }.Where(x => !string.IsNullOrWhiteSpace(x))),
        s.LectureType, s.GroupName, s.DayOfWeek, Period(data.Rows<TimeSlot>().Single(x => x.TimeSlotID == s.TimeSlotID)),
        data.Rows<Classroom>().Single(x => x.ClassroomID == s.ClassroomID).ClassroomNumber);
}
