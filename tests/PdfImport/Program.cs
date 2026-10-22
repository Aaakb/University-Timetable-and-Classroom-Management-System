using System.Data;
using System.Reflection;
using System.Runtime.InteropServices;
using ClosedXML.Excel;
using SkiaSharp;
using Topten.RichTextKit;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using University_Timetable_and_Classroom_Management_System;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;

internal static class Program
{
    private static readonly string Output = Path.GetFullPath("output/pdf-import-verification");
    private static int passed;
    [STAThread]
    private static void Main()
    {
        Directory.CreateDirectory(Output);
        Check("Multiple pages, source line numbers and empty cells", () =>
        {
            var read = ImportFileReader.Read(Pdf("academic.PDF", ["FacultyMemberID", "FullName", "AcademicTitle"],
                [["1", "Ahmed Ali", ""], ["2", "Sara Hassan", "Professor"]], 2));
            Assert(read.Sheets.Count == 2);
            foreach (var sheet in read.Sheets)
            {
                Assert(sheet.SuggestedSchemaName == "FacultyMembers");
                Assert(sheet.SourceRows.SequenceEqual([2, 3]));
                Assert((string)sheet.Table.Rows[0][1] == "Ahmed Ali");
                Assert((string)sheet.Table.Rows[0][2] == "");
                Assert((string)sheet.Table.Rows[1][2] == "Professor");
            }
        });
        Check("Timetable columns and automatic schema detection", () =>
        {
            var read = ImportFileReader.Read(Pdf("timetable.pdf", ExcelImportSchema.Timetable.Required,
                [["Sunday", "08:30", "Math", "Ahmed", "101", "A", "Theory", "1"]]));
            Assert(read.Sheets.Single().SuggestedSchemaName == "Timetable");
            Assert((string)read.Sheets[0].Table.Rows[0][1] == "08:30");
        });
        Check("Arabic column aliases and normalization", () =>
        {
            Assert(ImportLabels.MatchColumn("إسم المادة *", ["SubjectName"]) == "SubjectName");
            Assert(ImportLabels.MatchColumn("الفصل الدراسي", ["SemesterNumber"]) == "SemesterNumber");
            Assert(ImportLabels.MatchSheet("القاعات") == "Classrooms");
        });
        Check("Arabic text PDF retains names and multiword headers", ArabicPdf);
        Check("Reject image-only/empty PDFs, malformed files, duplicate headers and unstructured text", () =>
        {
            Reject(() => ImportFileReader.Read(Pdf("empty.pdf", [], [])), "OCR");
            Reject(() => ImportFileReader.Read(Pdf("duplicate.pdf", ["FullName", "FullName"], [["A", "B"]])), "مكرر");
            Reject(() => ImportFileReader.Read(Pdf("unstructured.pdf", ["A report"], [["Hello world"]])), "أعمدة");
            string bad = Path.Combine(Output, "malformed.pdf"); File.WriteAllText(bad, "not a PDF");
            Reject(() => ImportFileReader.Read(bad), "PDF");
            Reject(() => ImportFileReader.Read("bad.docx"), ".xlsx");
        });
        Check("Reject wrapped records rather than dropping a single-cell row", () =>
            Reject(() => ImportFileReader.Read(Pdf("wrapped.pdf", ["FacultyMemberID", "FullName"], [["1", "Ahmed"], ["", "Ali"]])), "سطر"));
        Check("Cancellation before reading", () =>
        {
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            try { ImportFileReader.Read(Path.Combine(Output, "academic.PDF"), cancel.Token); throw new Exception("Cancellation ignored"); }
            catch (OperationCanceledException) { }
        });
        Check("Excel routing remains available", () =>
        {
            string path = Path.Combine(Output, "existing.xlsx");
            using (var book = new XLWorkbook())
            {
                var sheet = book.AddWorksheet("Branches"); sheet.Cell(1, 1).Value = "BranchID"; sheet.Cell(1, 2).Value = "BranchName";
                sheet.Cell(3, 1).Value = 1; sheet.Cell(3, 2).Value = "البرمجيات"; book.SaveAs(path);
            }
            var read = ImportFileReader.Read(path).Sheets.Single();
            Assert(read.SourceRows.Single() == 3 && (string)read.Table.Rows[0][1] == "البرمجيات");
        });
        Check("Arabic UI, PDF preview, repair comparison and mapping across all four modes and small windows", Ui);
        Console.WriteLine($"PASS: {passed} PDF import checks. Fixtures and screenshots: {Output}");
    }
    private static void Check(string name, Action action) { action(); passed++; Console.WriteLine("PASS: " + name); }
    private static void Assert(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
    private static void Reject(Action action, string message)
    {
        try { action(); } catch (ArgumentException ex) { Assert(ex.Message.Contains(message), ex.Message); return; }
        throw new Exception("Expected rejection");
    }
    private static string Pdf(string name, string[] headers, string[][] rows, int pages = 1)
    {
        var builder = new PdfDocumentBuilder(); var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        for (int p = 0; p < pages; p++)
        {
            var page = builder.AddPage(Math.Max(640, headers.Length * 150 + 80), 480);
            for (int c = 0; c < headers.Length; c++) page.AddText(headers[c], 10, new PdfPoint(40 + c * 150, 420), font);
            for (int r = 0; r < rows.Length; r++)
            for (int c = 0; c < rows[r].Length; c++)
                if (rows[r][c].Length > 0) page.AddText(rows[r][c], 10, new PdfPoint(40 + c * 150, 390 - r * 28), font);
        }
        string path = Path.Combine(Output, name); File.WriteAllBytes(path, builder.Build()); return path;
    }
    private static void ArabicPdf()
    {
        string nativeRoot = Path.GetDirectoryName(typeof(SchedulePdfExportService).Assembly.Location)!;
        foreach (string name in new[] { "libSkiaSharp.dll", "libHarfBuzzSharp.dll" })
            NativeLibrary.Load(@"\\?\" + Path.Combine(nativeRoot, "runtimes", "win-x64", "native", name));
        string path = Path.Combine(Output, "arabic.pdf");
        using (var stream = File.Create(path))
        using (var document = SKDocument.CreatePdf(stream))
        {
            var canvas = document.BeginPage(700, 440);
            void Text(string value, float x, float y)
            {
                var text = new TextBlock { MaxWidth = 190, MaxLines = 1 };
                text.AddText(value, new Style { FontFamily = "Arial", FontSize = 13, TextColor = SKColors.Black });
                text.Paint(canvas, new SKPoint(x, y));
            }
            Text("معرف التدريسي", 480, 35); Text("الاسم الكامل", 250, 35); Text("اللقب العلمي", 20, 35);
            Text("1", 480, 70); Text("أحمد محمد", 250, 70); Text("أستاذ", 20, 70);
            document.EndPage(); document.Close();
        }
        var sheet = ImportFileReader.Read(path).Sheets.Single();
        Assert(sheet.SuggestedSchemaName == "FacultyMembers", string.Join(" | ", sheet.Table.Columns.Cast<DataColumn>().Select(c => c.ColumnName)));
        Assert(sheet.Table.Rows[0].ItemArray.Cast<string>().Contains("أحمد محمد"), string.Join(" | ", sheet.Table.Rows[0].ItemArray));
    }
    private static void Ui()
    {
        ApplicationConfiguration.Initialize();
        foreach (int modeIndex in new[] { 0, 1, 2, 3 })
        foreach (var size in new[] { new Size(800, 600), new Size(1180, 800) })
        {
            using var form = new ExcelImportForm { Size = size, ShowInTaskbar = false };
            form.Show(); Application.DoEvents();
            object Field(string name) => typeof(ExcelImportForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            ((ComboBox)Field("mode")).SelectedIndex = modeIndex;
            var workbook = ImportFileReader.Read(Path.Combine(Output, modeIndex == 0 ? "arabic.pdf" : "timetable.pdf"));
            typeof(ExcelImportForm).GetMethod("LoadWorkbook", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, [workbook, modeIndex == 0 ? "arabic.pdf" : "timetable.pdf"]);
            Application.DoEvents(); form.PerformLayout();
            Assert(form.RightToLeft == RightToLeft.Yes && form.RightToLeftLayout);
            var mapping = (DataGridView)Field("mapping");
            Assert(mapping.Rows.Cast<DataGridViewRow>().All(r => !string.IsNullOrEmpty(Convert.ToString(r.Cells[1].Value))));
            var prepared = (List<ExcelSheetData>)typeof(ExcelImportForm).GetMethod("PrepareSheets", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null)!;
            Assert(prepared.Count == 1 && prepared[0].Table.Columns.Contains(modeIndex == 0 ? "FullName" : "Day"));
            if (modeIndex == 0) Assert(prepared[0].Name == "FacultyMembers" && prepared[0].SourceName == "صفحة 1");
            foreach (string name in new[] { "validate", "generate", "save" })
            {
                var control = (Control)Field(name);
                if (control.Visible) Assert(form.ClientRectangle.Contains(form.RectangleToClient(control.RectangleToScreen(control.ClientRectangle))), "Clipped button " + name);
            }
            Assert(((DataGridView)Field("data")).Height >= 100, "Preview too short");
            if (modeIndex == 3)
            {
                Assert(((Control)Field("generate")).Visible && ((Control)Field("generate")).Text == "إصلاح الجدول المستورد");
                var grid = (DataGridView)Field("data");
                Assert(grid.Columns["Locked"] is DataGridViewCheckBoxColumn && grid.Rows[0].Cells["Locked"].Value.ToString() == "FALSE");
                var preview = new ExcelImportPreview();
                preview.RepairableConflicts.Add(new("صفحة 1", 2, "", "Classroom conflict with saved lecture #1 on Sunday, period #1."));
                preview.Lectures.Add(new("صفحة 1", 2, new() { SubjectID = 1, FacultyMemberID = 1, SectionID = 1, SemesterNumber = 1, DayOfWeek = "Sunday", LectureType = "Theory" }, false));
                typeof(ExcelImportForm).GetField("preview", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(form, preview);
                typeof(ExcelImportForm).GetMethod("ShowPreview", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
                typeof(ExcelImportForm).GetMethod("UpdateActions", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
                Assert(((Control)Field("generate")).Enabled && !((Control)Field("save")).Enabled);
                ((TabControl)Field("tabs")).SelectedIndex = 2; Application.DoEvents();
                using var conflictImage = new Bitmap(form.Width, form.Height); form.DrawToBitmap(conflictImage, new Rectangle(Point.Empty, form.Size));
                conflictImage.Save(Path.Combine(Output, $"repair-conflicts-{size.Width}.png"));
                preview.PlacementChanges.Add(new("صفحة 1", 2, "الرياضيات", "أحمد محمد", "A", "Theory", null, false,
                    "Sunday", "08:30 – 10:00", "101", "Monday", "08:30 – 10:00", "102", true));
                typeof(ExcelImportPreview).GetProperty("GenerationCompleted")!.SetValue(preview, true);
                typeof(ExcelImportForm).GetMethod("ShowPreview", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
                typeof(ExcelImportForm).GetMethod("UpdateActions", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
                Assert(((Control)Field("save")).Enabled);
                ((TabControl)Field("tabs")).SelectedIndex = 3; Application.DoEvents();
                form.DrawToBitmap(conflictImage, new Rectangle(Point.Empty, form.Size)); conflictImage.Save(Path.Combine(Output, $"repair-comparison-{size.Width}.png"));
            }
            using var image = new Bitmap(form.Width, form.Height); form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
            image.Save(Path.Combine(Output, $"arabic-import-{modeIndex}-{size.Width}.png"));
            if (modeIndex == 0 && size.Width == 1180)
            {
                var tabs = (TabControl)Field("tabs"); tabs.SelectedIndex = 1; Application.DoEvents();
                form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size)); image.Save(Path.Combine(Output, "arabic-column-mapping.png"));
                var preview = new ExcelImportPreview();
                preview.Issues.Add(new("صفحة 1", 2, "FullName", "FullName: value is required."));
                preview.Changes.Add(new("صفحة 1", 3, "Add", "2"));
                typeof(ExcelImportForm).GetField("preview", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(form, preview);
                typeof(ExcelImportForm).GetMethod("ShowPreview", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
                tabs.SelectedIndex = 2; Application.DoEvents();
                var report = (DataGridView)Field("report");
                Assert(report.Rows[0].Cells["Detail"].Value.ToString()!.Contains("القيمة مطلوبة"));
                form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size)); image.Save(Path.Combine(Output, "arabic-validation-report.png"));
            }
            form.Close();
        }
        using var automatic = new ExcelImportForm();
        typeof(ExcelImportForm).GetMethod("LoadWorkbook", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(automatic,
            [ImportFileReader.Read(Path.Combine(Output, "timetable.pdf")), "timetable.pdf"]);
        var selectedMode = (ComboBox)typeof(ExcelImportForm).GetField("mode", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(automatic)!;
        Assert(selectedMode.SelectedIndex == 1, "Timetable PDF should select timetable mode");
    }
}
