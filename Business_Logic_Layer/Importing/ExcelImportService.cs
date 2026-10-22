using System.Data;
using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

public sealed partial class ExcelImportService
{
    private readonly Func<AppDbContext> createContext;
    public ExcelImportService() : this(() => new AppDbContext()) { }
    public ExcelImportService(Func<AppDbContext> createContext) => this.createContext = createContext;

    public async Task<ExcelImportPreview> ValidateAsync(ExcelImportMode mode,
        IReadOnlyList<ExcelSheetData> sheets, bool updateExisting = false, int semester = 1,
        CancellationToken token = default)
    {
        if (semester is not (1 or 2)) throw new ArgumentException("Select semester 1 or 2.");
        if (!Enum.IsDefined(mode)) throw new ArgumentException("نوع الاستيراد غير صالح.");
        var preview = new ExcelImportPreview { Mode = mode, UpdateExisting = updateExisting, Semester = semester };
        await using var db = createContext();
        var catalog = await ImportCatalog.LoadAsync(db, token);
        preview.DatabaseVersion = catalog.Version();
        if (sheets.Count == 0 || sheets.Sum(s => s.Table.Rows.Count) == 0)
            preview.Issues.Add(new("", 0, "", "Select at least one worksheet containing data."));
        if (mode == ExcelImportMode.AcademicData) AcademicExcelImport.Prepare(sheets, catalog, preview, token);
        else
        {
            var sessions = sheets.SelectMany(s => s.Table.Rows.Cast<DataRow>().Select(r => s.Table.Columns.Contains("StudySession")
                ? Convert.ToString(r["StudySession"]) ?? "" : s.StudySession)).Where(s => s.Length > 0).Distinct().ToList();
            if (sessions.Count > 1)
            {
                preview.Issues.Add(new("Timetable", 0, "StudySession", "الملف يجمع دراسة صباحية ومسائية. اختر نطاق الدراسة من الواجهة؛ نموذج الشعب الحالي لا يفصل الدوامين، ولا يجوز دمجهما تلقائياً."));
                preview.ContentVersion = ContentVersion(preview); return preview;
            }
            await ParseTimetableAsync(db, sheets, catalog, preview, token);
            try { CheckWeeklyRequirements(catalog, preview); }
            catch (ArgumentException ex) { preview.Issues.Add(new("Timetable", 0, "", ex.Message)); }
            if (mode == ExcelImportMode.RepairTimetable)
            {
                preview.RepairOriginals.AddRange(preview.Lectures.Select(CloneRow));
                InspectRepairConflicts(catalog, preview);
            }
            if (mode == ExcelImportMode.Hybrid && preview.IsValid)
            {
                try
                {
                    var requests = CreateRequests(catalog, semester);
                    var fixedLectures = FixedLectures(catalog, preview);
                    preview.RemainingLectureCount = Consume(requests, fixedLectures).Count;
                }
                catch (ArgumentException ex) { preview.Issues.Add(new("Timetable", 0, "", ex.Message)); }
            }
        }
        preview.ContentVersion = ContentVersion(preview);
        return preview;
    }

    internal static bool CheckColumns(ExcelSheetData sheet, ImportSheetSchema schema, ExcelImportPreview preview)
    {
        var headers = sheet.Table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = schema.Required.Where(c => !headers.Contains(c)).ToList();
        foreach (string name in missing) preview.Issues.Add(new(sheet.SourceName, 1, name, "Required column is missing. Map the source column or use the template."));
        return missing.Count == 0;
    }

