using System.Globalization;
using Guna.UI2.WinForms;

namespace University_Timetable_and_Classroom_Management_System;

/// <summary>Arabic display formatting; stored identifiers and numeric values stay unchanged.</summary>
internal static class ArabicUi
{
    private static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Sunday"] = "الأحد", ["Monday"] = "الاثنين", ["Tuesday"] = "الثلاثاء",
        ["Wednesday"] = "الأربعاء", ["Thursday"] = "الخميس", ["Friday"] = "الجمعة", ["Saturday"] = "السبت",
        ["Theory"] = "نظري", ["Practical"] = "عملي", ["All"] = "الكل",
        ["Lecture"] = "قاعة محاضرات", ["Lab"] = "مختبر", ["Shared Lab"] = "مختبر مشترك",
        ["Professor"] = "أستاذ", ["Associate Professor"] = "أستاذ مشارك",
        ["Assistant Professor"] = "أستاذ مساعد", ["Lecturer"] = "مدرس", ["Assistant Lecturer"] = "مدرس مساعد",
        ["First Year"] = "المرحلة الأولى", ["Second Year"] = "المرحلة الثانية",
        ["Third Year"] = "المرحلة الثالثة", ["Fourth Year"] = "المرحلة الرابعة"
    };

    private static readonly HashSet<string> DisplayColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "DayOfWeek", "LectureType", "RoomType", "AcademicTitle", "YearName", "StudyYearName", "GroupName"
    };

    public static void InitializeCulture()
    {
        // Use Western digits and the existing decimal separator in every numeric
        // editor, independent of the Windows regional settings.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("ar-IQ");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ar-IQ");
    }

    public static string Display(string value) => Labels.GetValueOrDefault(value, value);

    public static void Apply(Control root)
    {
        using var layout = new LayoutUpdateScope(root);
        root.RightToLeft = RightToLeft.Yes;
        if (root is Form form) form.RightToLeftLayout = true;
        foreach (Control child in root.Controls) Configure(child);
    }

    private static void Configure(Control control)
    {
        if (control is DesignerHtmlLabel label)
            label.UseArabicTextLayout();

        // Guna mirrors the alignment with the form, but applies TextOffset in
        // screen coordinates. Keep the text inset from the right edge.
        if (control is Guna2Button button && button.TextAlign == HorizontalAlignment.Left)
            button.TextOffset = new Point(-Math.Abs(button.TextOffset.X), button.TextOffset.Y);

        if (control is ComboBox combo)
        {
            combo.FormattingEnabled = true;
            combo.Format += (_, e) =>
            {
                if (e.ListItem is string value) e.Value = Display(value);
                else if (e.ListItem is ComboOption option &&
                    (combo.Name.Contains("StudyYear", StringComparison.OrdinalIgnoreCase) || option.Text == "All"))
                    e.Value = Display(option.Text);
            };
        }

        if (control is DataGridView grid)
        {
            foreach (DataGridViewColumn column in grid.Columns)
            {
                column.MinimumWidth = Math.Max(column.MinimumWidth,
                    TextRenderer.MeasureText(column.HeaderText, grid.ColumnHeadersDefaultCellStyle.Font ?? grid.Font).Width + 16);
                if (column.DataPropertyName == "TimeSlotName")
                    column.MinimumWidth = Math.Max(column.MinimumWidth,
                        TextRenderer.MeasureText("12:30 PM - 02:00 PM", grid.DefaultCellStyle.Font ?? grid.Font).Width + 20);
            }
            grid.CellFormatting += (_, e) =>
            {
                if (e.ColumnIndex >= 0 && e.Value is string value && DisplayColumns.Contains(grid.Columns[e.ColumnIndex].DataPropertyName))
                    e.Value = Display(value);
            };
        }

        if (control is Guna2TextBox textBox && IsNumericEditor(textBox.Name))
        {
            textBox.RightToLeft = RightToLeft.No;
            textBox.TextAlign = HorizontalAlignment.Right;
        }
        if (control is DateTimePicker picker)
            picker.RightToLeft = RightToLeft.No;

        foreach (Control child in control.Controls) Configure(child);
    }

    private static bool IsNumericEditor(string name) =>
        name.EndsWith("Id", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Hours", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Units", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Capacity", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Count", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Number", StringComparison.OrdinalIgnoreCase);
}
