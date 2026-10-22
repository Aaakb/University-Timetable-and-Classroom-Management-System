using Data_Access_Layer;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

public sealed partial class ExcelImportService
{
    private static TimetableImportRow CloneRow(TimetableImportRow row) => row with { Schedule = FacultyCpSatPlanner.Copy(row.Schedule) };
    private static bool SamePlacement(Schedule a, Schedule b) => a.DayOfWeek == b.DayOfWeek && a.TimeSlotID == b.TimeSlotID && a.ClassroomID == b.ClassroomID;
    private static bool SameLectureIdentity(Schedule a, Schedule b) => a.SubjectID == b.SubjectID && a.FacultyMemberID == b.FacultyMemberID &&
        a.SectionID == b.SectionID && a.StudyYearID == b.StudyYearID && a.BranchID == b.BranchID && a.SemesterNumber == b.SemesterNumber && a.LectureType == b.LectureType &&
        string.Equals(a.GroupName ?? "", b.GroupName ?? "", StringComparison.OrdinalIgnoreCase);

    private static async Task ValidateMovableRepairRowAsync(AppDbContext db, ImportCatalog catalog, Schedule row)
    {
        var section = catalog.Rows<Section>().Single(s => s.SectionID == row.SectionID);
        var room = ScheduleRoomSelector.GetTargetClassrooms(catalog.Rows<Classroom>(),
            ScheduleRoomSelector.GetRequiredCapacity(section, row.GroupName), row.LectureType).FirstOrDefault()
            ?? throw new ArgumentException("لا توجد قاعة أو مختبر مناسب بسعة كافية لهذه المحاضرة.");
        int originalRoom = row.ClassroomID;
        try { row.ClassroomID = room.ClassroomID; await ScheduleService.ValidateAsync(db, row, row.ScheduleID > 0, checkConflicts: false); }
        finally { row.ClassroomID = originalRoom; }
    }

    private static List<Schedule> RepairFixedRows(ImportCatalog catalog, ExcelImportPreview preview)
    {
        var importedIds = preview.RepairOriginals.Where(r => r.Schedule.ScheduleID > 0).Select(r => r.Schedule.ScheduleID).ToHashSet();
        return catalog.Rows<Schedule>().Where(s => !importedIds.Contains(s.ScheduleID)).ToList();
    }
    private static void InspectRepairConflicts(ImportCatalog catalog, ExcelImportPreview preview)
    {
        var fixedRows = RepairFixedRows(catalog, preview);
        foreach (var row in preview.Lectures)
        {
            foreach (var other in fixedRows.Where(s => ScheduleConflictChecker.HasPairConflict(row.Schedule, s)))
                Add(row, row.Locked, ConflictMessage(row.Schedule, other, $"saved lecture #{other.ScheduleID}"));
            for (int j = 0; j < preview.Lectures.IndexOf(row); j++)
            {
                var other = preview.Lectures[j];
                if (ScheduleConflictChecker.HasPairConflict(row.Schedule, other.Schedule))
                    Add(row, row.Locked && other.Locked, ConflictMessage(row.Schedule, other.Schedule, $"{other.Sheet}, row {other.Row}"));
            }
            var room = catalog.Rows<Classroom>().Single(r => r.ClassroomID == row.Schedule.ClassroomID);
            var section = catalog.Rows<Section>().Single(s => s.SectionID == row.Schedule.SectionID);
            if (!ScheduleRoomSelector.MatchesLectureType(room, row.Schedule.LectureType) || !ScheduleRoomSelector.HasCapacity(room, section, row.Schedule.GroupName))
                Add(row, row.Locked, "القاعة الحالية غير مناسبة لنوع المحاضرة أو سعتها؛ سيبحث الإصلاح عن بديل.");
        }
        void Add(TimetableImportRow row, bool blocked, string message)
        {
            var issue = new ImportIssue(row.Sheet, row.Row, "", blocked ? "تعارض في محاضرات مثبتة؛ ألغِ تثبيت أحد الصفوف أو صحّحه. " + message : message);
            if (blocked) preview.Issues.Add(issue); else preview.RepairableConflicts.Add(issue);
        }
    }

