using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using UglyToad.PdfPig.Content;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

/// <summary>Flattens a ruled weekly timetable, retaining source context and missing values for review.</summary>
internal static class WeeklyPdfTimetableReader
{
    private sealed record Box(double Left, double Bottom, double Right, double Top)
    { public double Width => Right - Left; public double Height => Top - Bottom; }
    private sealed record Period(double Left, double Right, TimeSpan Start, TimeSpan End);
    private sealed record Day(string Name, double Top, double Bottom);
    private static readonly Dictionary<string, string> Days = new()
    {
        ["الاحد"] = "Sunday", ["الاثنين"] = "Monday", ["الثلاثاء"] = "Tuesday", ["الاربعاء"] = "Wednesday", ["الخميس"] = "Thursday",
        ["sunday"] = "Sunday", ["monday"] = "Monday", ["tuesday"] = "Tuesday", ["wednesday"] = "Wednesday", ["thursday"] = "Thursday"
    };
    private static string Value(Word w) => PdfTableReader.WordText(w).Trim();
    private static string Key(string text) => ImportLabels.Normalize(ImportLabels.NormalizeReference(text));
    private static double X(Word w) => (w.BoundingBox.Left + w.BoundingBox.Right) / 2;
    private static double Y(Word w) => w.Letters.Average(l => l.StartBaseLine.Y);
    private static bool Latin(Word w) => Regex.IsMatch(Value(w), "[A-Za-z]");
    private static string GroupValue(Word w) => Regex.Match(Value(w), @"[A-Za-z]{1,3}[12]").Value.ToUpperInvariant();
    private static bool Group(Word w) => GroupValue(w).Length > 0;
    private static string WithoutGroup(Word w) => Regex.Replace(Value(w), @"[A-Za-z]{1,3}[12]", "").Trim();
    private static string English(IEnumerable<Word> words) => string.Join(" ", words.OrderBy(w => w.BoundingBox.Left).Select(Value));
    private static string Arabic(IEnumerable<Word> words) => string.Join(" ", words.OrderByDescending(w => w.BoundingBox.Left).Select(Value));

