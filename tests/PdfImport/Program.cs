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
}
