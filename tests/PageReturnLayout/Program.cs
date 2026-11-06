using University_Timetable_and_Classroom_Management_System;

internal static class Program
{
    private static int failures;
    private static int checks;

    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();
        using var host = new Form { ClientSize = new Size(1610, 1000), ShowInTaskbar = false, Opacity = 0 };
        using var viewport = new Panel { Dock = DockStyle.Fill };
        host.Controls.Add(viewport);
        host.Shown += (_, _) =>
        {
            try
            {
                Func<UserControl>[] pages = [() => new SubjectsWithoutData(), () => new BranchesWithoutData(),
                    () => new StudyYearsWithoutData(), () => new SectionsWithoutData(),
                    () => new ClassroomsWithoutData(), () => new TimeSlotsWithoutData(),
                    () => new FacultyMembersWithoutData(), () => new AssignmentsWithoutData()];
                // Recreate pages as shell navigation does, then revisit each page.
                for (int visit = 0; visit < 2; visit++)
                    foreach (var create in pages) CheckPage(create(), viewport, visit);
            }
            catch (Exception e) { Check("Layout harness completes", false, e.ToString()); }
            finally { host.Close(); }
        };
        Application.Run(host);
        Console.WriteLine($"SUMMARY {checks} checks; {failures} failures; no database access.");
        return failures == 0 ? 0 : 1;
    }

    private static void CheckPage(UserControl page, Panel viewport, int visit)
    {
        using (page)
        {
            page.Visible = false;
            viewport.SuspendLayout();
            page.Size = viewport.ClientSize;
            viewport.Controls.Add(page);
            viewport.ResumeLayout(true);
            page.PerformLayout();
            page.Visible = true;
            Application.DoEvents();
            var workspace = (TableLayoutPanel)page.Controls.Find("layoutWorkspace", true).Single();
            Control editor = workspace.GetControlFromPosition(0, 0)!;
            Control results = workspace.GetControlFromPosition(0, 1)!;
            int compactHeight = editor.Height;
            int resultsHeight = results.Height;
            string name = page.GetType().BaseType!.Name + " visit " + visit;
            foreach (int narrowWidth in new[] { 420, 800, 1090 })
            {
                page.Width = narrowWidth;
                Application.DoEvents();
                page.Width = viewport.ClientSize.Width;
                Application.DoEvents();
                Check(name + " restores compact editor after width " + narrowWidth,
                    Math.Abs(editor.Height - compactHeight) <= 2,
                    $"expected {compactHeight}, actual {editor.Height}");
                Check(name + " restores results space after width " + narrowWidth,
                    results.Height >= resultsHeight - 2,
                    $"expected at least {resultsHeight - 2}, actual {results.Height}");
            }
            page.Visible = false;
            page.Size = new Size(420, 720);
            page.Size = viewport.ClientSize;
            page.Visible = true;
            Application.DoEvents();
            Check(name + " restores compact editor after hidden resize",
                Math.Abs(editor.Height - compactHeight) <= 2,
                $"expected {compactHeight}, actual {editor.Height}");
            Check(name + " keeps results below editor", results.Top >= editor.Bottom);
            viewport.Controls.Remove(page);
        }
    }

    private static void Check(string name, bool passed, string? detail = null)
    {
        checks++;
        if (!passed) failures++;
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name + (passed ? "" : " " + detail));
    }

    // Keep real runtime construction, Arabic setup and layout. Suppress only
    // OnLoad's data requests so resize/navigation checks never access a database.
    private sealed class SubjectsWithoutData : SubjectsForm { protected override void OnLoad(EventArgs e) { } }
    private sealed class BranchesWithoutData : BranchesForm { protected override void OnLoad(EventArgs e) { } }
    private sealed class StudyYearsWithoutData : StudyYearsForm { protected override void OnLoad(EventArgs e) { } }
    private sealed class SectionsWithoutData : SectionsForm { protected override void OnLoad(EventArgs e) { } }
    private sealed class ClassroomsWithoutData : ClassroomsForm { protected override void OnLoad(EventArgs e) { } }
    private sealed class TimeSlotsWithoutData : TimeSlotsForm { protected override void OnLoad(EventArgs e) { } }
    private sealed class FacultyMembersWithoutData : FacultyMembersForm { protected override void OnLoad(EventArgs e) { } }
    private sealed class AssignmentsWithoutData : FacultyMemberSubjectsForm { protected override void OnLoad(EventArgs e) { } }
}