    public async Task<ExcelImportPreview> ValidateSkippingInvalidAcademicRowsAsync(IReadOnlyList<ExcelSheetData> sheets,
        bool updateExisting = false, CancellationToken token = default)
    {
        var remaining = sheets;
        var skipped = new List<ImportIssue>();
        var excluded = new HashSet<(string Sheet, int Row)>();
        while (true)
        {
            token.ThrowIfCancellationRequested();
            // Always rebuild the catalog and dependencies; a failed row must not leave staged changes behind.
            var result = await ValidateAsync(ExcelImportMode.AcademicData, remaining, updateExisting, token: token);
            var rowErrors = result.Issues.Where(i => i.Row > 1 && remaining.Any(s => s.SourceName == i.Sheet && s.SourceRows.Contains(i.Row))).ToList();
            if (rowErrors.Count == 0)
            {
                result.SkippedRows.AddRange(skipped);
                result.ContentVersion = ContentVersion(result);
                return result;
            }
            skipped.AddRange(rowErrors);
            foreach (var issue in rowErrors) excluded.Add((issue.Sheet, issue.Row));
            remaining = sheets.Select(source =>
            {
                var table = source.Table.Clone(); var rows = new List<int>();
                for (int i = 0; i < source.Table.Rows.Count; i++)
                    if (!excluded.Contains((source.SourceName, source.SourceRows[i])))
                    { table.ImportRow(source.Table.Rows[i]); rows.Add(source.SourceRows[i]); }
                return new ExcelSheetData(source.Name, table, rows) { SourceName = source.SourceName,
                    SuggestedSchemaName = source.SuggestedSchemaName, IsWeeklyTimetable = source.IsWeeklyTimetable, StudySession = source.StudySession };
            }).ToList();
        }
    }