    public async Task<ExcelImportPreview> RepairTimetableAsync(ExcelImportPreview validated,
        double timeLimitSeconds = 20, CancellationToken token = default)
    {
        EnsureUntampered(validated);
        if (validated.Mode != ExcelImportMode.RepairTimetable) throw new ArgumentException("اختر وضع إصلاح الجدول المستورد.");
        if (!double.IsFinite(timeLimitSeconds) || timeLimitSeconds is < .01 or > 120)
            throw new ArgumentException("مهلة الإصلاح يجب أن تكون بين 0.01 و120 ثانية.");
        await using var db = createContext();
        var catalog = await ImportCatalog.LoadAsync(db, token);
        if (catalog.Version() != validated.DatabaseVersion) throw new InvalidOperationException("Database data changed. Validate the workbook again.");
        var result = new ExcelImportPreview { Mode = validated.Mode, Semester = validated.Semester, DatabaseVersion = validated.DatabaseVersion };
        result.ReadingNotes.AddRange(validated.ReadingNotes);
        result.RepairOriginals.AddRange(validated.RepairOriginals.Select(CloneRow));
        var planned = await Task.Run(() => ImportedTimetableRepairPlanner.Solve(catalog, result.RepairOriginals,
            RepairFixedRows(catalog, result), timeLimitSeconds, token), token);
        token.ThrowIfCancellationRequested();
        result.RepairSearchStatus = planned.Status;
        if (planned.Plan is null)
        {
            result.Lectures.AddRange(result.RepairOriginals.Select(CloneRow));
            result.Issues.Add(new("Timetable", 0, "", planned.Status == "Infeasible"
                ? "لا يوجد توزيع يحقق القيود الحالية مع المحاضرات المثبتة. راجع الأقفال والقاعات والفترات. لم يُحفظ شيء."
                : "انتهت حدود البحث دون إيجاد جدول كامل؛ هذا لا يثبت استحالة الحل. زِد المهلة أو راجع القيود. لم يُحفظ شيء."));
        }
        else
        {
            for (int i = 0; i < result.RepairOriginals.Count; i++)
            {
                var before = result.RepairOriginals[i];
                var after = Describe(before with { Schedule = planned.Plan[i] }, catalog);
                result.Lectures.Add(after);
                bool changed = !SamePlacement(before.Schedule, after.Schedule);
                result.PlacementChanges.Add(new(before.Sheet, before.Row, after.SubjectName, after.FacultyName, after.SectionName,
                    after.Schedule.LectureType, after.Schedule.GroupName, after.Locked,
                    before.Schedule.DayOfWeek, before.Period, before.ClassroomName,
                    after.Schedule.DayOfWeek, after.Period, after.ClassroomName, changed));
                var saved = catalog.Rows<Schedule>().SingleOrDefault(s => s.ScheduleID == after.Schedule.ScheduleID);
                result.Changes.Add(new(after.Sheet, after.Row, saved is null ? "Add" : !ImportCatalog.Same(saved, after.Schedule) ? "Update" : "Unchanged",
                    changed ? $"{after.SubjectName}: {ImportLabels.Display(before.Schedule.DayOfWeek)} {before.Period} / {before.ClassroomName} ← {ImportLabels.Display(after.Schedule.DayOfWeek)} {after.Period} / {after.ClassroomName}" : ImportCatalog.LectureKey(after.Schedule)));
            }
            await ValidateRepairPlanAsync(db, catalog, result, token);
            result.GenerationCompleted = true;
        }
        result.ContentVersion = ContentVersion(result);
        return result;
    }

