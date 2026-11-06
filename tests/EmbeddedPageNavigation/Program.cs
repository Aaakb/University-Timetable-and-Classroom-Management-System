using System.Reflection;
using Data_Access_Layer;
using Guna.UI2.WinForms;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

internal static class Program
{
    private static int checks, failures;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly string database = "UniversityTimetable_EmbeddedNavigation_" + Guid.NewGuid().ToString("N")[..8];

    [STAThread]
    private static int Main()
    {
        string? previousConnection = Environment.GetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName);
        var connection = new SqlConnectionStringBuilder
        {
            DataSource = Environment.GetEnvironmentVariable("UNIVERSITY_TIMETABLE_TEST_SERVER") ?? "localhost",
            InitialCatalog = database, IntegratedSecurity = true, Encrypt = false, TrustServerCertificate = true
        };
        Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, connection.ConnectionString);
        Console.WriteLine("ISOLATED DATABASE " + database);
        try
        {
            Seed().GetAwaiter().GetResult();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            ApplicationConfiguration.Initialize();
            using var shell = new MainShellForm { Opacity = 0, ShowInTaskbar = false, WindowState = FormWindowState.Normal,
                ClientSize = new Size(1500, 900) };
            shell.Shown += async (_, _) =>
            {
                try { await Exercise(shell); }
                catch (Exception e) { Check("Navigation harness completes", false, e.ToString()); }
                finally { shell.Close(); }
            };
            Application.Run(shell);
            Console.WriteLine($"SUMMARY {checks} checks; {failures} failures");
            return failures == 0 ? 0 : 1;
        }
        finally
        {
            if (connection.InitialCatalog != database || !database.StartsWith("UniversityTimetable_EmbeddedNavigation_", StringComparison.Ordinal))
                throw new InvalidOperationException("Unsafe database cleanup target.");
            using (var db = new AppDbContext()) db.Database.EnsureDeleted();
            Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, previousConnection);
        }
    }

    private static async Task Seed()
    {
        await using var db = new AppDbContext();
        await db.Database.EnsureCreatedAsync();
        db.StudyYears.Add(new() { StudyYearID = 1, YearName = "First Year" });
        db.Sections.Add(new() { SectionID = 1, SectionName = "A", StudyYearID = 1, StudentCount = 20 });
        db.Subjects.Add(new() { SubjectID = 1, SubjectName = "Test subject", StudyYearID = 1,
            SemesterNumber = 1, TheoreticalHours = 1, TheoreticalLectureCount = 1 });
        db.FacultyMembers.Add(new() { FacultyMemberID = 1, FullName = "Test faculty" });
        db.FacultyMemberSubjects.Add(new() { FacultyMemberID = 1, SubjectID = 1, LectureType = "Theory" });
        db.Classrooms.Add(new() { ClassroomID = 1, ClassroomNumber = "Test room", RoomType = "Lecture", Capacity = 30 });
        db.TimeSlots.Add(new() { TimeSlotID = 1, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(9) });
        db.Schedules.Add(new() { FacultyMemberID = 1, SubjectID = 1, SectionID = 1, StudyYearID = 1,
            ClassroomID = 1, TimeSlotID = 1, SemesterNumber = 1, DayOfWeek = "Sunday", LectureType = "Theory", GroupName = "All" });
        await db.SaveChangesAsync();
        var request = new FacultyScheduleRequest(1, 1, FacultyAvailabilityMode.Strict, [], TimeLimitSeconds: 5)
        {
            UseDayPreferences = true,
            DayPreferences = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday" }
                .Select(day => new FacultyDaySetting(day, FacultyPreferenceLevel.Preferred)).ToArray()
        };
        await new FacultyPreferenceService().SaveAsync(request, 0);
    }

    private static async Task Exercise(MainShellForm shell)
    {
        Guna2Button Button(string name) => (Guna2Button)shell.Controls.Find(name, true).Single();
        var host = shell.Controls.Find("pnlPageHost", true).Single();
        var sidebar = shell.Controls.Find("pnlSidebar", true).Single();
        Form? Embedded() => host.Controls.Cast<Control>().SelectMany(Descendants).OfType<Form>().SingleOrDefault(f => !f.IsDisposed);
        string? NormalPage() => host.Controls.Cast<Control>().FirstOrDefault(x => !x.IsDisposed)?.GetType().Name;

        Button("btnNavigationSubjects").PerformClick();
        await Until(() => NormalPage() == "SubjectsForm", "subject page opens");
        Check("Subject page starts inside the shell", NormalPage() == "SubjectsForm");
        Button("btnFacultyScheduleEditor").PerformClick();
        await Until(() => Embedded() is FacultyPreferencesForm p && !GetBool(p, "busy") && Find<Button>(p, "save").Enabled,
            "faculty preferences load");
        var preferences = (FacultyPreferencesForm)Embedded()!;
        CheckEmbedded("Faculty preferences", preferences, shell, host, sidebar);
        Check("Faculty preferences navigation item is selected", Button("btnFacultyScheduleEditor").Checked);
        Check("Shown loads all five preference choices", Descendants(preferences).OfType<ComboBox>()
            .Count(c => c.Name.StartsWith("dayPreference", StringComparison.Ordinal) && c.Items.Count == 3) == 5);

        FormClosingEventHandler rejectClose = (_, e) => e.Cancel = true;
        preferences.FormClosing += rejectClose;
        Button("btnNavigationBranches").PerformClick();
        Check("A canceled close keeps the preferences page", Embedded() == preferences && !preferences.IsDisposed);
        Check("A canceled close keeps the preferences navigation selected", Button("btnFacultyScheduleEditor").Checked);
        preferences.FormClosing -= rejectClose;
        using (var preferenceCancellation = new CancellationTokenSource())
        {
            Set(preferences, "cancellation", preferenceCancellation);
            Set(preferences, "busy", true);
            Button("btnTimetableAlternatives").PerformClick();
            Check("Navigation while preferences are busy keeps the page", Embedded() == preferences && !preferences.IsDisposed);
            Check("Navigation while preferences are busy requests cancellation", preferenceCancellation.IsCancellationRequested);
            shell.Close();
            Check("Closing the shell while preferences are busy is canceled", !shell.IsDisposed && Embedded() == preferences);
            Set(preferences, "busy", false);
            Set(preferences, "cancellation", null);
        }

        var enabled = Find<CheckBox>(preferences, "enabled");
        Check("Saved preferences load in the embedded page", enabled.Checked);
        enabled.Checked = false;
        Find<Button>(preferences, "save").PerformClick();
        await Until(() => !GetBool(preferences, "busy") && !GetBool(preferences, "dirty"), "preference save completes");
        await using (var db = new AppDbContext())
            Check("Saving while embedded removes the optional profile", !await db.FacultySchedulingPreferences.AnyAsync(p => p.FacultyMemberID == 1 && p.SemesterNumber == 1));
        Check("Saving leaves the embedded page and sidebar visible", Embedded() == preferences && sidebar.Visible);
        Find<Button>(preferences, "close").PerformClick();
        await Until(() => NormalPage() == "SubjectsForm", "close returns to previous screen");
        Check("Preferences close returns to the previous page", NormalPage() == "SubjectsForm" && preferences.IsDisposed);

        Button("btnTimetableAlternatives").PerformClick();
        await Until(() => Embedded() is TimetableAlternativesForm a && !GetBool(a, "busy") &&
            Find<DataGridView>(a, "alternativeComparison").Rows.Count > 0, "alternatives load");
        var alternatives = (TimetableAlternativesForm)Embedded()!;
        CheckEmbedded("Timetable alternatives", alternatives, shell, host, sidebar);
        Check("Alternatives navigation item is selected", Button("btnTimetableAlternatives").Checked);
        Check("Shown loads the current timetable", Find<DataGridView>(alternatives, "alternativeComparison").Rows.Count > 0);
        Button("btnNavigationSubjects").PerformClick();
        await Until(() => NormalPage() == "SubjectsForm", "sidebar navigation returns to subjects");
        Check("Sidebar navigation closes embedded alternatives", alternatives.IsDisposed && NormalPage() == "SubjectsForm");

        Button("btnTimetableAlternatives").PerformClick();
        await Until(() => Embedded() is TimetableAlternativesForm a && !GetBool(a, "busy") &&
            Find<DataGridView>(a, "alternativeComparison").Rows.Count > 0, "alternatives reload");
        alternatives = (TimetableAlternativesForm)Embedded()!;
        using var cancellation = new CancellationTokenSource();
        Set(alternatives, "cancellation", cancellation);
        Set(alternatives, "busy", true);
        Button("btnNavigationBranches").PerformClick();
        Check("Navigation while busy keeps the alternatives page", Embedded() == alternatives && !alternatives.IsDisposed);
        Check("Navigation while busy requests cancellation", cancellation.IsCancellationRequested);
        Set(alternatives, "busy", false);
        Set(alternatives, "cancellation", null);
        Find<Button>(alternatives, "closeAlternatives").PerformClick();
        await Until(() => NormalPage() == "SubjectsForm", "alternatives close returns to previous page");
        Check("Alternatives close button returns to the previous page", alternatives.IsDisposed && NormalPage() == "SubjectsForm");
        await using (var db = new AppDbContext())
            Check("Navigation and preview retain the saved lecture", await db.Schedules.CountAsync() == 1);
    }

    private static void CheckEmbedded(string label, Form page, Form shell, Control host, Control sidebar)
    {
        Check(label + " is a visible child inside the existing host", !page.TopLevel && page.Visible && Descendants(host).Contains(page));
        Check(label + " has no separate window chrome or taskbar entry", page.FormBorderStyle == FormBorderStyle.None && !page.ShowInTaskbar);
        Check(label + " preserves the existing sidebar and shell", sidebar.Visible && shell.Visible && page.FindForm() == page);
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static T Find<T>(Control root, string name) where T : Control => (T)root.Controls.Find(name, true).Single();
    private static bool GetBool(object obj, string field) => (bool)obj.GetType().GetField(field, Private)!.GetValue(obj)!;
    private static void Set(object obj, string field, object? value) => obj.GetType().GetField(field, Private)!.SetValue(obj, value);
    private static async Task Until(Func<bool> condition, string reason)
    {
        var end = DateTime.UtcNow.AddSeconds(30);
        while (!condition())
        {
            if (DateTime.UtcNow >= end) throw new TimeoutException(reason);
            await Task.Delay(50);
        }
    }
    private static void Check(string name, bool passed, string? detail = null)
    {
        checks++;
        if (!passed) failures++;
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name + (passed || detail is null ? "" : " " + detail));
    }
}
