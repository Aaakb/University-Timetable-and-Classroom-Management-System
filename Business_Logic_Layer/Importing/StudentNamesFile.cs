namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

internal static class StudentNamesFile
{
    internal sealed record Entry(string Name, string Location);

    public static List<Entry> Read(string path)
    {
        var names = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".xlsx" => ReadExcel(path),
            ".csv" => ReadCsv(path),
            _ => throw new ArgumentException("اختر ملف Excel بصيغة .xlsx أو ملف CSV.")
        };
        if (names.Count == 0) throw new ArgumentException("لا توجد أسماء بعد صف العنوان.");
        return names;
    }

    private static List<Entry> ReadExcel(string path)
    {
        var names = new List<Entry>();
        foreach (var sheet in ExcelWorkbookReader.Read(path).Sheets)
        {
            if (sheet.Table.Columns.Count != 1 || !IsNameHeader(sheet.Table.Columns[0].ColumnName))
                throw new ArgumentException($"ورقة «{sheet.Name}»: استخدم عمودًا واحدًا بعنوان «اسم الطالب».");
            for (int i = 0; i < sheet.Table.Rows.Count; i++)
                AddName(names, (string)sheet.Table.Rows[i][0], $"ورقة «{sheet.Name}»، الصف {sheet.SourceRows[i]}");
        }
        return names;
    }

    private static List<Entry> ReadCsv(string path)
    {
        var names = new List<Entry>();
        using var reader = CsvImportReader.Read(path).GetEnumerator();
        if (!reader.MoveNext() || reader.Current.Fields is not [var header] || !IsNameHeader(header))
            throw new ArgumentException("ضع عنوان «اسم الطالب» في الصف الأول من ملف CSV.");
        while (reader.MoveNext())
        {
            var (row, fields) = reader.Current;
            if (fields is not [var name])
                throw new ArgumentException($"الصف {row}: استخدم عمودًا واحدًا لاسم الطالب.");
            AddName(names, name, $"الصف {row}");
        }
        return names;
    }

    private static void AddName(List<Entry> names, string value, string location)
    {
        string name = value.Trim();
        if (name.Length == 0) return;
        if (name.Length > 150 || name.Contains('\n') || name.Contains('\r'))
            throw new ArgumentException($"{location}: ضع كل اسم في سطر واحد، بحد أقصى 150 حرفًا.");
        names.Add(new Entry(name, location));
    }

    private static bool IsNameHeader(string header) => header.Trim().ToLowerInvariant()
        is "اسم الطالب" or "الاسم" or "fullname" or "studentname";
}
