using System.Data;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

internal static class FacultyListFile
{
    public static List<FacultyMember> Read(string path)
    {
        var rows = new List<FacultyMember>();
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".xlsx":
                foreach (var sheet in ExcelWorkbookReader.Read(path).Sheets)
                {
                    var headers = sheet.Table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray();
                    var columns = FindColumns(headers, $"ورقة «{sheet.Name}»");
                    for (int i = 0; i < sheet.Table.Rows.Count; i++)
                        ReadRow(rows, sheet.Table.Rows[i].ItemArray.Cast<string>().ToArray(), columns,
                            $"ورقة «{sheet.Name}»، الصف {sheet.SourceRows[i]}");
                }
                break;
            case ".csv":
                using (var records = CsvImportReader.Read(path).GetEnumerator())
                {
                    if (!records.MoveNext()) throw new ArgumentException("ملف CSV فارغ.");
                    var columns = FindColumns(records.Current.Fields, "ملف CSV");
                    while (records.MoveNext())
                        ReadRow(rows, records.Current.Fields, columns, $"الصف {records.Current.Row}");
                }
                break;
            default:
                throw new ArgumentException("اختر ملف Excel بصيغة .xlsx أو ملف CSV.");
        }
        if (rows.Count == 0) throw new ArgumentException("لا توجد أسماء تدريسيين بعد صف العناوين.");
        return rows;
    }

    private static (int Name, int Title, int Width) FindColumns(string[] headers, string location)
    {
        var names = headers.Select(ImportLabels.Normalize).ToArray();
        int name = Array.FindIndex(names, value => value is "fullname" or "facultyname" or "الاسم" or "الاسمالكامل" or "اسمالتدريسي" or "التدريسي");
        int title = Array.FindIndex(names, value => value is "academictitle" or "اللقب" or "اللقبالعلمي");
        if (name < 0 || headers.Length > 2 || (headers.Length == 2 && title < 0))
            throw new ArgumentException($"{location}: ضع «اسم التدريسي» في الصف الأول، وأضف «اللقب العلمي» إذا أردت.");
        return (name, title, headers.Length);
    }

    private static void ReadRow(List<FacultyMember> rows, string[] fields, (int Name, int Title, int Width) columns, string location)
    {
        if (fields.All(string.IsNullOrWhiteSpace)) return;
        if (fields.Length > columns.Width || columns.Name >= fields.Length)
            throw new ArgumentException($"{location}: راجع أعمدة الاسم واللقب العلمي.");
        string name = fields[columns.Name].Trim();
        string? title = columns.Title >= 0 && columns.Title < fields.Length ? fields[columns.Title].Trim() : null;
        if (name.Length is 0 or > 100 || name.Contains('\n') || name.Contains('\r'))
            throw new ArgumentException($"{location}: أدخل اسمًا في سطر واحد، بحد أقصى 100 حرف.");
        if (title is not null && (title.Length > 50 || title.Contains('\n') || title.Contains('\r')))
            throw new ArgumentException($"{location}: أدخل لقبًا في سطر واحد، بحد أقصى 50 حرفًا.");
        rows.Add(new FacultyMember { FullName = name, AcademicTitle = string.IsNullOrEmpty(title) ? null : title });
    }
}
