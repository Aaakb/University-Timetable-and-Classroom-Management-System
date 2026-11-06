using System.Data;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

internal static class SubjectListWorkbook
{
    internal sealed record Entry(string Sheet, int Row, string Name, string Year);

    public static List<Entry> Read(string path)
    {
        var result = new List<Entry>();
        foreach (var sheet in ExcelWorkbookReader.Read(path).Sheets)
        {
            var headers = sheet.Table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
            int subject = headers.FindIndex(h => h is "المادة" or "اسم المادة" || h.Equals("SubjectName", StringComparison.OrdinalIgnoreCase));
            int year = headers.FindIndex(h => h is "المرحلة" or "المرحلة الدراسية" || h.Equals("StudyYear", StringComparison.OrdinalIgnoreCase));
            if (subject < 0 || year < 0 || headers.Count != 2)
                throw new ArgumentException($"ورقة «{sheet.Name}»: ضع عمودَي «المادة» و«المرحلة» في الصف الأول.");
            for (int i = 0; i < sheet.Table.Rows.Count; i++)
            {
                var row = sheet.Table.Rows[i];
                string name = (string)row[subject];
                string stage = (string)row[year];
                if (name.Length is 0 or > 100 || stage.Length == 0)
                    throw new ArgumentException($"{sheet.Name}، الصف {sheet.SourceRows[i]}: أدخل اسم مادة لا يتجاوز 100 حرف ومرحلة دراسية.");
                result.Add(new(sheet.Name, sheet.SourceRows[i], name, stage));
            }
        }
        if (result.Count == 0) throw new ArgumentException("لا توجد مواد في الملف بعد صف العناوين.");
        return result;
    }

    public static StudyYear? ResolveYear(IEnumerable<StudyYear> years, string value)
    {
        var available = years.Where(y => y.StudyYearID > 0).ToList();
        var match = available.FirstOrDefault(y => string.Equals(y.YearName.Trim(), value, StringComparison.OrdinalIgnoreCase));
        if (match is not null) return match;

        string normalized = string.Concat(value.Select(c => c switch
        {
            >= '٠' and <= '٩' => (char)('0' + c - '٠'),
            >= '۰' and <= '۹' => (char)('0' + c - '۰'),
            _ => c
        }));
        if (normalized.StartsWith("المرحلة ", StringComparison.Ordinal))
            normalized = normalized["المرحلة ".Length..].Trim();
        int yearId = normalized switch
        {
            "الأولى" or "الاولى" => 1,
            "الثانية" => 2,
            "الثالثة" => 3,
            "الرابعة" => 4,
            _ => int.TryParse(normalized, out int id) ? id : 0
        };
        return available.FirstOrDefault(y => y.StudyYearID == yearId);
    }
}
