using System.Data;
using System.Globalization;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

internal sealed class ImportValues(DataRow row)
{
    public string Text(string column, bool required = true)
    {
        var source = row.Table.Columns.Cast<DataColumn>().SingleOrDefault(c =>
            c.ColumnName.Equals(column, StringComparison.OrdinalIgnoreCase));
        string value = source is null ? "" : Convert.ToString(row[source], CultureInfo.InvariantCulture)?.Trim() ?? "";
        if (required && value.Length == 0) throw new ArgumentException($"{column}: value is required.");
        return value;
    }
    public int Int(string column) => int.TryParse(ImportLabels.NormalizeReference(Text(column)), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
        ? value : throw new ArgumentException($"{column}: enter a whole number.");
    public int? OptionalInt(string column) => Text(column, false).Length == 0 ? null : Int(column);
    public double Number(string column) => double.TryParse(ImportLabels.NormalizeReference(Text(column)).Replace('٫', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
        && double.IsFinite(value) && value >= 0 ? value : throw new ArgumentException($"{column}: enter a finite nonnegative number.");
    public bool Bool(string column, bool fallback)
    {
        string value = ImportLabels.NormalizeReference(Text(column, false));
        if (value.Length == 0) return fallback;
        return value.ToLowerInvariant() switch
        {
            "true" or "1" or "yes" or "نعم" => true,
            "false" or "0" or "no" or "لا" => false,
            _ => throw new ArgumentException($"{column}: use TRUE or FALSE.")
        };
    }
    public TimeSpan Time(string column)
    {
        string text = ImportLabels.NormalizeReference(Text(column));
        if (TimeSpan.TryParseExact(text, [@"h\:mm", @"hh\:mm", @"hh\:mm\:ss"], CultureInfo.InvariantCulture, out var time)) return time;
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double serial) && serial >= 0 && serial < 1)
            return TimeSpan.FromMinutes(Math.Round(serial * 1440));
        throw new ArgumentException($"{column}: use HH:mm or an Excel time value.");
    }
}
