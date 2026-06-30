namespace University_Timetable_and_Classroom_Management_System
{
    internal enum NavigationPage
    {
        Branches,
        StudyYears,
        Sections,
        Subjects,
        Classrooms,
        TimeSlots,
        FacultyAssignments,
        FacultyMembers,
        Schedules
    }

    internal static class FormNavigation
    {
        private static readonly IReadOnlyList<NavigationItem> NavigationItems =
        [
            new("btnNavigationBranches", "Branches", NavigationPage.Branches),
            new("btnNavigationStudyYears", "Study Years", NavigationPage.StudyYears),
            new("btnNavigationSections", "Sections", NavigationPage.Sections),
            new("btnNavigationSubjects", "Subjects", NavigationPage.Subjects),
            new("btnNavigationClassrooms", "Classrooms", NavigationPage.Classrooms),
            new("btnNavigationTimeSlots", "Time Slots", NavigationPage.TimeSlots),
            new("btnNavigationFacultyAssignments", "Teaching", NavigationPage.FacultyAssignments),
            new("btnNavigationFacultyMembers", "Faculty", NavigationPage.FacultyMembers),
            new("btnNavigationSchedules", "Schedule", NavigationPage.Schedules)
        ];

        public static void ConfigureSidebar(
            System.Windows.Forms.Control currentPage,
            Guna.UI2.WinForms.Guna2Panel sidebar,
            NavigationPage currentPageKey)
        {
            PrepareContentPage(currentPage);
        }

        public static void ConfigureShellSidebar(
            MainShellForm shell,
            Guna.UI2.WinForms.Guna2Panel sidebar,
            NavigationPage currentPage)
        {
            RemoveExistingNavigationButtons(sidebar);

            sidebar.AutoScroll = true;
            sidebar.AutoScrollMinSize = new System.Drawing.Size(0, 670);

            int top = 110;

            foreach (var item in NavigationItems)
            {
                var button = CreateNavigationButton(item, top);
                sidebar.Controls.Add(button);
                sidebar.Controls.SetChildIndex(button, 0);

                if (item.Page == currentPage)
                {
                    SetActive(button);
                }
                else
                {
                    button.Click += (_, _) => shell.ShowPage(item.Page);
                }

                top += 56;
            }
        }

        public static System.Windows.Forms.UserControl CreatePage(NavigationPage page)
        {
            return page switch
            {
                NavigationPage.Branches => new BranchesForm(),
                NavigationPage.StudyYears => new StudyYearsForm(),
                NavigationPage.Sections => new SectionsForm(),
                NavigationPage.Subjects => new SubjectsForm(),
                NavigationPage.Classrooms => new ClassroomsForm(),
                NavigationPage.TimeSlots => new TimeSlotsForm(),
                NavigationPage.FacultyAssignments => new FacultyMemberSubjectsForm(),
                NavigationPage.FacultyMembers => new FacultyMembersForm(),
                NavigationPage.Schedules => new SchedulesForm(),
                _ => throw new ArgumentOutOfRangeException(nameof(page), page, null)
            };
        }

        private static void PrepareContentPage(System.Windows.Forms.Control currentPage)
        {
            currentPage.Dock = System.Windows.Forms.DockStyle.Fill;
            currentPage.BackColor = System.Drawing.Color.FromArgb(245, 247, 250);

            if (FindControl(currentPage, "pnlSidebar") is { } legacySidebar)
            {
                legacySidebar.Visible = false;
                legacySidebar.Enabled = false;
                legacySidebar.Dock = System.Windows.Forms.DockStyle.None;
                legacySidebar.Width = 0;
            }

            if (FindControl(currentPage, "pnlMain") is { } mainPanel)
            {
                mainPanel.Dock = System.Windows.Forms.DockStyle.Fill;
                mainPanel.Location = System.Drawing.Point.Empty;
                mainPanel.Margin = System.Windows.Forms.Padding.Empty;
            }

            PolishPage(currentPage);
        }

        private static void PolishPage(System.Windows.Forms.Control parent)
        {
            foreach (System.Windows.Forms.Control child in parent.Controls)
            {
                PolishControl(child);
                PolishPage(child);
            }
        }

        private static void PolishControl(System.Windows.Forms.Control control)
        {
            if (control.Name is "lblEditorSubtitle" or "lblTableSubtitle")
            {
                control.Visible = false;
                return;
            }

            if (control.Name == "pnlHeader")
            {
                control.Height = 72;
                control.BackColor = System.Drawing.Color.White;
            }

            if (control.Name == "pnlWorkspace" && control is Guna.UI2.WinForms.Guna2Panel workspace)
            {
                workspace.Padding = new System.Windows.Forms.Padding(20);
                workspace.FillColor = System.Drawing.Color.FromArgb(245, 247, 250);
                workspace.AutoScroll = true;
            }

            if (control is Guna.UI2.WinForms.Guna2HtmlLabel label)
            {
                PolishLabel(label);
            }

            if (control is Guna.UI2.WinForms.Guna2Panel panel)
            {
                PolishPanel(panel);
            }

            if (control is Guna.UI2.WinForms.Guna2Button button)
            {
                PolishActionButton(button);
            }

            if (control is Guna.UI2.WinForms.Guna2TextBox textBox)
            {
                textBox.BorderRadius = 6;
                textBox.BorderColor = System.Drawing.Color.FromArgb(203, 213, 225);
                textBox.FocusedState.BorderColor = System.Drawing.Color.FromArgb(37, 99, 235);
                textBox.HoverState.BorderColor = System.Drawing.Color.FromArgb(148, 163, 184);
            }

            if (control is Guna.UI2.WinForms.Guna2ComboBox comboBox)
            {
                comboBox.BorderRadius = 6;
                comboBox.BorderColor = System.Drawing.Color.FromArgb(203, 213, 225);
                comboBox.FocusedState.BorderColor = System.Drawing.Color.FromArgb(37, 99, 235);
                comboBox.HoverState.BorderColor = System.Drawing.Color.FromArgb(148, 163, 184);
            }

            if (control is Guna.UI2.WinForms.Guna2DataGridView grid)
            {
                PolishGrid(grid);
            }
        }

        private static void PolishLabel(Guna.UI2.WinForms.Guna2HtmlLabel label)
        {
            label.BackColor = System.Drawing.Color.Transparent;

            if (label.Name == "lblPageTitle")
            {
                label.Font = new System.Drawing.Font("Segoe UI Semibold", 16F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
                label.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
                label.Location = new System.Drawing.Point(28, 12);
                return;
            }

            if (label.Name == "lblPageSubtitle")
            {
                label.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
                label.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
                label.Location = new System.Drawing.Point(28, 42);
                return;
            }

            if (label.Name is "lblEditorTitle" or "lblTableTitle")
            {
                label.Font = new System.Drawing.Font("Segoe UI Semibold", 11F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
                label.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            }
        }

        private static void PolishPanel(Guna.UI2.WinForms.Guna2Panel panel)
        {
            if (panel.Name is "pnlSidebar" or "pnlMain" or "pnlWorkspace" or "pnlHeader")
            {
                return;
            }

            panel.BorderRadius = 6;
            panel.BorderColor = System.Drawing.Color.FromArgb(226, 232, 240);
            panel.BorderThickness = 1;
            panel.FillColor = System.Drawing.Color.White;
        }

        private static void PolishActionButton(Guna.UI2.WinForms.Guna2Button button)
        {
            if (button.Name.StartsWith("btnNavigation", StringComparison.Ordinal))
            {
                return;
            }

            button.BorderRadius = 7;
            button.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            button.ForeColor = System.Drawing.Color.White;

            if (button.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase))
            {
                button.FillColor = System.Drawing.Color.FromArgb(220, 38, 38);
                button.HoverState.FillColor = System.Drawing.Color.FromArgb(185, 28, 28);
            }
            else if (button.Name.Contains("Update", StringComparison.OrdinalIgnoreCase))
            {
                button.FillColor = System.Drawing.Color.FromArgb(37, 99, 235);
                button.HoverState.FillColor = System.Drawing.Color.FromArgb(29, 78, 216);
            }
            else if (button.Name.Contains("Add", StringComparison.OrdinalIgnoreCase) ||
                     button.Name.Contains("Export", StringComparison.OrdinalIgnoreCase))
            {
                button.FillColor = System.Drawing.Color.FromArgb(22, 163, 74);
                button.HoverState.FillColor = System.Drawing.Color.FromArgb(21, 128, 61);
            }
            else if (button.Name.Contains("Clear", StringComparison.OrdinalIgnoreCase))
            {
                button.FillColor = System.Drawing.Color.FromArgb(100, 116, 139);
                button.HoverState.FillColor = System.Drawing.Color.FromArgb(71, 85, 105);
            }
            else if (button.Name.Contains("Generate", StringComparison.OrdinalIgnoreCase))
            {
                button.FillColor = System.Drawing.Color.FromArgb(15, 23, 42);
                button.HoverState.FillColor = System.Drawing.Color.FromArgb(30, 41, 59);
            }
        }

        private static void PolishGrid(Guna.UI2.WinForms.Guna2DataGridView grid)
        {
            grid.BackgroundColor = System.Drawing.Color.White;
            grid.BorderStyle = System.Windows.Forms.BorderStyle.None;
            grid.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = System.Drawing.Color.FromArgb(226, 232, 240);
            grid.ColumnHeadersHeight = 44;
            grid.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(15, 23, 42);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = System.Drawing.Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            grid.DefaultCellStyle.BackColor = System.Drawing.Color.White;
            grid.DefaultCellStyle.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            grid.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.FromArgb(219, 234, 254);
            grid.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.FromArgb(30, 64, 175);
            grid.RowTemplate.Height = 38;
        }

        private static System.Windows.Forms.Control? FindControl(
            System.Windows.Forms.Control parent,
            string name)
        {
            foreach (System.Windows.Forms.Control child in parent.Controls)
            {
                if (child.Name == name)
                {
                    return child;
                }

                var match = FindControl(child, name);

                if (match is not null)
                {
                    return match;
                }
            }

            return null;
        }

        private static void RemoveExistingNavigationButtons(Guna.UI2.WinForms.Guna2Panel sidebar)
        {
            var navigationButtons = sidebar.Controls
                .OfType<Guna.UI2.WinForms.Guna2Button>()
                .Where(control => control.Name.StartsWith("btnNavigation", StringComparison.Ordinal))
                .ToList();

            foreach (var button in navigationButtons)
            {
                sidebar.Controls.Remove(button);
                button.Dispose();
            }
        }

        private static Guna.UI2.WinForms.Guna2Button CreateNavigationButton(NavigationItem item, int top)
        {
            return new Guna.UI2.WinForms.Guna2Button
            {
                BorderRadius = 6,
                Cursor = System.Windows.Forms.Cursors.Hand,
                FillColor = System.Drawing.Color.Transparent,
                Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point),
                ForeColor = System.Drawing.Color.FromArgb(226, 232, 240),
                HoverState = { FillColor = System.Drawing.Color.FromArgb(36, 55, 86) },
                Location = new System.Drawing.Point(24, top),
                Name = item.ButtonName,
                Size = new System.Drawing.Size(192, 40),
                Text = item.Text,
                TextAlign = System.Windows.Forms.HorizontalAlignment.Left,
                TextOffset = new System.Drawing.Point(14, 0)
            };
        }

        private static void SetActive(Guna.UI2.WinForms.Guna2Button button)
        {
            button.Checked = true;
            button.Cursor = System.Windows.Forms.Cursors.Default;
            button.Enabled = false;
            button.FillColor = System.Drawing.Color.FromArgb(37, 99, 235);
            button.ForeColor = System.Drawing.Color.White;
            button.HoverState.FillColor = System.Drawing.Color.FromArgb(29, 78, 216);
            button.DisabledState.FillColor = System.Drawing.Color.FromArgb(37, 99, 235);
            button.DisabledState.ForeColor = System.Drawing.Color.White;
            button.DisabledState.BorderColor = System.Drawing.Color.FromArgb(37, 99, 235);
        }

        private sealed record NavigationItem(string ButtonName, string Text, NavigationPage Page);
    }
}