    private static async Task ParseTimetableAsync(AppDbContext db, IReadOnlyList<ExcelSheetData> sheets,
        ImportCatalog catalog, ExcelImportPreview preview, CancellationToken token)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var seenIds = new HashSet<int>();
        var existing = catalog.Rows<Schedule>();
        foreach (var sheet in sheets)
        {
            if (!CheckColumns(sheet, ExcelImportSchema.Timetable, preview)) continue;
            for (int i = 0; i < sheet.Table.Rows.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                int r = sheet.SourceRows[i];
                try
                {
                    var v = new ImportValues(sheet.Table.Rows[i]);
                    string readingNote = v.Text("ImportNote", false);
                    if (readingNote.Length > 0) preview.ReadingNotes.Add(new(sheet.SourceName, r, "", readingNote));
                    int semester = v.Int("SemesterNumber");
                    if (semester != preview.Semester) throw new ArgumentException("SemesterNumber differs from the selected import semester.");
                    var subjects = catalog.Rows<Subject>().Where(x => x.SemesterNumber == semester && !x.IsDeleted).ToList();
                    string yearText = v.Text("StudyYear", false), branchText = v.Text("Branch", false);
                    int? year = yearText.Length == 0 ? null : Resolve(yearText, catalog.Rows<StudyYear>(), x => x.StudyYearID, x => x.YearName, "StudyYear").StudyYearID;
                    int? branch = branchText.Length == 0 ? null : Resolve(branchText, catalog.Rows<Branch>(), x => x.BranchID, x => x.BranchName, "Branch").BranchID;
                    if (year.HasValue) subjects = subjects.Where(s => s.StudyYearID == year).ToList();
                    if (branch.HasValue) subjects = subjects.Where(s => s.BranchID == branch).ToList();
                    var subject = Resolve(v.Text("Subject"), subjects, x => x.SubjectID, x => x.SubjectName, "Subject");
                    var sectionOptions = catalog.Rows<Section>().Where(x => x.StudyYearID == subject.StudyYearID &&
                        AcademicStructureRules.SectionMatchesSubject(subject.StudyYearID, subject.BranchID, x.BranchID, x.SectionName)).ToList();
                    string sectionText = v.Text("Section", false);
                    var section = sectionText.Length == 0 && sheet.IsWeeklyTimetable && sectionOptions.Count == 1 ? sectionOptions[0] :
                        Resolve(v.Text("Section"), sectionOptions, x => x.SectionID, x => x.SectionName, "Section");
                    var faculty = Resolve(v.Text("Faculty"), catalog.Rows<FacultyMember>(), x => x.FacultyMemberID, x => x.FullName, "Faculty");
                    var room = Resolve(v.Text("Classroom"), catalog.Rows<Classroom>(), x => x.ClassroomID, x => x.ClassroomNumber, "Classroom");
                    var slot = Resolve(v.Text("TimeSlot"), catalog.Rows<TimeSlot>().Where(x => !x.IsBreak).ToList(),
                        x => x.TimeSlotID, x => x.StartTime.ToString(@"hh\:mm"), "TimeSlot");
                    if (v.Text("EndTime", false).Length > 0 && v.Time("EndTime") != slot.EndTime)
                        throw new ArgumentException($"وقت انتهاء المحاضرة في الملف ({v.Text("EndTime")}) لا يطابق نهاية الفترة المعرّفة ({slot.EndTime:hh\\:mm}). اضبط الفترات أو راجع الوقت؛ لن تُغيّر مدة المحاضرة تلقائياً.");
                    var schedule = new Schedule
                    {
                        SubjectID = subject.SubjectID, FacultyMemberID = faculty.FacultyMemberID,
                        ClassroomID = room.ClassroomID, TimeSlotID = slot.TimeSlotID,
                        SectionID = section.SectionID, SemesterNumber = semester,
                        DayOfWeek = TranslateDay(v.Text("Day")), LectureType = TranslateType(v.Text("LectureType")),
                        GroupName = v.Text("GroupName", false)
                    };
                    if (schedule.LectureType == "Theory" && !string.IsNullOrWhiteSpace(schedule.GroupName))
                        throw new ArgumentException("Theory lectures cover a whole section; GroupName must be empty.");
                    Schedule? referenced = null;
                    bool repairing = preview.Mode == ExcelImportMode.RepairTimetable;
                    if (repairing && v.OptionalInt("ScheduleID") is { } id)
                    {
                        referenced = existing.SingleOrDefault(s => s.ScheduleID == id)
                            ?? throw new ArgumentException("معرف المحاضرة المحفوظة غير موجود.");
                        schedule.ScheduleID = referenced.ScheduleID;
                    }
                    bool locked = v.Bool("Locked", false) || referenced?.IsLocked == true;
                    // Movable repair rows may have a wrong room; all identity/teaching rules
                    // are still validated with a compatible room before solving placements.
                    if (repairing && !locked) await ValidateMovableRepairRowAsync(db, catalog, schedule);
                    else await ScheduleService.ValidateAsync(db, schedule, false, checkConflicts: false);
                    string key = ImportCatalog.LectureKey(schedule);
                    if (!seen.Add(key)) throw new ArgumentException("Duplicate lecture in this workbook.");
                    var identical = referenced ?? existing.SingleOrDefault(x => ImportCatalog.LectureKey(x) == key);
                    if (referenced is not null && !SameLectureIdentity(schedule, referenced))
                        throw new ArgumentException("معرف المحاضرة لا يطابق المادة أو التدريسي أو الشعبة أو المجموعة في الصف.");
                    if (identical?.IsLocked == true && key != ImportCatalog.LectureKey(identical))
                        throw new ArgumentException("لا يمكن تغيير موضع محاضرة محفوظة ومثبتة.");
                    if (identical is not null) schedule.ScheduleID = identical.ScheduleID;
                    if (schedule.ScheduleID > 0 && !seenIds.Add(schedule.ScheduleID)) throw new ArgumentException("معرف المحاضرة مكرر داخل الملف.");
                    foreach (var other in existing.Where(x => !repairing && x.ScheduleID != schedule.ScheduleID))
                        if (ScheduleConflictChecker.HasPairConflict(schedule, other))
                            throw new ArgumentException(ConflictMessage(schedule, other, $"saved lecture #{other.ScheduleID}"));
                    foreach (var other in preview.Lectures.Where(_ => !repairing))
                        if (ScheduleConflictChecker.HasPairConflict(schedule, other.Schedule))
                            throw new ArgumentException(ConflictMessage(schedule, other.Schedule, $"{other.Sheet}, row {other.Row}"));
                    locked |= identical?.IsLocked == true;
                    schedule.IsLocked = locked;
                    preview.Lectures.Add(Describe(new(sheet.Name, r, schedule, locked), catalog));
                    preview.Changes.Add(new(sheet.Name, r, identical is null ? "Add" : locked && !identical.IsLocked ? "Lock existing" : "Unchanged", key));
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
                { preview.Issues.Add(new(sheet.Name, r, "", ex.Message)); }
            }
        }
    }

