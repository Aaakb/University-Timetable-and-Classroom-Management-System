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
            new("btnNavigationBranches", NavigationPage.Branches),
            new("btnNavigationStudyYears", NavigationPage.StudyYears),
            new("btnNavigationSections", NavigationPage.Sections),
            new("btnNavigationSubjects", NavigationPage.Subjects),
            new("btnNavigationClassrooms", NavigationPage.Classrooms),
            new("btnNavigationTimeSlots", NavigationPage.TimeSlots),
            new("btnNavigationFacultyAssignments", NavigationPage.FacultyAssignments),
            new("btnNavigationFacultyMembers", NavigationPage.FacultyMembers),
            new("btnNavigationSchedules", NavigationPage.Schedules)
        ];

        public static void ConfigureShellSidebar(
            MainShellForm shell,
            Guna.UI2.WinForms.Guna2Panel sidebar,
            NavigationPage currentPage)
        {
            foreach (var item in NavigationItems)
            {
                if (FindControl(sidebar, item.ButtonName) is not Guna.UI2.WinForms.Guna2Button button)
                {
                    continue;
                }

                ResetNavigationButton(button);

                if (button.Tag is not NavigationPage)
                {
                    button.Tag = item.Page;
                    button.Click += (_, _) =>
                    {
                        if (button.Tag is NavigationPage page)
                        {
                            shell.ShowPage(page);
                        }
                    };
                }

                if (item.Page == currentPage)
                {
                    SetActive(button);
                }
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

        private static void ResetNavigationButton(Guna.UI2.WinForms.Guna2Button button)
        {
            button.Checked = false;
            button.Cursor = System.Windows.Forms.Cursors.Hand;
            button.Enabled = true;
        }

        private static void SetActive(Guna.UI2.WinForms.Guna2Button button)
        {
            button.Checked = true;
            button.Cursor = System.Windows.Forms.Cursors.Default;
            button.Enabled = false;
        }

        private sealed record NavigationItem(string ButtonName, NavigationPage Page);
    }
}
