using System.Data;
using System.Text.Json;
using System.Runtime.InteropServices;
using ClosedXML.Excel;
using SkiaSharp;
using Topten.RichTextKit;
using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

internal static class Program
{
    private static Func<AppDbContext> factory = null!;
    private static ExcelImportService service = null!;
    private static readonly Fault fault = new();
    private static readonly List<object> results = [];
    private static readonly string Output = Path.GetFullPath("output/import-repair-verification");
    private static int failures;
    private static void Assert(bool value, string message = "Assertion failed") { if (!value) throw new Exception(message); }
    private static async Task Reject(Func<Task> action)
    {
        try { await action(); } catch (Exception e) when (e is ArgumentException or InvalidOperationException or OperationCanceledException) { return; }
        throw new Exception("Expected rejection");
    }
    private static async Task Check(string name, Func<Task> run)
    {
        fault.FailOnSave = fault.Saves = 0; fault.CancelAfterSave = null;
        await using (var db = factory()) await db.Schedules.ExecuteDeleteAsync();
        try { await run(); results.Add(new { Name = name, Passed = true }); Console.WriteLine("PASS " + name); }
        catch (Exception e) { failures++; results.Add(new { Name = name, Passed = false, Error = e.ToString() }); Console.WriteLine("FAIL " + name + ": " + e); }
        finally { fault.FailOnSave = fault.Saves = 0; fault.CancelAfterSave = null; }
    }
    private static ExcelSheetData Sheet(params object[][] rows)
    {
        var table = new DataTable("Timetable");
        foreach (string c in new[] { "Day", "TimeSlot", "Subject", "Faculty", "Classroom", "Section", "LectureType", "SemesterNumber", "GroupName", "Locked", "ScheduleID" }) table.Columns.Add(c);
        foreach (var row in rows) table.Rows.Add(row.Select(x => (object)(Convert.ToString(x) ?? "")).ToArray());
        return new("Timetable", table, Enumerable.Range(2, rows.Length).ToArray());
    }
    private static object[] Theory(int subject = 1, int faculty = 1, int room = 1, int section = 1, string day = "Sunday", int period = 1, bool locked = false, int? id = null)
        => [day, "#" + period, "#" + subject, "#" + faculty, "#" + room, "#" + section, "Theory", 1, "", locked, id?.ToString() ?? ""];
    private static object[] Practical(int subject, int faculty, int room, string group, string day = "Sunday", int period = 1, bool locked = false)
        => [day, "#" + period, "#" + subject, "#" + faculty, "#" + room, "#1", "Practical", 1, group, locked, ""];
    private static Task<ExcelImportPreview> Validate(params object[][] rows) => service.ValidateAsync(ExcelImportMode.RepairTimetable, [Sheet(rows)]);
    private static async Task<ExcelImportPreview> Repair(params object[][] rows)
    {
        var preview = await Validate(rows); Assert(preview.IsValid, string.Join("; ", preview.Issues));
        return await service.RepairTimetableAsync(preview, 5);
    }
    private static async Task<List<Schedule>> Rows() { await using var db = factory(); return await db.Schedules.AsNoTracking().OrderBy(s => s.ScheduleID).ToListAsync(); }
    private static string Json(object value) => JsonSerializer.Serialize(value);
    private static async Task<Schedule> AddSaved(int subject = 1, int faculty = 1, int room = 1, int section = 1, string day = "Sunday", int period = 1, bool locked = false)
    {
        await using var db = factory();
        var row = new Schedule { SubjectID = subject, FacultyMemberID = faculty, ClassroomID = room, SectionID = section, StudyYearID = 1,
            DayOfWeek = day, TimeSlotID = period, SemesterNumber = 1, LectureType = "Theory", IsLocked = locked };
        db.Schedules.Add(row); await db.SaveChangesAsync(); return row;
    }
    private static bool Conflict(Schedule a, Schedule b) => a.SemesterNumber == b.SemesterNumber && a.DayOfWeek == b.DayOfWeek && a.TimeSlotID == b.TimeSlotID &&
        (a.ClassroomID == b.ClassroomID || a.FacultyMemberID == b.FacultyMemberID || a.SectionID == b.SectionID &&
        (string.IsNullOrWhiteSpace(a.GroupName) || string.IsNullOrWhiteSpace(b.GroupName) || a.GroupName == b.GroupName));
    private static void ValidPlan(ExcelImportPreview preview)
    {
        Assert(preview.IsValid && preview.GenerationCompleted, string.Join("; ", preview.Issues));
        for (int i = 0; i < preview.Lectures.Count; i++) for (int j = i + 1; j < preview.Lectures.Count; j++)
            Assert(!Conflict(preview.Lectures[i].Schedule, preview.Lectures[j].Schedule));
    }
    private static async Task Main()
    {
        Directory.CreateDirectory(Output);
        string database = "UniversityTimetable_ImportRepair_" + Guid.NewGuid().ToString("N");
        var connection = new SqlConnectionStringBuilder { DataSource = Environment.GetEnvironmentVariable("UNIVERSITY_TIMETABLE_TEST_SERVER") ?? "localhost",
            InitialCatalog = database, IntegratedSecurity = true, Encrypt = false, TrustServerCertificate = true };
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection.ConnectionString).AddInterceptors(fault).Options;
        factory = () => new AppDbContext(options); service = new(factory);
        File.WriteAllText(Path.Combine(Output, "isolated-database.txt"), database);
        await using (var db = factory())
        {
            await db.Database.EnsureCreatedAsync();
            db.StudyYears.Add(new() { StudyYearID = 1, YearName = "First Year" });
            for (int i = 1; i <= 4; i++)
            {
                db.FacultyMembers.Add(new() { FacultyMemberID = i, FullName = "تدريسي " + i });
                db.Subjects.Add(new() { SubjectID = i, SubjectName = "مادة " + i, StudyYearID = 1, SemesterNumber = 1,
                    TheoreticalHours = i <= 2 ? 2 : 0, PracticalHours = i <= 2 ? 0 : 2, CreditUnits = 2 });
                db.FacultyMemberSubjects.Add(new() { FacultyMemberID = i, SubjectID = i });
            }
            db.Sections.AddRange(new Section { SectionID = 1, SectionName = "A", StudyYearID = 1, StudentCount = 40 }, new Section { SectionID = 2, SectionName = "B", StudyYearID = 1, StudentCount = 40 });
            db.Classrooms.AddRange(new Classroom { ClassroomID = 1, ClassroomNumber = "Room 101", Capacity = 60, RoomType = "Lecture" },
                new Classroom { ClassroomID = 2, ClassroomNumber = "Room 102", Capacity = 60, RoomType = "Lecture" },
                new Classroom { ClassroomID = 3, ClassroomNumber = "Lab 1", Capacity = 30, RoomType = "Lab" },
                new Classroom { ClassroomID = 4, ClassroomNumber = "Lab 2", Capacity = 30, RoomType = "Lab" });
            db.TimeSlots.AddRange(new TimeSlot { TimeSlotID = 1, StartTime = new(8, 30, 0), EndTime = new(10, 0, 0) },
                new TimeSlot { TimeSlotID = 2, StartTime = new(10, 10, 0), EndTime = new(11, 40, 0) },
                new TimeSlot { TimeSlotID = 3, StartTime = new(11, 50, 0), EndTime = new(13, 20, 0) });
            await db.SaveChangesAsync();
        }
        await Check("Room conflict becomes a repairable diagnostic; validation and solving never write", async () =>
        {
            var p = await Validate(Theory(), Theory(2, 2, section: 2));
            Assert(p.IsValid && p.RepairableConflicts.Count == 1);
            await Reject(() => service.SaveAsync(p, true));
            var fixedPlan = await service.RepairTimetableAsync(p, 5); ValidPlan(fixedPlan);
            Assert(fixedPlan.PlacementChanges.Count(c => c.Changed) == 1 && (await Rows()).Count == 0);
            await Reject(() => service.SaveAsync(fixedPlan, false)); Assert((await Rows()).Count == 0);
            Assert((await service.SaveAsync(fixedPlan, true)).Added == 2);
        });
        await Check("Faculty conflicts are repaired without changing the teacher or either section", async () =>
        {
            var p = await Repair(Theory(), Theory(room: 2, section: 2)); ValidPlan(p);
            Assert(p.Lectures.All(l => l.Schedule.FacultyMemberID == 1) && p.PlacementChanges.Count(c => c.Changed) == 1);
        });
        await Check("Section conflicts are repaired despite separate teachers and rooms", async () =>
        {
            var p = await Repair(Theory(), Theory(2, 2, room: 2)); ValidPlan(p);
            Assert(p.PlacementChanges.Count(c => c.Changed) == 1);
        });
        await Check("A locked row stays in place and only its movable conflicting row changes", async () =>
        {
            var p = await Repair(Theory(locked: true), Theory(2, 2, section: 2)); ValidPlan(p);
            Assert(!p.PlacementChanges[0].Changed && p.Lectures[0].Locked && p.PlacementChanges[1].Changed);
        });
        await Check("Conflicting locked rows are blocking errors and never saved", async () =>
        {
            var p = await Validate(Theory(locked: true), Theory(2, 2, section: 2, locked: true));
            Assert(!p.IsValid); await Reject(() => service.RepairTimetableAsync(p)); await Reject(() => service.SaveAsync(p, true)); Assert((await Rows()).Count == 0);
        });
        await Check("Wrong room type/capacity can be fixed only for movable rows", async () =>
        {
            var p = await Repair(Theory(room: 3)); ValidPlan(p);
            Assert(p.Lectures.Single().Schedule.ClassroomID is 1 or 2);
            Assert(p.Lectures.Single().Schedule.DayOfWeek == "Sunday" && p.Lectures.Single().Schedule.TimeSlotID == 1);
            Assert(!(await Validate(Theory(room: 3, locked: true))).IsValid);
        });
        await Check("Saved imported rows can move with stable IDs; unrelated saved rows stay unchanged", async () =>
        {
            var saved = await AddSaved(); var unrelated = await AddSaved(2, 2, room: 2, day: "Tuesday", period: 3);
            var p = await Repair(Theory(id: saved.ScheduleID), Theory(2, 2, section: 2, locked: true)); ValidPlan(p);
            Assert(p.Lectures[0].Schedule.ScheduleID == saved.ScheduleID && p.PlacementChanges[0].Changed);
            var result = await service.SaveAsync(p, true); Assert(result.Updated == 1 && result.Added == 1);
            Assert(Json((await Rows()).Single(s => s.ScheduleID == unrelated.ScheduleID)) == Json(unrelated));
            Assert((await Rows()).Any(s => s.ScheduleID == saved.ScheduleID));
        });
        await Check("Existing locks cannot be removed by the file and ScheduleID cannot change identity", async () =>
        {
            var saved = await AddSaved(locked: true);
            var p = await Validate(Theory(id: saved.ScheduleID), Theory(2, 2, section: 2, locked: true)); Assert(!p.IsValid);
            Assert(!(await Validate(Theory(day: "Monday", id: saved.ScheduleID))).IsValid);
            Assert(!(await Validate(Theory(2, 2, id: saved.ScheduleID))).IsValid);
        });
        await Check("Clean imports need zero moves and exact saved matches remain idempotent", async () =>
        {
            var saved = await AddSaved();
            var p = await Repair(Theory()); ValidPlan(p); Assert(!p.PlacementChanges.Single().Changed);
            var result = await service.SaveAsync(p, true); Assert(result.Added == 0 && result.Unchanged == 1 && (await Rows()).Single().ScheduleID == saved.ScheduleID);
        });
        await Check("Unknown references, duplicates and excess weekly demand remain blocking errors", async () =>
        {
            Assert(!(await Validate(Theory(faculty: 999))).IsValid);
            Assert(!(await Validate(Theory(), Theory())).IsValid);
            Assert(!(await Validate(Theory(), Theory(day: "Monday"), Theory(day: "Tuesday"))).IsValid);
        });
        await Check("Practical repair preserves complementary groups, teachers and exchange blocks", async () =>
        {
            var p = await Repair(Practical(3, 3, 3, "A1"), Practical(3, 3, 3, "A2"), Practical(4, 4, 4, "A1"), Practical(4, 4, 4, "A2")); ValidPlan(p);
            var a = p.Lectures.Single(l => l.Schedule.SubjectID == 3 && l.Schedule.GroupName == "A1").Schedule;
            var b = p.Lectures.Single(l => l.Schedule.SubjectID == 3 && l.Schedule.GroupName == "A2").Schedule;
            Assert(a.DayOfWeek == b.DayOfWeek && Math.Abs(a.TimeSlotID - b.TimeSlotID) == 1 && a.ClassroomID == b.ClassroomID);
            Assert(p.Lectures.All(l => l.Schedule.FacultyMemberID == l.Schedule.SubjectID));
            Assert((await service.SaveAsync(p, true)).Added == 4);
        });
        await Check("Incompatible locked practical exchange returns no savable partial result", async () =>
        {
            var p = await Repair(Practical(3, 3, 3, "A1", locked: true), Practical(3, 3, 3, "A2", day: "Tuesday", locked: true),
                Practical(4, 4, 4, "A1"), Practical(4, 4, 4, "A2"));
            Assert(!p.GenerationCompleted && p.RepairSearchStatus == "Infeasible"); await Reject(() => service.SaveAsync(p, true)); Assert((await Rows()).Count == 0);
        });
        await Check("No available placement fails without modifying saved lectures", async () =>
        {
            foreach (string day in new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday" })
                for (int period = 1; period <= 3; period++) await AddSaved(section: 2, day: day, period: period);
            string before = Json(await Rows()); var p = await Repair(Theory());
            Assert(!p.GenerationCompleted && p.RepairSearchStatus == "Infeasible"); await Reject(() => service.SaveAsync(p, true)); Assert(before == Json(await Rows()));
        });
        await Check("Preview tampering, invalid budgets, cancellation and stale resources are rejected", async () =>
        {
            var input = await Validate(Theory()); input.Lectures[0].Schedule.DayOfWeek = "Monday";
            await Reject(() => service.RepairTimetableAsync(input));
            input = await Validate(Theory()); await Reject(() => service.RepairTimetableAsync(input, 0));
            using var cancel = new CancellationTokenSource(); cancel.Cancel(); await Reject(() => service.RepairTimetableAsync(input, token: cancel.Token));
            var p = await Repair(Theory()); p.Lectures[0].Schedule.DayOfWeek = "Friday"; await Reject(() => service.SaveAsync(p, true));
            p = await Repair(Theory());
            await using var db = factory(); await db.Classrooms.Where(r => r.ClassroomID == 1).ExecuteUpdateAsync(set => set.SetProperty(r => r.Capacity, 61));
            try { await Reject(() => service.SaveAsync(p, true)); await Reject(() => service.RepairTimetableAsync(input)); }
            finally { await db.Classrooms.Where(r => r.ClassroomID == 1).ExecuteUpdateAsync(set => set.SetProperty(r => r.Capacity, 60)); }
            Assert((await Rows()).Count == 0);
        });
        await Check("A save failure rolls back an updated saved row and a new imported row together", async () =>
        {
            var saved = await AddSaved(); string before = Json(await Rows());
            var p = await Repair(Theory(id: saved.ScheduleID), Theory(2, 2, section: 2, locked: true)); ValidPlan(p);
            fault.Saves = 0; fault.FailOnSave = 2;
            await Reject(() => service.SaveAsync(p, true)); fault.FailOnSave = 0;
            Assert(before == Json(await Rows()));
        });
        await Check("Arabic names and headers in actual Excel and multi-page PDF files can be repaired and saved", async () =>
        {
            foreach (string format in new[] { "xlsx", "pdf" })
            {
                await using (var db = factory()) await db.Schedules.ExecuteDeleteAsync();
                var source = Sheet(Theory(), Theory(2, 2, section: 2));
                for (int r = 0; r < 2; r++)
                {
                    source.Table.Rows[r]["Day"] = "الأحد";
                    source.Table.Rows[r]["Subject"] = "مادة " + (r + 1);
                    source.Table.Rows[r]["Faculty"] = "تدريسي " + (r + 1);
                    source.Table.Rows[r]["LectureType"] = "نظري";
                    source.Table.Rows[r]["SemesterNumber"] = "١";
                }
                string path = Path.Combine(Output, "conflicting-timetable." + format);
                if (format == "xlsx")
                {
                    using var book = new XLWorkbook(); var sheet = book.AddWorksheet("Timetable");
                    for (int c = 0; c < source.Table.Columns.Count; c++) sheet.Cell(1, c + 1).Value = ImportLabels.Display(source.Table.Columns[c].ColumnName);
                    for (int r = 0; r < source.Table.Rows.Count; r++) for (int c = 0; c < source.Table.Columns.Count; c++) sheet.Cell(r + 2, c + 1).Value = source.Table.Rows[r][c].ToString();
                    book.SaveAs(path);
                }
                else
                {
                    ArabicPdf(path, source.Table);
                }
                var loaded = ImportFileReader.Read(path);
                var mapped = loaded.Sheets.Select(sheet => sheet.MapColumns(sheet.Table.Columns.Cast<DataColumn>().ToDictionary(c => c.ColumnName,
                    c => ImportLabels.MatchColumn(c.ColumnName, ExcelImportSchema.Timetable.Columns) ?? ""))).ToList();
                Assert(mapped[0].Table.Rows[0]["Subject"].ToString() == "مادة 1" && mapped[0].Table.Rows[0]["Faculty"].ToString() == "تدريسي 1");
                var p = await service.ValidateAsync(ExcelImportMode.RepairTimetable, mapped);
                Assert(p.IsValid && p.Lectures.Count == 2 && p.RepairableConflicts.Count > 0);
                if (format == "pdf") Assert(p.Lectures.Select(l => l.Sheet).Distinct().Count() == 2);
                p = await service.RepairTimetableAsync(p, 5); ValidPlan(p);
                Assert(p.PlacementChanges.Count(c => c.Changed) == 1 && (await Rows()).Count == 0);
                Assert((await service.SaveAsync(p, true)).Added == 2);
            }
        });
        await Check("Arabic presentation forms, diacritics, bidi marks and extra spaces resolve safely", async () =>
        {
            var sheet = Sheet(Theory());
            sheet.Table.Rows[0]["Subject"] = "\u200fمَادَة   ١\u200e";
            sheet.Table.Rows[0]["Faculty"] = "تَدْرِيسي   ١";
            sheet.Table.Rows[0]["SemesterNumber"] = "١";
            var p = await service.ValidateAsync(ExcelImportMode.RepairTimetable, [sheet]);
            Assert(p.IsValid, string.Join("; ", p.Issues));
            p = await service.RepairTimetableAsync(p, 5); ValidPlan(p);
            Assert(p.Lectures.Single().Schedule.SubjectID == 1 && p.Lectures.Single().Schedule.FacultyMemberID == 1);
            await using var db = factory();
            await db.FacultyMembers.Where(f => f.FacultyMemberID == 2).ExecuteUpdateAsync(set => set.SetProperty(f => f.FullName, "تَدْرِيسي 1"));
            try
            {
                p = await service.ValidateAsync(ExcelImportMode.RepairTimetable, [sheet]);
                Assert(!p.IsValid && p.Issues.Any(i => i.Message.Contains("ambiguous")));
            }
            finally { await db.FacultyMembers.Where(f => f.FacultyMemberID == 2).ExecuteUpdateAsync(set => set.SetProperty(f => f.FullName, "تدريسي 2")); }
        });
        await Check("Cancelling after the first write rolls back the entire repaired import", async () =>
        {
            var p = await Repair(Theory(), Theory(2, 2, section: 2)); ValidPlan(p);
            using var cancel = new CancellationTokenSource(); fault.CancelAfterSave = cancel;
            await Reject(() => service.SaveAsync(p, true, token: cancel.Token)); fault.CancelAfterSave = null;
            Assert((await Rows()).Count == 0);
        });
        await Check("Changing the before/after review after solving invalidates approval", async () =>
        {
            var p = await Repair(Theory()); ValidPlan(p);
            p.PlacementChanges[0] = p.PlacementChanges[0] with { BeforeClassroom = "different review" };
            await Reject(() => service.SaveAsync(p, true)); Assert((await Rows()).Count == 0);
        });
        File.WriteAllText(Path.Combine(Output, "results.json"), JsonSerializer.Serialize(new { Count = results.Count, Failures = failures, Checks = results }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"SUMMARY: {results.Count} checks, {failures} failures"); Environment.ExitCode = failures == 0 ? 0 : 1;
    }
    private static void ArabicPdf(string path, DataTable source)
    {
        string nativeRoot = Path.GetDirectoryName(typeof(SchedulePdfExportService).Assembly.Location)!;
        foreach (string name in new[] { "libSkiaSharp.dll", "libHarfBuzzSharp.dll" })
            NativeLibrary.Load(@"\\?\" + Path.Combine(nativeRoot, "runtimes", "win-x64", "native", name));
        using var stream = File.Create(path);
        using var document = SKDocument.CreatePdf(stream);
        for (int r = 0; r < source.Rows.Count; r++)
        {
            var canvas = document.BeginPage(1520, 440);
            void Text(string value, float x, float y)
            {
                var block = new TextBlock { MaxWidth = 150, MaxLines = 1 };
                block.AddText(value, new Style { FontFamily = "Arial", FontSize = 12, TextColor = SKColors.Black });
                block.Paint(canvas, new SKPoint(x, y));
            }
            for (int c = 0; c < 8; c++)
            {
                Text(ImportLabels.Display(source.Columns[c].ColumnName), 40 + c * 180, 35);
                Text(source.Rows[r][c].ToString()!, 40 + c * 180, 70);
            }
            document.EndPage();
        }
        document.Close();
    }
    private sealed class Fault : SaveChangesInterceptor
    {
        public int FailOnSave, Saves;
        public CancellationTokenSource? CancelAfterSave;
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            CancelAfterSave?.Cancel(); return base.SavedChangesAsync(eventData, result, cancellationToken);
        }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (FailOnSave > 0 && ++Saves == FailOnSave) throw new InvalidOperationException("Injected save failure");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