    public static bool TryRead(Page page, List<Word> words, CancellationToken token, out ExcelSheetData sheet)
    {
        sheet = null!;
        var dayWords = words.Where(w => Days.ContainsKey(Key(Value(w)))).ToList();
        if (dayWords.Select(w => Days[Key(Value(w))]).Distinct().Count() < 3) return false;
        var timeWords = words.Where(w => Regex.IsMatch(ImportLabels.NormalizeReference(Value(w)), @"^\d{1,2}:\d{2}$") &&
            Y(w) > dayWords.Max(Y)).ToList();
        var timeLine = Lines(timeWords).OrderByDescending(l => l.Count).FirstOrDefault();
        if (timeLine is null || timeLine.Count < 4 || timeLine.Count % 2 != 0) return false;
        double headerY = timeLine.Average(Y);
        var boxes = page.Paths.Where(p => p.IsFilled || p.IsStroked).Select(p => p.GetBoundingRectangle())
            .Where(b => b.HasValue).Select(b => new Box(b!.Value.Left, b.Value.Bottom, b.Value.Right, b.Value.Top)).ToList();
        var vertical = boxes.Where(b => b.Width <= 3 && b.Height >= 2).ToList();
        var horizontal = boxes.Where(b => b.Height <= 3 && b.Width >= 1.5).ToList();
        var practicalBoxes = page.Paths.Where(p => p.IsFilled && p.FillColor is not null).Where(p =>
        {
            var rgb = p.FillColor!.ToRGBValues();
            return rgb.Item1 > .5 && rgb.Item3 > .7 && rgb.Item1 > rgb.Item2 + .08 && rgb.Item3 > rgb.Item2 + .1;
        }).Select(p => p.GetBoundingRectangle()).Where(b => b.HasValue).Select(b => b!.Value).ToList();
        var headerText = words.Where(w => Y(w) > headerY + 10).ToList();
        string heading = string.Join(" ", Lines(headerText).Select(line => Arabic(line.Where(w => Regex.IsMatch(Value(w), @"[\p{IsArabic}]")))));
        bool evening = Key(heading).Contains("مسائي");
        double dayLeft = vertical.Where(b => b.Top >= headerY && b.Bottom <= dayWords.Min(Y) && b.Right < dayWords.Min(w => w.BoundingBox.Left))
            .Select(b => b.Right).DefaultIfEmpty(dayWords.Min(w => w.BoundingBox.Left) - 5).Max();
        double left = horizontal.Where(b => b.Width > page.Width * .6 && b.Left < timeLine.Min(w => w.BoundingBox.Left))
            .Select(b => b.Left).DefaultIfEmpty(25).Min();
        var xEdges = vertical.Where(b => b.Top >= headerY - 6 && b.Bottom <= headerY + 6 && b.Left <= dayLeft + 1)
            .Select(b => (b.Left + b.Right) / 2).Append(left).Append(dayLeft).ToList();
        var periods = new List<Period>();
        var orderedTimes = timeLine.OrderByDescending(X).ToList();
        for (int i = 0; i < orderedTimes.Count; i += 2)
        {
            token.ThrowIfCancellationRequested();
            var a = orderedTimes[i]; var b = orderedTimes[i + 1]; double center = (X(a) + X(b)) / 2;
            double l = xEdges.Where(x => x < center).DefaultIfEmpty(left).Max();
            double r = xEdges.Where(x => x > center).DefaultIfEmpty(dayLeft).Min();
            TimeSpan first = Time(Value(a)), second = Time(Value(b));
            if (first > second) (first, second) = (second, first);
            if (first >= second || second - first > TimeSpan.FromHours(3)) throw new ArgumentException($"الصفحة {page.Number}: أوقات الجدول الأسبوعي غير واضحة.");
            periods.Add(new(l, r, first, second));
        }
        if (periods.Zip(periods.Skip(1)).Any(pair => pair.First.End > pair.Second.Start))
            throw new ArgumentException($"الصفحة {page.Number}: ترتيب أوقات الجدول يحتاج مراجعة.");
        evening |= periods[0].Start >= TimeSpan.FromHours(12);
        string session = evening ? "مسائي" : "صباحي";
        string year = Key(heading).Contains("المرحلةالاولي") ? "#1" : Key(heading).Contains("المرحلةالثانية") ? "#2" :
            Key(heading).Contains("المرحلةالثالثة") ? "#3" : Key(heading).Contains("المرحلةالرابعة") ? "#4" : "";
        string branch = Key(heading).Contains("علومالحاسوب") ? "علوم الحاسوب" : Key(heading).Contains("تكنولوجياالمعلومات") ? "تكنولوجيا المعلومات" :
            Key(heading).Contains("الامن") ? "الأمن السيبراني" : "";
        string semester = Key(heading).Contains("الكورسالاول") || Key(heading).Contains("الفصلالاول") ? "1" :
            Key(heading).Contains("الكورسالثاني") || Key(heading).Contains("الفصلالثاني") ? "2" : "";
        var headerSections = headerText.Where(w => Regex.IsMatch(Value(w), "^[A-Za-z]$")).Select(w => Value(w).ToUpperInvariant()).Distinct().ToList();
        var groupBases = words.Where(Group).Select(w => GroupValue(w)[..^1]).Distinct().ToList();
        string section = headerSections.Count == 1 ? headerSections[0] : groupBases.Count == 1 ? groupBases[0] : "";
        var dayEdges = horizontal.Where(b => b.Width > (dayLeft - left) * .85 && b.Left <= left + 3 && b.Right >= dayLeft - 3)
            .Select(b => (b.Top + b.Bottom) / 2).ToList();
        var days = dayWords.Select(w => new Day(Days[Key(Value(w))], dayEdges.Where(y => y > Y(w)).DefaultIfEmpty(headerY - 6).Min(),
            dayEdges.Where(y => y < Y(w)).DefaultIfEmpty(0).Max())).OrderByDescending(d => d.Top).ToList();
        var table = new DataTable { Locale = CultureInfo.InvariantCulture };
        foreach (string column in ExcelImportSchema.Timetable.Columns.Concat(new[] { "EndTime", "StudySession", "SourceText", "ImportNote" }).Distinct()) table.Columns.Add(column);
        var sourceRows = new List<int>();
        var pageNotes = new List<string>();
        int sourceIndex = 1;
        foreach (var day in days)
        {
            token.ThrowIfCancellationRequested();
            var body = words.Where(w => X(w) > left + 1 && X(w) < dayLeft - 2 && Y(w) < day.Top - 1 && Y(w) > day.Bottom + 1).ToList();
            var anchors = new Dictionary<(int Left, int Right, int Top, int Bottom), List<Word>>();
            foreach (var line in Lines(body))
            foreach (var word in line.Where(w => Latin(w) && !Group(w)))
            {
                var bounds = Cell(X(word), Y(word), day);
                var key = ((int)Math.Round(bounds.Left), (int)Math.Round(bounds.Right), (int)Math.Round(bounds.Top), (int)Math.Round(bounds.Bottom));
                if (!anchors.TryGetValue(key, out var list)) anchors[key] = list = [];
                list.Add(word);
            }
            // Arabic-only titles are also supported when followed by a lecturer/room line.
            foreach (var line in Lines(body).Where(line => !line.Any(Latin)))
            {
                foreach (var segment in line.GroupBy(w => { var c = Cell(X(w), Y(w), day); return ((int)Math.Round(c.Left), (int)Math.Round(c.Right), (int)Math.Round(c.Top), (int)Math.Round(c.Bottom)); }))
                {
                    var candidate = segment.ToList(); string title = Arabic(candidate);
                    if (candidate.Any(IsRoomToken) || Regex.IsMatch(title, @"(?:[مدا]\s*\.)") || title.Trim().Length < 4) continue;
                    var b = Cell(X(candidate[0]), Y(candidate[0]), day);
                    var below = body.Where(w => X(w) >= b.Left && X(w) <= b.Right && Y(w) < b.Bottom - 1 && Y(w) > day.Bottom).ToList();
                    if (!below.Any(IsRoomToken)) continue;
                    if (!anchors.ContainsKey(segment.Key)) anchors[segment.Key] = candidate;
                }
            }
            foreach (var anchor in anchors.OrderByDescending(a => a.Key.Top).ThenByDescending(a => a.Key.Left))
            {
                token.ThrowIfCancellationRequested();
                var bounds = anchor.Key;
                var titleWords = anchor.Value;
                if (titleWords.All(Latin) && string.Concat(titleWords.Select(Value)).Count(char.IsLetter) < 2)
                {
                    pageNotes.Add($"كتابة منفردة «{English(titleWords)}» في {ImportLabels.Display(day.Name)} لم تُعامل كمحاضرة."); continue;
                }
                var cellWords = body.Where(w => X(w) > bounds.Left + 1 && X(w) < bounds.Right - 1 && Y(w) < bounds.Top && Y(w) > bounds.Bottom).ToList();
                var groups = cellWords.Where(Group).Select(GroupValue).Distinct().ToList();
                string group = groups.Count == 1 ? groups[0].ToUpperInvariant() : "";
                bool practical = group.Length > 0 || practicalBoxes.Any(b => b.Left < X(titleWords[0]) && b.Right > X(titleWords[0]) && b.Top > Y(titleWords[0]) && b.Bottom < Y(titleWords[0]));
                var details = cellWords.Except(titleWords).ToList();
                if (!details.Any(IsRoomToken))
                {
                    double nextBottom = horizontal.Where(b => b.Left <= (bounds.Left + bounds.Right) / 2 + 3 && b.Right >= (bounds.Left + bounds.Right) / 2 - 3 &&
                        b.Top < bounds.Bottom - 2 && b.Top >= day.Bottom).Select(b => b.Top).DefaultIfEmpty(day.Bottom).Max();
                    var below = body.Where(w => X(w) > bounds.Left + 1 && X(w) < bounds.Right - 1 && Y(w) < bounds.Bottom - 1 && Y(w) > nextBottom + 1).ToList();
                    if (!below.Any(w => Latin(w) && !Group(w))) details.AddRange(below);
                }
                var roomWords = new HashSet<Word>(); var roomNames = new List<string>();
                foreach (var roomWord in details.Where(IsRoomToken))
                {
                    string roomText = ImportLabels.NormalizeReference(WithoutGroup(roomWord));
                    bool lab = roomText.StartsWith("مختبر") || Regex.IsMatch(roomText, @"^(?:م\d*|\d+م)$");
                    var digits = Regex.Match(roomText, @"\d+");
                    Word? number = null;
                    if (!digits.Success)
                        number = details.Where(w => Regex.IsMatch(ImportLabels.NormalizeReference(Value(w)), @"^\d+$") && Math.Abs(Y(w) - Y(roomWord)) < 6)
                            .OrderBy(w => Math.Abs(X(w) - X(roomWord))).FirstOrDefault(w => Math.Abs(X(w) - X(roomWord)) < 65);
                    string roomNumber = digits.Success ? digits.Value : number is null ? "" : ImportLabels.NormalizeReference(Value(number));
                    roomWords.Add(roomWord); if (number is not null) roomWords.Add(number);
                    if (roomNumber.Length > 0) roomNames.Add((lab ? "مختبر " : "قاعة ") + roomNumber);
                }
                var teacherWords = details.Where(w => !roomWords.Contains(w) && !Regex.IsMatch(WithoutGroup(w), "[A-Za-z]") && !Regex.IsMatch(WithoutGroup(w), @"^\d+$")).ToList();
                string teacher = string.Join(" ", teacherWords.OrderByDescending(X).Select(WithoutGroup).Where(t => t.Length > 0));
                teacher = Regex.Replace(teacher, @"^(?:(?:أ|ا|م|د)\s*\.\s*){1,3}(?:(?:م|د)(?=\s|$)\s*)?", "").Trim();
                string subject = titleWords.Any(Latin) ? English(titleWords) : Arabic(titleWords);
                var occupied = periods.Where(p => (p.Left + p.Right) / 2 > bounds.Left && (p.Left + p.Right) / 2 < bounds.Right).ToList();
                if (occupied.Count == 0) throw new ArgumentException($"الصفحة {page.Number}: تعذّر تحديد وقت المادة {subject}.");
                string classroom = roomNames.Distinct().Count() == 1 ? roomNames[0] : "";
                var notes = new List<string>();
                if (teacher.Length == 0) notes.Add("اسم التدريسي غير مذكور بوضوح في الخانة الأصلية");
                if (classroom.Length == 0) notes.Add("القاعة غير مذكورة أو ملتبسة في الخانة الأصلية");
                if (groups.Count > 1) notes.Add("توجد أكثر من مجموعة في الخانة؛ راجع تقسيمها");
                if (section.Length == 0 && group.Length == 0) notes.Add("اسم الشعبة غير ظاهر؛ حدده في المعاينة");
                var row = table.NewRow();
                row["Day"] = day.Name; row["TimeSlot"] = occupied.Min(p => p.Start).ToString(@"hh\:mm");
                row["EndTime"] = occupied.Max(p => p.End).ToString(@"hh\:mm");
                row["Subject"] = subject; row["Faculty"] = teacher; row["Classroom"] = classroom;
                row["Section"] = section.Length > 0 ? section : group.Length > 0 ? group[..^1] : "";
                row["LectureType"] = practical ? "Practical" : "Theory"; row["SemesterNumber"] = semester;
                row["GroupName"] = group; row["StudyYear"] = year; row["Branch"] = branch; row["Locked"] = "FALSE";
                row["StudySession"] = session; row["SourceText"] = English(cellWords.Where(Latin)) + " | " + Arabic(cellWords.Where(w => !Latin(w))) + " | " + Arabic(details);
                row["ImportNote"] = string.Join("؛ ", notes); table.Rows.Add(row); sourceRows.Add(++sourceIndex);
            }
            if (body.Count > 0 && !anchors.Any()) throw new ArgumentException($"الصفحة {page.Number}، {ImportLabels.Display(day.Name)}: توجد كتابة لم تُقرأ كمحاضرات؛ راجع هذه الصفحة.");
        }
        if (table.Rows.Count == 0) return false;
        if (pageNotes.Count > 0) table.Rows[0]["ImportNote"] = string.Join("؛ ", pageNotes.Append(table.Rows[0]["ImportNote"].ToString()!).Where(n => n.Length > 0));
        sheet = new($"صفحة {page.Number} — {year.TrimStart('#')} {section} {session}", table, sourceRows)
            { SuggestedSchemaName = "Timetable", IsWeeklyTimetable = true, StudySession = session };
        return true;

        TimeSpan Time(string value)
        {
            var time = TimeSpan.ParseExact(ImportLabels.NormalizeReference(value), @"h\:mm", CultureInfo.InvariantCulture);
            return time.Hours < (evening ? 12 : 7) ? time + TimeSpan.FromHours(12) : time;
        }
        Box Cell(double x, double y, Day day)
        {
            var edges = vertical.Where(b => b.Top >= y - 1 && b.Bottom <= y + 1).Select(b => (b.Left + b.Right) / 2).ToList();
            double l = edges.Where(v => v < x).DefaultIfEmpty(left).Max(), r = edges.Where(v => v > x).DefaultIfEmpty(dayLeft).Min();
            var hs = horizontal.Where(b => b.Left <= x + 3 && b.Right >= x - 3).Select(b => (b.Top + b.Bottom) / 2).ToList();
            return new(l, hs.Where(v => v < y - 1).DefaultIfEmpty(day.Bottom).Max(), r, hs.Where(v => v > y + 1).DefaultIfEmpty(day.Top).Min());
        }
    }
    private static bool IsRoomToken(Word w) => Regex.IsMatch(ImportLabels.NormalizeReference(WithoutGroup(w)), @"^(?:قاعة|قاع|ق|مختبر)(?:\s*\d+)?$|^(?:م\d*|\d+م)$");
    private static List<List<Word>> Lines(List<Word> words)
    {
        var lines = new List<List<Word>>();
        foreach (var word in words.OrderByDescending(Y))
        {
            var last = lines.LastOrDefault();
            if (last is null || Math.Abs(Y(word) - last.Average(Y)) > 3) lines.Add([word]); else last.Add(word);
        }
        return lines;
    }
}
