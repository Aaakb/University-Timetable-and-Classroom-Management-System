using System.Text;
using Microsoft.VisualBasic.FileIO;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

internal static class CsvImportReader
{
    public static IEnumerable<(long Row, string[] Fields)> Read(string path)
    {
        using var reader = new TextFieldParser(path, new UTF8Encoding(false, true), detectEncoding: true)
        {
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true
        };
        reader.SetDelimiters(",");
        while (ReadRecord(reader) is { } record) yield return record;
    }

    private static (long Row, string[] Fields)? ReadRecord(TextFieldParser reader)
    {
        try
        {
            if (reader.EndOfData) return null;
            long row = reader.LineNumber;
            return reader.ReadFields() is { } fields ? (row, fields) : null;
        }
        catch (MalformedLineException ex)
        {
            throw new ArgumentException($"تعذر قراءة الصف {ex.LineNumber} من ملف CSV. راجع علامات الاقتباس والفواصل.", ex);
        }
        catch (DecoderFallbackException ex)
        {
            throw new ArgumentException("احفظ ملف CSV بترميز UTF-8 ثم أعد المحاولة.", ex);
        }
    }
}
