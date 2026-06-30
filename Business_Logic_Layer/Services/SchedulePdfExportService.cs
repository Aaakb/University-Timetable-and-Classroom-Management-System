using System.Globalization;
using System.Text;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    public sealed class SchedulePdfExportService
    {
        private const int PageWidth = 842;
        private const int PageHeight = 595;
        private const int Margin = 30;
        private const int TableTop = 486;
        private const int HeaderHeight = 30;
        private const int TimeColumnWidth = 112;
        private const int LectureRowHeight = 72;
        private const int BreakRowHeight = 24;
        private const int TableWidth = 782;

        private const string HeaderColor = "0.10 0.18 0.31";
        private const string HeaderTextColor = "1 1 1";
        private const string BorderColor = "0.77 0.84 0.91";
        private const string RowLineColor = "0.88 0.92 0.96";
        private const string AlternatingRowColor = "0.97 0.98 1";
        private const string BreakColor = "0.85 0.95 0.82";
        private const string TextColor = "0.05 0.09 0.18";
        private const string MutedTextColor = "0.30 0.38 0.50";

        public async Task ExportAsync(string filePath, IReadOnlyList<SchedulePdfRow> rows)
        {
            byte[] pdfBytes = BuildPdf(rows);
            await File.WriteAllBytesAsync(filePath, pdfBytes);
        }

        private static byte[] BuildPdf(IReadOnlyList<SchedulePdfRow> rows)
        {
            var objects = new List<string>();
            int catalogObjectId = AddObject(objects, string.Empty);
            int pagesObjectId = AddObject(objects, string.Empty);
            int fontObjectId = AddObject(objects, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

            var pageRows = BuildPages(rows);
            var pageObjectIds = new List<int>();

            foreach (var page in pageRows)
            {
                int contentObjectId = objects.Count + 1;
                AddObject(objects, BuildPageContent(page));

                int pageObjectId = objects.Count + 1;
                pageObjectIds.Add(pageObjectId);
                AddObject(objects,
                    $"<< /Type /Page /Parent {pagesObjectId} 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] /Resources << /Font << /F1 {fontObjectId} 0 R >> >> /Contents {contentObjectId} 0 R >>");
            }

            objects[catalogObjectId - 1] = $"<< /Type /Catalog /Pages {pagesObjectId} 0 R >>";
            objects[pagesObjectId - 1] = $"<< /Type /Pages /Kids [{string.Join(" ", pageObjectIds.Select(id => $"{id} 0 R"))}] /Count {pageObjectIds.Count} >>";

            return WritePdf(objects);
        }

        private static List<SchedulePdfPage> BuildPages(IReadOnlyList<SchedulePdfRow> rows)
        {
            if (rows.Count == 0)
            {
                return [new SchedulePdfPage(new SchedulePdfPageKey(0, "No Records", "-", "-"), [])];
            }

            return rows
                .GroupBy(row => new SchedulePdfPageKey(
                    row.SemesterNumber,
                    row.StudyYear,
                    NormalizeBranch(row.Branch),
                    CleanSection(row.Section)))
                .OrderBy(group => StudyYearOrder(group.Key.StudyYear))
                .ThenBy(group => group.Key.Branch)
                .ThenBy(group => group.Key.Section)
                .ThenBy(group => group.Key.SemesterNumber)
                .Select(group => new SchedulePdfPage(
                    group.Key,
                    group.OrderBy(row => DayOrder(row.DayOfWeek))
                        .ThenBy(row => ParseDisplayTime(row.StartTime))
                        .ThenBy(row => row.GroupName)
                        .ThenBy(row => row.Subject)
                        .ToList()))
                .ToList();
        }

        private static string BuildPageContent(SchedulePdfPage page)
        {
            var builder = new StringBuilder();
            var days = GetPageDays(page);
            var timeline = BuildTimeline(page.Rows);

            WritePageTitle(builder, page);
            DrawTable(builder, page.Rows, days, timeline);

            string content = builder.ToString();
            return $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}endstream";
        }

        private static void WritePageTitle(StringBuilder builder, SchedulePdfPage page)
        {
            string title = page.Key.SemesterNumber == 0
                ? "Schedule"
                : $"Semester {page.Key.SemesterNumber} - {page.Key.StudyYear} - {page.Key.Branch} - Section {page.Key.Section}";

            WriteText(builder, Margin, 552, title, 14, TextColor);
            WriteText(builder, Margin, 532, "Weekly timetable by day, period, room, and instructor.", 8, MutedTextColor);
        }

        private static void DrawTable(
            StringBuilder builder,
            IReadOnlyList<SchedulePdfRow> rows,
            IReadOnlyList<string> days,
            IReadOnlyList<PdfTimeBlock> timeline)
        {
            int dayColumnWidth = (TableWidth - TimeColumnWidth) / days.Count;
            int tableHeight = HeaderHeight + timeline.Sum(row => row.IsBreak ? BreakRowHeight : LectureRowHeight);
            int tableBottom = TableTop - tableHeight;

            DrawFilledRect(builder, Margin, TableTop - HeaderHeight, TableWidth, HeaderHeight, HeaderColor);
            DrawRect(builder, Margin, tableBottom, TableWidth, tableHeight, BorderColor);
            WriteHeader(builder, days, dayColumnWidth);

            int rowTop = TableTop - HeaderHeight;

            for (int index = 0; index < timeline.Count; index++)
            {
                var block = timeline[index];
                int rowHeight = block.IsBreak ? BreakRowHeight : LectureRowHeight;
                int rowBottom = rowTop - rowHeight;

                if (block.IsBreak)
                {
                    DrawFilledRect(builder, Margin, rowBottom, TableWidth, rowHeight, BreakColor);
                }
                else if (index % 2 == 1)
                {
                    DrawFilledRect(builder, Margin, rowBottom, TableWidth, rowHeight, AlternatingRowColor);
                }

                WriteText(builder, Margin + 8, rowBottom + rowHeight / 2 - 3, block.Label, 7, TextColor);

                if (block.IsBreak)
                {
                    WriteBreakCells(builder, days, dayColumnWidth, rowBottom, rowHeight);
                }
                else
                {
                    WriteLectureCells(builder, rows, days, dayColumnWidth, block, rowBottom, rowHeight);
                }

                DrawLine(builder, Margin, rowBottom, Margin + TableWidth, rowBottom, RowLineColor);
                rowTop = rowBottom;
            }

            DrawVerticalLines(builder, days.Count, dayColumnWidth, tableBottom);
        }

        private static void WriteHeader(StringBuilder builder, IReadOnlyList<string> days, int dayColumnWidth)
        {
            WriteText(builder, Margin + 36, TableTop - 19, "Time", 9, HeaderTextColor);

            for (int index = 0; index < days.Count; index++)
            {
                int x = Margin + TimeColumnWidth + index * dayColumnWidth;
                WriteText(builder, x + 12, TableTop - 19, days[index], 9, HeaderTextColor);
            }
        }

        private static void WriteBreakCells(
            StringBuilder builder,
            IReadOnlyList<string> days,
            int dayColumnWidth,
            int rowBottom,
            int rowHeight)
        {
            for (int index = 0; index < days.Count; index++)
            {
                int x = Margin + TimeColumnWidth + index * dayColumnWidth;
                WriteText(builder, x + dayColumnWidth / 2 - 14, rowBottom + rowHeight / 2 - 3, "Break", 8, TextColor);
            }
        }

        private static void WriteLectureCells(
            StringBuilder builder,
            IReadOnlyList<SchedulePdfRow> rows,
            IReadOnlyList<string> days,
            int dayColumnWidth,
            PdfTimeBlock block,
            int rowBottom,
            int rowHeight)
        {
            for (int index = 0; index < days.Count; index++)
            {
                string day = days[index];
                int x = Margin + TimeColumnWidth + index * dayColumnWidth;
                var entries = rows
                    .Where(row =>
                        string.Equals(row.DayOfWeek, day, StringComparison.OrdinalIgnoreCase) &&
                        ParseDisplayTime(row.StartTime) == block.Start &&
                        ParseDisplayTime(row.EndTime) == block.End)
                    .OrderBy(row => BuildGroupLabel(row))
                    .ThenBy(row => row.Subject)
                    .ToList();

                if (entries.Count == 0)
                {
                    continue;
                }

                WriteCellEntries(builder, x + 6, rowBottom + rowHeight - 11, dayColumnWidth - 12, entries);
            }
        }

        private static void WriteCellEntries(
            StringBuilder builder,
            int x,
            int yTop,
            int cellWidth,
            IReadOnlyList<SchedulePdfRow> entries)
        {
            var lines = new List<string>();

            foreach (var entry in entries.Take(2))
            {
                if (lines.Count > 0)
                {
                    lines.Add(string.Empty);
                }

                string typeAndGroup = BuildTypeLabel(entry);
                string group = BuildGroupLabel(entry);

                if (group != "-")
                {
                    typeAndGroup += $" {group}";
                }

                lines.Add(Shorten(entry.Subject, GetCellMaxLength(cellWidth)));
                lines.Add(Shorten(entry.FacultyMember, GetCellMaxLength(cellWidth)));
                lines.Add(Shorten($"{entry.Classroom} | {typeAndGroup}", GetCellMaxLength(cellWidth)));
            }

            if (entries.Count > 2)
            {
                lines.Add($"+{entries.Count - 2} more");
            }

            int y = yTop;

            foreach (string line in lines.Take(7))
            {
                WriteText(builder, x, y, line, string.IsNullOrEmpty(line) ? 4 : 6, TextColor);
                y -= 8;
            }
        }

        private static IReadOnlyList<string> GetPageDays(SchedulePdfPage page)
        {
            return ScheduleDayRules.GetSchedulingDays(page.Key.StudyYear)
                .Concat(page.Rows.Select(row => row.DayOfWeek))
                .Where(day => !string.IsNullOrWhiteSpace(day))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(DayOrder)
                .ToList();
        }

        private static IReadOnlyList<PdfTimeBlock> BuildTimeline(IReadOnlyList<SchedulePdfRow> rows)
        {
            var lectureBlocks = rows
                .Select(row => new PdfTimeBlock(
                    $"{row.StartTime} - {row.EndTime}",
                    ParseDisplayTime(row.StartTime),
                    ParseDisplayTime(row.EndTime),
                    false))
                .Where(block => block.Start != TimeSpan.Zero || block.End != TimeSpan.Zero)
                .Distinct()
                .OrderBy(block => block.Start)
                .ToList();

            if (lectureBlocks.Count == 0)
            {
                return [new PdfTimeBlock("No records", TimeSpan.Zero, TimeSpan.Zero, false)];
            }

            var timeline = new List<PdfTimeBlock>();

            foreach (var block in lectureBlocks)
            {
                if (HasConfiguredBreak() &&
                    timeline.Count > 0 &&
                    timeline[^1].Start < ScheduleTimingRules.BreakStart &&
                    block.Start >= ScheduleTimingRules.BreakEnd)
                {
                    timeline.Add(new PdfTimeBlock(
                        $"{FormatDisplayTime(ScheduleTimingRules.BreakStart)} - {FormatDisplayTime(ScheduleTimingRules.BreakEnd)}",
                        ScheduleTimingRules.BreakStart,
                        ScheduleTimingRules.BreakEnd,
                        true));
                }

                timeline.Add(block);
            }

            return timeline;
        }

        private static bool HasConfiguredBreak()
        {
            return ScheduleTimingRules.BreakEnd > ScheduleTimingRules.BreakStart;
        }

        private static int GetCellMaxLength(int cellWidth)
        {
            return Math.Max(24, cellWidth / 4);
        }

        private static string BuildTypeLabel(SchedulePdfRow row)
        {
            return string.Equals(row.LectureType, "Practical", StringComparison.OrdinalIgnoreCase)
                ? "Practical"
                : "Theory";
        }

        private static string BuildGroupLabel(SchedulePdfRow row)
        {
            return string.IsNullOrWhiteSpace(row.GroupName) ||
                row.GroupName == "-" ||
                row.GroupName == "All"
                ? "-"
                : row.GroupName.Trim();
        }

        private static string NormalizeBranch(string branch)
        {
            return string.IsNullOrWhiteSpace(branch) || branch == "-"
                ? "General"
                : branch.Trim();
        }

        private static string CleanSection(string section)
        {
            return section.Split(" - ", StringSplitOptions.TrimEntries)[0];
        }

        private static TimeSpan ParseDisplayTime(string value)
        {
            return DateTime.TryParseExact(
                value,
                "hh:mm tt",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dateTime)
                ? dateTime.TimeOfDay
                : TimeSpan.Zero;
        }

        private static string FormatDisplayTime(TimeSpan value)
        {
            return DateTime.Today.Add(value).ToString("hh:mm tt", CultureInfo.InvariantCulture);
        }

        private static int StudyYearOrder(string yearName)
        {
            return yearName.Trim().ToLowerInvariant() switch
            {
                "first year" => 1,
                "second year" => 2,
                "third year" => 3,
                "fourth year" => 4,
                _ => 99
            };
        }

        private static int DayOrder(string day)
        {
            return day.Trim().ToLowerInvariant() switch
            {
                "sunday" => 1,
                "monday" => 2,
                "tuesday" => 3,
                "wednesday" => 4,
                "thursday" => 5,
                _ => 99
            };
        }

        private static void DrawVerticalLines(StringBuilder builder, int dayCount, int dayColumnWidth, int tableBottom)
        {
            int x = Margin;
            DrawLine(builder, x, tableBottom, x, TableTop, BorderColor);

            x += TimeColumnWidth;
            DrawLine(builder, x, tableBottom, x, TableTop, BorderColor);

            for (int index = 0; index < dayCount; index++)
            {
                x += dayColumnWidth;
                DrawLine(builder, x, tableBottom, x, TableTop, BorderColor);
            }
        }

        private static void WriteText(StringBuilder builder, int x, int y, string text, int fontSize, string color)
        {
            builder.AppendLine("BT");
            builder.AppendLine($"/F1 {fontSize} Tf");
            builder.AppendLine($"{color} rg");
            builder.AppendLine($"1 0 0 1 {x} {y} Tm ({Escape(text)}) Tj");
            builder.AppendLine("ET");
        }

        private static void DrawFilledRect(StringBuilder builder, int x, int y, int width, int height, string color)
        {
            builder.AppendLine("q");
            builder.AppendLine($"{color} rg");
            builder.AppendLine($"{x} {y} {width} {height} re f");
            builder.AppendLine("Q");
        }

        private static void DrawRect(StringBuilder builder, int x, int y, int width, int height, string color)
        {
            builder.AppendLine("q");
            builder.AppendLine($"{color} RG");
            builder.AppendLine("0.7 w");
            builder.AppendLine($"{x} {y} {width} {height} re S");
            builder.AppendLine("Q");
        }

        private static void DrawLine(StringBuilder builder, int x1, int y1, int x2, int y2, string color)
        {
            builder.AppendLine("q");
            builder.AppendLine($"{color} RG");
            builder.AppendLine("0.45 w");
            builder.AppendLine($"{x1} {y1} m {x2} {y2} l S");
            builder.AppendLine("Q");
        }

        private static string Shorten(string? value, int maxLength)
        {
            string text = string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
            return text.Length <= maxLength ? text : text[..Math.Max(0, maxLength - 3)] + "...";
        }

        private static string Escape(string value)
        {
            var safe = new string(value.Select(ch => ch is >= ' ' and <= '~' ? ch : '?').ToArray());
            return safe.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        }

        private static int AddObject(List<string> objects, string content)
        {
            objects.Add(content);
            return objects.Count;
        }

        private static byte[] WritePdf(IReadOnlyList<string> objects)
        {
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);
            var offsets = new List<long> { 0 };

            writer.WriteLine("%PDF-1.4");
            writer.Flush();

            for (int index = 0; index < objects.Count; index++)
            {
                offsets.Add(stream.Position);
                writer.WriteLine($"{index + 1} 0 obj");
                writer.WriteLine(objects[index]);
                writer.WriteLine("endobj");
                writer.Flush();
            }

            long xrefOffset = stream.Position;
            writer.WriteLine("xref");
            writer.WriteLine($"0 {objects.Count + 1}");
            writer.WriteLine("0000000000 65535 f ");

            foreach (long offset in offsets.Skip(1))
            {
                writer.WriteLine($"{offset:0000000000} 00000 n ");
            }

            writer.WriteLine("trailer");
            writer.WriteLine($"<< /Size {objects.Count + 1} /Root 1 0 R >>");
            writer.WriteLine("startxref");
            writer.WriteLine(xrefOffset);
            writer.WriteLine("%%EOF");
            writer.Flush();

            return stream.ToArray();
        }
    }

    public sealed record SchedulePdfRow(
        int SemesterNumber,
        string StudyYear,
        string Branch,
        string Section,
        string GroupName,
        string LectureType,
        string Subject,
        string FacultyMember,
        string Classroom,
        string DayOfWeek,
        string StartTime,
        string EndTime);

    internal sealed record SchedulePdfPage(
        SchedulePdfPageKey Key,
        IReadOnlyList<SchedulePdfRow> Rows);

    internal sealed record SchedulePdfPageKey(
        int SemesterNumber,
        string StudyYear,
        string Branch,
        string Section);

    internal sealed record PdfTimeBlock(
        string Label,
        TimeSpan Start,
        TimeSpan End,
        bool IsBreak);
}