    private static T Resolve<T>(string text, IReadOnlyList<T> source, Func<T, int> id, Func<T, string> name, string field)
    {
        List<T> matches;
        string reference = ImportLabels.NormalizeReference(text);
        if (reference.StartsWith('#'))
        {
            if (!int.TryParse(reference.AsSpan(1), out int key)) throw new ArgumentException($"{field}: invalid #ID reference.");
            matches = source.Where(x => id(x) == key).ToList();
        }
        else
        {
            matches = source.Where(x => Reference(field, name(x)).Equals(Reference(field, reference), StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count == 0 && field == "Faculty")
                matches = source.Where(x => FacultyName(name(x)).Equals(FacultyName(reference), StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count == 0 && field == "Classroom")
            {
                var room = System.Text.RegularExpressions.Regex.Match(reference, @"^(قاعة|مختبر|Room|Lab)\s*(\d+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (room.Success)
                {
                    bool lab = room.Groups[1].Value.Equals("Lab", StringComparison.OrdinalIgnoreCase) || room.Groups[1].Value == "مختبر";
                    matches = source.Where(x => x is Classroom c && ClassroomRules.IsLab(c) == lab &&
                        System.Text.RegularExpressions.Regex.Match(ImportLabels.NormalizeReference(c.ClassroomNumber), @"\d+$").Value == room.Groups[2].Value).ToList();
                }
            }
        }
        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new ArgumentException($"{field}: '{text}' was not found in this scope."),
            _ => throw new ArgumentException($"{field}: '{text}' is ambiguous. Use #ID or specify StudyYear/Branch.")
        };
        static string Reference(string field, string value)
        {
            string normalized = ImportLabels.NormalizeReference(value);
            if (field != "Branch") return normalized;
            return ImportLabels.Normalize(normalized) switch
            {
                "علومالحاسوب" or "computerscience" or "cs" => "CS",
                "الامنالسيبراني" or "الامنالسبراني" or "cybersecurity" or "cy" => "CY",
                "تكنولوجياالمعلومات" or "تقنيةالمعلومات" or "informationtechnology" or "it" => "IT",
                _ => normalized
            };
        }
        static string FacultyName(string value) => System.Text.RegularExpressions.Regex.Replace(ImportLabels.NormalizeReference(value),
            @"^(?:(?:أ|ا|م|د)\s*\.\s*){1,3}(?:(?:م|د)(?=\s|$)\s*)?", "").Trim();
    }
    private static string TranslateDay(string day) => ImportLabels.NormalizeReference(day) switch
    {
        "الأحد" or "الاحد" => "Sunday", "الاثنين" or "الإثنين" => "Monday", "الثلاثاء" => "Tuesday",
        "الأربعاء" or "الاربعاء" => "Wednesday", "الخميس" => "Thursday", _ => ScheduleDayRules.NormalizeDayName(ImportLabels.NormalizeReference(day))
    };
    private static string TranslateType(string type) => ImportLabels.NormalizeReference(type).ToLowerInvariant() switch
    {
        "theory" or "theoretical" or "نظري" => "Theory", "practical" or "عملي" => "Practical",
        _ => throw new ArgumentException("LectureType: use Theory or Practical (نظري / عملي).")
    };
    private static string ConflictMessage(Schedule s, Schedule other, string source)
    {
        string resource = s.ClassroomID == other.ClassroomID ? "Classroom" : s.FacultyMemberID == other.FacultyMemberID ? "Faculty" : "Section/group";
        return $"{resource} conflict with {source} on {s.DayOfWeek}, period #{s.TimeSlotID}.";
    }

    private static void CheckWeeklyRequirements(ImportCatalog catalog, ExcelImportPreview preview)
    {
        foreach (var scope in preview.Lectures.GroupBy(l => new { l.Schedule.SubjectID, l.Schedule.SectionID }))
        {
            var subject = catalog.Rows<Subject>().Single(s => s.SubjectID == scope.Key.SubjectID);
            var section = catalog.Rows<Section>().Single(s => s.SectionID == scope.Key.SectionID);
            var required = ScheduleGenerator.CreateScheduleRequests(subject, section, catalog.Rows<FacultyMemberSubject>()).ToList();
            var saved = catalog.Rows<Schedule>().Where(s => s.SubjectID == subject.SubjectID && s.SectionID == section.SectionID).ToList();
            var remaining = Consume(required, saved);
            foreach (var row in scope.Where(l => l.Schedule.ScheduleID == 0))
            {
                var lecture = row.Schedule;
                bool Matches(ScheduleGenerationRequest r) => r.LectureType == lecture.LectureType &&
                    (string.Equals(r.GroupName ?? "", lecture.GroupName ?? "", StringComparison.OrdinalIgnoreCase) ||
                     lecture.LectureType == "Practical" && string.IsNullOrWhiteSpace(lecture.GroupName));
                var expectedGroups = required.Where(Matches).Select(r => r.GroupName ?? "").Distinct().ToList();
                var availableGroups = remaining.Where(Matches).Select(r => r.GroupName ?? "").ToHashSet();
                if (expectedGroups.Count == 0 || expectedGroups.Any(g => !availableGroups.Contains(g)))
                    preview.Issues.Add(new(row.Sheet, row.Row, "Subject", "This lecture exceeds the existing engine's weekly requirement for this section/group, including saved lectures."));
                else remaining = Consume(remaining, [lecture]);
            }
        }
    }

    public async Task<ExcelImportPreview> GenerateRemainingAsync(ExcelImportPreview validated, CancellationToken token = default)
    {
        EnsureUntampered(validated);
        if (validated.Mode != ExcelImportMode.Hybrid) throw new ArgumentException("Select Hybrid mode.");
        await using var db = createContext();
        var catalog = await ImportCatalog.LoadAsync(db, token);
        if (catalog.Version() != validated.DatabaseVersion) throw new InvalidOperationException("Database data changed. Validate the workbook again.");
        var result = new ExcelImportPreview
        {
            Mode = validated.Mode, Semester = validated.Semester, DatabaseVersion = validated.DatabaseVersion
        };
        result.ReadingNotes.AddRange(validated.ReadingNotes);
        var fixedLectures = FixedLectures(catalog, validated);
        foreach (var fixedLecture in fixedLectures.Where(s => s.SemesterNumber == validated.Semester))
        {
            string signature = ImportCatalog.ScalarJson(fixedLecture);
            await ScheduleService.ValidateAsync(db, fixedLecture, fixedLecture.ScheduleID > 0, checkConflicts: false);
            if (signature != ImportCatalog.ScalarJson(fixedLecture))
                throw new ArgumentException("A saved lecture requires correction before hybrid generation. Validate the existing timetable first.");
        }
        var requests = Consume(CreateRequests(catalog, validated.Semester), fixedLectures);
        result.RemainingLectureCount = requests.Count;
        var plan = await Task.Run(() => ScheduleGenerator.BuildPlan(requests,
            new ScheduleBuildContext(catalog.Rows<Classroom>(), catalog.Rows<TimeSlot>().Where(t => !t.IsBreak).ToList())
            { FixedSchedules = fixedLectures, RequirePracticalExchange = false, CancellationToken = token,
                Preferences = FacultyPreferenceRules.FromCatalog(catalog) }), token);
        token.ThrowIfCancellationRequested();
        var fixedSet = fixedLectures.ToHashSet();
        result.Lectures.AddRange(catalog.Rows<Schedule>().Where(s => s.SemesterNumber == validated.Semester)
            .Select(s => validated.Lectures.FirstOrDefault(l => l.Schedule.ScheduleID == s.ScheduleID)
                ?? Describe(new("Saved", 0, s, s.IsLocked), catalog)));
        result.Lectures.AddRange(validated.Lectures.Where(l => l.Locked && l.Schedule.ScheduleID == 0));
        result.Lectures.AddRange(plan.Schedules.Where(s => !fixedSet.Contains(s)).Select(s => Describe(new("Generated", 0, s, false), catalog)));
        var outstanding = Consume(requests, plan.Schedules.Where(s => !fixedSet.Contains(s)).ToList());
        result.RemainingLectureCount = outstanding.Count;
        foreach (var request in outstanding)
        {
            string detail = $"{request.Subject.SubjectName} (#{request.Subject.SubjectID}), section {request.Section.SectionName} (#{request.Section.SectionID}), {request.LectureType}, group {request.GroupName ?? "whole section"}";
            bool hasRoom = ScheduleRoomSelector.GetTargetClassrooms(catalog.Rows<Classroom>(),
                ScheduleRoomSelector.GetRequiredCapacity(request.Section, request.GroupName), request.LectureType).Any();
            result.UnscheduledLectures.Add(detail + (hasRoom
                ? ": no placement found within the search limits; review locked bookings, teachers and periods."
                : ": no suitable classroom/laboratory with sufficient capacity."));
        }
        if (outstanding.Count > 0)
            result.Issues.Add(new("Generated", 0, "", "The search did not complete the timetable. Review the unresolved lectures; no data has been saved."));
        result.GenerationCompleted = result.IsValid;
        foreach (var row in result.Lectures)
            result.Changes.Add(new(row.Sheet, row.Row, row.Schedule.ScheduleID == 0 ? "Add"
                : row.Locked && !catalog.Rows<Schedule>().Single(s => s.ScheduleID == row.Schedule.ScheduleID).IsLocked ? "Lock existing" : "Unchanged", ImportCatalog.LectureKey(row.Schedule)));
        result.ContentVersion = ContentVersion(result);
        return result;
    }

    private static List<Schedule> FixedLectures(ImportCatalog catalog, ExcelImportPreview preview)
        => catalog.Rows<Schedule>().Concat(preview.Lectures.Where(l => l.Locked && l.Schedule.ScheduleID == 0).Select(l => l.Schedule)).ToList();

    private static TimetableImportRow Describe(TimetableImportRow row, ImportCatalog catalog)
    {
        var s = row.Schedule;
        var time = catalog.Rows<TimeSlot>().Single(t => t.TimeSlotID == s.TimeSlotID);
        return row with
        {
            SubjectName = catalog.Rows<Subject>().Single(x => x.SubjectID == s.SubjectID).SubjectName,
            FacultyName = catalog.Rows<FacultyMember>().Single(x => x.FacultyMemberID == s.FacultyMemberID).FullName,
            ClassroomName = catalog.Rows<Classroom>().Single(x => x.ClassroomID == s.ClassroomID).ClassroomNumber,
            SectionName = catalog.Rows<Section>().Single(x => x.SectionID == s.SectionID).SectionName,
            Period = $"{time.StartTime:hh\\:mm}–{time.EndTime:hh\\:mm}"
        };
    }

    private static List<ScheduleGenerationRequest> CreateRequests(ImportCatalog catalog, int semester)
    {
        var all = new List<ScheduleGenerationRequest>();
        var assignments = catalog.Rows<FacultyMemberSubject>();
        foreach (var subject in catalog.Rows<Subject>().Where(s => s.SemesterNumber == semester && s.HasWeeklyLectures))
        {
            var faculty = assignments.Where(a => a.SubjectID == subject.SubjectID).Select(a => a.FacultyMemberID).Distinct().Order().ToList();
            if (faculty.Count == 0) throw new ArgumentException($"Assign faculty to {subject.SubjectName} (#{subject.SubjectID}) before hybrid generation.");
            var sections = catalog.Rows<Section>().Where(s => s.StudyYearID == subject.StudyYearID &&
                AcademicStructureRules.SectionMatchesSubject(subject.StudyYearID, subject.BranchID, s.BranchID, s.SectionName)).ToList();
            if (sections.Count == 0) throw new ArgumentException($"No matching sections for {subject.SubjectName} (#{subject.SubjectID}).");
            foreach (var section in sections)
            {
                var sectionFaculty = FacultyAssignmentRules.FacultyIds(assignments, subject.SubjectID, section.SectionID);
                if (sectionFaculty.Count == 0) throw new ArgumentException($"لا يوجد تكليف لمادة «{subject.SubjectName}» للشعبة {section.SectionName}.");
                all.AddRange(ScheduleGenerator.CreateScheduleRequests(subject, section, assignments));
            }
        }
        if (all.Count == 0) throw new ArgumentException("No lecture requirements exist for the selected semester.");
        return all;
    }
    private static List<ScheduleGenerationRequest> Consume(IReadOnlyList<ScheduleGenerationRequest> requests, IReadOnlyList<Schedule> lectures)
        => ScheduleRequirementMatcher.Remaining(requests, lectures);

    public async Task<ImportSaveResult> SaveAsync(ExcelImportPreview preview, bool confirmed,
        IProgress<string>? progress = null, CancellationToken token = default)
    {
        if (!confirmed) throw new ArgumentException("Explicit import confirmation is required.");
        EnsureUntampered(preview);
        if (preview.Mode is ExcelImportMode.Hybrid or ExcelImportMode.RepairTimetable && !preview.GenerationCompleted)
            throw new ArgumentException("Generate and review the completed timetable before saving.");
        return await ImportTransaction.ExecuteAsync(createContext, async (db, ct) =>
        {
            var current = await ImportCatalog.LoadAsync(db, ct);
            if (current.Version() != preview.DatabaseVersion)
                throw new InvalidOperationException("Database data changed after preview. Validate again before saving.");
            if (preview.Mode == ExcelImportMode.RepairTimetable)
                return await SaveRepairedAsync(db, current, preview, ct);
            int added = 0, updated = 0, unchanged = 0;
            if (preview.Mode == ExcelImportMode.AcademicData)
            {
                foreach (var row in preview.AcademicRows)
                {
                    ct.ThrowIfCancellationRequested();
                    progress?.Report($"{row.Sheet}, row {row.Row}: {row.Action}");
                    if (row.Action == "Unchanged") { unchanged++; continue; }
                    try { await AcademicExcelImport.SaveAsync(db, row); }
                    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateException)
                    { throw new InvalidOperationException($"{row.Sheet}, row {row.Row}: {ex.Message}. The whole import was rolled back.", ex); }
                    if (row.Action == "Add") added++; else updated++;
                }
            }
            else
            {
                foreach (var row in preview.Lectures)
                {
                    ct.ThrowIfCancellationRequested();
                    if (row.Schedule.ScheduleID > 0)
                    {
                        var saved = await db.Schedules.FindAsync([row.Schedule.ScheduleID], ct)
                            ?? throw new InvalidOperationException("A saved lecture no longer exists. Validate again.");
                        if (row.Locked && !saved.IsLocked)
                        {
                            saved.IsLocked = true; await db.SaveChangesAsync(ct); updated++;
                        }
                        else unchanged++;
                        continue;
                    }
                    progress?.Report($"{row.Sheet}, row {row.Row}: adding lecture");
                    // Saving each row inside this transaction makes preceding imported rows
                    // visible to the existing database-backed conflict checker.
                    var copy = Copy(row.Schedule);
                    await ScheduleService.ValidateAsync(db, copy, false);
                    db.Schedules.Add(copy);
                    await db.SaveChangesAsync(ct);
                    added++;
                }
                FacultyPreferenceRules.Validate(await db.Schedules.AsNoTracking().ToListAsync(ct), current.Rows<TimeSlot>().ToDictionary(p => p.TimeSlotID),
                    FacultyPreferenceRules.FromCatalog(current));
            }
            return new ImportSaveResult(added, updated, unchanged);
        }, token);
    }
    private static Schedule Copy(Schedule s) => new()
    {
        IsLocked = s.IsLocked, SubjectID = s.SubjectID, FacultyMemberID = s.FacultyMemberID, ClassroomID = s.ClassroomID,
        TimeSlotID = s.TimeSlotID, SemesterNumber = s.SemesterNumber, LectureType = s.LectureType,
        GroupName = s.GroupName, DayOfWeek = s.DayOfWeek, StudyYearID = s.StudyYearID, BranchID = s.BranchID, SectionID = s.SectionID
    };
    private static string ContentVersion(ExcelImportPreview p) => ImportCatalog.Hash(
        $"{p.Mode}/{p.Semester}/{p.UpdateExisting}/{p.GenerationCompleted}/{p.RemainingLectureCount}\n" +
        string.Join("\n", p.Lectures.Select(l => $"{l.Sheet}/{l.Row}/{l.Locked}/" + ImportCatalog.ScalarJson(l.Schedule))) +
        string.Join("\n", p.AcademicRows.Select(r => $"{r.Action}/{r.Key}/" + ImportCatalog.ScalarJson(r.Entity))) +
        string.Join("\n", p.RepairOriginals.Select(l => $"{l.Sheet}/{l.Row}/{l.Locked}/" + ImportCatalog.ScalarJson(l.Schedule))) +
        string.Join("\n", p.RepairableConflicts.Select(i => i.ToString())) +
        string.Join("\n", p.ReadingNotes.Select(i => i.ToString())) +
        string.Join("\n", p.SkippedRows.Select(i => i.ToString())) +
        string.Join("\n", p.PlacementChanges.Select(c => c.ToString())) +
        string.Join("\n", p.Changes.Select(c => c.ToString())) +
        string.Join("\n", p.Issues.Select(i => i.ToString())));
    private static void EnsureUntampered(ExcelImportPreview p)
    {
        if (!p.IsValid || p.ContentVersion.Length == 0 || p.ContentVersion != ContentVersion(p))
            throw new ArgumentException("The preview is invalid or changed. Run validation again.");
    }
}
