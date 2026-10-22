using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

internal static class Program
{
    [STAThread] private static int Main(string[] args)
    {
        string app = Path.GetFullPath(args[0]), output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        var resolver = new AssemblyDependencyResolver(app);
        AssemblyLoadContext.Default.Resolving += (_, name) => resolver.ResolveAssemblyToPath(name) is { } path
            ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(app);
        const string ns = "University_Timetable_and_Classroom_Management_System.";
        LicenseManager.CurrentContext = new DesignContext();
        ApplicationConfiguration.Initialize();
        var checks = new List<object>();
        int failures = 0;
        void Check(string name, bool passed)
        {
            checks.Add(new { name, passed });
            if (!passed) failures++;
            Console.WriteLine((passed ? "PASS " : "FAIL ") + name);
        }
        var measurements = new List<object>();
        foreach (string name in new[] { "BranchesForm", "StudyYearsForm", "SectionsForm", "SubjectsForm", "ClassroomsForm", "TimeSlotsForm", "FacultyMembersForm", "FacultyMemberSubjectsForm", "SchedulesForm" })
        {
            using var page = (Control)Activator.CreateInstance(assembly.GetType(ns + name)!)!;
            var controls = Descendants(page).Prepend(page).ToArray();
            int layouts = 0;
            foreach (var control in controls) control.Layout += (_, _) => layouts++;
            var watch = Stopwatch.StartNew();
            assembly.GetType(ns + "ArabicUi")!.GetMethod("Apply")!.Invoke(null, [page]);
            measurements.Add(new { name, controls = controls.Length, layouts, milliseconds = watch.Elapsed.TotalMilliseconds });
            // A screen-wide language update must not repeatedly lay out the
            // same nested hierarchy for every individual Arabic caption.
            Check(name + " avoids cascading layout work", layouts <= controls.Length * 2);
            Check(name + " keeps its Arabic captions and RTL", page.RightToLeft == RightToLeft.Yes &&
                controls.Where(c => c.GetType().Name == "DesignerHtmlLabel").All(c => c.Text.Any(ch => ch is >= '\u0600' and <= '\u06ff')));
            int laterLayouts = 0;
            page.Layout += (_, _) => laterLayouts++;
            page.Width += 20;
            page.PerformLayout();
            Check(name + " resumes normal layout after setup", laterLayouts > 0);
        }
        File.WriteAllText(Path.Combine(output, "checks.json"), JsonSerializer.Serialize(new { count = checks.Count, failures, checks, measurements }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"SUMMARY {checks.Count} checks; {failures} failures");
        return failures == 0 ? 0 : 1;
    }
    private static IEnumerable<Control> Descendants(Control control) => control.Controls.Cast<Control>().SelectMany(c => Descendants(c).Prepend(c));
    private sealed class DesignContext : LicenseContext { public override LicenseUsageMode UsageMode => LicenseUsageMode.Designtime; }
}
