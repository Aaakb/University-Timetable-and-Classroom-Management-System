using Guna.UI2.WinForms;

namespace University_Timetable_and_Classroom_Management_System
{
    public partial class SchedulesForm
    {
        private bool resizingWorkspace;

        private void ConfigureScheduleTableHeader()
        {
            int Px(int value) => WindowSizing.Pixels(this, value);
            dgvSchedules.SetBounds(Px(24), Px(68),
                Math.Max(Px(700), pnlSchedulesTable.Width - Px(48)),
                Math.Max(Px(180), pnlSchedulesTable.Height - Px(92)));
            pnlTimetableHost.Bounds = dgvSchedules.Bounds;
            btnScheduleGridView.Location = new Point(Px(24), Px(22));
            btnScheduleTimetableView.Location = new Point(Px(120), Px(22));
            lblTableTitle.Left = pnlSchedulesTable.Width - Px(24) - lblTableTitle.Width;
        }

        private void ResizeScheduleWorkspace()
        {
            if (IsDisposed || Disposing || resizingWorkspace || pnlWorkspace.ClientSize.Width <= 0) return;
            int Px(int value) => WindowSizing.Pixels(this, value);
            resizingWorkspace = true;
            pnlWorkspace.SuspendLayout();
            try
            {
                // Keep the toolbar and filters above the table, including when they wrap.
                pnlWorkspace.AutoScroll = true;
                pnlScheduleEditor.Anchor = pnlScheduleFilters.Anchor = pnlSchedulesTable.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                int width = Math.Max(Px(420), pnlWorkspace.ClientSize.Width - Px(76));
                lblPageTitle.Left = lblPageTitle.Parent!.Width - Px(28) - lblPageTitle.Width;
                lblPageSubtitle.Left = lblPageSubtitle.Parent!.Width - Px(28) - lblPageSubtitle.Width;
                var scroll = pnlWorkspace.AutoScrollPosition;
                pnlScheduleCommands.SetBounds(Px(28) + scroll.X, Px(84) + scroll.Y, width, pnlScheduleCommands.Height);
                LayoutScheduleButtons(pnlScheduleCommands);
                pnlScheduleFilters.SetBounds(Px(28) + scroll.X, pnlScheduleCommands.Bottom + Px(10), width, pnlScheduleFilters.Height);
                LayoutScheduleFields(pnlScheduleFilters, [(lblSemesterFilter, cmbSemesterFilter),
                    (lblStudyYearFilter, cmbStudyYearFilter), (lblSectionFilter, cmbSectionFilter),
                    (lblGroupFilter, cmbGroupFilter), (lblFacultyFilter, cmbFacultyFilter)], top: 12, columnWidth: 155);
                pnlSchedulesTable.SetBounds(Px(28) + scroll.X, pnlScheduleFilters.Bottom + Px(10), width,
                    Math.Max(Px(270), pnlWorkspace.ClientSize.Height - (pnlScheduleFilters.Bottom - scroll.Y) - Px(34)));
                ConfigureScheduleTableHeader();
                // Prevent anchors from pushing header buttons off-screen after their new positions are set.
                btnScheduleGridView.Anchor = btnScheduleTimetableView.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            }
            finally
            {
                pnlWorkspace.ResumeLayout();
                resizingWorkspace = false;
            }
        }

        private static void LayoutScheduleFields(Control parent,
            (DesignerHtmlLabel Label, Guna2ComboBox Combo)[] fields, int top = 24, int columnWidth = 195)
        {
            int Px(int value) => WindowSizing.Pixels(parent, value);
            top = Px(top);
            int columns = Math.Clamp((parent.ClientSize.Width - Px(32)) / Px(columnWidth), 1, fields.Length);
            int cellWidth = (parent.ClientSize.Width - Px(32)) / columns;
            for (int i = 0; i < fields.Length; i++)
            {
                int x = parent.ClientSize.Width - Px(16) - (i % columns + 1) * cellWidth + Px(14);
                int y = top + i / columns * Px(62);
                fields[i].Label.Location = new Point(x + cellWidth - Px(14) - fields[i].Label.Width, y);
                fields[i].Combo.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                fields[i].Combo.SetBounds(x, y + Px(21), Math.Max(Px(90), cellWidth - Px(14)), Px(34));
            }
            foreach (Control title in parent.Controls)
                if (title is DesignerHtmlLabel && !fields.Any(field => ReferenceEquals(field.Label, title)))
                    title.Left = parent.ClientSize.Width - Px(16) - title.Width;
            parent.Height = top + ((fields.Length + columns - 1) / columns) * Px(62) + Px(8);
        }

        private void LayoutScheduleButtons(Control parent)
        {
            int Px(int value) => WindowSizing.Pixels(parent, value);
            int x = Px(10), y = Px(8);
            foreach (var button in new[] { btnGenerateSchedule, btnAddSchedule, btnUpdateSchedule,
                btnDeleteSchedule, btnExportSchedulePdf, btnBackupTimetable, btnRestoreTimetable })
            {
                if (x > Px(10) && x + button.Width + Px(10) > parent.ClientSize.Width) { x = Px(10); y += Px(43); }
                int left = parent.ClientSize.Width - x - button.Width;
                button.Location = new Point(left, y);
                x += button.Width + Px(10);
            }
            parent.Height = y + Px(42);
        }
    }
}
