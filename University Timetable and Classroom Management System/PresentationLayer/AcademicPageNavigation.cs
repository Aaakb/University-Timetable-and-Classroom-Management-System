using Guna.UI2.WinForms;

namespace University_Timetable_and_Classroom_Management_System;

internal enum AcademicPage { Stages, Sections, Students }

internal static class AcademicPageNavigation
{
    public static Control Create(AcademicPage current, Func<AcademicPage, Task> navigate)
    {
        var row = new FlowLayoutPanel
        {
            Name = "academicPageNavigation", Anchor = AnchorStyles.None, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0),
            RightToLeft = RightToLeft.Yes
        };
        foreach (var (page, text) in new[] { (AcademicPage.Stages, "المراحل"), (AcademicPage.Sections, "الشعب"), (AcademicPage.Students, "الطلبة") })
        {
            bool active = page == current;
            var button = new Guna2Button
            {
                Name = $"btnAcademic{page}", Text = text,
                AccessibleName = active ? $"{text}، الشاشة الحالية" : $"فتح شاشة {text}",
                Size = new Size(108, 45), Margin = new Padding(4, 0, 4, 0), BorderRadius = DesktopCorners.Button,
                BorderThickness = 1, BorderColor = DesktopPalette.FieldBorder,
                FillColor = active ? DesktopPalette.Header : Color.White,
                ForeColor = active ? Color.White : DesktopPalette.Ink,
                Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold), Cursor = Cursors.Hand,
                Checked = active, TabStop = !active
            };
            button.CheckedState.FillColor = DesktopPalette.Header;
            button.CheckedState.ForeColor = Color.White;
            button.Click += async (_, _) => { if (!active) await navigate(page); };
            row.Controls.Add(button);
        }
        var center = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1, RowCount = 1, Margin = new Padding(0, 2, 0, 14)
        };
        center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        center.Controls.Add(row, 0, 0);
        return center;
    }

    public static void PlaceBelowEditor(TableLayoutPanel workspace, Control editor, Control table, Control navigation)
    {
        using var layout = new LayoutUpdateScope(workspace);
        editor.Margin = new Padding(0, 0, 0, 10);
        workspace.RowCount = 3;
        workspace.RowStyles.Insert(1, new RowStyle(SizeType.AutoSize));
        workspace.SetRow(table, 2);
        workspace.Controls.Add(navigation, 0, 1);
    }
}
