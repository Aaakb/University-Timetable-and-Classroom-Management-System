using Guna.UI2.WinForms;

using System.Drawing;
using System.Windows.Forms;

namespace University_Timetable_and_Classroom_Management_System
{
    public partial class SchedulesForm
    {
        private void ConfigureScheduleFilterControls()
        {
            lblSemesterFilter = new Guna2HtmlLabel
            {
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                Location = new Point(984, 14),
                Name = "lblSemesterFilter",
                Size = new Size(55, 17),
                TabIndex = 8,
                Text = "Semester"
            };

            cmbSemesterFilter = new Guna2ComboBox
            {
                BackColor = Color.Transparent,
                BorderColor = Color.FromArgb(203, 213, 225),
                BorderRadius = 8,
                DrawMode = DrawMode.OwnerDrawFixed,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FocusedColor = Color.FromArgb(37, 99, 235),
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(15, 23, 42),
                ItemHeight = 30,
                Location = new Point(984, 36),
                Name = "cmbSemesterFilter",
                Size = new Size(92, 36),
                TabIndex = 9
            };

            lblGroupFilter = new Guna2HtmlLabel
            {
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                Location = new Point(896, 14),
                Name = "lblGroupFilter",
                Size = new Size(38, 17),
                TabIndex = 10,
                Text = "Group"
            };

            cmbGroupFilter = new Guna2ComboBox
            {
                BackColor = Color.Transparent,
                BorderColor = Color.FromArgb(203, 213, 225),
                BorderRadius = 8,
                DrawMode = DrawMode.OwnerDrawFixed,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FocusedColor = Color.FromArgb(37, 99, 235),
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(15, 23, 42),
                ItemHeight = 30,
                Location = new Point(896, 36),
                Name = "cmbGroupFilter",
                Size = new Size(72, 36),
                TabIndex = 11
            };

            cmbSemesterFilter.FocusedState.BorderColor = Color.FromArgb(37, 99, 235);
            cmbGroupFilter.FocusedState.BorderColor = Color.FromArgb(37, 99, 235);
            pnlScheduleFilters.Controls.Add(cmbGroupFilter);
            pnlScheduleFilters.Controls.Add(lblGroupFilter);
            pnlScheduleFilters.Controls.Add(cmbSemesterFilter);
            pnlScheduleFilters.Controls.Add(lblSemesterFilter);
        }

        private void ConfigureSchedulePageHeader()
        {
            lblPageTitle = new Guna2HtmlLabel
            {
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                Name = "lblPageTitle",
                Text = "Schedules Management"
            };

            lblPageSubtitle = new Guna2HtmlLabel
            {
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(100, 116, 139),
                Name = "lblPageSubtitle",
                Text = "Build, review, and export the weekly academic timetable."
            };

            pnlWorkspace.Controls.Add(lblPageSubtitle);
            pnlWorkspace.Controls.Add(lblPageTitle);
        }

        private void ConfigureLectureTypeAndGroupControls()
        {
            lblLectureType = CreateEditorLabel("Lecture Type", new Point(200, 119), "lblLectureType");
            cmbLectureType = CreateEditorComboBox(new Point(200, 144), "cmbLectureType");
            cmbLectureType.Items.AddRange(new object[] { "Theory", "Practical" });
            cmbLectureType.SelectedIndex = 0;

            lblGroupName = CreateEditorLabel("Group", new Point(376, 119), "lblGroupName");
            cmbGroupName = CreateEditorComboBox(new Point(376, 144), "cmbGroupName");
            cmbGroupName.Enabled = false;

            pnlScheduleEditor.Controls.Add(cmbGroupName);
            pnlScheduleEditor.Controls.Add(lblGroupName);
            pnlScheduleEditor.Controls.Add(cmbLectureType);
            pnlScheduleEditor.Controls.Add(lblLectureType);
        }

