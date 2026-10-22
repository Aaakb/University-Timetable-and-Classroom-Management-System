using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

internal static class WeeklyVerification
{
    private static readonly string Output = Path.GetFullPath("output/weekly-pdf-import-verification");
    private static readonly List<string> checks = [];
    private static void Assert(bool value, string message = "Assertion failed") { if (!value) throw new Exception(message); }
    private static void Check(string name, Action run) { run(); checks.Add(name); Console.WriteLine("PASS " + name); }
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 0) throw new ArgumentException("Pass the supplied 17-page weekly timetable PDF path. See README.md.");
        Directory.CreateDirectory(Output); string source = Path.GetFullPath(args[0]);
        string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source)));
        var loaded = ImportFileReader.Read(source);
        Check("17 weekly matrix pages become 235 lectures across 10 morning and 7 evening pages", () =>
        {
            Assert(loaded.Sheets.Count == 17 && loaded.Sheets.All(s => s.IsWeeklyTimetable && s.SuggestedSchemaName == "Timetable"));
            Assert(loaded.Sheets.Count(s => s.StudySession == "صباحي") == 10 && loaded.Sheets.Count(s => s.StudySession == "مسائي") == 7);
            Assert(loaded.Sheets.Select(s => s.Table.Rows.Count).SequenceEqual([10, 11, 15, 15, 18, 12, 18, 11, 11, 13, 15, 15, 15, 12, 12, 16, 16]));
        });
        Check("Merged cells retain Arabic lecturer names, year/section and full time spans", () =>
        {
            var rows = loaded.Sheets[0].Table.Rows.Cast<DataRow>().ToList();
            Assert(rows.All(r => (string)r["StudyYear"] == "#1" && (string)r["Section"] == "A" && (string)r["SemesterNumber"] == "1"));
            var programming = rows.Single(r => (string)r["Subject"] == "Programming Fundamentals (I)" && (string)r["LectureType"] == "Theory");
            Assert((string)programming["Faculty"] == "جلال حميد" && (string)programming["Classroom"] == "قاعة 401");
            var ethics = rows.Single(r => (string)r["Subject"] == "Computer Ethics");
            Assert((string)ethics["TimeSlot"] == "08:30" && (string)ethics["EndTime"] == "10:10" && (string)ethics["Faculty"] == "شذى جاسم");
        });
        Check("Branches, CY groups, whole-section practicals and glued group/room glyphs survive extraction", () =>
        {
            Assert(loaded.Sheets[4].Table.Rows.Cast<DataRow>().All(r => (string)r["StudyYear"] == "#3" && (string)r["Branch"] == "علوم الحاسوب"));
            var cy = loaded.Sheets[5].Table.Rows.Cast<DataRow>().Single(r => (string)r["Subject"] == "Advanced Web Development" && (string)r["GroupName"] == "CY2");
            Assert((string)cy["Faculty"] == "ايمان هاتو" && (string)cy["LectureType"] == "Practical");
            Assert(loaded.Sheets[7].Table.Rows.Cast<DataRow>().Any(r => (string)r["LectureType"] == "Practical" && (string)r["GroupName"] == ""));
            var glued = loaded.Sheets[15].Table.Rows.Cast<DataRow>().Single(r => (string)r["Subject"] == "Advanced kali Linux" && (string)r["GroupName"] == "A1");
            Assert((string)glued["Classroom"] == "مختبر 1" && (string)glued["TimeSlot"] == "13:40" && (string)glued["EndTime"] == "15:00");
        });
        Check("33 missing teachers remain blank with notes; stray letters are not invented courses", () =>
        {
            var missing = loaded.Sheets.SelectMany(s => s.Table.Rows.Cast<DataRow>()).Where(r => string.IsNullOrWhiteSpace((string)r["Faculty"])).ToList();
            Assert(missing.Count == 33 && missing.All(r => ((string)r["ImportNote"]).Contains("التدريسي")));
            Assert(!loaded.Sheets.SelectMany(s => s.Table.Rows.Cast<DataRow>()).Any(r => (string)r["Subject"] == "h"));
            Assert(loaded.Sheets[11].Table.Rows.Cast<DataRow>().Any(r => ((string)r["ImportNote"]).Contains("كتابة منفردة")));
        });
        DatabaseChecks(loaded).GetAwaiter().GetResult();
        Check("Actual-file Arabic UI filters morning/evening pages at 800x600 and 1180x800", () => Ui(loaded, source));
        Check("Original PDF stays byte-for-byte unchanged", () => Assert(hash == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source)))));
        File.WriteAllText(Path.Combine(Output, "results.json"), JsonSerializer.Serialize(new { Count = checks.Count, Pages = 17, Lectures = 235, Checks = checks }, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(Output, "extracted-lectures.json"), JsonSerializer.Serialize(loaded.Sheets.Select(s => new
        { s.Name, s.StudySession, Rows = s.Table.Rows.Cast<DataRow>().Select(r => s.Table.Columns.Cast<DataColumn>().ToDictionary(c => c.ColumnName, c => r[c].ToString())) }), new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"SUMMARY: {checks.Count} checks, 0 failures");
    }
    private static async Task DatabaseChecks(ExcelWorkbookData loaded)
    {
        string name = "UniversityTimetable_WeeklyPdf_" + Guid.NewGuid().ToString("N");
        var connection = new SqlConnectionStringBuilder { DataSource = Environment.GetEnvironmentVariable("UNIVERSITY_TIMETABLE_TEST_SERVER") ?? "localhost",
            InitialCatalog = name, IntegratedSecurity = true, Encrypt = false, TrustServerCertificate = true };
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection.ConnectionString).Options;
        Func<AppDbContext> factory = () => new(options); var service = new ExcelImportService(factory);
        File.WriteAllText(Path.Combine(Output, "isolated-database.txt"), name);
        var first = loaded.Sheets[0]; var rows = first.Table.Rows.Cast<DataRow>().ToList();
        await using (var db = factory())
        {
            await db.Database.EnsureCreatedAsync();
            db.StudyYears.Add(new() { StudyYearID = 1, YearName = "First Year" });
            db.Sections.Add(new() { SectionID = 1, SectionName = "A", StudyYearID = 1, StudentCount = 40 });
            var subjects = rows.Select(r => (string)r["Subject"]).Distinct().Select((s, i) => new Subject { SubjectID = i + 1, SubjectName = s, StudyYearID = 1,
                SemesterNumber = 1, TheoreticalHours = rows.Any(r => (string)r["Subject"] == s && (string)r["LectureType"] == "Theory") ? 2 : 0,
                PracticalHours = rows.Any(r => (string)r["Subject"] == s && (string)r["LectureType"] == "Practical") ? 2 : 0, CreditUnits = 2 }).ToList();
            var faculty = rows.Select(r => (string)r["Faculty"]).Distinct().Select((s, i) => new FacultyMember { FacultyMemberID = i + 1, FullName = s }).ToList();
            db.Subjects.AddRange(subjects); db.FacultyMembers.AddRange(faculty);
            foreach (var assignment in rows.Select(r => ((string)r["Subject"], (string)r["Faculty"])).Distinct())
                db.FacultyMemberSubjects.Add(new() { SubjectID = subjects.Single(s => s.SubjectName == assignment.Item1).SubjectID, FacultyMemberID = faculty.Single(f => f.FullName == assignment.Item2).FacultyMemberID });
            faculty[0].FullName = "د." + faculty[0].FullName;
            db.Classrooms.AddRange(new Classroom { ClassroomID = 1, ClassroomNumber = "302", Capacity = 60, RoomType = "Lecture" },
                new Classroom { ClassroomID = 2, ClassroomNumber = "301", Capacity = 60, RoomType = "Lecture" },
                new Classroom { ClassroomID = 3, ClassroomNumber = "401", Capacity = 60, RoomType = "Lecture" },
                new Classroom { ClassroomID = 4, ClassroomNumber = "Lab 5", Capacity = 30, RoomType = "Lab" },
                new Classroom { ClassroomID = 5, ClassroomNumber = "Lab 2", Capacity = 60, RoomType = "Shared Lab" });
            db.TimeSlots.AddRange(new TimeSlot { TimeSlotID = 1, StartTime = new(8, 30, 0), EndTime = new(10, 10, 0) },
                new TimeSlot { TimeSlotID = 2, StartTime = new(10, 20, 0), EndTime = new(12, 0, 0) },
                new TimeSlot { TimeSlotID = 3, StartTime = new(12, 10, 0), EndTime = new(13, 50, 0) });
            await db.SaveChangesAsync();
        }
        var mixed = await service.ValidateAsync(ExcelImportMode.RepairTimetable, loaded.Sheets);
        Check("Mixed study sessions are blocked rather than merged as one cohort", () => Assert(!mixed.IsValid && mixed.Issues.Any(i => i.Column == "StudySession")));
        var incorrect = first.MapColumns(new Dictionary<string, string>()); incorrect.Table.Rows[0]["EndTime"] = "10:00";
        var invalid = await service.ValidateAsync(ExcelImportMode.RepairTimetable, [incorrect]);
        Check("Imported end time is checked so 100-minute source lectures are never silently shortened", () => Assert(!invalid.IsValid && invalid.Issues.Any(i => i.Message.Contains("وقت انتهاء"))));
        var parsed = await service.ValidateAsync(ExcelImportMode.RepairTimetable, [first]);
        Check("Actual first-page lectures resolve Arabic lecturers, source metadata and room labels", () => Assert(parsed.IsValid && parsed.Lectures.Count == 10, string.Join("; ", parsed.Issues)));
        await using (var db = factory()) Assert(!await db.Schedules.AnyAsync());
        var repaired = await service.RepairTimetableAsync(parsed, 10);
        Check("Real weekly lectures pass repair while preserving teachers, groups and durations", () => Assert(repaired.IsValid && repaired.GenerationCompleted && repaired.Lectures.Count == 10, string.Join("; ", repaired.Issues)));
        await using (var db = factory()) Assert(!await db.Schedules.AnyAsync());
        var saved = await service.SaveAsync(repaired, true);
        await using (var db = factory()) Check("Reviewed real-PDF repair saves atomically into an isolated database", () => Assert(saved.Added == 10 && db.Schedules.Count() == 10));
    }
    private static void Ui(ExcelWorkbookData loaded, string path)
    {
        ApplicationConfiguration.Initialize();
        foreach (var size in new[] { new Size(800, 600), new Size(1180, 800) })
        {
            using var form = new ExcelImportForm { Size = size, ShowInTaskbar = false }; form.Show(); Application.DoEvents();
            object Field(string n) => typeof(ExcelImportForm).GetField(n, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            typeof(ExcelImportForm).GetMethod("LoadWorkbook", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, [loaded, path]); Application.DoEvents();
            Assert(((ComboBox)Field("mode")).SelectedIndex == 3);
            var session = (ComboBox)Field("studySession"); var sheets = (ComboBox)Field("sheet"); Assert(sheets.Items.Count == 17 && session.Visible);
            session.SelectedIndex = 1; Application.DoEvents(); Assert(sheets.Items.Count == 10);
            var selected = (List<ExcelSheetData>)typeof(ExcelImportForm).GetMethod("PrepareSheets", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null)!;
            Assert(selected.Count == 10 && selected.All(s => s.StudySession == "صباحي"));
            var data = (DataGridView)Field("data"); Assert(data.Rows.Count == 10 && data.Height >= 80);
            foreach (string n in new[] { "validate", "generate", "save" })
            {
                var control = (Control)Field(n); Assert(form.ClientRectangle.Contains(form.RectangleToClient(control.RectangleToScreen(control.ClientRectangle))), "Clipped " + n);
            }
            using var image = new Bitmap(form.Width, form.Height); form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size)); image.Save(Path.Combine(Output, $"weekly-import-{size.Width}.png"));
            session.SelectedIndex = 2; Application.DoEvents(); Assert(sheets.Items.Count == 7);
            selected = (List<ExcelSheetData>)typeof(ExcelImportForm).GetMethod("PrepareSheets", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null)!;
            Assert(selected.Count == 7 && selected.All(s => s.StudySession == "مسائي")); form.Close();
        }
    }
}
