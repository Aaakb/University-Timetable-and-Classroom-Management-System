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
        var mode = (ComboBox)form.GetType().GetField("mode", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
        Check(mode.Items.Count == 1, "Import UI has only the faculty and subjects mode");
        var tabs = (TabControl)form.GetType().GetField("tabs", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
        Check(tabs.TabPages.Count == 3, "Timetable generation and repair tab is removed");
        var generate = (Control)form.GetType().GetField("generate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
        Check(generate.Parent is null, "Timetable generation action is removed from the form");
        bool pdfRejected = false;
        try { ImportFileReader.Read("unsupported.pdf"); } catch (ArgumentException) { pdfRejected = true; }
        Check(pdfRejected, "PDF import is rejected before reading the file");
        var book = new ExcelWorkbookData(); var roster = Roster(["موجود", "General", "الأولى", "عام", "صباحي"]);
        book.Sheets.Add(new(roster.Name, roster.Table, roster.SourceRows) { SuggestedSchemaName = "NamedAssignments" });
        form.GetType().GetMethod("LoadWorkbook", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, [book, "roster.xlsx"]);
        var entity = (ComboBox)form.GetType().GetField("entity", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
        Check(entity.SelectedItem?.ToString() == "NamedAssignments", "UI automatically selects the named morning roster schema");
        var prepared = (List<ExcelSheetData>)form.GetType().GetMethod("PrepareSheets", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null)!;
        Check(prepared.Single().Table.Columns.Count == 2 && prepared.Single().Table.Columns.Contains("SubjectName"), "UI maps only the two name columns and ignores academic details");
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
            var names = new DataTable(); names.Columns.Add("اسم التدريسي"); names.Columns.Add("المادة");
            names.Rows.Add("جديد", "مادة جديدة"); names.Rows.Add("جديد", "مادة جديدة");
            names.Rows.Add("موجود", "General"); names.Rows.Add("منفرد", ""); names.Rows.Add("", "مادة منفردة");
            var sheet = new ExcelSheetData("Names", names, [2,3,4,5,6]);
            var preview = await service.ValidateAsync(ExcelImportMode.AcademicData, [sheet]);
            Check(preview.IsValid && preview.Changes.Count == 4, "Only two name columns import new faculty and subjects with no academic requirements");
            await using (var db = new AppDbContext()) Check(await db.Subjects.CountAsync() == 2 && await db.FacultyMembers.CountAsync() == 1, "Validation writes nothing");
            await service.SaveAsync(preview, true);
            await using (var db = new AppDbContext())
            {
                Check(await db.FacultyMembers.CountAsync() == 3 && await db.Subjects.CountAsync() == 4, "Names saved and duplicates skipped within the file and database");
                var imported = await db.Subjects.Where(s => s.StudyYearID == Subject.UnclassifiedStudyYearId).ToListAsync();
                Check(imported.Count == 2 && imported.All(s => !s.HasWeeklyLectures && s.EffectiveTheoreticalLectureCount == 0 && s.EffectivePracticalLectureCount == 0), "Name-only subjects remain unclassified and have no schedule requirements");
                Check(await db.FacultyMemberSubjects.CountAsync() == 1, "Import creates no teaching assignments");
                Check((await db.FacultyMemberSubjects.SingleAsync()).SectionID == 1, "Existing assignment remains unchanged");
                Check((await db.Subjects.FindAsync(1))!.SemesterNumber == 2, "Existing subject scope stays unchanged");
                Check((await db.FacultyMembers.FindAsync(1))!.AcademicTitle == "Lecturer", "Existing teacher details stay unchanged");
            }
            var repeated = await service.ValidateAsync(ExcelImportMode.AcademicData, [sheet]);
            Check(repeated.IsValid && repeated.Changes.Count == 0, "Reimport adds nothing");
            var extra = Roster(["موجود", "General", "invalid year", "invalid branch", "invalid session"]);
            Check((await service.ValidateAsync(ExcelImportMode.AcademicData, [extra])).IsValid, "Additional columns are ignored even when invalid");
            Check(ExcelImportSchema.AcademicInputs.Single().Columns.SequenceEqual(new[] { "FacultyName", "SubjectName" }), "All import interfaces expose only name fields");
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xlsx");
            try
            {
                ExcelImportTemplates.Create(path, ExcelImportMode.AcademicData);
                var template = ExcelWorkbookReader.Read(path);
                Check(template.Sheets.Single().Table.Columns.Count == 2, "Excel template has only two columns");
            }
            finally { File.Delete(path); }
            var list = await new SubjectService().GetAllAsync();
            Check(list.Any(s => s.SubjectName == "مادة جديدة"), "Imported names appear in the normal subject list");
            Check(!(await new StudyYearService().GetAllAsync()).Any(y => y.StudyYearID == 0), "Reserved classification is not offered as an academic study year");
            var promoted = list.Single(s => s.SubjectName == "مادة جديدة");
            promoted.StudyYearID = 1; promoted.SemesterNumber = 2; promoted.TheoreticalLectureCount = 1;
            await new SubjectService().UpdateAsync(promoted);
            Check(!(await new SubjectService().GetByIdAsync(promoted.SubjectID))!.IsUnclassified, "Subject academic details can be completed later");
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
