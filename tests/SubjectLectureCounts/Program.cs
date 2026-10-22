using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Reflection;
using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using UniversityTimetable.API;
using University_Timetable_and_Classroom_Management_System;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

internal static class Program
{
    private static int checks, failures;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        CheckDefaults();
        CheckDayLimitDiagnostic();
        var database = "UniversityTimetable_LectureCounts_" + Guid.NewGuid().ToString("N")[..8];
        var connection = new SqlConnectionStringBuilder(DatabaseConnection.ConnectionString) { InitialCatalog = database };
        Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, connection.ConnectionString);
        Console.WriteLine("ISOLATED DATABASE " + database);
        using var dispatcher = new Form { ShowInTaskbar = false, Opacity = 0 };
        dispatcher.Shown += async (_, _) =>
        {
            try
            {
                await CheckDatabaseAndGenerator();
                await CheckRegeneration();
                await CheckPreferenceRegeneration();
                await CheckEditor();
                await CheckSubjectDeletion();
                await CheckArchivedRegenerationBackup();
            }
            catch (Exception exception) { Check("Harness completes", false, exception.ToString()); }
            finally { dispatcher.Close(); }
        };
        Application.Run(dispatcher);
        Console.WriteLine($"SUMMARY {checks} checks; {failures} failures");
        return failures == 0 ? 0 : 1;
    }

    private static void CheckDefaults()
    {
        foreach (var (theory, practical, expectedTheory, expectedPractical) in new[] { (0d,0d,1,0), (4d,0d,1,0), (0d,3d,0,1), (4d,3d,1,1) })
        {
            var subject = new Subject { TheoreticalHours = theory, PracticalHours = practical };
            Check($"Automatic defaults {theory}/{practical}", subject.EffectiveTheoreticalLectureCount == expectedTheory && subject.EffectivePracticalLectureCount == expectedPractical);
        }
        var manual = new Subject { TheoreticalHours = 4, PracticalHours = 2, TheoreticalLectureCount = 2, PracticalLectureCount = 3 };
        Check("Manual counts replace automatic values", manual.EffectiveTheoreticalLectureCount == 2 && manual.EffectivePracticalLectureCount == 3);
        manual.TheoreticalLectureCount = manual.PracticalLectureCount = 0;
        Check("Explicit zero is preserved", manual.EffectiveTheoreticalLectureCount == 0 && manual.EffectivePracticalLectureCount == 0);
        manual.TheoreticalLectureCount = -1;
        try { SubjectLectureRules.Validate(manual); Check("Negative count rejected", false); }
        catch (ArgumentException) { Check("Negative count rejected", true); }
        var dto = SubjectDto.From(new Subject { SubjectName = "DTO subject", StudyYearID = 1, SemesterNumber = 1, TheoreticalLectureCount = 2, PracticalLectureCount = 3 });
        Check("API maps counts in both directions", dto.ToEntity(1).TheoreticalLectureCount == 2 && dto.ToEntity(1).PracticalLectureCount == 3);
        dto.PracticalLectureCount = 101;
        Check("API rejects excessive counts", !Validator.TryValidateObject(dto, new ValidationContext(dto), [], true));
    }

    private static async Task CheckDatabaseAndGenerator()
    {
        await using (var db = new AppDbContext())
        {
            await db.Database.EnsureCreatedAsync();
            // Exercise migration against an existing schema, then its idempotence.
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Subjects DROP COLUMN TheoreticalLectureCount, PracticalLectureCount");
            await SubjectLectureCountSchema.EnsureAsync(db);
            await SubjectLectureCountSchema.EnsureAsync(db);
            await db.Database.ExecuteSqlRawAsync("""
                DROP INDEX [IX_Subjects_SubjectName_StudyYearID_BranchID_SemesterNumber] ON [dbo].[Subjects];
                DECLARE @defaultName sysname = (SELECT dc.name FROM sys.default_constraints dc
                    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
                    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Subjects') AND c.name = N'IsDeleted');
                IF @defaultName IS NOT NULL EXEC(N'ALTER TABLE [dbo].[Subjects] DROP CONSTRAINT ' + @defaultName);
                ALTER TABLE [dbo].[Subjects] DROP COLUMN [IsDeleted];
                CREATE UNIQUE INDEX [IX_Subjects_SubjectName_StudyYearID_BranchID_SemesterNumber]
                    ON [dbo].[Subjects] ([SubjectName], [StudyYearID], [BranchID], [SemesterNumber]);
                """);
            await SubjectDeletionSchema.EnsureAsync(db);
            await SubjectDeletionSchema.EnsureAsync(db);
            Check("Existing subject schema upgrades without removing foreign keys", true);
            db.StudyYears.Add(new StudyYear { StudyYearID = 1, YearName = "First Year" });
            db.FacultyMembers.Add(new FacultyMember { FacultyMemberID = 1, FullName = "Lecture count test" });
            db.Sections.Add(new Section { SectionID = 1, SectionName = "A", StudyYearID = 1, StudentCount = 30 });
            db.Classrooms.AddRange(new Classroom { ClassroomID = 1, ClassroomNumber = "Theory room", Capacity = 40, RoomType = "Lecture" },
                new Classroom { ClassroomID = 2, ClassroomNumber = "Practical room", Capacity = 40, RoomType = "Lab" });
            for (int i = 0; i < 4; i++) db.TimeSlots.Add(new TimeSlot { TimeSlotID = i + 1, StartTime = TimeSpan.FromMinutes(510 + i * 90), EndTime = TimeSpan.FromMinutes(590 + i * 90) });
            await db.SaveChangesAsync();
        }
        var service = new SubjectService();
        var subject = await service.AddAsync(new Subject { SubjectName = "Counted subject", StudyYearID = 1, SemesterNumber = 1,
            TheoreticalHours = 4, PracticalHours = 2, CreditUnits = 3, TheoreticalLectureCount = 2, PracticalLectureCount = 2 });
        var saved = (await service.GetByIdAsync(subject.SubjectID))!;
        Check("Database persists manual counts", saved.TheoreticalLectureCount == 2 && saved.PracticalLectureCount == 2);
        await service.AddAsync(new Subject { SubjectName = "Zero count subject", StudyYearID = 1, SemesterNumber = 1,
            TheoreticalHours = 4, TheoreticalLectureCount = 0, PracticalLectureCount = 0 });
        await using (var db = new AppDbContext())
        {
            db.FacultyMemberSubjects.Add(new FacultyMemberSubject { SubjectID = subject.SubjectID, FacultyMemberID = 1 });
            await db.SaveChangesAsync();
        }
        var result = await new ScheduleService().GenerateAsync();
        Check("Generator uses configured weekly requirements", result.RequiredCount == 6 && result.CreatedCount == 6 && result.SkippedCount == 0,
            $"required={result.RequiredCount}, created={result.CreatedCount}, skipped={result.SkippedCount}");
        Check("Zero count subjects do not need teaching assignments", result.UnassignedSubjectsCount == 0);
        await using (var db = new AppDbContext())
        {
            var lectures = await db.Schedules.AsNoTracking().ToListAsync();
            Check("Two theory lectures per section", lectures.Count(s => s.LectureType == "Theory") == 2);
            Check("Two practical lectures for each subgroup", lectures.Count(s => s.GroupName == "A1") == 2 && lectures.Count(s => s.GroupName == "A2") == 2);
            var exchange = typeof(SubjectService).Assembly.GetType("University_Timetable_and_Classroom_Management_System.BusinessLayer.FacultyPracticalExchange")!;
            exchange.GetMethod("Validate")!.Invoke(null, [lectures, new HashSet<int> { 1 }, await db.TimeSlots.OrderBy(t => t.StartTime).ToListAsync()]);
            Check("Repeated practical occurrences remain valid for faculty editing", true);
        }
        saved.PracticalLectureCount = 1;
        try { await service.UpdateAsync(saved); Check("Reducing below saved lectures is rejected", false); }
        catch (ArgumentException) { Check("Reducing below saved lectures is rejected", true); }
        Check("Rejected reduction keeps saved counts", (await service.GetByIdAsync(subject.SubjectID))!.PracticalLectureCount == 2);
        var facultyPreview = await new FacultyScheduleEditorService().PreviewAsync(new FacultyScheduleRequest(1, 1,
            FacultyAvailabilityMode.Strict, ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday"], TimeLimitSeconds: 5));
        Check("Faculty solver supports repeated practical occurrences", facultyPreview.CanSave,
            facultyPreview.Status + ": " + string.Join("; ", facultyPreview.Suggestions));

        var table = new DataTable("Subjects");
        foreach (string column in new[] { "SubjectID", "SubjectName", "StudyYearID", "SemesterNumber", "TheoreticalHours", "PracticalHours" }) table.Columns.Add(column);
        table.Rows.Add(subject.SubjectID, subject.SubjectName, 1, 1, 4, 2);
        var preview = await new ExcelImportService().ValidateAsync(ExcelImportMode.AcademicData, [new ExcelSheetData("Subjects", table, [2])], true);
        Check("Old import files preserve omitted overrides and units", preview.IsValid && preview.Changes.Single().Action == "Unchanged", string.Join("; ", preview.Issues.Select(i => i.Message)));
    }

    private static void CheckDayLimitDiagnostic()
    {
        var rule = typeof(SubjectService).Assembly.GetType("University_Timetable_and_Classroom_Management_System.BusinessLayer.ScheduleDayRules")!
            .GetMethod("EnsureSectionDayLimit")!;
        Section[] sections = [new() { SectionID = 144, SectionName = "B", StudyYearID = 3, BranchID = 7 }];
        StudyYear[] years = [new() { StudyYearID = 3, YearName = "Third Year" }];
        Branch[] branches = [new() { BranchID = 7, BranchName = "هندسة البرمجيات" }];
        var rows = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday" }
            .Select(day => new Schedule { SectionID = 144, SemesterNumber = 2, DayOfWeek = day }).ToList();
        rule.Invoke(null, [rows.Take(4), sections, years, branches]);
        Check("Four teaching days stay within the limit", true);
        try { rule.Invoke(null, [rows, sections, years, branches]); Check("Five-day distribution rejected", false); }
        catch (TargetInvocationException ex) when (ex.InnerException is ArgumentException error)
        {
            string message = error.Message;
            Check("Day-limit error identifies section, year, branch and semester", message.Contains("«B»") && message.Contains("المرحلة الثالثة") &&
                message.Contains("هندسة البرمجيات") && message.Contains("الفصل الدراسي: 2"), message);
            Check("Day-limit error explains actual days, limit and remedy", message.Contains("أيام التدريس: 5") && message.Contains("الحد الأقصى: 4") &&
                message.Contains("الأحد، الاثنين، الثلاثاء، الأربعاء، الخميس") && message.Contains("انقل محاضرات") && message.Contains("لم يُحفظ أي تغيير"));
        }
        rows[^1].SemesterNumber = 1;
        rule.Invoke(null, [rows, sections, years, branches]);
        Check("Teaching-day limits are checked separately by semester", true);
    }

    private static async Task CheckEditor()
    {
        using var control = new SubjectsForm();
        using var host = new Form { ClientSize = new Size(1280, 940), RightToLeft = RightToLeft.Yes, RightToLeftLayout = true };
        host.Controls.Add(control); control.Dock = DockStyle.Fill; host.Show();
        Control Find(string name) => control.Controls.Find(name, true).Single();
        for (int i = 0; i < 100 && !Find("btnAddSubject").Enabled; i++) await Task.Delay(100);
        await Task.Delay(100);
        var grid = (DataGridView)Find("dgvSubjects");
        Check("Units replaced with two count columns", !grid.Columns.Cast<DataGridViewColumn>().Any(c => c.DataPropertyName == "CreditUnits") &&
            grid.Columns.Cast<DataGridViewColumn>().Count(c => c.DataPropertyName.EndsWith("LectureCount")) == 2);
        grid.CurrentCell = grid.Rows[0].Cells.Cast<DataGridViewCell>().First(c => c.Visible);
        grid.Rows[0].Selected = true;
        typeof(SubjectsForm).GetMethod("PopulateSubjectEditorFromSelection", Private)!.Invoke(control, null);
        Check("Hours removed from editor and table", !control.Controls.Find("txtTheoreticalHours", true).Any() &&
            !control.Controls.Find("txtPracticalHours", true).Any() &&
            !grid.Columns.Cast<DataGridViewColumn>().Any(c => c.DataPropertyName is "TheoreticalHours" or "PracticalHours"));
        Check("Saved manual counts remain editable", Find("txtTheoreticalLectureCount").Text == "2" && Find("txtPracticalLectureCount").Text == "2");
        var args = new object?[] { null, true };
        bool built = (bool)typeof(SubjectsForm).GetMethod("TryBuildSubject", Private)!.Invoke(control, args)!;
        var changed = (Subject)args[0]!;
        Check("Editor preserves retired hours and units", built && changed.TheoreticalLectureCount == 2 && changed.PracticalLectureCount == 2 &&
            changed.TheoreticalHours == 4 && changed.PracticalHours == 2 && changed.CreditUnits == 3);
        typeof(SubjectsForm).GetMethod("ResetLectureCounts", Private)!.Invoke(control, null);
        Check("Reset restores automatic values", Find("txtTheoreticalLectureCount").Text == "1" && Find("txtPracticalLectureCount").Text == "1");
        Find("txtTheoreticalLectureCount").Text = "4";
        Find("txtPracticalLectureCount").Text = "0";
        args = [null, true];
        typeof(SubjectsForm).GetMethod("TryBuildSubject", Private)!.Invoke(control, args);
        changed = (Subject)args[0]!;
        Check("Only count inputs change scheduling requirements", changed.TheoreticalLectureCount == 4 && changed.PracticalLectureCount == 0 &&
            changed.TheoreticalHours == 4 && changed.PracticalHours == 2);
        typeof(SubjectsForm).GetMethod("PopulateSubjectEditorFromSelection", Private)!.Invoke(control, null);
        Directory.CreateDirectory("tmp/lecture-count-qa");
        using var bitmap = new Bitmap(host.Width, host.Height);
        host.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save("tmp/lecture-count-qa/SubjectsForm.png");
        host.Close();
    }

    private static async Task<List<Schedule>> SavedLectures()
    {
        await using var db = new AppDbContext();
        return await db.Schedules.AsNoTracking().OrderBy(s => s.ScheduleID).ToListAsync();
    }

    private static string Snapshot(IEnumerable<Schedule> rows) => System.Text.Json.JsonSerializer.Serialize(rows.Select(s => new
        { s.ScheduleID, s.SubjectID, s.FacultyMemberID, s.ClassroomID, s.TimeSlotID, s.DayOfWeek, s.LectureType, s.GroupName,
            s.StudyYearID, s.BranchID, s.SectionID, s.SemesterNumber, s.IsLocked }));

    private static string BackupPath()
    {
        string folder = Path.GetFullPath("output/regeneration-verification");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "BeforeGenerate-" + Guid.NewGuid().ToString("N") + ".uttbackup.json");
    }

    private static async Task CheckRegeneration()
    {
        var service = new ScheduleService();
        var backups = new TimetableBackupService();
        var original = await SavedLectures();
        var sample = original.First(s => s.LectureType == "Theory");
        var input = new DataTable("Timetable");
        foreach (string name in new[] { "Day", "TimeSlot", "Subject", "Faculty", "Classroom", "Section", "LectureType", "SemesterNumber", "GroupName" }) input.Columns.Add(name);
        input.Rows.Add(sample.DayOfWeek, "#" + sample.TimeSlotID, "#" + sample.SubjectID, "#" + sample.FacultyMemberID,
            "#" + sample.ClassroomID, "#" + sample.SectionID, sample.LectureType, sample.SemesterNumber, "");
        var import = await new ExcelImportService().ValidateAsync(ExcelImportMode.Hybrid, [new ExcelSheetData("Timetable", input, [2])], true);
        Check("Hybrid import does not automatically select locks when Locked is absent", import.IsValid && import.Lectures.Count == 1 && !import.Lectures[0].Locked,
            string.Join("; ", import.Issues.Select(i => i.Message)));
        Check("Initial generation does not automatically lock any lecture", original.All(s => !s.IsLocked));
        string path = BackupPath();
        var result = await service.RegenerateAsync(path);
        Check("Regeneration builds every weekly requirement from scratch", result.IsRegenerated && result.RequiredCount == 6 && result.CreatedCount == 6 &&
            result.AddedCount == 6 && result.ReplacedCount == 6 && result.PreservedCount == 0);
        Check("Regeneration saves a verified full backup", result.SafetyBackupPath == path && File.Exists(path));
        var preview = await backups.PreviewRestoreAsync(path);
        Check("Automatic pre-generation backup is restorable", preview.CanRestore && preview.LectureCount == 6, string.Join("; ", preview.Issues));
        await backups.RestoreAsync(preview, true, BackupPath());
        Check("Restoring pre-generation backup recovers exact old lecture data", Snapshot(original) == Snapshot(await SavedLectures()));
        var pinned = original.First(s => s.LectureType == "Theory");
        await service.SetLockedAsync(pinned.ScheduleID, true);
        pinned.IsLocked = true;
        result = await service.RegenerateAsync(BackupPath());
        var rows = await SavedLectures();
        Check("User-selected lock survives fresh generation at its exact placement", result.PreservedCount == 1 && result.ReplacedCount == 5 && result.AddedCount == 5 &&
            Snapshot([rows.Single(s => s.ScheduleID == pinned.ScheduleID)]) == Snapshot([pinned]));
        Check("Regeneration never automatically locks replacement lectures", rows.Count(s => s.IsLocked) == 1);
        await service.SetLockedAsync(pinned.ScheduleID, false);
        var variants = new HashSet<string>();
        for (int i = 0; i < 4; i++)
        {
            await service.RegenerateAsync(BackupPath());
            variants.Add(string.Join(";", (await SavedLectures()).Select(s => $"{s.SubjectID}/{s.GroupName}/{s.LectureType}/{s.DayOfWeek}/{s.TimeSlotID}").Order()));
        }
        Check("Repeated fresh generation varies placements when choices are available", variants.Count > 1);
        rows = await SavedLectures();
        Check("Every fresh plan has exactly the configured lecture counts with no automatic locks", rows.Count == 6 && rows.All(s => !s.IsLocked) &&
            rows.Count(s => s.LectureType == "Theory") == 2 && rows.Count(s => s.GroupName == "A1") == 2 && rows.Count(s => s.GroupName == "A2") == 2);
        var validate = typeof(ScheduleService).GetMethod("ValidateAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
        await using (var db = new AppDbContext())
            foreach (var row in rows) await (Task)validate.Invoke(null, [db, row, true, true])!;
        Check("Fresh plan passes business and collision validation for every lecture", true);
        string before = Snapshot(rows);
        string occupiedPath = BackupPath();
        await File.WriteAllTextAsync(occupiedPath, "keep existing file");
        try { await service.RegenerateAsync(occupiedPath); Check("Backup write failure aborts regeneration", false); }
        catch (IOException) { Check("Backup write failure preserves timetable and existing file", before == Snapshot(await SavedLectures()) && await File.ReadAllTextAsync(occupiedPath) == "keep existing file"); }
        await using (var db = new AppDbContext())
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER [dbo].[RejectRegeneration] ON [dbo].[Schedules] AFTER INSERT AS BEGIN THROW 51000, 'Injected regeneration failure', 1; END");
        try
        {
            try { await service.RegenerateAsync(BackupPath()); Check("Database write failure aborts regeneration", false); }
            catch (DbUpdateException) { Check("Database write failure rolls back deletion of the old timetable", before == Snapshot(await SavedLectures())); }
        }
        finally
        {
            await using var db = new AppDbContext();
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER [dbo].[RejectRegeneration]");
        }
        var assignment = (await new FacultyMemberSubjectService().GetAllAsync()).Single();
        await using (var db = new AppDbContext()) await db.FacultyMemberSubjects.ExecuteDeleteAsync();
        try
        {
            try { await service.RegenerateAsync(BackupPath()); Check("Incomplete requirements abort regeneration", false); }
            catch (ArgumentException) { Check("Incomplete requirements leave the old timetable intact", before == Snapshot(await SavedLectures())); }
        }
        finally { await new FacultyMemberSubjectService().AddAsync(new() { FacultyMemberID = assignment.FacultyMemberID, SubjectID = assignment.SubjectID }); }
        using var screen = new SchedulesForm();
        Check("Main scheduling screen exposes a checkbox for manual lecture locking",
            ((DataGridView)screen.Controls.Find("dgvSchedules", true).Single()).Columns["colLockStatus"] is DataGridViewCheckBoxColumn { Visible: true, ReadOnly: false } &&
            screen.Controls.Find("btnGenerateSchedule", true).Single().Text == "توليد جدول جديد");
        Directory.CreateDirectory("tmp/regeneration-qa");
        await using (var db = new AppDbContext())
            await (Task)typeof(DatabaseMaintenanceService).GetMethod("RecreateScheduleDetailsViewAsync", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [db])!;
        using var host = new Form { ClientSize = new Size(1280, 940), RightToLeft = RightToLeft.Yes, RightToLeftLayout = true };
        host.Controls.Add(screen); screen.Dock = DockStyle.Fill;
        host.Show();
        for (int i = 0; i < 70 && !screen.Controls.Find("btnGenerateSchedule", true).Single().Enabled; i++) await Task.Delay(100);
        Check("Scheduling screen loads saved lectures with every new row visibly unpinned",
            screen.Controls.Find("btnGenerateSchedule", true).Single().Enabled &&
            ((DataGridView)screen.Controls.Find("dgvSchedules", true).Single()).Rows.Count == 6);
        var lectureGrid = (DataGridView)screen.Controls.Find("dgvSchedules", true).Single();
        lectureGrid.ClearSelection();
        string beforeCheckbox = Snapshot(await SavedLectures());
        Check("Lock checkboxes start empty and loading the grid changes no saved locks", lectureGrid.Rows.Cast<DataGridViewRow>().All(r => !Convert.ToBoolean(r.Cells["colLockStatus"].Value)) &&
            (await SavedLectures()).All(s => !s.IsLocked));
        foreach (bool locked in new[] { true, false })
        {
            var checkbox = (DataGridViewCheckBoxCell)lectureGrid.Rows[0].Cells["colLockStatus"];
            lectureGrid.CurrentCell = checkbox;
            lectureGrid.BeginEdit(false);
            checkbox.EditingCellFormattedValue = locked;
            lectureGrid.NotifyCurrentCellDirty(true);
            for (int i = 0; i < 70 && (bool)typeof(SchedulesForm).GetField("isSavingScheduleLock", Private)!.GetValue(screen)!; i++) await Task.Delay(100);
            var savedCheckboxes = await SavedLectures();
            Check(locked ? "Checking a box saves only the selected lecture lock" : "Clearing a box saves removal of the selected lecture lock",
                savedCheckboxes.Count(s => s.IsLocked) == (locked ? 1 : 0));
        }
        Check("Checkbox changes preserve all lecture identities and placements", beforeCheckbox == Snapshot(await SavedLectures()));
        lectureGrid.ClearSelection();
        using var bitmap = new Bitmap(host.Width, host.Height);
        host.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save("tmp/regeneration-qa/SchedulesForm.png");
        host.Close();
    }

    private static async Task CheckPreferenceRegeneration()
    {
        string[] days = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday"];
        var preferenceService = new FacultyPreferenceService();
        var profile = await preferenceService.SaveAsync(new FacultyScheduleRequest(1, 1, FacultyAvailabilityMode.Strict, days)
        {
            UseDayPreferences = true,
            DayPreferences = days.Select(day => new FacultyDaySetting(day, FacultyPreferenceLevel.StronglyPreferred)).ToArray()
        }, 0);
        int extraSubjectId = 0;
        try
        {
            var result = await new ScheduleService().RegenerateAsync(BackupPath());
            Check("Preference optimizer supports two unpaired occurrences of the same practical subject", result.CreatedCount == 6 && result.AddedCount == 6);
            var rows = await SavedLectures();
            string before = Snapshot(rows);
            await using (var db = new AppDbContext())
            {
                db.FacultyMembers.AddRange(new FacultyMember { FacultyMemberID = 2, FullName = "Exchange teacher" }, new FacultyMember { FacultyMemberID = 3, FullName = "Backup teacher" });
                db.Classrooms.Add(new Classroom { ClassroomID = 3, ClassroomNumber = "Second lab", RoomType = "Lab", Capacity = 40 });
                await db.SaveChangesAsync();
            }
            var extra = await new SubjectService().AddAsync(new Subject { SubjectName = "Exchange subject", StudyYearID = 1, SemesterNumber = 1,
                TheoreticalLectureCount = 0, PracticalLectureCount = 1, PracticalHours = 2 });
            extraSubjectId = extra.SubjectID;
            var assignments = new FacultyMemberSubjectService();
            await assignments.AddAsync(new FacultyMemberSubject { FacultyMemberID = 2, SubjectID = extraSubjectId });
            await assignments.AddAsync(new FacultyMemberSubject { FacultyMemberID = 3, SubjectID = rows[0].SubjectID });
            foreach (var row in rows.Where(s => s.LectureType == "Practical")) row.FacultyMemberID = row.GroupName == "A1" ? 1 : 3;
            rows.First(s => s.LectureType == "Practical").IsLocked = true;
            foreach (string group in new[] { "A1", "A2" }) rows.Add(new Schedule { SubjectID = extraSubjectId, FacultyMemberID = 2, ClassroomID = 3,
                StudyYearID = 1, SectionID = 1, SemesterNumber = 1, LectureType = "Practical", GroupName = group, TimeSlotID = 1, DayOfWeek = "Tuesday" });
            var assembly = typeof(SubjectService).Assembly;
            await using var context = new AppDbContext();
            var load = (Task)assembly.GetType("University_Timetable_and_Classroom_Management_System.BusinessLayer.ImportCatalog")!.GetMethod("LoadAsync")!.Invoke(null, [context, CancellationToken.None])!;
            await load;
            var catalog = load.GetType().GetProperty("Result")!.GetValue(load);
            var solve = assembly.GetType("University_Timetable_and_Classroom_Management_System.BusinessLayer.ImportedTimetableRepairPlanner")!.GetMethod("Solve")!;
            object Search(bool allow, bool exchange = true) => solve.Invoke(null, [catalog, rows.Select(s => new TimetableImportRow("Generated", 0, s, s.IsLocked)).ToList(),
                new List<Schedule>(), 10d, CancellationToken.None, true, null, allow, exchange])!;
            var frozen = Search(false);
            Check("Regression fixture reproduces false infeasibility from frozen provisional teachers", frozen.GetType().GetProperty("Plan")!.GetValue(frozen) is null);
            var flexible = Search(true);
            var plan = (List<Schedule>?)flexible.GetType().GetProperty("Plan")!.GetValue(flexible);
            Check("Fresh optimization can select assigned backup teachers to complete practical exchanges", plan?.Count == 8,
                flexible.GetType().GetProperty("Status")!.GetValue(flexible)?.ToString() ?? "");
            Check("Fresh optimization preserves the user's checked lecture", plan is not null && Snapshot(plan.Where(s => s.IsLocked)) == Snapshot(rows.Where(s => s.IsLocked)));
            var independent = Search(false, false);
            Check("Generation optimization respects independent practical sessions supported by the generator",
                independent.GetType().GetProperty("Plan")!.GetValue(independent) is List<Schedule> { Count: 8 });
            Check("Preference and teacher search performs no database writes", before == Snapshot(await SavedLectures()));
        }
        finally
        {
            await preferenceService.DeleteAsync(1, 1, profile.Revision);
            await using var db = new AppDbContext();
            await db.FacultyMemberSubjects.Where(a => a.FacultyMemberID == 2 || a.FacultyMemberID == 3).ExecuteDeleteAsync();
            if (extraSubjectId > 0) await db.Subjects.Where(s => s.SubjectID == extraSubjectId).ExecuteDeleteAsync();
            await db.FacultyMembers.Where(f => f.FacultyMemberID == 2 || f.FacultyMemberID == 3).ExecuteDeleteAsync();
            await db.Classrooms.Where(c => c.ClassroomID == 3).ExecuteDeleteAsync();
        }
    }

    private static async Task CheckArchivedRegenerationBackup()
    {
        var subjectService = new SubjectService();
        var active = (await subjectService.GetAllAsync()).Single(s => s.SubjectName == "Counted subject");
        active.TheoreticalLectureCount = 1;
        await subjectService.UpdateAsync(active);
        await new FacultyMemberSubjectService().AddAsync(new() { FacultyMemberID = 1, SubjectID = active.SubjectID });
        string before = Snapshot(await SavedLectures());
        string path = BackupPath();
        var result = await new ScheduleService().RegenerateAsync(path);
        var current = await SavedLectures();
        Check("Fresh generation replaces archived unlocked lectures with active requirements", result.AddedCount == 1 && result.ReplacedCount == 6 &&
            current.Count == 1 && current[0].SubjectID == active.SubjectID && !current[0].IsLocked);
        var backups = new TimetableBackupService();
        var preview = await backups.PreviewRestoreAsync(path);
        Check("Archived-subject backup remains restorable after its lectures and assignment are removed", preview.CanRestore, string.Join("; ", preview.Issues));
        await backups.RestoreAsync(preview, true, BackupPath());
        Check("Archived-subject backup restores exact retained lecture history", Snapshot(await SavedLectures()) == before);
    }

    private static async Task CheckSubjectDeletion()
    {
        var service = new SubjectService();
        var subject = (await service.GetAllAsync()).Single(s => s.SubjectName == "Counted subject");
        var schedules = new ScheduleService();
        string before = System.Text.Json.JsonSerializer.Serialize((await schedules.GetAllAsync()).Select(s => new
            { s.ScheduleID, s.SubjectID, s.FacultyMemberID, s.ClassroomID, s.TimeSlotID, s.DayOfWeek, s.LectureType, s.GroupName }));
        var assignments = new FacultyMemberSubjectService();
        try { await assignments.DeleteAsync(1, subject.SubjectID); Check("Used active assignment remains protected", false); }
        catch (ArgumentException ex) { Check("Used active assignment error gives material name and lecture count", ex.Message.Contains(subject.SubjectName) && ex.Message.Contains("6")); }
        await service.DeleteAsync(subject.SubjectID);
        Check("Deleted subject disappears from active list and lookup", !(await service.GetAllAsync()).Any(s => s.SubjectID == subject.SubjectID) &&
            await service.GetByIdAsync(subject.SubjectID) is null);
        var assignment = (await new FacultyMemberSubjectService().GetAllAsync()).Single(a => a.SubjectID == subject.SubjectID);
        Check("Deletion preserves assignment and original subject name", assignment.FacultyMemberID == 1 && assignment.Subject.SubjectName == subject.SubjectName && assignment.Subject.IsDeleted);
        var dto = AssignmentDto.From(assignment);
        Check("Web can display retained deleted-subject assignments", dto.SubjectName == subject.SubjectName && dto.IsSubjectDeleted);
        string after = System.Text.Json.JsonSerializer.Serialize((await schedules.GetAllAsync()).Select(s => new
            { s.ScheduleID, s.SubjectID, s.FacultyMemberID, s.ClassroomID, s.TimeSlotID, s.DayOfWeek, s.LectureType, s.GroupName }));
        Check("Deletion preserves saved timetable", before == after);
        var result = await schedules.GenerateAsync();
        Check("Deleted subjects add no new timetable requirements", result.RequiredCount == 0 && result.AddedCount == 0 && result.PreservedCount == 6);
        var replacement = await service.AddAsync(new Subject { SubjectName = subject.SubjectName, StudyYearID = 1, SemesterNumber = 1 });
        Check("Same name can be reused without transferring old assignments", replacement.SubjectID != subject.SubjectID &&
            (await new FacultyMemberSubjectService().GetAllAsync()).Single().SubjectID == subject.SubjectID);
        try
        {
            await new FacultyMemberSubjectService().AddAsync(new FacultyMemberSubject { FacultyMemberID = 1, SubjectID = subject.SubjectID });
            Check("New assignments cannot target deleted subjects", false);
        }
        catch (ArgumentException) { Check("New assignments cannot target deleted subjects", true); }

        await assignments.DeleteAsync(1, subject.SubjectID);
        Check("Deleted-subject assignment can be removed despite saved lectures", await assignments.GetByIdAsync(1, subject.SubjectID) is null);
        after = System.Text.Json.JsonSerializer.Serialize((await schedules.GetAllAsync()).Select(s => new
            { s.ScheduleID, s.SubjectID, s.FacultyMemberID, s.ClassroomID, s.TimeSlotID, s.DayOfWeek, s.LectureType, s.GroupName }));
        Check("Removing archived assignment preserves lecture identities and placements", before == after);
        // Keep the replacement subject out of new generation so this run exercises
        // the case where every assignment has been removed but history remains.
        replacement.TheoreticalLectureCount = replacement.PracticalLectureCount = 0;
        await service.UpdateAsync(replacement);
        result = await schedules.GenerateAsync();
        Check("Generation preserves archived lectures after the last assignment is removed", result.RequiredCount == 0 && result.AddedCount == 0 && result.PreservedCount == 6);
        var original = (await schedules.GetAllAsync()).First();
        var copy = new Schedule { ScheduleID = original.ScheduleID, SubjectID = original.SubjectID, FacultyMemberID = original.FacultyMemberID,
            SectionID = original.SectionID, ClassroomID = original.ClassroomID, TimeSlotID = original.TimeSlotID, DayOfWeek = original.DayOfWeek,
            SemesterNumber = original.SemesterNumber, LectureType = original.LectureType, GroupName = original.GroupName };
        await schedules.UpdateAsync(copy);
        Check("Archived lecture remains editable without its removed assignment", (await schedules.GetByIdAsync(copy.ScheduleID))!.SubjectID == subject.SubjectID);
        try { await schedules.AddAsync(copy); Check("An archived lecture ID cannot be reused to add new lectures", false); }
        catch (ArgumentException) { Check("An archived lecture ID cannot be reused to add new lectures", true); }
        copy.GroupName = "new group";
        try { await schedules.UpdateAsync(copy); Check("Archived lecture identity cannot be repurposed", false); }
        catch (ArgumentException) { Check("Archived lecture identity cannot be repurposed", true); }
        var facultyPreview = await new FacultyScheduleEditorService().PreviewAsync(new FacultyScheduleRequest(1, 1,
            FacultyAvailabilityMode.Strict, ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday"], TimeLimitSeconds: 5));
        Check("Faculty solver can reorganize retained lectures after assignment removal", facultyPreview.CanSave, facultyPreview.Status);
    }

    private static void Check(string name, bool ok, string detail = "")
    {
        checks++; if (!ok) failures++;
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (detail.Length == 0 ? "" : ": " + detail));
    }
}