    private static async Task ValidateRepairPlanAsync(AppDbContext db, ImportCatalog catalog, ExcelImportPreview preview, CancellationToken token)
    {
        if (preview.Lectures.Count == 0 || preview.Lectures.Count != preview.RepairOriginals.Count)
            throw new ArgumentException("الإصلاح يجب أن يحافظ على جميع محاضرات الملف دون إضافة أو حذف.");
        for (int i = 0; i < preview.Lectures.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            var row = preview.Lectures[i]; var original = preview.RepairOriginals[i];
            if (row.Sheet != original.Sheet || row.Row != original.Row || row.Schedule.ScheduleID != original.Schedule.ScheduleID ||
                !SameLectureIdentity(row.Schedule, original.Schedule) || row.Locked != original.Locked || row.Schedule.IsLocked != original.Schedule.IsLocked ||
                original.Locked && !SamePlacement(row.Schedule, original.Schedule))
                throw new ArgumentException("الإصلاح غيّر هوية محاضرة أو موضع محاضرة مثبتة.");
            if (row.Schedule.ScheduleID > 0)
            {
                var old = catalog.Rows<Schedule>().Single(s => s.ScheduleID == row.Schedule.ScheduleID);
                if (!SameLectureIdentity(old, row.Schedule) || old.IsLocked && !SamePlacement(old, row.Schedule))
                    throw new ArgumentException("لا يمكن تغيير هوية محاضرة محفوظة أو تحريك محاضرة محفوظة ومثبتة.");
            }
            var copy = FacultyCpSatPlanner.Copy(row.Schedule);
            await ScheduleService.ValidateAsync(db, copy, copy.ScheduleID > 0, checkConflicts: false);
            if (!ImportCatalog.Same(copy, row.Schedule)) throw new ArgumentException("تحتاج بيانات إحدى المحاضرات إلى إعادة التحقق.");
            var periods = catalog.Rows<TimeSlot>();
            var before = periods.Single(p => p.TimeSlotID == original.Schedule.TimeSlotID);
            var after = periods.Single(p => p.TimeSlotID == row.Schedule.TimeSlotID);
            if (before.EndTime - before.StartTime != after.EndTime - after.StartTime) throw new ArgumentException("لا يمكن أن يغيّر الإصلاح مدة المحاضرة.");
        }
        var full = RepairFixedRows(catalog, preview).Concat(preview.Lectures.Select(r => r.Schedule)).ToList();
        FacultyPreferenceRules.Validate(full, catalog.Rows<TimeSlot>().ToDictionary(p => p.TimeSlotID), FacultyPreferenceRules.FromCatalog(catalog));
        if (ScheduleConflictChecker.HasAnyPairConflict(full.Where(s => s.SemesterNumber == preview.Semester).ToList()))
            throw new ArgumentException("بقي تعارض في الجدول النهائي؛ لن يُحفظ الإصلاح.");
        var affected = preview.Lectures.Where(r => r.Schedule.LectureType == "Practical" && !string.IsNullOrWhiteSpace(r.Schedule.GroupName))
            .Select(r => r.Schedule.SectionID!.Value).ToHashSet();
        FacultyPracticalExchange.Validate(full.Where(s => s.SemesterNumber == preview.Semester).ToList(), affected,
            catalog.Rows<TimeSlot>().Where(p => !p.IsBreak).OrderBy(p => p.StartTime).ToList());
        var requirements = new ExcelImportPreview(); requirements.Lectures.AddRange(preview.Lectures);
        CheckWeeklyRequirements(catalog, requirements);
        if (!requirements.IsValid) throw new ArgumentException("الإصلاح يتجاوز المتطلبات الأسبوعية للمحاضرات.");
    }

    private static async Task<ImportSaveResult> SaveRepairedAsync(AppDbContext db, ImportCatalog catalog, ExcelImportPreview preview, CancellationToken token)
    {
        await ValidateRepairPlanAsync(db, catalog, preview, token);
        var existing = catalog.Rows<Schedule>().ToDictionary(s => s.ScheduleID);
        var changed = preview.Lectures.Where(r => r.Schedule.ScheduleID > 0 && !ImportCatalog.Same(existing[r.Schedule.ScheduleID], r.Schedule))
            .Select(r => FacultyCpSatPlanner.Copy(r.Schedule)).ToList();
        await FacultyScheduleWriter.ReplacePlacementsAsync(db, changed, token);
        int added = 0;
        foreach (var row in preview.Lectures.Where(r => r.Schedule.ScheduleID == 0))
        {
            token.ThrowIfCancellationRequested();
            var copy = FacultyCpSatPlanner.Copy(row.Schedule);
            await ScheduleService.ValidateAsync(db, copy, false);
            db.Schedules.Add(copy); await db.SaveChangesAsync(token); added++;
        }
        return new(added, changed.Count, preview.Lectures.Count - added - changed.Count);
    }
}
