using System.Globalization;
using System.Runtime.InteropServices;
using SkiaSharp;
using Topten.RichTextKit;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    public sealed class SchedulePdfExportService
    {
        private static readonly Lazy<bool> NativeLibrariesReady = new(LoadWindowsNativeLibraries);
        private const int PageWidth = 842;
        private const int PageHeight = 595;
        private const int Margin = 30;
        private const int TableTop = 486;
        private const int HeaderHeight = 30;
        private const int TimeColumnWidth = 112;
        private const int LectureRowHeight = 84;
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

        public async Task ExportAsync(string filePath, IReadOnlyList<SchedulePdfRow> rows,
            SchedulePdfExportOptions? options = null)
        {
            byte[] pdfBytes = BuildPdf(rows, options);
            await File.WriteAllBytesAsync(filePath, pdfBytes);
        }

        private static byte[] BuildPdf(IReadOnlyList<SchedulePdfRow> rows, SchedulePdfExportOptions? options)
        {
            _ = NativeLibrariesReady.Value;
            using var stream = new MemoryStream();
            using var document = SKDocument.CreatePdf(stream);
            foreach (var page in BuildPages(rows, options))
            {
                var canvas = document.BeginPage(PageWidth, PageHeight);
                WritePageTitle(canvas, page);
                DrawTable(canvas, page.Rows, GetPageDays(page), BuildTimeline(page.Rows), page.Key.FacultyMember != null);
                document.EndPage();
            }
            document.Close();
            return stream.ToArray();
        }

        private static bool LoadWindowsNativeLibraries()
        {
            if (!OperatingSystem.IsWindows()) return true;
            string? runtime = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "win-x64",
                Architecture.X86 => "win-x86",
                Architecture.Arm64 => "win-arm64",
                _ => null
            };
            if (runtime is null) return true;

            string directory = Path.GetDirectoryName(typeof(SchedulePdfExportService).Assembly.Location)
                ?? AppContext.BaseDirectory;
            foreach (string library in new[] { "libSkiaSharp.dll", "libHarfBuzzSharp.dll" })
            {
                string path = Path.Combine(directory, "runtimes", runtime, "native", library);
                if (!File.Exists(path)) path = Path.Combine(directory, library);
                // Published/single-file applications may use the normal runtime resolver.
                if (!File.Exists(path)) continue;
                path = Path.GetFullPath(path);
                // LoadLibrary's normal path handling can fail in a deeply nested checkout.
                // Keep the native modules loaded for the lifetime of their P/Invoke users.
                string extendedPath = path.StartsWith(@"\\", StringComparison.Ordinal)
                    ? @"\\?\UNC\" + path[2..]
                    : @"\\?\" + path;
                _ = NativeLibrary.Load(extendedPath);
            }
            return true;
        }

        private static List<SchedulePdfPage> BuildPages(IReadOnlyList<SchedulePdfRow> rows, SchedulePdfExportOptions? options)
        {
            string? faculty = string.IsNullOrWhiteSpace(options?.FacultyMemberName)
                ? null : options.FacultyMemberName.Trim();
            if (faculty != null && rows.Any(row =>
                !string.Equals(row.FacultyMember.Trim(), faculty, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("An instructor timetable must contain only the selected instructor's lessons.");

            if (rows.Count == 0)
            {
                return [new SchedulePdfPage(new SchedulePdfPageKey(0, "No Records", "-", "-", faculty), [])];
            }

            return rows
                .GroupBy(row => new SchedulePdfPageKey(
                    row.SemesterNumber,
                    faculty == null ? row.StudyYear : string.Empty,
                    faculty == null ? NormalizeBranch(row.Branch) : string.Empty,
                    faculty == null ? CleanSection(row.Section) : string.Empty,
                    faculty))
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

        private static void WritePageTitle(SKCanvas builder, SchedulePdfPage page)
        {
            string title = page.Key.FacultyMember != null
                ? $"جدول التدريسي: {page.Key.FacultyMember}" +
                    (page.Key.SemesterNumber == 0 ? string.Empty : $" - الكورس {page.Key.SemesterNumber}")
                : page.Key.SemesterNumber == 0
                ? "Schedule"
                : $"Semester {page.Key.SemesterNumber} - {page.Key.StudyYear} - {page.Key.Branch} - Section {page.Key.Section}";

            WriteText(builder, Margin, 552, title, 14, TextColor);
            WriteText(builder, Margin, 532, "المحاضرات الأسبوعية حسب اليوم والوقت والقاعة والتدريسي.", 8, MutedTextColor);
        }

        private static void DrawTable(
            SKCanvas builder,
            IReadOnlyList<SchedulePdfRow> rows,
            IReadOnlyList<string> days,
            IReadOnlyList<PdfTimeBlock> timeline,
            bool instructorTimetable)
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

                WriteText(builder, Margin + TableWidth - TimeColumnWidth + 8,
                    rowBottom + rowHeight / 2 - 3, block.Label, 7, TextColor, TimeColumnWidth - 16);

                if (block.IsBreak)
                {
                    WriteBreakCells(builder, days, dayColumnWidth, rowBottom, rowHeight);
                }
                else
                {
                    WriteLectureCells(builder, rows, days, dayColumnWidth, block, rowBottom, rowHeight, instructorTimetable);
                }

                DrawLine(builder, Margin, rowBottom, Margin + TableWidth, rowBottom, RowLineColor);
                rowTop = rowBottom;
            }

            DrawVerticalLines(builder, days.Count, dayColumnWidth, tableBottom);
        }

        private static void WriteHeader(SKCanvas builder, IReadOnlyList<string> days, int dayColumnWidth)
        {
            WriteText(builder, Margin + TableWidth - TimeColumnWidth + 12, TableTop - 19,
                "الوقت", 9, HeaderTextColor, TimeColumnWidth - 24);

            for (int index = 0; index < days.Count; index++)
            {
                int x = DayColumnX(index, dayColumnWidth);
                WriteText(builder, x + 12, TableTop - 19, DisplayDay(days[index]), 9, HeaderTextColor, dayColumnWidth - 24);
            }
        }

        private static void WriteBreakCells(
            SKCanvas builder,
            IReadOnlyList<string> days,
            int dayColumnWidth,
            int rowBottom,
            int rowHeight)
        {
            for (int index = 0; index < days.Count; index++)
            {
                int x = DayColumnX(index, dayColumnWidth);
                WriteText(builder, x + dayColumnWidth / 2 - 14, rowBottom + rowHeight / 2 - 3, "Break", 8, TextColor);
            }
        }

        private static void WriteLectureCells(
            SKCanvas builder,
            IReadOnlyList<SchedulePdfRow> rows,
            IReadOnlyList<string> days,
            int dayColumnWidth,
            PdfTimeBlock block,
            int rowBottom,
            int rowHeight,
            bool instructorTimetable)
        {
            for (int index = 0; index < days.Count; index++)
            {
                string day = days[index];
                int x = DayColumnX(index, dayColumnWidth);
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

                WriteCellEntries(builder, x + 6, rowBottom + rowHeight - 11, dayColumnWidth - 12, entries, instructorTimetable);
            }
        }

        private static void WriteCellEntries(
            SKCanvas builder,
            int x,
            int yTop,
            int cellWidth,
            IReadOnlyList<SchedulePdfRow> entries,
            bool instructorTimetable)
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
                lines.Add(Shorten(instructorTimetable ? BuildAcademicContext(entry) : entry.FacultyMember,
                    GetCellMaxLength(cellWidth)));
                lines.Add(Shorten($"{entry.Classroom} | {typeAndGroup}", GetCellMaxLength(cellWidth)));
            }

            if (entries.Count > 2)
            {
                lines.Add($"+{entries.Count - 2} more");
            }

            int y = yTop;

            foreach (string line in lines.Take(7))
            {
                WriteText(builder, x, y, line, string.IsNullOrEmpty(line) ? 4 : 8, TextColor, cellWidth);
                y -= 10;
            }
        }

        private static IReadOnlyList<string> GetPageDays(SchedulePdfPage page)
        {
            return ScheduleDayRules.AllDays
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
                ["hh:mm tt", "h:mm tt", "HH:mm", "H:mm", "HH:mm:ss"],
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

        private static int DayColumnX(int index, int dayColumnWidth) =>
            Margin + TableWidth - TimeColumnWidth - (index + 1) * dayColumnWidth;

        private static string DisplayDay(string day) => day.Trim().ToLowerInvariant() switch
        {
            "sunday" => "الأحد",
            "monday" => "الاثنين",
            "tuesday" => "الثلاثاء",
            "wednesday" => "الأربعاء",
            "thursday" => "الخميس",
            _ => day
        };

        private static string BuildAcademicContext(SchedulePdfRow row)
        {
            string year = row.StudyYear.Trim().ToLowerInvariant() switch
            {
                "first year" => "الأولى",
                "second year" => "الثانية",
                "third year" => "الثالثة",
                "fourth year" => "الرابعة",
                _ => row.StudyYear
            };
            string branch = NormalizeBranch(row.Branch).ToLowerInvariant() switch
            {
                "computer science" => "CS",
                "cyber security" => "Cyber",
                "information technology" => "IT",
                "general" => "عام",
                _ => row.Branch
            };
            return $"{year} / {branch} / {CleanSection(row.Section)}";
        }

        private static void DrawVerticalLines(SKCanvas builder, int dayCount, int dayColumnWidth, int tableBottom)
        {
            int x = Margin + TableWidth;
            DrawLine(builder, x, tableBottom, x, TableTop, BorderColor);
            for (int index = 0; index <= dayCount; index++)
            {
                x = Margin + TableWidth - TimeColumnWidth - index * dayColumnWidth;
                DrawLine(builder, x, tableBottom, x, TableTop, BorderColor);
            }
        }

        private static void WriteText(SKCanvas builder, int x, int y, string text, int fontSize, string color, int? maxWidth = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            // RichTextKit applies Unicode bidi ordering and HarfBuzz shaping, including mixed Arabic/Latin text.
            var block = new TextBlock { MaxWidth = maxWidth ?? PageWidth - Margin - x, MaxLines = 1 };
            block.AddText(text, new Style { FontFamily = "Arial", FontSize = fontSize, TextColor = ParseColor(color) });
            block.Paint(builder, new SKPoint(x, PageHeight - y - fontSize));
        }

        private static SKColor ParseColor(string value)
        {
            var components = value.Split(' ').Select(part => float.Parse(part, CultureInfo.InvariantCulture)).ToArray();
            return new SKColor((byte)(components[0] * 255), (byte)(components[1] * 255), (byte)(components[2] * 255));
        }

        private static void DrawFilledRect(SKCanvas builder, int x, int y, int width, int height, string color)
        {
            using var paint = new SKPaint { Color = ParseColor(color), Style = SKPaintStyle.Fill };
            builder.DrawRect(x, PageHeight - y - height, width, height, paint);
        }

        private static void DrawRect(SKCanvas builder, int x, int y, int width, int height, string color)
        {
            using var paint = new SKPaint { Color = ParseColor(color), Style = SKPaintStyle.Stroke, StrokeWidth = 0.7f };
            builder.DrawRect(x, PageHeight - y - height, width, height, paint);
        }

        private static void DrawLine(SKCanvas builder, int x1, int y1, int x2, int y2, string color)
        {
            using var paint = new SKPaint { Color = ParseColor(color), Style = SKPaintStyle.Stroke, StrokeWidth = 0.45f };
            builder.DrawLine(x1, PageHeight - y1, x2, PageHeight - y2, paint);
        }

        private static string Shorten(string? value, int maxLength)
        {
            string text = string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
            return text.Length <= maxLength ? text : text[..Math.Max(0, maxLength - 3)] + "...";
        }

    }

    public sealed record SchedulePdfExportOptions(string? FacultyMemberName = null);

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
        string Section,
        string? FacultyMember = null);

    internal sealed record PdfTimeBlock(
        string Label,
        TimeSpan Start,
        TimeSpan End,
        bool IsBreak);
}
