using System.Data;
using System.Globalization;
using ClosedXML.Excel;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

public static class ExcelWorkbookReader
{
    public static ExcelWorkbookData Read(string path, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Select an .xlsx workbook.");
        cancellationToken.ThrowIfCancellationRequested();
        using var snapshot = new MemoryStream();
        try
        {
            // Excel can keep the workbook open for editing. Read only the saved
            // bytes with sharing enabled; ClosedXML operates on our own snapshot.
            using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            source.CopyTo(snapshot);
        }
        catch (IOException ex) when ((ex.HResult & 0xffff) is 32 or 33)
        {
            throw new IOException("تعذّر قراءة ملف Excel لأن برنامجاً آخر يمنع الوصول إليه. احفظ الملف وأغلقه في Excel، ثم أعد المحاولة، أو استورد نسخة منه باسم جديد.", ex);
        }
        cancellationToken.ThrowIfCancellationRequested();
        snapshot.Position = 0;
        using var workbook = new XLWorkbook(snapshot);
        var result = new ExcelWorkbookData();
        foreach (var sheet in workbook.Worksheets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sheet.Name.Equals("Instructions", StringComparison.OrdinalIgnoreCase) ||
                sheet.Name.StartsWith("Example_", StringComparison.OrdinalIgnoreCase)) continue;
            var range = sheet.RangeUsed(XLCellsUsedOptions.Contents);
            if (range is null) continue;
            if (range.LastColumn().ColumnNumber() > 100 || range.LastRow().RowNumber() > 50000)
                throw new ArgumentException($"{sheet.Name}: maximum 100 columns and 50,000 rows per sheet.");
            var table = new DataTable(sheet.Name) { Locale = CultureInfo.InvariantCulture };
            int width = range.LastColumn().ColumnNumber();
            for (int c = 1; c <= width; c++)
            {
                var cell = sheet.Cell(1, c);
                string header = cell.GetString().Trim().TrimEnd('*').Trim();
                if (cell.HasFormula || header.Length == 0)
                    throw new ArgumentException($"{sheet.Name}: column {c} needs a text header in row 1.");
                if (table.Columns.Cast<DataColumn>().Any(x => x.ColumnName.Equals(header, StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException($"{sheet.Name}: duplicate column {header}.");
                table.Columns.Add(header, typeof(string));
            }
            var sourceRows = new List<int>();
            for (int r = 2; r <= range.LastRow().RowNumber(); r++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var values = new string[width];
                for (int c = 1; c <= width; c++)
                {
                    var cell = sheet.Cell(r, c);
                    if (cell.HasFormula)
                        throw new ArgumentException($"{sheet.Name}, row {r}, column {c}: replace formulas with values before import.");
                    if (cell.DataType == XLDataType.Error)
                        throw new ArgumentException($"{sheet.Name}, row {r}, column {c}: Excel error value.");
                    values[c - 1] = cell.DataType switch
                    {
                        XLDataType.Number => cell.GetDouble().ToString("G17", CultureInfo.InvariantCulture),
                        XLDataType.TimeSpan => cell.GetTimeSpan().ToString(@"hh\:mm", CultureInfo.InvariantCulture),
                        XLDataType.DateTime => cell.GetDateTime().TimeOfDay.ToString(@"hh\:mm", CultureInfo.InvariantCulture),
                        _ => cell.GetString().Trim()
                    };
                }
                if (values.All(string.IsNullOrWhiteSpace)) continue;
                table.Rows.Add(values.Cast<object>().ToArray());
                sourceRows.Add(r);
            }
            result.Sheets.Add(new(sheet.Name, table, sourceRows));
        }
        if (result.Sheets.Count == 0) throw new ArgumentException("The workbook has no import worksheets.");
        return result;
    }
}
