using System.Data;
using System.Text.Json;
using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

internal static partial class Program
{
    static Func<AppDbContext> factory = null!;
    static FacultyScheduleEditorService editor = null!;
    static FacultyPreferenceService preferences = null!;
    static List<Schedule> initial = [];
    static readonly string output = Path.GetFullPath("output/faculty-preferences-verification");
    static readonly List<object> results = [];
    static int failed;
    static readonly string[] days = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday"];
    static void Assert(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
    static string Json(object value) => JsonSerializer.Serialize(value);
    static Schedule Copy(Schedule s) => JsonSerializer.Deserialize<Schedule>(Json(s))!;
    static async Task<List<Schedule>> Rows() { await using var db = factory(); return await db.Schedules.AsNoTracking().OrderBy(s => s.ScheduleID).ToListAsync(); }
    static FacultyScheduleRequest Profile(int faculty = 1, int strongPeriod = 3, string[]? attendance = null)
    {
        attendance ??= ["Sunday", "Wednesday"];
        return new(faculty, 1, FacultyAvailabilityMode.Strict, attendance, TimeLimitSeconds: 5)
        {
            UsePreferenceMatrix = true,
            PeriodPreferences = days.SelectMany(d => Enumerable.Range(1, 3).Select(p => new FacultyPeriodSetting(d, p,
                !attendance.Contains(d) ? FacultyPreferenceLevel.Prohibited : p == strongPeriod ? FacultyPreferenceLevel.StronglyPreferred : FacultyPreferenceLevel.Preferred))).ToArray()
        };
    }
    static async Task Reject(Func<Task> action)
    {
        try { await action(); } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OperationCanceledException or DbUpdateException) { return; }
        throw new Exception("Expected rejection");
    }
    static async Task Reset()
    {
        await using var db = factory();
        await db.FacultySchedulingPreferences.ExecuteDeleteAsync(); await db.Schedules.ExecuteDeleteAsync();
        await Restore(db);
    }
    static async Task Check(string name, Func<Task> action)
    {
        try { await Reset(); await action(); results.Add(new { name, passed = true }); Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; results.Add(new { name, passed = false, error = ex.ToString() }); Console.WriteLine("FAIL " + name + ": " + ex); }
    }
    [STAThread]
    static void Main(string[] args)
    {
        Directory.CreateDirectory(output);
        Run().GetAwaiter().GetResult();
        if (args.Contains("web") && failed == 0) WebSmokeAsync().GetAwaiter().GetResult();
        if (args.Contains("ui") && failed == 0) Ui();
        if (args.Contains("alternatives-ui") && failed == 0) AlternativesUi();
        File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { count = results.Count, failed, results }, new JsonSerializerOptions { WriteIndented = true }));
        Environment.ExitCode = failed == 0 ? 0 : 1; Console.WriteLine($"SUMMARY {results.Count} checks, {failed} failures");
    }
    static async Task Run()
    {
        var connection = new SqlConnectionStringBuilder { DataSource = Environment.GetEnvironmentVariable("UNIVERSITY_TIMETABLE_TEST_SERVER") ?? "localhost", InitialCatalog = "UniversityTimetable_Preferences_" + Guid.NewGuid().ToString("N"), IntegratedSecurity = true, Encrypt = false, TrustServerCertificate = true };
        Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, connection.ConnectionString);
        factory = () => new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection.ConnectionString).Options);
        editor = new(factory); preferences = editor.Preferences;
        await using (var db = factory())
        {
            await db.Database.EnsureCreatedAsync();
            await FacultyPreferenceSchema.EnsureAsync(db); await FacultyPreferenceSchema.EnsureAsync(db);
            db.StudyYears.Add(new() { StudyYearID = 1, YearName = "First Year" });
            db.FacultyMembers.AddRange(new FacultyMember { FacultyMemberID = 1, FullName = "د. أحمد" }, new FacultyMember { FacultyMemberID = 2, FullName = "د. سارة" });
            db.Sections.AddRange(new Section { SectionID = 1, SectionName = "A", StudyYearID = 1, StudentCount = 40 }, new Section { SectionID = 2, SectionName = "B", StudyYearID = 1, StudentCount = 40 });
            db.Classrooms.AddRange(new Classroom { ClassroomID = 1, ClassroomNumber = "قاعة 1", RoomType = "Lecture", Capacity = 60 }, new Classroom { ClassroomID = 2, ClassroomNumber = "صغيرة", RoomType = "Lecture", Capacity = 10 });
            for (int p = 1; p <= 3; p++) db.TimeSlots.Add(new() { TimeSlotID = p, StartTime = TimeSpan.FromMinutes(510 + (p - 1) * 70), EndTime = TimeSpan.FromMinutes(570 + (p - 1) * 70) });
            for (int id = 1; id <= 4; id++) { db.Subjects.Add(new() { SubjectID = id, SubjectName = "مادة " + id, StudyYearID = 1, SemesterNumber = 1, TheoreticalHours = 2, CreditUnits = 2, RequirementType = "Core" }); db.FacultyMemberSubjects.Add(new() { SubjectID = id, FacultyMemberID = id <= 2 ? 1 : 2 }); }
            await db.SaveChangesAsync();
            for (int id = 1; id <= 4; id++) db.Schedules.Add(new() { SubjectID = id, FacultyMemberID = id <= 2 ? 1 : 2, SectionID = id <= 2 ? 1 : 2, StudyYearID = 1, SemesterNumber = 1, LectureType = "Theory", DayOfWeek = id % 2 == 1 ? "Sunday" : "Wednesday", TimeSlotID = id <= 2 ? 1 : 2, ClassroomID = 1 });
            await db.SaveChangesAsync();
        }
        initial = await Rows();
        await Check("Additive schema upgrade is repeatable and preserves existing lectures", async () =>
        {
            string before = Json(await Rows()); await using var db = factory();
            await db.Database.ExecuteSqlRawAsync("DROP TABLE [FacultyPeriodPreferences]; DROP TABLE [FacultySchedulingPreferences];");
            await FacultyPreferenceSchema.EnsureAsync(db); await FacultyPreferenceSchema.EnsureAsync(db);
            Assert(before == Json(await Rows()));
        });
        await Check("Absent profile preserves legacy behavior and no faculty accounts are created", async () => { Assert(await preferences.GetAsync(1, 1) is null); Assert((await editor.GetCurrentAsync(1, 1)).Count == 2); await using var db = factory(); Assert(await db.ApplicationUsers.CountAsync() == 0); });
        await Check("Complete three-level matrix persists without moving lectures", async () => { string before = Json(await Rows()); var saved = await preferences.SaveAsync(Profile(), 0); var loaded = await preferences.GetAsync(1, 1); Assert(loaded!.Revision == saved.Revision && loaded.Settings.PeriodPreferences.Count == 15); Assert(Json(await Rows()) == before); Assert(loaded.Settings.SelectedDays.SequenceEqual(new[] { "Sunday", "Wednesday" })); });
        await Check("Duplicates invalid levels incomplete cells and one-day plans are rejected", async () => { var p = Profile(); await Reject(() => preferences.SaveAsync(p with { PeriodPreferences = p.PeriodPreferences.Append(p.PeriodPreferences[0]).ToArray() }, 0)); await Reject(() => preferences.SaveAsync(p with { PeriodPreferences = p.PeriodPreferences.Skip(1).ToArray() }, 0)); await Reject(() => preferences.SaveAsync(p with { PeriodPreferences = p.PeriodPreferences.Select(c => c with { Level = (FacultyPreferenceLevel)9 }).ToArray() }, 0)); await Reject(() => preferences.SaveAsync(Profile(attendance: ["Sunday"]), 0)); });
        await Check("Outside-day cell overrides are rejected", async () => { var p = Profile(); await Reject(() => preferences.SaveAsync(p with { PeriodPreferences = p.PeriodPreferences.Select(c => c.Day == "Monday" ? c with { Level = FacultyPreferenceLevel.StronglyPreferred } : c).ToArray() }, 0)); });
        await Check("Strong preferences improve period placement while preserving attendance and identity", async () => { var p = Profile(); await preferences.SaveAsync(p, 0); string before = Json(await Rows()); var plan = await editor.PreviewAsync(p); Assert(plan.CanSave, plan.Message); Assert(plan.Changes.All(c => c.NewPeriod.StartsWith("10:50"))); Assert(Json(await Rows()) == before); Assert(await editor.SaveAsync(plan, true) == 2); var rows = await Rows(); Assert(rows.Where(s => s.FacultyMemberID == 1).Select(s => s.DayOfWeek).ToHashSet().SetEquals(p.SelectedDays)); Assert(rows.Where(s => s.FacultyMemberID == 2).All(s => Json(s) == Json(initial.Single(i => i.ScheduleID == s.ScheduleID)))); var m = plan.PreferenceMeasurements.Single(); Assert(m.BeforePenalty == 4 && m.AfterPenalty == 0 && m.StronglyPreferredPercentage == 100); File.WriteAllText(Path.Combine(output, "before-after.json"), Json(plan.PreferenceMeasurements)); });
        await Check("Changing StronglyPreferred to Preferred never adds or substitutes a day", async () => { var p = Profile(); var saved = await preferences.SaveAsync(p, 0); var first = await editor.PreviewAsync(p); await editor.SaveAsync(first, true); var weaker = p with { PeriodPreferences = p.PeriodPreferences.Select(c => c.Level == FacultyPreferenceLevel.StronglyPreferred ? c with { Level = FacultyPreferenceLevel.Preferred } : c).ToArray() }; await preferences.SaveAsync(weaker, saved.Revision); var plan = await editor.PreviewAsync(weaker); Assert(plan.CanSave && plan.Changes.All(c => !c.Changed)); Assert(plan.Changes.Select(c => c.NewDay).ToHashSet().SetEquals(p.SelectedDays)); });
        await Check("Prohibited cells are hard exclusions even when previously occupied", async () => { var p = Profile() with { PeriodPreferences = Profile().PeriodPreferences.Select(c => c.TimeSlotId == 1 ? c with { Level = FacultyPreferenceLevel.Prohibited } : c).ToArray() }; await preferences.SaveAsync(p, 0); var plan = await editor.PreviewAsync(p); Assert(plan.CanSave && plan.Changes.All(c => c.NewPeriod.StartsWith("10:50"))); });
        await Check("All permitted cells prohibited reports infeasibility with no writes", async () => { var p = Profile() with { PeriodPreferences = Profile().PeriodPreferences.Select(c => c with { Level = FacultyPreferenceLevel.Prohibited }).ToArray() }; await preferences.SaveAsync(p, 0); string before = Json(await Rows()); var plan = await editor.PreviewAsync(p); Assert(!plan.CanSave && plan.Status == "Infeasible"); Assert(Json(await Rows()) == before); });
        await Check("Locked lectures cannot move to improve preferences", async () => { await preferences.SaveAsync(Profile(), 0); await using (var db = factory()) await db.Schedules.Where(s => s.FacultyMemberID == 1).ExecuteUpdateAsync(x => x.SetProperty(s => s.IsLocked, true)); var plan = await editor.PreviewAsync(Profile()); Assert(plan.CanSave && plan.Changes.All(c => !c.Changed)); });
        await Check("A prohibited locked lecture prevents a saveable solution", async () => { var p = Profile() with { PeriodPreferences = Profile().PeriodPreferences.Select(c => c.TimeSlotId == 1 ? c with { Level = FacultyPreferenceLevel.Prohibited } : c).ToArray() }; await preferences.SaveAsync(p, 0); await using (var db = factory()) await db.Schedules.Where(s => s.FacultyMemberID == 1).ExecuteUpdateAsync(x => x.SetProperty(s => s.IsLocked, true)); Assert(!(await editor.PreviewAsync(p)).CanSave); });
        await Check("Moving other faculty is bounded explicit and improves total preference quality", async () => { var p = Profile(strongPeriod: 2); await preferences.SaveAsync(p, 0); await preferences.SaveAsync(Profile(2, 1), 0); var fixedPlan = await editor.PreviewAsync(p); Assert(fixedPlan.CanSave && fixedPlan.Changes.All(c => !c.Changed)); var moved = await editor.PreviewAsync(p with { MaxOtherLectureChanges = 2 }); Assert(moved.CanSave, moved.Message); Assert(moved.Changes.Count(c => c.IsOtherFaculty && c.Changed) == 2); Assert(moved.PreferenceMeasurements.Sum(m => m.AfterPenalty) < fixedPlan.PreferenceMeasurements.Sum(m => m.AfterPenalty)); await editor.SaveAsync(moved, true); });
        await Check("Equal total penalty protects worst individual attainment rather than favoring the first teacher", async () =>
        {
            var p = Profile(strongPeriod: 1); await preferences.SaveAsync(p, 0); await preferences.SaveAsync(Profile(2, 1), 0);
            var plan = await editor.PreviewAsync(p with { MaxOtherLectureChanges = 2 }); Assert(plan.CanSave, plan.Message);
            Assert(plan.PreferenceMeasurements.Count == 2 && plan.PreferenceMeasurements.All(m => m.AfterStronglyPreferredMinutes == 60), Json(plan.PreferenceMeasurements));
            Assert(plan.PreferenceMeasurements.Sum(m => m.AfterPenalty) == 4);
        });
        await Check("Three-day plan needs enough lectures and never fabricates attendance", async () => { var p = Profile(attendance: ["Sunday", "Tuesday", "Wednesday"]); await preferences.SaveAsync(p, 0); Assert(!(await editor.PreviewAsync(p)).CanSave); });
        await Check("Three-day plan with three lectures is honored", async () => { await using (var db = factory()) { var extra = Copy(initial[0]); extra.ScheduleID = 0; extra.DayOfWeek = "Tuesday"; extra.TimeSlotID = 3; db.Schedules.Add(extra); await db.SaveChangesAsync(); } var p = Profile(attendance: ["Sunday", "Tuesday", "Wednesday"]); await preferences.SaveAsync(p, 0); var plan = await editor.PreviewAsync(p); Assert(plan.CanSave && plan.Changes.Select(c => c.NewDay).ToHashSet().SetEquals(p.SelectedDays)); });
        await Check("Stale profile updates and stale schedule previews are rejected", async () => { var saved = await preferences.SaveAsync(Profile(), 0); var plan = await editor.PreviewAsync(Profile()); await preferences.SaveAsync(Profile(strongPeriod: 1), saved.Revision); await Reject(() => preferences.SaveAsync(Profile(), saved.Revision)); await Reject(() => editor.SaveAsync(plan, true)); });
        await Check("Repeated searches are deterministic and canceled operations do not save", async () => { var p = Profile(); await preferences.SaveAsync(p, 0); var a = await editor.PreviewAsync(p); var b = await editor.PreviewAsync(p); Assert(Json(a.Changes) == Json(b.Changes)); string before = Json(await Rows()); using var cts = new CancellationTokenSource(); cts.Cancel(); await Reject(() => editor.PreviewAsync(p, token: cts.Token)); await Reject(() => editor.SaveAsync(a, true, cts.Token)); Assert(Json(await Rows()) == before); });
        await Check("Generation actively applies saved matrices on a blank timetable", async () => { await preferences.SaveAsync(Profile(), 0); await using (var db = factory()) await db.Schedules.ExecuteDeleteAsync(); await new ScheduleService().GenerateAsync(); var rows = (await Rows()).Where(s => s.FacultyMemberID == 1).ToList(); Assert(rows.Count == 4 && rows.Count(s => s.TimeSlotID == 3) == 2, Json(rows)); Assert(rows.Select(s => s.DayOfWeek).ToHashSet().SetEquals(new[] { "Sunday", "Wednesday" })); });
        await Check("SQL keys reject duplicate cells and invalid enum values", async () => { await preferences.SaveAsync(Profile(), 0); await using (var db = factory()) { db.FacultyPeriodPreferences.Add(new() { FacultyMemberID = 1, SemesterNumber = 1, DayOfWeek = "Sunday", TimeSlotID = 1, Level = 1 }); await Reject(async () => await db.SaveChangesAsync()); } await using var invalid = factory(); var cell = await invalid.FacultyPeriodPreferences.FirstAsync(); cell.Level = 9; await Reject(async () => await invalid.SaveChangesAsync()); });
        await Check("Imported timetable repair enforces saved period preferences and attendance", async () =>
        {
            var p = Profile() with { PeriodPreferences = Profile().PeriodPreferences.Select(c => c.TimeSlotId == 1 ? c with { Level = FacultyPreferenceLevel.Prohibited } : c).ToArray() };
            await preferences.SaveAsync(p, 0);
            var table = new DataTable("Timetable"); foreach (string column in new[] { "Day", "TimeSlot", "Subject", "Faculty", "Classroom", "Section", "LectureType", "SemesterNumber", "Locked", "ScheduleID" }) table.Columns.Add(column);
            foreach (var row in initial.Where(s => s.FacultyMemberID == 1)) table.Rows.Add(row.DayOfWeek, "#1", "#" + row.SubjectID, "#1", "#1", "#1", "Theory", "1", "false", row.ScheduleID.ToString());
            var importer = new ExcelImportService(factory); var validated = await importer.ValidateAsync(ExcelImportMode.RepairTimetable, [new("Timetable", table, [2, 3])]);
            Assert(validated.IsValid, Json(validated.Issues)); var repaired = await importer.RepairTimetableAsync(validated, 5);
            Assert(repaired.IsValid && repaired.GenerationCompleted, Json(repaired.Issues));
            Assert(repaired.Lectures.All(r => r.Schedule.TimeSlotID != 1) && repaired.Lectures.Select(r => r.Schedule.DayOfWeek).ToHashSet().SetEquals(p.SelectedDays));
            await importer.SaveAsync(repaired, true);
        });
        await Check("Whole-section practical and theory use compatible rooms inside the same attendance plan", async () =>
        {
            await using (var db = factory())
            {
                db.Branches.Add(new() { BranchID = 8, BranchName = "Computer Science" }); db.StudyYears.Add(new() { StudyYearID = 4, YearName = "Fourth Year" });
                db.Sections.Add(new() { SectionID = 4, SectionName = "A", StudyYearID = 4, BranchID = 8, StudentCount = 30 }); db.Classrooms.Add(new() { ClassroomID = 3, ClassroomNumber = "مختبر", RoomType = "Lab", Capacity = 40 });
                foreach (int id in new[] { 5, 6 }) { db.Subjects.Add(new() { SubjectID = id, SubjectName = "عملي " + id, StudyYearID = 4, BranchID = 8, SemesterNumber = 1, PracticalHours = 2, CreditUnits = 2, RequirementType = "Core" }); db.FacultyMemberSubjects.Add(new() { FacultyMemberID = 1, SubjectID = id }); }
                await db.SaveChangesAsync(); foreach (int id in new[] { 5, 6 }) db.Schedules.Add(new() { SubjectID = id, FacultyMemberID = 1, ClassroomID = 3, SectionID = 4, BranchID = 8, StudyYearID = 4, SemesterNumber = 1, LectureType = "Practical", DayOfWeek = id == 5 ? "Sunday" : "Wednesday", TimeSlotID = 2 }); await db.SaveChangesAsync();
            }
            try { await preferences.SaveAsync(Profile(), 0); var plan = await editor.PreviewAsync(Profile()); Assert(plan.CanSave, plan.Message); Assert(plan.Changes.Where(c => c.LectureType == "Practical").All(c => c.NewRoom == "مختبر")); await editor.SaveAsync(plan, true); }
            finally { await using var db = factory(); await db.Schedules.Where(s => s.SubjectID >= 5).ExecuteDeleteAsync(); await db.FacultyMemberSubjects.Where(s => s.SubjectID >= 5).ExecuteDeleteAsync(); await db.Subjects.Where(s => s.SubjectID >= 5).ExecuteDeleteAsync(); await db.Sections.Where(s => s.SectionID == 4).ExecuteDeleteAsync(); await db.StudyYears.Where(s => s.StudyYearID == 4).ExecuteDeleteAsync(); await db.Branches.Where(b => b.BranchID == 8).ExecuteDeleteAsync(); await db.Classrooms.Where(c => c.ClassroomID == 3).ExecuteDeleteAsync(); }
        });
        await DayPreferenceChecksAsync();
        await SectionDayLimitChecksAsync();
        await AlternativesChecksAsync();
        await Reset(); await preferences.SaveAsync(Profile(), 0);
    }

    internal static async Task Restore(AppDbContext db)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Schedules] ON");
        db.Schedules.AddRange(initial.Select(Copy)); await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Schedules] OFF"); await tx.CommitAsync();
    }
    static IEnumerable<Control> Descendants(Control root) { foreach (Control c in root.Controls) { yield return c; foreach (var child in Descendants(c)) yield return child; } }
    static void Ui()
    {
        Reset().GetAwaiter().GetResult();
        preferences.SaveAsync(DayProfile(), 0).GetAwaiter().GetResult();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); Application.EnableVisualStyles();
        using var form = new FacultyPreferencesForm(editor);
        form.Shown += async (_, _) =>
        {
            try
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var save = Descendants(form).OfType<Button>().Single(b => b.Name == "savePreferences");
                var checkbox = Descendants(form).OfType<CheckBox>().Single(c => c.Name == "enableDayPreferences");
                while (!save.Enabled || Descendants(form).OfType<ComboBox>().Count(c => c.Name.StartsWith("dayPreference_")) != 5)
                { await Task.Delay(50); if (timer.Elapsed.TotalSeconds > 30) throw new Exception("UI loading timed out"); }
                var choices = Descendants(form).OfType<ComboBox>().Where(c => c.Name.StartsWith("dayPreference_")).ToList();
                Assert(choices.Count == 5 && choices.All(c => c.Items.Count == 3));
                Assert(!Descendants(form).Any(c => c.Name is "preferenceMatrix" or "attendanceCount" or "otherLectureMoves"));
                foreach (var size in new[] { new Size(800, 680), new Size(1180, 820) })
                {
                    form.Size = size; await Task.Delay(120);
                    foreach (var control in Descendants(form).Where(c => c is Button || c.Name.StartsWith("dayPreference_")))
                    { var bounds = form.RectangleToClient(control.RectangleToScreen(control.ClientRectangle)); Assert(form.ClientRectangle.Contains(bounds), "Clipped control " + control.Name); }
                    var grid = Descendants(form).OfType<DataGridView>().Single(g => g.Name == "preferenceCurrent");
                    Assert(grid.Height >= 125, $"Timetable clipped: {grid.Height} pixels at {size}");
                    using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(output, $"preferences-days-{size.Width}.png"));
                }
                foreach (var choice in choices) choice.SelectedValue = choice.Name.EndsWith("Tuesday") ? FacultyPreferenceLevel.StronglyPreferred : FacultyPreferenceLevel.Preferred;
                checkbox.Checked = true;
                save.PerformClick(); timer.Restart();
                while (!save.Enabled) { await Task.Delay(50); if (timer.Elapsed.TotalSeconds > 30) throw new Exception("UI save timed out"); }
                var profile = (await preferences.GetAsync(1, 1))!.Settings;
                Assert(profile.UseDayPreferences && profile.DayPreferences.Count == 5 && profile.PeriodPreferences.Count == 0);
                Assert((await Rows()).Where(s => s.FacultyMemberID == 1).All(s => s.DayOfWeek == "Tuesday"));
                Assert(form.HasSavedChanges);
                Assert(Descendants(form).OfType<DataGridView>().Single(g => g.Name == "preferenceChanges").Rows.Count > 0);
                using (var updated = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(updated, new Rectangle(Point.Empty, updated.Size)); updated.Save(Path.Combine(output, "preferences-days-updated.png")); }
                checkbox.Checked = false; save.PerformClick(); timer.Restart();
                while (!save.Enabled) { await Task.Delay(50); if (timer.Elapsed.TotalSeconds > 30) throw new Exception("UI clear timed out"); }
                Assert(await preferences.GetAsync(1, 1) is null && choices.All(c => !c.Enabled));
                results.Add(new { name = "Day-only Windows UI at 800 and 1180, automatic timetable update and optional preferences", passed = true });
            }
            catch (Exception ex) { failed++; results.Add(new { name = "UI", passed = false, error = ex.ToString() }); Console.WriteLine("FAIL UI " + ex); }
            finally { form.Close(); }
        };
        Application.Run(form);
    }
}
