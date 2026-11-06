using Guna.UI2.WinForms;

namespace University_Timetable_and_Classroom_Management_System;

internal static class ListFilterField
{
    public static void Add(FlowLayoutPanel header, Guna2ComboBox combo, string caption, Guna2ComboBox style)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.AccessibleName = caption;
        combo.Font = style.Font;
        combo.ForeColor = style.ForeColor;
        combo.BorderColor = style.BorderColor;
        combo.BorderRadius = style.BorderRadius;
        combo.Size = new Size(194, 36);
        combo.Dock = DockStyle.Fill;
        combo.Margin = new Padding(0);
        var field = new TableLayoutPanel
        {
            Name = "layout" + combo.Name, ColumnCount = 1, RowCount = 2,
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(194, 0), MaximumSize = new Size(194, 0),
            Margin = new Padding(0, 0, 20, 12), BackColor = Color.White
        };
        field.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        field.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        field.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        field.Controls.Add(new Label { Text = caption, AutoSize = true,
            Margin = new Padding(0, 0, 0, 6), ForeColor = DesktopPalette.Header }, 0, 0);
        field.Controls.Add(combo, 0, 1);
        header.Controls.Add(field);
    }
}
