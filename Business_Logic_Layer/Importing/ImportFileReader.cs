namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

public static class ImportFileReader
{
    public static ExcelWorkbookData Read(string path, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".xlsx" => ExcelWorkbookReader.Read(path, token),
            _ => throw new ArgumentException("اختر ملف Excel بصيغة .xlsx للتدريسيين والمواد.")
        };
    }
}
