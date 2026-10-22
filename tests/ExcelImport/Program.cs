using System.Data;
using System.Collections;
using System.Reflection;
using System.Text.Json;
using ClosedXML.Excel;
using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using University_Timetable_and_Classroom_Management_System;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

internal static class Program
{
    private static readonly List<object> checks = [];
    private static readonly string Output = Path.GetFullPath("output/excel-import-verification");
    private static int failures;
    private static ExcelImportService service = null!;
    private static Func<AppDbContext> factory = null!;
    private static readonly FailureInterceptor fault = new();
    [STAThread]
    private static void Main(string[] args)
    {
        Directory.CreateDirectory(Output);
        if (args.Contains("ui")) { VerifyUi(); return; }
        RunAsync().GetAwaiter().GetResult();
        Check("Subject editor removes requirement selector and column in Designer layout", () =>
        {
            using var editor = new SubjectsForm();
            IEnumerable<Control> Walk(Control root) { foreach (Control child in root.Controls) { yield return child; foreach(var nested in Walk(child)) yield return nested; } }
            Assert(!Walk(editor).Any(c => c.Name.Contains("RequirementType", StringComparison.OrdinalIgnoreCase)));
            var grid = Walk(editor).OfType<DataGridView>().Single();
            Assert(!grid.Columns.Cast<DataGridViewColumn>().Any(c => c.DataPropertyName == "RequirementType"));
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(Output, "results.json"), JsonSerializer.Serialize(new { Count = checks.Count, Failures = failures, Checks = checks }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"SUMMARY: {checks.Count} checks, {failures} failures");
        Environment.ExitCode = failures == 0 ? 0 : 1;
    }
    private static void Assert(bool condition, string message = "Assertion failed")
    { if (!condition) throw new Exception(message); }
    private static async Task Check(string name, Func<Task> action)
    {
        try { await action(); checks.Add(new { Name = name, Passed = true }); Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failures++; checks.Add(new { Name = name, Passed = false, Error = ex.ToString() }); Console.WriteLine("FAIL " + name + ": " + ex.Message); }
    }
    private static async Task Reject(Func<Task> operation)
    {
        try { await operation(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return; }
        throw new Exception("Expected rejection.");
    }
    private static ExcelSheetData Sheet(string name, string[] columns, params object[][] rows)
    {
        var table = new DataTable(name);
        foreach (string c in columns) table.Columns.Add(c, typeof(string));
        foreach (var row in rows) table.Rows.Add(row.Select(x => Convert.ToString(x, System.Globalization.CultureInfo.InvariantCulture) ?? "").Cast<object>().ToArray());
        return new(name, table, Enumerable.Range(2, rows.Length).ToList());
    }
    private static ExcelSheetData Timetable(params object[][] rows) => Sheet("Timetable",
        ["Day", "TimeSlot", "Subject", "Faculty", "Classroom", "Section", "LectureType", "SemesterNumber", "GroupName", "Locked"], rows);
    private static List<ExcelSheetData> Academic() =>
    [
        Sheet("StudyYears", ["StudyYearID", "YearName"], [1, "First Year"]),
        Sheet("Branches", ["BranchID", "BranchName"], [1, "Computer Science"]),
        Sheet("FacultyMembers", ["FacultyMemberID", "FullName", "AcademicTitle"], [1, "أحمد محمد", "Lecturer"], [2, "Sara Ali", "Professor"]),
        Sheet("Classrooms", ["ClassroomID", "ClassroomNumber", "Capacity", "RoomType"], [1, "Room 101", 60, "Lecture"], [2, "Lab 1", 30, "Lab"]),
        Sheet("TimeSlots", ["TimeSlotID", "StartTime", "EndTime"], [1, "08:30", "10:00"], [2, "10:10", "11:40"]),
        Sheet("Sections", ["SectionID", "SectionName", "StudentCount", "StudyYearID"], [1, "A", 40, 1], [2, "B", 40, 1]),
        Sheet("Subjects", ["SubjectID", "SubjectName", "StudyYearID", "SemesterNumber", "TheoreticalHours", "PracticalHours", "CreditUnits", "RequirementType"],
            [1, "الرياضيات", 1, 1, 2, 0, 2, "Core"], [2, "Programming", 1, 1, 0, 2, 2, "Core"]),
        Sheet("FacultyMemberSubjects", ["FacultyMemberID", "SubjectID"], [1, 1], [2, 2])
    ];
    private static async Task<string> Snapshot()
    {
        await using var db = factory();
        return JsonSerializer.Serialize(await db.Schedules.OrderBy(s => s.ScheduleID).Select(s => new
        { s.ScheduleID, s.SubjectID, s.FacultyMemberID, s.ClassroomID, s.TimeSlotID, s.DayOfWeek, s.GroupName, s.SectionID, s.SemesterNumber }).ToListAsync());
    }
    private static IReadOnlyList<ExcelSheetData> RoundTrip(IReadOnlyList<ExcelSheetData> sheets, string name)
    {
        string path = Path.Combine(Output, name + ".xlsx");
        using var book = new XLWorkbook();
        foreach (var source in sheets)
        {
            var sheet = book.AddWorksheet(source.Name);
            for (int c = 0; c < source.Table.Columns.Count; c++) sheet.Cell(1, c + 1).Value = source.Table.Columns[c].ColumnName;
            for (int r = 0; r < source.Table.Rows.Count; r++)
                for (int c = 0; c < source.Table.Columns.Count; c++) sheet.Cell(r + 2, c + 1).Value = source.Table.Rows[r][c].ToString();
        }
        book.SaveAs(path);
        return ExcelWorkbookReader.Read(path).Sheets;
    }
    private static async Task RunAsync()
    {
        string database = "UniversityTimetable_ExcelImport_" + Guid.NewGuid().ToString("N");
        var connection = new SqlConnectionStringBuilder
        {
            DataSource = Environment.GetEnvironmentVariable("UNIVERSITY_TIMETABLE_TEST_SERVER") ?? "localhost",
            InitialCatalog = database, IntegratedSecurity = true, Encrypt = false, TrustServerCertificate = true
        };
        Assert(database.StartsWith("UniversityTimetable_ExcelImport_", StringComparison.Ordinal));
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection.ConnectionString).AddInterceptors(fault).Options;
        factory = () => new AppDbContext(options);
        service = new(factory);
        await using (var db = factory()) await db.Database.EnsureCreatedAsync();
        File.WriteAllText(Path.Combine(Output, "isolated-database.txt"), database);
        var oldConnection = Environment.GetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName);
        Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, connection.ConnectionString);
        try
        {
            await Check("Additive legacy lock migration preserves existing rows and defaults to unlocked", async () =>
            {
                var legacyConnection = new SqlConnectionStringBuilder(connection.ConnectionString) { InitialCatalog = database + "_Legacy" };
                await using (var master = new SqlConnection(new SqlConnectionStringBuilder(connection.ConnectionString) { InitialCatalog = "master" }.ConnectionString))
                {
                    await master.OpenAsync();
                    await using var create = new SqlCommand($"CREATE DATABASE [{legacyConnection.InitialCatalog}]", master);
                    await create.ExecuteNonQueryAsync();
                }
                await using var legacy = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(legacyConnection.ConnectionString).Options);
                await legacy.Database.ExecuteSqlRawAsync("CREATE TABLE [dbo].[Schedules] ([ScheduleID] int PRIMARY KEY, [Tag] nvarchar(20)); INSERT INTO [dbo].[Schedules] VALUES (1, N'Preserve me');");
                await ScheduleLockSchema.EnsureAsync(legacy); await ScheduleLockSchema.EnsureAsync(legacy);
                Assert(await legacy.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM [Schedules] WHERE [ScheduleID] = 1 AND [Tag] = N'Preserve me' AND [IsLocked] = 0").SingleAsync() == 1);
            });
            await Check("All three templates reopen and contain required headers and ignored examples", () =>
            {
                foreach (var mode in Enum.GetValues<ExcelImportMode>())
                {
                    string path = Path.Combine(Output, mode + "-template.xlsx");
                    ExcelImportTemplates.Create(path, mode);
                    var loaded = ExcelWorkbookReader.Read(path);
                    Assert(loaded.Sheets.Count == ExcelImportSchema.For(mode).Count());
                    Assert(loaded.Sheets.All(s => s.Table.Rows.Count == 0));
                    foreach (var schema in ExcelImportSchema.For(mode))
                        Assert(schema.Required.All(c => loaded.Sheets.Single(s => s.Name == schema.Name).Table.Columns.Contains(c)));
                }
                return Task.CompletedTask;
            });
            await Check("Workbook open for editing can be imported read-only without changing saved bytes", () =>
            {
                string path = Path.Combine(Output, "open-for-editing.xlsx");
                RoundTrip(Academic(), "open-for-editing");
                byte[] before = File.ReadAllBytes(path);
                using (var editing = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                {
                    var loaded = ExcelWorkbookReader.Read(path);
                    Assert(loaded.Sheets.Single(s => s.Name == "FacultyMembers").Table.Rows.Count == 2);
                }
                Assert(before.SequenceEqual(File.ReadAllBytes(path)), "Reader changed the workbook");
                return Task.CompletedTask;
            });
            await Check("Exclusive workbook locks report an actionable Arabic message", () =>
            {
                string path = Path.Combine(Output, "open-for-editing.xlsx");
                using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                bool rejected = false;
                try { ExcelWorkbookReader.Read(path); }
                catch (IOException ex) { rejected = ex.Message.Contains("احفظ الملف") && ex.InnerException is IOException; }
                Assert(rejected, "An exclusive lock must not be bypassed or reported as an unknown error");
                return Task.CompletedTask;
            });
            await Check("Invalid extension, malformed workbook, formula, duplicate headers and Excel row numbers", async () =>
            {
                await Reject(() => Task.Run(() => ExcelWorkbookReader.Read("bad.xls")));
                var bad = Path.Combine(Output, "malformed.xlsx"); File.WriteAllText(bad, "not a workbook");
                bool rejected = false; try { ExcelWorkbookReader.Read(bad); } catch { rejected = true; } Assert(rejected);
                string path = Path.Combine(Output, "reader-fixture.xlsx");
                using (var book = new XLWorkbook())
                {
                    var sheet = book.AddWorksheet("FacultyMembers"); sheet.Cell(1, 1).Value = "FacultyMemberID *"; sheet.Cell(1, 2).Value = "FullName";
                    sheet.Cell(4, 1).Value = 1; sheet.Cell(4, 2).Value = "أحمد محمد"; book.SaveAs(path);
                }
                var read = ExcelWorkbookReader.Read(path).Sheets.Single();
                Assert(read.SourceRows.Single() == 4 && (string)read.Table.Rows[0][1] == "أحمد محمد");
                using (var book = new XLWorkbook(path)) { book.Worksheet(1).Cell(4, 1).FormulaA1 = "1+1"; book.Save(); }
                await Reject(() => Task.Run(() => ExcelWorkbookReader.Read(path)));
                using (var book = new XLWorkbook(path)) { book.Worksheet(1).Cell(1, 2).Value = "FacultyMemberID"; book.Save(); }
                await Reject(() => Task.Run(() => ExcelWorkbookReader.Read(path)));
            });
            await Check("Missing columns and invalid academic values are reported without writes", async () =>
            {
                var p = await service.ValidateAsync(ExcelImportMode.AcademicData, [Sheet("FacultyMembers", ["FullName"], ["Ahmed"])]);
                Assert(!p.IsValid && p.Issues.Any(i => i.Column == "FacultyMemberID"));
                await using var db = factory(); Assert(!await db.FacultyMembers.AnyAsync());
            });
            await Check("Academic preview performs no writes; dependency-ordered atomic import preserves Arabic", async () =>
            {
                var p = await service.ValidateAsync(ExcelImportMode.AcademicData, RoundTrip(Academic().AsEnumerable().Reverse().ToList(), "valid-academic"));
                Assert(p.IsValid, string.Join("; ", p.Issues));
                await using (var before = factory()) Assert(!await before.Subjects.AnyAsync());
                var saved = await service.SaveAsync(p, true); Assert(saved.Added == 14, saved.ToString());
                await using var db = factory(); Assert((await db.FacultyMembers.FindAsync(1))!.FullName == "أحمد محمد");
            });
            await Check("Academic re-import is idempotent", async () =>
            {
                var p = await service.ValidateAsync(ExcelImportMode.AcademicData, Academic());
                Assert(p.IsValid); var saved = await service.SaveAsync(p, true); Assert(saved.Added == 0 && saved.Unchanged == 14);
            });
            await Check("Duplicate faculty and explicit update selection", async () =>
            {
                var duplicate = Sheet("FacultyMembers", ["FacultyMemberID", "FullName"], [3, "أحمد محمد"]);
                Assert(!(await service.ValidateAsync(ExcelImportMode.AcademicData, [duplicate])).IsValid);
                var change = Sheet("FacultyMembers", ["FacultyMemberID", "FullName", "AcademicTitle"], [1, "أحمد محمد", "Professor"]);
                Assert(!(await service.ValidateAsync(ExcelImportMode.AcademicData, [change])).IsValid);
                var p = await service.ValidateAsync(ExcelImportMode.AcademicData, [change], true); Assert(p.IsValid);
                await Reject(() => service.SaveAsync(p, false));
                Assert((await service.SaveAsync(p, true)).Updated == 1);
            });
            await Check("Unknown subject and ambiguous subject names", async () =>
            {
                var bad = await service.ValidateAsync(ExcelImportMode.CompleteTimetable,
                    [Timetable(["Sunday", "#1", "missing", "#1", "#1", "#1", "Theory", 1, "", true])]);
                Assert(!bad.IsValid && bad.Issues.Any(i => i.Message.Contains("not found")));
                await using (var db = factory())
                {
                    db.StudyYears.Add(new StudyYear { StudyYearID = 2, YearName = "Second Year" });
                    db.Subjects.Add(new Subject { SubjectID = 3, SubjectName = "الرياضيات", StudyYearID = 2, SemesterNumber = 1, TheoreticalHours = 1, RequirementType = "Core" });
                    await db.SaveChangesAsync();
                }
                var ambiguous = await service.ValidateAsync(ExcelImportMode.CompleteTimetable,
                    [Timetable(["Sunday", "#1", "الرياضيات", "#1", "#1", "#1", "Theory", 1, "", true])]);
                Assert(!ambiguous.IsValid && ambiguous.Issues.Any(i => i.Message.Contains("ambiguous")));
                await using (var db = factory()) { db.Subjects.Remove((await db.Subjects.FindAsync(3))!); await db.SaveChangesAsync(); }
            });
            await Check("Ambiguous faculty names in a legacy database require an explicit ID", async () =>
            {
                // Isolated legacy fixture: old installations may predate the unique name index.
                await using var db = factory();
                await db.Database.ExecuteSqlRawAsync("DROP INDEX [IX_FacultyMembers_FullName] ON [FacultyMembers]");
                try
                {
                    db.FacultyMembers.Add(new FacultyMember { FacultyMemberID = 99, FullName = "أحمد محمد" }); await db.SaveChangesAsync();
                    var p = await service.ValidateAsync(ExcelImportMode.CompleteTimetable,
                        [Timetable(["Sunday", "#1", "#1", "أحمد محمد", "#1", "#1", "Theory", 1, "", true])]);
                    Assert(!p.IsValid && p.Issues.Any(i => i.Message.Contains("Faculty") && i.Message.Contains("ambiguous")));
                }
                finally
                {
                    await db.FacultyMembers.Where(x => x.FacultyMemberID == 99).ExecuteDeleteAsync();
                    await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX [IX_FacultyMembers_FullName] ON [FacultyMembers] ([FullName])");
                }
            });
            await Check("Arabic column mapping and omitted optional fields preserve existing values", async () =>
            {
                var sheet = Sheet("FacultyMembers", ["معرف", "اسم"], [1, "أحمد محمد"])
                    .MapColumns(new Dictionary<string, string> { ["معرف"] = "FacultyMemberID", ["اسم"] = "FullName" });
                var p = await service.ValidateAsync(ExcelImportMode.AcademicData, [sheet]);
                Assert(p.IsValid && p.Changes.Single().Action == "Unchanged");
                Assert(sheet.MapColumns(new Dictionary<string, string> { ["FullName"] = "" }).Table.Columns.Count == 1);
            });
            await Check("Practical lab suitability, group and capacity validation", async () =>
            {
                foreach (var row in new object[][]
                {
                    ["Sunday", "#1", "#2", "#2", "#1", "#1", "Practical", 1, "A1", true],
                    ["Sunday", "#1", "#2", "#2", "#2", "#1", "Practical", 1, "", true],
                    ["Sunday", "#1", "#1", "#1", "#2", "#1", "Theory", 1, "", true]
                }) Assert(!(await service.ValidateAsync(ExcelImportMode.CompleteTimetable, [Timetable(row)])).IsValid);
            });
            await Check("Faculty, classroom and student conflicts within workbook", async () =>
            {
                await using (var db = factory())
                {
                    db.Classrooms.Add(new Classroom { ClassroomID = 3, ClassroomNumber = "Room 102", Capacity = 60, RoomType = "Lecture" });
                    db.FacultyMemberSubjects.Add(new FacultyMemberSubject { FacultyMemberID = 2, SubjectID = 1 }); await db.SaveChangesAsync();
                }
                var first = new object[] { "Sunday", "#1", "#1", "#1", "#1", "#1", "Theory", 1, "", true };
                var roomClash = new object[] { "Sunday", "#1", "#1", "#2", "#1", "#2", "Theory", 1, "", true };
                var roomResult = await service.ValidateAsync(ExcelImportMode.CompleteTimetable, [Timetable(first, roomClash)]);
                Assert(!roomResult.IsValid && roomResult.Issues.Any(i => i.Message.StartsWith("Classroom conflict")));
                var facultyClash = new object[] { "Sunday", "#1", "#1", "#1", "#3", "#2", "Theory", 1, "", true };
                var facultyResult = await service.ValidateAsync(ExcelImportMode.CompleteTimetable, [Timetable(first, facultyClash)]);
                Assert(!facultyResult.IsValid && facultyResult.Issues.Any(i => i.Message.StartsWith("Faculty conflict")));
                var studentClash = new object[] { "Sunday", "#1", "#2", "#2", "#2", "#1", "Practical", 1, "A1", true };
                var p = await service.ValidateAsync(ExcelImportMode.CompleteTimetable, [Timetable(first, studentClash)]);
                Assert(!p.IsValid && p.Issues.Any(i => i.Message.Contains("Section/group")));
            });
            await Check("Insufficient lab capacity and duplicate weekly demand", async () =>
            {
                await using (var db = factory()) { var lab = await db.Classrooms.FindAsync(2); lab!.Capacity = 1; await db.SaveChangesAsync(); }
                var p = await service.ValidateAsync(ExcelImportMode.CompleteTimetable,
                    [Timetable(["Sunday", "#1", "#2", "#2", "#2", "#1", "Practical", 1, "A1", true])]);
                Assert(!p.IsValid && p.Issues.Any(i => i.Message.Contains("capacity")));
                await using (var db = factory()) { var lab = await db.Classrooms.FindAsync(2); lab!.Capacity = 30; await db.SaveChangesAsync(); }
                p = await service.ValidateAsync(ExcelImportMode.CompleteTimetable,
                    [Timetable(["Sunday", "#1", "#1", "#1", "#1", "#1", "Theory", 1, "", true],
                        ["Monday", "#1", "#1", "#1", "#1", "#1", "Theory", 1, "", true])]);
                Assert(!p.IsValid && p.Issues.Any(i => i.Message.Contains("weekly requirement")));
            });
            var partial = Timetable(["الأحد", "08:30", "#1", "أحمد محمد", "Room 101", "#1", "نظري", 1, "", true]);
            await Check("Full timetable import, saved conflicts and safe timetable re-import", async () =>
            {
                var p = await service.ValidateAsync(ExcelImportMode.CompleteTimetable, [partial]); Assert(p.IsValid, string.Join("; ", p.Issues));
                Assert((await service.SaveAsync(p, true)).Added == 1);
                p = await service.ValidateAsync(ExcelImportMode.CompleteTimetable, [partial]); Assert(p.IsValid);
                Assert((await service.SaveAsync(p, true)).Unchanged == 1);
                Assert(!(await service.ValidateAsync(ExcelImportMode.CompleteTimetable,
                    [Timetable(["Sunday", "#1", "#1", "#1", "#1", "#2", "Theory", 1, "", true])])).IsValid);
            });
            await Check("Hybrid generation preserves saved and imported locked lectures and handles partial practical pairs", async () =>
            {
                var input = Timetable(["Monday", "#1", "#2", "#2", "#2", "#1", "Practical", 1, "A1", true]);
                string before = await Snapshot();
                var p = await service.ValidateAsync(ExcelImportMode.Hybrid, [input]); Assert(p.IsValid, string.Join("; ", p.Issues));
                Assert(p.RemainingLectureCount == 4, "Unexpected remaining lecture count: " + p.RemainingLectureCount);
                var generated = await service.GenerateRemainingAsync(p); Assert(generated.IsValid, string.Join("; ", generated.UnscheduledLectures));
                Assert(generated.GenerationCompleted && generated.RemainingLectureCount == 0);
                Assert(generated.Lectures.Count(l => l.Locked && l.Schedule.SubjectID == 2 && l.Schedule.GroupName == "A1" && l.Schedule.DayOfWeek == "Monday" && l.Schedule.TimeSlotID == 1) == 1);
                Assert(before == await Snapshot(), "Generation wrote to the database.");
                await Reject(() => service.SaveAsync(p, true));
                Assert((await service.SaveAsync(generated, true)).Added == 5);
                Assert((await Snapshot()).Contains("Sunday"));
            });
            await Check("Preview edits and changed database invalidate the save", async () =>
            {
                var p = await service.ValidateAsync(ExcelImportMode.CompleteTimetable, [partial]); Assert(p.IsValid);
                p.Lectures[0].Schedule.DayOfWeek = "Tuesday"; await Reject(() => service.SaveAsync(p, true));
                p = await service.ValidateAsync(ExcelImportMode.CompleteTimetable, [partial]);
                await using (var db = factory()) { db.Branches.Add(new Branch { BranchID = 2, BranchName = "New branch" }); await db.SaveChangesAsync(); }
                await Reject(() => service.SaveAsync(p, true));
            });
            await Check("Mid-import failure rolls back preceding academic writes", async () =>
            {
                var input = Sheet("FacultyMembers", ["FacultyMemberID", "FullName"], [10, "Rollback 1"], [11, "Rollback 2"]);
                var p = await service.ValidateAsync(ExcelImportMode.AcademicData, [input]); Assert(p.IsValid);
                fault.FailOnSave = 2; fault.Saves = 0;
                try { await Reject(() => service.SaveAsync(p, true)); }
                finally { fault.FailOnSave = 0; }
                await using var db = factory(); Assert(!await db.FacultyMembers.AnyAsync(f => f.FacultyMemberID >= 10));
            });
            await Check("Original generation still works after import", async () =>
            {
                await using var beforeDb = factory();
                var locks = await beforeDb.Schedules.AsNoTracking().Where(s => s.IsLocked).OrderBy(s => s.ScheduleID).ToListAsync();
                string before = JsonSerializer.Serialize(locks);
                var result = await new ScheduleService().GenerateAsync(); Assert(result is not null);
                await using var db = factory(); Assert(await db.Schedules.CountAsync() == 6);
                Assert(before == JsonSerializer.Serialize(await db.Schedules.AsNoTracking().Where(s => s.IsLocked).OrderBy(s => s.ScheduleID).ToListAsync()), "Persisted locks moved or changed IDs.");
            });
            await Check("Lock schema/view upgrade is idempotent and ordinary editing preserves lock metadata", async () =>
            {
                string before = await Snapshot();
                await using (var db = factory()) { await ScheduleLockSchema.EnsureAsync(db); await ScheduleLockSchema.EnsureAsync(db); }
                Assert(before == await Snapshot());
                var saved = (await new ScheduleService().GetAllAsync()).First(s => s.IsLocked);
                saved.IsLocked = false;
                // Strip navigation graphs, as the ordinary editor does.
                saved.Subject = null!; saved.FacultyMember = null!; saved.Classroom = null!; saved.Section = null; saved.TimeSlot = null!; saved.StudyYear = null; saved.Branch = null;
                await new ScheduleService().UpdateAsync(saved);
                await using var check = factory(); Assert((await check.Schedules.FindAsync(saved.ScheduleID))!.IsLocked);
                await new DatabaseMaintenanceService().ApplyPendingFixesAsync();
                var details = await new ScheduleService().GetScheduleDetailsAsync();
                var persisted = await check.Schedules.AsNoTracking().ToDictionaryAsync(s => s.ScheduleID);
                Assert(details.Count == persisted.Count && details.All(d => d.IsLocked == persisted[d.ScheduleID].IsLocked),
                    "The schedule details view must expose IsLocked after startup upgrade.");
            });
            await Check("Repeated generation preserves unlocked placements and stable IDs", async () =>
            {
                await using var db = factory();
                var lockedIds = await db.Schedules.Where(s => s.IsLocked).Select(s => s.ScheduleID).ToListAsync();
                await db.Schedules.ExecuteUpdateAsync(set => set.SetProperty(s => s.IsLocked, false));
                try
                {
                    string before = await Snapshot();
                    await new ScheduleService().GenerateAsync();
                    Assert(before == await Snapshot(), "Unlocked lectures were moved or recreated.");
                    await new ScheduleService().GenerateAsync();
                    Assert(before == await Snapshot(), "Repeated generation changed the timetable.");
                    Assert(!await db.Schedules.AnyAsync(s => s.IsLocked), "Preservation must not set permanent locks.");
                }
                finally
                {
                    await db.Schedules.Where(s => lockedIds.Contains(s.ScheduleID))
                        .ExecuteUpdateAsync(set => set.SetProperty(s => s.IsLocked, true));
                }
            });
            await Check("Cancelled saving rolls back rows already written", async () =>
            {
                var p = await service.ValidateAsync(ExcelImportMode.AcademicData,
                    [Sheet("FacultyMembers", ["FacultyMemberID", "FullName"], [20, "Cancel 1"], [21, "Cancel 2"])]);
                Assert(p.IsValid);
                using var cancel = new CancellationTokenSource();
                fault.CancelAfterSave = cancel;
                bool cancelled = false;
                try { await service.SaveAsync(p, true, token: cancel.Token); }
                catch (OperationCanceledException) { cancelled = true; }
                finally { fault.CancelAfterSave = null; }
                Assert(cancelled);
                await using var db = factory(); Assert(!await db.FacultyMembers.AnyAsync(f => f.FacultyMemberID >= 20));
            });
            await Check("Impossible hybrid reports unresolved lectures and preserves all locks without writes", async () =>
            {
                // Only this isolated fixture is cleared; the application feature never clears schedules.
                await using (var db = factory())
                {
                    await db.Schedules.ExecuteDeleteAsync();
                    var lab = await db.Classrooms.FindAsync(2); lab!.Capacity = 1; await db.SaveChangesAsync();
                }
                var p = await service.ValidateAsync(ExcelImportMode.Hybrid, [partial]); Assert(p.IsValid);
                string before = await Snapshot();
                var generated = await service.GenerateRemainingAsync(p);
                Assert(!generated.IsValid && generated.UnscheduledLectures.Count == 4);
                Assert(generated.Lectures.Count(l => l.Locked && l.Schedule.SubjectID == 1 && l.Schedule.DayOfWeek == "Sunday") == 1);
                Assert(before == await Snapshot()); await Reject(() => service.SaveAsync(generated, true));
            });
            await Check("Repair search cannot evict fixed lectures; state rejects removal and incomplete snapshots", () =>
            {
                const string ns = "University_Timetable_and_Classroom_Management_System.BusinessLayer.";
                var assembly = typeof(ScheduleService).Assembly;
                var generator = assembly.GetType(ns + "ScheduleGenerator")!;
                var requestType = assembly.GetType(ns + "ScheduleGenerationRequest")!;
                var teachingType = assembly.GetType(ns + "SubjectTeachingAssignment")!;
                var subject = new Subject { SubjectID = 1, SubjectName = "Blocked", StudyYearID = 1, SemesterNumber = 1, TheoreticalHours = 2 };
                var section = new Section { SectionID = 1, SectionName = "A", StudyYearID = 1, StudentCount = 40 };
                var teaching = Activator.CreateInstance(teachingType, [subject, new List<int> { 1 }])!;
                var requests = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(requestType))!;
                foreach (var item in (IEnumerable)generator.GetMethod("CreateScheduleRequests", [teachingType, typeof(Section)])!.Invoke(null, [teaching, section])!) requests.Add(item);
                var fixedRows = new List<Schedule>();
                foreach (string day in new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday" })
                foreach (int slot in new[] { 1, 2 }) fixedRows.Add(new Schedule
                {
                    SubjectID = 99, SectionID = 99, FacultyMemberID = 1, ClassroomID = 99,
                    SemesterNumber = 1, DayOfWeek = day, TimeSlotID = slot, IsLocked = true
                });
                var contextType = assembly.GetType(ns + "ScheduleBuildContext")!;
                var context = Activator.CreateInstance(contextType, [new List<Classroom> { new() { ClassroomID = 1, Capacity = 60, RoomType = "Lecture" } },
                    new List<TimeSlot> { new() { TimeSlotID = 1 }, new() { TimeSlotID = 2 } }])!;
                contextType.GetProperty("FixedSchedules")!.SetValue(context, fixedRows);
                contextType.GetProperty("RequirePracticalExchange")!.SetValue(context, false);
                string before = JsonSerializer.Serialize(fixedRows);
                var planned = generator.GetMethod("BuildPlan")!.Invoke(null, [requests, context])!;
                var schedules = (List<Schedule>)planned.GetType().GetProperty("Schedules")!.GetValue(planned)!;
                Assert(schedules.Count == fixedRows.Count && (int)planned.GetType().GetProperty("ConflictCount")!.GetValue(planned)! == 1);
                Assert(before == JsonSerializer.Serialize(schedules));
                var stateType = assembly.GetType(ns + "ScheduleGenerationState")!;
                var plansType = typeof(Dictionary<,>).MakeGenericType(assembly.GetType(ns + "ScheduleDayPlanKey")!, assembly.GetType(ns + "ScheduleDayPlan")!);
                var state = Activator.CreateInstance(stateType, [Activator.CreateInstance(plansType)!])!;
                stateType.GetMethod("SeedFixed")!.Invoke(state, [fixedRows]);
                foreach (var call in new[] { ("RemoveRange", fixedRows), ("ResetTo", new List<Schedule>()) })
                {
                    bool rejected = false;
                    try { stateType.GetMethod(call.Item1)!.Invoke(state, [call.Item2]); }
                    catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException) { rejected = true; }
                    Assert(rejected);
                }
                return Task.CompletedTask;
            });
            await Check("Subject add/update accepts retired requirement values under actual SQL check constraints and preserves linked lessons", async () =>
            {
                await using(var db = factory())
                    await db.Database.ExecuteSqlRawAsync("ALTER TABLE [Subjects] WITH NOCHECK ADD CONSTRAINT [CK_Subject_Requirement_Regression] CHECK ([RequirementType] IN (N'University',N'College',N'Department'))");
                string before = await Snapshot();
                var subjects = new SubjectService();
                var added = await subjects.AddAsync(new Subject { SubjectName = "مادة اختبار", StudyYearID = 1, SemesterNumber = 1, TheoreticalHours = 2, CreditUnits = 2, RequirementType = "University Requirement" });
                Assert(added.RequirementType == "University");
                await using(var db = factory()) await db.Subjects.Where(x => x.SubjectID == added.SubjectID).ExecuteUpdateAsync(set => set.SetProperty(x => x.RequirementType,"College"));
                added.SubjectName = "مادة اختبار معدلة"; added.RequirementType = "Elective";
                await subjects.UpdateAsync(added);
                Assert((await subjects.GetByIdAsync(added.SubjectID))!.RequirementType == "College");
                var linked = (await subjects.GetByIdAsync(1))!;
                string originalName = linked.SubjectName;
                linked.SubjectName += " اختبار"; linked.RequirementType = "Core";
                await subjects.UpdateAsync(linked);
                Assert(before == await Snapshot(), "Editing a subject changed scheduled lectures.");
                linked.SubjectName = originalName; await subjects.UpdateAsync(linked);
                await subjects.DeleteAsync(added.SubjectID);
            });
            await Check("Excel subjects omit requirement field and ignore legacy requirement values without overwriting metadata", async () =>
            {
                var input = Sheet("Subjects", ["SubjectID","SubjectName","StudyYearID","SemesterNumber","TheoreticalHours","PracticalHours","CreditUnits"], [41,"استيراد دون متطلب",1,1,2,0,2]);
                var p = await service.ValidateAsync(ExcelImportMode.AcademicData,[input]); Assert(p.IsValid, string.Join(";",p.Issues));
                Assert((await service.SaveAsync(p,true)).Added == 1);
                await using(var db = factory()) await db.Subjects.Where(x => x.SubjectID == 41).ExecuteUpdateAsync(set => set.SetProperty(x => x.RequirementType,"Department"));
                input.Table.Columns.Add("RequirementType"); input.Table.Rows[0]["RequirementType"] = "University Requirement";
                p = await service.ValidateAsync(ExcelImportMode.AcademicData,[input]); Assert(p.IsValid);
                Assert((await service.SaveAsync(p,true)).Unchanged == 1);
                await using var check = factory(); Assert((await check.Subjects.FindAsync(41))!.RequirementType == "Department");
                Assert(!ExcelImportSchema.Academic.Single(s => s.Name == "Subjects").Columns.Contains("RequirementType"));
                await new SubjectService().DeleteAsync(41);
            });
            await Check("PDF preview and atomic saving reuse import validation and retain page diagnostics", async () =>
            {
                var builder = new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
                var font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);
                var page = builder.AddPage(640, 480);
                page.AddText("FacultyMemberID", 10, new UglyToad.PdfPig.Core.PdfPoint(40, 420), font);
                page.AddText("FullName", 10, new UglyToad.PdfPig.Core.PdfPoint(240, 420), font);
                page.AddText("701", 10, new UglyToad.PdfPig.Core.PdfPoint(40, 390), font);
                page.AddText("PDF import faculty", 10, new UglyToad.PdfPig.Core.PdfPoint(240, 390), font);
                string path = Path.Combine(Output, "faculty-import.pdf"); File.WriteAllBytes(path, builder.Build());
                var read = ImportFileReader.Read(path).Sheets.Single();
                var input = new ExcelSheetData(read.SuggestedSchemaName!, read.Table, read.SourceRows) { SourceName = read.Name };
                var p = await service.ValidateAsync(ExcelImportMode.AcademicData, [input]);
                Assert(p.IsValid, string.Join("; ", p.Issues));
                Assert(p.Changes.Single().Sheet == "صفحة 1" && p.Changes.Single().Row == 2);
                await using (var db = factory()) Assert(!await db.FacultyMembers.AnyAsync(f => f.FacultyMemberID == 701));
                Assert((await service.SaveAsync(p, true)).Added == 1);
                p = await service.ValidateAsync(ExcelImportMode.AcademicData, [input]);
                Assert(p.IsValid && (await service.SaveAsync(p, true)).Unchanged == 1);
                input.Table.Rows[0]["FacultyMemberID"] = "invalid";
                p = await service.ValidateAsync(ExcelImportMode.AcademicData, [input]);
                Assert(!p.IsValid && p.Issues.Single().Sheet == "صفحة 1" && p.Issues.Single().Row == 2);
                await new FacultyMemberService().DeleteAsync(701);
            });
        }
        finally { Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, oldConnection); }
    }
    private static void VerifyUi()
    {
        ApplicationConfiguration.Initialize();
        foreach (int selectedMode in new[] { 0, 1, 2 })
        foreach (var size in new[] { new Size(800, 600), new Size(1180, 800) })
        {
            using var form = new ExcelImportForm { Size = size, ShowInTaskbar = false };
            form.Show(); Application.DoEvents(); form.PerformLayout();
            object Field(string name) => typeof(ExcelImportForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            ((ComboBox)Field("mode")).SelectedIndex = selectedMode;
            var workbook = new ExcelWorkbookData();
            workbook.Sheets.AddRange(selectedMode == 0 ? Academic() : [Timetable(["Sunday", "#1", "#1", "#1", "#1", "#1", "Theory", 1, "", true])]);
            typeof(ExcelImportForm).GetField("workbook", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(form, workbook);
            var sheets = (ComboBox)Field("sheet");
            sheets.Items.AddRange(workbook.Sheets.Select(s => (object)s.Name).ToArray()); sheets.SelectedIndex = 0;
            Application.DoEvents(); form.PerformLayout();
            var dataGrid = (DataGridView)Field("data");
            Assert(dataGrid.Rows.Count > 0);
            if (selectedMode == 2) Assert(dataGrid.Columns["Locked"] is DataGridViewCheckBoxColumn);
            foreach (string name in new[] { "validate", "skipProblems", "generate", "save" })
            {
                var control = (Control)Field(name);
                if (!control.Visible) continue;
                var bounds = form.RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
                Assert(form.ClientRectangle.Contains(bounds), "Clipped action: " + name);
            }
            using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
            bitmap.Save(Path.Combine(Output, $"import-ui-{selectedMode}-{size.Width}.png")); form.Close();
        }
        Console.WriteLine("PASS: 6 UI layouts, populated previews, Hybrid lock checkboxes and visible actions. Screenshots saved.");
    }
    private sealed class FailureInterceptor : SaveChangesInterceptor
    {
        public int FailOnSave, Saves;
        public CancellationTokenSource? CancelAfterSave;
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            CancelAfterSave?.Cancel();
            return base.SavedChangesAsync(eventData, result, cancellationToken);
        }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (FailOnSave > 0 && ++Saves == FailOnSave) throw new InvalidOperationException("Injected database write failure");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