        private void ConfigureScheduleLayoutEnhancements()
        {
            int contentWidth = Math.Max(900, pnlWorkspace.ClientSize.Width - 56);

            lblPageTitle.Location = new Point(28, 14);
            lblPageSubtitle.Location = new Point(30, 50);

            pnlScheduleEditor.Location = new Point(28, 92);
            pnlScheduleEditor.Size = new Size(pnlScheduleEditor.Width, 326);
            pnlScheduleFilters.Location = new Point(28, 436);
            pnlScheduleFilters.Size = new Size(pnlScheduleFilters.Width, 132);
            pnlSchedulesTable.Location = new Point(28, 586);
            pnlSchedulesTable.Size = new Size(
                pnlSchedulesTable.Width,
                Math.Max(300, pnlWorkspace.ClientSize.Height - pnlSchedulesTable.Top - 28));

            lblEditorTitle.Location = new Point(24, 10);
            lblEditorSubtitle.Location = new Point(24, 36);

            var basicGroup = CreateScheduleGroupPanel(
                "Course & Staff",
                new Point(24, 62),
                new Size(contentWidth - 48, 84));

            var placementGroup = CreateScheduleGroupPanel(
                "Academic Placement & Room",
                new Point(24, 154),
                new Size(contentWidth - 48, 84));

            var commandGroup = CreateCommandGroupPanel(new Point(24, 246), new Size(contentWidth - 48, 58));

            pnlScheduleEditor.Controls.Add(basicGroup);
            pnlScheduleEditor.Controls.Add(placementGroup);
            pnlScheduleEditor.Controls.Add(commandGroup);

            MoveEditorField(basicGroup, lblSubject, cmbSubject, 16, 24, 280);
            MoveEditorField(basicGroup, lblFacultyMember, cmbFacultyMember, 316, 24, 260);
            MoveEditorField(basicGroup, lblLectureType, cmbLectureType, 596, 24, 150);
            MoveEditorField(basicGroup, lblGroupName, cmbGroupName, 766, 24, 130);

            MoveEditorField(placementGroup, lblStudyYear, cmbStudyYear, 16, 24, 142);
            MoveEditorField(placementGroup, lblBranch, cmbBranch, 174, 24, 142);
            MoveEditorField(placementGroup, lblSection, cmbSection, 332, 24, 178);
            MoveEditorField(placementGroup, lblDayOfWeek, cmbDayOfWeek, 526, 24, 154);
            MoveEditorField(placementGroup, lblTimeSlot, cmbTimeSlot, 696, 24, 220);
            MoveEditorField(placementGroup, lblClassroom, cmbClassroom, 932, 24, 210);

            MoveActionButton(commandGroup, btnGenerateSchedule, 0, "Automation", 220, 38);
            MoveActionButton(commandGroup, btnAddSchedule, 260, "Manual records");
            MoveActionButton(commandGroup, btnUpdateSchedule, 370);
            MoveActionButton(commandGroup, btnDeleteSchedule, 480);
            MoveActionButton(commandGroup, btnClearScheduleForm, 590);
            MoveActionButton(commandGroup, btnExportSchedulePdf, 720, width: 128);

            ConfigureFilterLayout();
            ConfigureScheduleTableHeader();
        }

        private static void MoveEditorField(
            Control parent,
            Guna2HtmlLabel label,
            Guna2ComboBox combo,
            int x,
            int y,
            int width)
        {
            label.Location = new Point(x, y);
            combo.Location = new Point(x, y + 20);
            combo.Size = new Size(width, 34);
            combo.ItemHeight = 28;
            parent.Controls.Add(label);
            parent.Controls.Add(combo);
        }

        private static void MoveActionButton(
            Control parent,
            Guna2Button button,
            int x,
            string? groupTitle = null,
            int width = 100,
            int height = 34)
        {
            if (!string.IsNullOrWhiteSpace(groupTitle))
            {
                parent.Controls.Add(new Guna2HtmlLabel
                {
                    BackColor = Color.Transparent,
                    Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(100, 116, 139),
                    Location = new Point(x, 2),
                    Text = groupTitle
                });
            }

            button.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            button.Location = new Point(x, 22);
            button.Size = new Size(width, height);
            parent.Controls.Add(button);
        }

        private void ConfigureFilterLayout()
        {
            MoveFilterField(lblSemesterFilter, cmbSemesterFilter, 24, 12, 140);
            MoveFilterField(lblStudyYearFilter, cmbStudyYearFilter, 188, 12, 220);
            MoveFilterField(lblSectionFilter, cmbSectionFilter, 432, 12, 280);
            MoveFilterField(lblGroupFilter, cmbGroupFilter, 736, 12, 140);
            MoveFilterField(lblFacultyFilter, cmbFacultyFilter, 900, 12, 260);
        }

        private static void MoveFilterField(
            Guna2HtmlLabel label,
            Guna2ComboBox combo,
            int x,
            int y,
            int width)
        {
            label.Location = new Point(x, y);
            combo.Location = new Point(x, y + 21);
            combo.Size = new Size(width, 34);
            combo.ItemHeight = 28;
        }

        private void ConfigureScheduleTableHeader()
        {
            lblTableTitle.Location = new Point(24, 14);
            lblTableSubtitle.Location = new Point(24, 39);

            dgvSchedules.Location = new Point(24, 68);
            dgvSchedules.Size = new Size(
                Math.Max(700, pnlSchedulesTable.Width - 48),
                Math.Max(180, pnlSchedulesTable.Height - 92));

            if (pnlTimetableHost is not null)
            {
                pnlTimetableHost.Location = dgvSchedules.Location;
                pnlTimetableHost.Size = dgvSchedules.Size;
            }

            if (btnScheduleGridView is not null && btnScheduleTimetableView is not null)
            {
                btnScheduleGridView.Location = new Point(pnlSchedulesTable.Width - 264, 22);
                btnScheduleTimetableView.Location = new Point(pnlSchedulesTable.Width - 168, 22);
            }
        }
    }
}
