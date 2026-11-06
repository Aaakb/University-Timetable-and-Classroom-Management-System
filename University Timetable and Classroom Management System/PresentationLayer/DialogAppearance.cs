namespace University_Timetable_and_Classroom_Management_System;

// Apply the desktop palette to native dialog buttons and grids.
internal static class DialogAppearance
{
    private static readonly Color Ink = DesktopPalette.Ink;
    private static readonly Color Accent = DesktopPalette.Primary;
    private static readonly Color Hover = DesktopPalette.Header;
    private static readonly Color Border = DesktopPalette.Border;
    private static readonly Color Selected = DesktopPalette.Selection;
    private static readonly Color Alternating = DesktopPalette.Canvas;

    public static void Apply(Control root)
    {
        if (root is FacultyPreferencesForm)
        {
            var heading = root.Controls.Find("heading", true).FirstOrDefault();
            if (heading is not null) heading.BackColor = Color.White;
        }

        foreach (Control child in root.Controls)
        {
            switch (child)
            {
                case Button button:
                    StyleButton(button);
                    break;
                case DataGridView grid:
                    StyleGrid(grid);
                    break;
                case Label label when label.Name == "lblHeading":
                    label.ForeColor = Ink;
                    label.Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold);
                    break;
            }
            Apply(child);
        }
    }

    private static void StyleButton(Button button)
    {
        bool primary = button.Name is "save" or "saveAlternative" or "generateAlternatives" or "btnSaveManualSchedule";
        button.UseVisualStyleBackColor = false;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = primary ? 0 : 1;
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseOverBackColor = primary ? Hover : Selected;
        button.FlatAppearance.MouseDownBackColor = primary ? Ink : Alternating;
        void SetColors()
        {
            button.BackColor = primary && button.Enabled ? Accent : Color.White;
            button.ForeColor = primary && button.Enabled ? Color.White : Ink;
        }
        SetColors();
        if (primary) button.EnabledChanged += (_, _) => SetColors();
        button.Cursor = Cursors.Hand;
        button.SizeChanged += (_, _) => RoundButton(button);
        RoundButton(button);
    }

    private static void RoundButton(Button button)
    {
        int width = button.ClientSize.Width, height = button.ClientSize.Height;
        if (width < 12 || height < 12) return;
        int diameter = Math.Min(WindowSizing.Pixels(button, DesktopCorners.Button * 2), Math.Min(width, height) - 1);
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(0, 0, diameter, diameter, 180, 90);
        path.AddArc(width - diameter, 0, diameter, diameter, 270, 90);
        path.AddArc(width - diameter, height - diameter, diameter, diameter, 0, 90);
        path.AddArc(0, height - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        var previous = button.Region;
        button.Region = new Region(path);
        previous?.Dispose();
    }

    private static void StyleGrid(DataGridView grid)
    {
        grid.EnableHeadersVisualStyles = false;
        grid.BackgroundColor = Color.White;
        grid.GridColor = DesktopPalette.Border;
        grid.ColumnHeadersDefaultCellStyle.BackColor = DesktopPalette.Header;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = DesktopPalette.Header;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
        grid.DefaultCellStyle.BackColor = Color.White;
        grid.DefaultCellStyle.ForeColor = Ink;
        grid.DefaultCellStyle.SelectionBackColor = Selected;
        grid.DefaultCellStyle.SelectionForeColor = Ink;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Alternating;
        grid.AlternatingRowsDefaultCellStyle.ForeColor = Ink;
        grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = Selected;
        grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = Ink;
    }
}
