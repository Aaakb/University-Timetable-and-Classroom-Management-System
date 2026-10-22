using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

/// <summary>Reads text tables with one record per line and a repeated header on each page.</summary>
public static class PdfTableReader
{
    private sealed record Cell(string Text, double Left, double Right);
    private sealed record Line(double Y, List<Word> Words);

    public static ExcelWorkbookData Read(string path, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (!string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("اختر ملفاً بصيغة PDF.");
        if (new FileInfo(path).Length > 50 * 1024 * 1024)
            throw new ArgumentException("حجم ملف PDF يتجاوز الحد المسموح: 50 ميغابايت.");
        try
        {
            using var document = PdfDocument.Open(path);
            if (document.NumberOfPages > 200)
                throw new ArgumentException("الحد الأقصى لملف PDF هو 200 صفحة. قسّم الملف ثم أعد المحاولة.");
            var result = new ExcelWorkbookData();
            int totalRows = 0;
            foreach (var page in document.GetPages())
            {
                token.ThrowIfCancellationRequested();
                var words = page.GetWords().Where(w => !string.IsNullOrWhiteSpace(w.Text)).ToList();
                if (words.Count == 0)
                    throw new ArgumentException($"الصفحة {page.Number} لا تحتوي نصاً قابلاً للاستخراج. ملفات PDF المصوّرة تحتاج إلى التعرّف الضوئي (OCR) قبل الاستيراد.");
                if (words.Count > 100000)
                    throw new ArgumentException($"الصفحة {page.Number} تحتوي بيانات أكثر من الحد المسموح.");
                var lines = Lines(words, token);
                int headerIndex = lines.FindIndex(line => IsHeader(Cells(line.Words)));
                if (headerIndex < 0 && WeeklyPdfTimetableReader.TryRead(page, words, token, out var weekly))
                {
                    totalRows += weekly.Table.Rows.Count;
                    if (totalRows > 50000) throw new ArgumentException("الحد الأقصى هو 50,000 صف لكل ملف PDF.");
                    result.Sheets.Add(weekly); continue;
                }
                if (headerIndex < 0)
                    throw new ArgumentException($"الصفحة {page.Number}: لم يُعثر على جدول بأعمدة واضحة. استخدم جدولاً نصياً، كل صف فيه سجل واحد، وعناوين أعمدة مثل القالب بالعربية أو الإنجليزية. الجداول الأسبوعية ذات الخلايا المدمجة غير مدعومة.");
                var headers = Cells(lines[headerIndex].Words);
                if (headers.Count > 100) throw new ArgumentException("الحد الأقصى هو 100 عمود لكل جدول.");
                var table = new DataTable { Locale = CultureInfo.InvariantCulture };
                foreach (var cell in headers)
                {
                    string name = Clean(cell.Text).TrimEnd('*').Trim();
                    if (table.Columns.Cast<DataColumn>().Any(c => ImportLabels.Normalize(c.ColumnName) == ImportLabels.Normalize(name)))
                        throw new ArgumentException($"الصفحة {page.Number}: عنوان العمود «{name}» مكرر.");
                    table.Columns.Add(name, typeof(string));
                }
                // Full rows refine the whitespace boundaries when numeric values and headings
                // have different alignment. Empty cells still retain their original positions.
                var completeRows = lines.Skip(headerIndex + 1).Select(l => Cells(l.Words))
                    .Where(cells => cells.Count == headers.Count).Prepend(headers).ToList();
                var boundaries = new double[headers.Count - 1];
                for (int c = 0; c < boundaries.Length; c++)
                {
                    double left = completeRows.Max(cells => cells[c].Right);
                    double right = completeRows.Min(cells => cells[c + 1].Left);
                    if (left >= right)
                        throw new ArgumentException($"الصفحة {page.Number}: لا يمكن فصل الأعمدة بوضوح. استخدم أعمدة متباعدة أو ملف Excel.");
                    boundaries[c] = (left + right) / 2;
                }
                var rows = new List<int>();
                for (int i = headerIndex + 1; i < lines.Count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    if (IsHeader(Cells(lines[i].Words)))
                        throw new ArgumentException($"الصفحة {page.Number}: يوجد أكثر من جدول أو عنوان داخل البيانات. ضع كل جدول في صفحة مستقلة.");
                    var buckets = Enumerable.Range(0, headers.Count).Select(_ => new List<Word>()).ToArray();
                    foreach (var word in lines[i].Words)
                    {
                        double center = (word.BoundingBox.Left + word.BoundingBox.Right) / 2;
                        int column = Array.FindIndex(boundaries, boundary => center < boundary);
                        if (column < 0) column = headers.Count - 1;
                        if ((column > 0 && word.BoundingBox.Left < boundaries[column - 1] - 2) ||
                            (column < boundaries.Length && word.BoundingBox.Right > boundaries[column] + 2))
                            throw new ArgumentException($"الصفحة {page.Number}، السطر {i + 1}: النص يتداخل بين الأعمدة. وسّع الأعمدة أو استخدم ملف Excel لضمان صحة البيانات.");
                        buckets[column].Add(word);
                    }
                    var values = buckets.Select(Text).ToArray();
                    // Do not guess whether a one-cell line is a footer or a wrapped record.
                    if (values.Count(v => v.Length > 0) < 2)
                        throw new ArgumentException($"الصفحة {page.Number}، السطر {i + 1}: سطر منفرد أو خلية متعددة الأسطر. احذف التذييلات واجعل كل سجل في سطر مستقل قبل الاستيراد.");
                    table.Rows.Add(values.Cast<object>().ToArray()); rows.Add(i + 1);
                    if (++totalRows > 50000) throw new ArgumentException("الحد الأقصى هو 50,000 صف لكل ملف PDF.");
                }
                if (table.Rows.Count == 0) throw new ArgumentException($"الصفحة {page.Number}: الجدول لا يحتوي صفوف بيانات.");
                var schema = ExcelImportSchema.Academic.Append(ExcelImportSchema.Timetable).FirstOrDefault(s => s.Required.All(c =>
                    table.Columns.Cast<DataColumn>().Any(h => ImportLabels.MatchColumn(h.ColumnName, [c]) is not null)));
                result.Sheets.Add(new($"صفحة {page.Number}", table, rows) { SuggestedSchemaName = schema?.Name });
            }
            if (result.Sheets.Count == 0) throw new ArgumentException("ملف PDF لا يحتوي جداول قابلة للاستيراد.");
            return result;
        }
        catch (Exception ex) when (ex is not (ArgumentException or OperationCanceledException or IOException or UnauthorizedAccessException))
        {
            throw new ArgumentException("تعذّرت قراءة ملف PDF. تأكد من سلامته وأنه غير محمي بكلمة مرور.", ex);
        }
    }

    private static List<Line> Lines(List<Word> words, CancellationToken token)
    {
        var lines = new List<Line>();
        foreach (var word in words.OrderByDescending(w => w.BoundingBox.Bottom))
        {
            token.ThrowIfCancellationRequested();
            double y = word.BoundingBox.Bottom;
            var line = lines.LastOrDefault();
            if (line is null || Math.Abs(line.Y - y) > Math.Max(2, word.BoundingBox.Height * .4))
                lines.Add(new(y, [word]));
            else line.Words.Add(word);
        }
        return lines;
    }
    private static List<Cell> Cells(List<Word> words)
    {
        var groups = new List<List<Word>>();
        foreach (var word in words.OrderBy(w => w.BoundingBox.Left))
        {
            var group = groups.LastOrDefault();
            if (group is null || word.BoundingBox.Left - group.Max(w => w.BoundingBox.Right) > Math.Max(8, word.BoundingBox.Height * .9))
                groups.Add([word]);
            else group.Add(word);
        }
        return groups.Select(g => new Cell(Text(g), g.Min(w => w.BoundingBox.Left), g.Max(w => w.BoundingBox.Right))).ToList();
    }
    private static bool IsHeader(List<Cell> cells)
    {
        var columns = ExcelImportSchema.Academic.SelectMany(s => s.Columns).Concat(ExcelImportSchema.Timetable.Columns).Distinct().ToArray();
        return cells.Count >= 2 && cells.Count(c => ImportLabels.MatchColumn(c.Text, columns) is not null) >= 2;
    }
    private static string Text(List<Word> words)
    {
        bool arabic = words.SelectMany(w => w.Text).Any(IsArabic);
        var ordered = arabic ? words.OrderByDescending(w => w.BoundingBox.Left) : words.OrderBy(w => w.BoundingBox.Left);
        return Clean(string.Join(" ", ordered.Select(WordText)));
    }
    private static bool IsArabic(char c) => c is >= '\u0600' and <= '\u06ff' or >= '\ufb50' and <= '\ufdff' or >= '\ufe70' and <= '\ufeff';
    internal static string WordText(Word word)
    {
        // PdfPig commonly returns Arabic glyphs in visual (left-to-right) order.
        // Reverse Arabic runs before decomposing presentation ligatures such as lam-alef.
        bool visualOrder = word.Letters.Count > 1 && word.Letters[0].StartBaseLine.X < word.Letters[^1].StartBaseLine.X;
        if (!visualOrder || !word.Text.Any(IsArabic)) return Clean(word.Text);
        // A PDF glyph may map to several Unicode characters (for example lam-alef).
        // Reverse glyphs, not characters, so names such as جلال remain intact.
        var runs = new List<(bool Arabic, List<string> Values)>();
        foreach (var letter in word.Letters)
        {
            bool arabic = letter.Value.Any(c => IsArabic(c) && char.IsLetter(c));
            if (runs.Count == 0 || runs[^1].Arabic != arabic) runs.Add((arabic, []));
            runs[^1].Values.Add(letter.Value);
        }
        runs.Reverse();
        return Clean(string.Concat(runs.Select(run => string.Concat(run.Arabic ? run.Values.AsEnumerable().Reverse() : run.Values))));
    }
    private static string Clean(string text) => new(text.Normalize(NormalizationForm.FormKC)
        .Replace('ی', 'ي').Replace('ک', 'ك')
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.Format).ToArray());
}
