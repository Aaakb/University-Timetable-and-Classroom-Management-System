using System.Data;
using System.Reflection;
using System.Text.Json;
using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

internal static class Program
{
    static int checks;
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
    static ExcelSheetData Roster(params string[][] rows)
    {
        var table = new DataTable(); foreach (string name in new[] { "اسم التدريسي", "المادة", "المرحلة", "التخصص", "الدراسة" }) table.Columns.Add(name);
        foreach (var row in rows) table.Rows.Add(row);
        return new("التدريسيون والمواد", table, Enumerable.Range(2, rows.Length).ToArray());
    }
    [STAThread] static void Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "inspect")
        {
            Inspect(args[1]).GetAwaiter().GetResult(); return;
        }
        Run().GetAwaiter().GetResult();
        ApplicationConfiguration.Initialize();
        using var form = new ExcelImportForm();
        var book = new ExcelWorkbookData(); var roster = Roster(["موجود", "General", "الأولى", "عام", "صباحي"]);
        book.Sheets.Add(new(roster.Name, roster.Table, roster.SourceRows) { SuggestedSchemaName = "NamedAssignments" });
        form.GetType().GetMethod("LoadWorkbook", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, [book, "roster.xlsx"]);
        var entity = (ComboBox)form.GetType().GetField("entity", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
        Check(entity.SelectedItem?.ToString() == "NamedAssignments", "UI automatically selects the named morning roster schema");
        var prepared = (List<ExcelSheetData>)form.GetType().GetMethod("PrepareSheets", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null)!;
        Check(prepared.Single().Table.Columns.Contains("StudySession") && prepared.Single().Table.Columns.Contains("SubjectName"), "UI maps all Arabic roster headers without manual IDs");
        form.Size = new(1180, 800); form.Show(); Application.DoEvents(); form.PerformLayout();
        var problemPreview = new ExcelImportPreview(); problemPreview.Issues.Add(new(roster.SourceName, 2, "", "مادة تحتاج مراجعة"));
        form.GetType().GetField("preview", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(form, problemPreview);
        form.GetType().GetMethod("UpdateActions", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
        var skip = (Control)form.Controls.Find("skipProblemImportRows", true).Single();
        Check(skip.Enabled && skip.Visible, "Skip action is visible and enabled after a row validation error");
        Directory.CreateDirectory("output/named-assignment-import");
        using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save("output/named-assignment-import/preview.png");
        form.Close();
        Console.WriteLine($"SUMMARY {checks} checks, 0 failures");
    }
    static async Task Run()
    {
        var connection = new SqlConnectionStringBuilder { DataSource = "localhost", InitialCatalog = "UniversityTimetable_NamedRoster_" + Guid.NewGuid().ToString("N"), IntegratedSecurity = true, Encrypt = false, TrustServerCertificate = true };
        Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, connection.ConnectionString);
        Console.WriteLine("ISOLATED DATABASE " + connection.InitialCatalog);
        await using (var db = new AppDbContext())
        {
            await db.Database.EnsureCreatedAsync();
            db.StudyYears.AddRange(new StudyYear { StudyYearID = 1, YearName = "First Year" }, new StudyYear { StudyYearID = 3, YearName = "Third Year" });
            db.Branches.Add(new() { BranchID = 1, BranchName = "Cyber Security" });
            db.Sections.Add(new() { SectionID = 1, StudyYearID = 1, SectionName = "A", StudentCount = 20 });
            db.Subjects.AddRange(new Subject { SubjectID = 1, SubjectName = "General", StudyYearID = 1, SemesterNumber = 2, TheoreticalHours = 2 },
                new Subject { SubjectID = 2, SubjectName = "Security", StudyYearID = 3, BranchID = 1, SemesterNumber = 1, TheoreticalHours = 2 });
            db.FacultyMembers.Add(new() { FacultyMemberID = 1, FullName = "موجود", AcademicTitle = "Lecturer" });
            db.FacultyMemberSubjects.Add(new() { FacultyMemberID = 1, SubjectID = 1, SectionID = 1, LectureType = "Theory" });
            await db.SaveChangesAsync();
        }
        var service = new ExcelImportService();
        try
        {
            var sheet = Roster(["موجود", "General", "الأولى", "عام", "صباحي"],
                ["جديد", "Security", "الثالثة", "CYS", "صباحي"],
                ["مسائي فقط", "Security", "الثالثة", "CYS", "مسائي"],
                ["جديد", "Security", "الثالثة", "CYS", "صباحي"]);
            var preview = await service.ValidateAsync(ExcelImportMode.AcademicData, [sheet]);
            Check(preview.IsValid && preview.Changes.Count == 1, "Roster imports only one new teacher and never assignments");
            await service.SaveAsync(preview, true);
            var repeated = await service.ValidateAsync(ExcelImportMode.AcademicData, [sheet]);
            Check(repeated.IsValid && repeated.Changes.Count == 0, "Reimport does not duplicate existing names");

            var newMaterial = Roster(["", "New material", "الأولى", "عام", ""]);
            var missingTerm = await service.ValidateAsync(ExcelImportMode.AcademicData, [newMaterial]);
            Check(!missingTerm.IsValid && missingTerm.Issues.Any(i => i.Message.Contains("الفصل الدراسي")), "New subject needs an explicit semester");
            newMaterial.Table.Columns.Add("الفصل الدراسي"); newMaterial.Table.Rows[0]["الفصل الدراسي"] = "1";
            var materialPreview = await service.ValidateAsync(ExcelImportMode.AcademicData, [newMaterial]);
            Check(materialPreview.IsValid && materialPreview.Changes.Count == 1, "Subject alone imports without a teacher");
            await service.SaveAsync(materialPreview, true);

            var teacher = new DataTable(); teacher.Columns.Add("اسم التدريسي"); teacher.Rows.Add("تدريسي منفرد");
            var teacherPreview = await service.ValidateAsync(ExcelImportMode.AcademicData, [new("Names", teacher, [2])]);
            Check(teacherPreview.IsValid && teacherPreview.Changes.Count == 1, "Teacher alone requires no subject or session columns");
            await service.SaveAsync(teacherPreview, true);
            var assignment = new DataTable(); assignment.Columns.Add("FacultyMemberID"); assignment.Columns.Add("SubjectID"); assignment.Rows.Add("2", "1");
            var ignored = await service.ValidateAsync(ExcelImportMode.AcademicData, [new("FacultyMemberSubjects", assignment, [2])]);
            Check(ignored.IsValid && ignored.Changes.Count == 0 && ignored.ReadingNotes.Count > 0, "Assignment sheets are ignored explicitly");
            Check(!ExcelImportSchema.AcademicInputs.Any(s => s.Name == "FacultyMemberSubjects"), "Desktop and web schema choices exclude assignments");
            Check(ExcelImportSchema.For(ExcelImportMode.AcademicData).Select(s => s.Name).Order().SequenceEqual(new[] { "FacultyMembers", "Subjects" }), "Template contains only faculty and subjects");
            await using var db = new AppDbContext();
            Check(await db.FacultyMembers.CountAsync() == 3 && await db.Subjects.CountAsync() == 3, "Independent faculty and material records are saved");
            Check(await db.FacultyMemberSubjects.CountAsync() == 1, "No new assignments are saved");
            var existing = await db.FacultyMemberSubjects.SingleAsync();
            Check(existing.SectionID == 1 && existing.LectureType == "Theory", "Existing assignments stay unchanged");
            Check((await db.Subjects.FindAsync(1))!.SemesterNumber == 2, "Existing subjects retain their semester");
            using var canceled = new CancellationTokenSource(); canceled.Cancel(); bool rejected = false;
            try { await service.ValidateSkippingInvalidAcademicRowsAsync([sheet], token: canceled.Token); } catch (OperationCanceledException) { rejected = true; }
            Check(rejected, "Cancellation stops validation");
        }
        finally
        {
            await using var cleanup = new AppDbContext();
            await cleanup.Database.EnsureDeletedAsync();
        }
    }
    static async Task Inspect(string path)
    {
        // Read-only inspection of a supplied workbook against the configured database. Never save or migrate here.
        var settings = Path.GetFullPath("University Timetable and Classroom Management System/database-settings.json");
        using var json = JsonDocument.Parse(File.ReadAllText(settings));
        Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, json.RootElement.GetProperty("ConnectionStrings").GetProperty("UniversityTimetable").GetString());
        var sheets = ExcelWorkbookReader.Read(path).Sheets;
        var service = new ExcelImportService();
        var preview = await service.ValidateAsync(ExcelImportMode.AcademicData, sheets);
        var skipped = await service.ValidateSkippingInvalidAcademicRowsAsync(sheets);
        var report = new { Rows = sheets.Sum(s => s.Table.Rows.Count), Schema = sheets.Select(s => s.SuggestedSchemaName), Errors = preview.Issues,
            Evening = preview.ReadingNotes.Count(n => n.Message.Contains("استُبعد")), ChangesAfterSkip = skipped.Changes.Count,
            Skipped = skipped.SkippedRows.Select(i => (i.Sheet, i.Row)).Distinct().Count(), RemainingErrors = skipped.Issues };
        Directory.CreateDirectory("output/named-assignment-import");
        File.WriteAllText("output/named-assignment-import/file-inspection.json", JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"READ ONLY: rows={report.Rows}, evening={report.Evening}, errors={preview.Issues.Count}, changesAfterSkip={report.ChangesAfterSkip}, skipped={report.Skipped}, remainingErrors={skipped.Issues.Count}");
        foreach (var error in preview.Issues.Take(8)) Console.WriteLine($"ROW {error.Row}: {error.Message}");
    }
}
