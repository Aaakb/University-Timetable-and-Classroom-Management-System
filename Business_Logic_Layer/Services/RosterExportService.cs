using ClosedXML.Excel;
using SkiaSharp;
using Topten.RichTextKit;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

public sealed record RosterRow(string Name, string Detail);

public static class RosterExportService
{
    private static readonly SKColor Ink = new(48, 56, 43);
    private static readonly SKColor Stripe = new(244, 245, 235);

    public static void Export(string path, string title, string nameHeader, string detailHeader, IReadOnlyList<RosterRow> rows)
    {
        if (rows.Count == 0) throw new ArgumentException("لا توجد سجلات للتصدير.");
        string[] headings = ["ت", nameHeader, detailHeader];
        path = Path.GetFullPath(path);
        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is not (".pdf" or ".xlsx")) throw new ArgumentException("اختر PDF أو Excel بصيغة XLSX.");

        string temporaryPath = Path.Combine(Path.GetDirectoryName(path)!, $".{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                if (extension == ".pdf") WritePdf(stream, title, headings, rows);
                else WriteExcel(stream, title, headings, rows);
            }
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static void WriteExcel(Stream stream, string title, string[] headings, IReadOnlyList<RosterRow> rows)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("القائمة");
        sheet.RightToLeft = true;
        sheet.Style.Font.FontName = "Arial";
        sheet.Style.Font.FontSize = 11;
        var titleRow = sheet.Range(1, 1, 1, headings.Length).Merge();
        titleRow.FirstCell().Value = title;
        titleRow.Style.Font.Bold = true;
        titleRow.Style.Font.FontSize = 14;
        sheet.Row(1).Height = 26;

        for (int column = 0; column < headings.Length; column++) sheet.Cell(2, column + 1).Value = headings[column];
        var header = sheet.Range(2, 1, 2, headings.Length);
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#30382B");
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Font.Bold = true;
        sheet.Row(2).Height = 22;

        for (int index = 0; index < rows.Count; index++)
        {
            int row = index + 3;
            sheet.Cell(row, 1).Value = index + 1;
            sheet.Cell(row, 2).Value = rows[index].Name;
            sheet.Cell(row, 3).Value = rows[index].Detail;
            if (index % 2 == 1) sheet.Range(row, 1, row, headings.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#F4F5EB");
        }
        sheet.Column(1).Width = 8;
        sheet.Column(2).Width = 55;
        sheet.Column(3).Width = 28;
        var table = sheet.Range(2, 1, rows.Count + 2, headings.Length);
        table.Style.Alignment.WrapText = true;
        sheet.Rows(3, rows.Count + 2).AdjustToContents();
        sheet.SheetView.FreezeRows(2);
        table.SetAutoFilter();
        sheet.PageSetup.SetRowsToRepeatAtTop(1, 2);
        book.SaveAs(stream);
    }

    private static void WritePdf(Stream stream, string title, string[] headings, IReadOnlyList<RosterRow> rows)
    {
        const int pageWidth = 595, pageHeight = 842, margin = 32, width = pageWidth - 2 * margin;
        int[] columnWidths = [35, 336, 160];
        SchedulePdfExportService.EnsurePdfNativeLibraries();
        using var document = SKDocument.CreatePdf(stream);
        using var paint = new SKPaint();
        var titleBlock = MakeText(title, width, 19, Ink, bold: true);
        var header = headings.Select((heading, column) => MakeText(heading, columnWidths[column] - 12, 11, SKColors.White, bold: true)).ToArray();
        float headerHeight = header.Max(block => block.MeasuredHeight) + 12;
        float top = margin + titleBlock.MeasuredHeight + 18;
        const int bottom = pageHeight - margin - 18;
        int index = 0, page = 0;
        while (index < rows.Count)
        {
            var canvas = document.BeginPage(pageWidth, pageHeight);
            titleBlock.Paint(canvas, new SKPoint(margin, margin));
            DrawRow(canvas, top, headerHeight, header, Ink);
            float y = top + headerHeight;
            while (index < rows.Count)
            {
                var row = rows[index];
                string[] values = [(index + 1).ToString(), row.Name, row.Detail];
                var cells = values.Select((value, column) => MakeText(value, columnWidths[column] - 12, 11, Ink)).ToArray();
                float height = Math.Max(24, cells.Max(block => block.MeasuredHeight) + 10);
                if (height > bottom - top - headerHeight)
                    throw new ArgumentException("محتوى أحد السجلات أطول من مساحة صفحة PDF.");
                if (y + height > bottom) break;
                DrawRow(canvas, y, height, cells, index % 2 == 0 ? SKColors.White : Stripe);
                y += height;
                index++;
            }
            MakeText($"صفحة {++page}", width, 9, Ink).Paint(canvas, new SKPoint(margin, pageHeight - margin));
            document.EndPage();
        }
        document.Close();

        void DrawRow(SKCanvas canvas, float y, float height, TextBlock[] cells, SKColor color)
        {
            paint.Color = color;
            canvas.DrawRect(margin, y, width, height, paint);
            float right = pageWidth - margin;
            for (int column = 0; column < cells.Length; column++)
            {
                right -= columnWidths[column];
                var cell = cells[column];
                cell.Paint(canvas, new SKPoint(right + 6, y + (height - cell.MeasuredHeight) / 2));
            }
        }
    }

    private static TextBlock MakeText(string text, float width, int size, SKColor color, bool bold = false)
    {
        var block = new TextBlock { MaxWidth = width, Alignment = TextAlignment.Right };
        block.AddText(text, new Style { FontFamily = "Arial", FontSize = size, TextColor = color, FontWeight = bold ? 700 : 400 });
        return block;
    }
}
