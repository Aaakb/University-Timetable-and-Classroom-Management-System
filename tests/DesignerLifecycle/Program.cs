using System.ComponentModel;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

internal static class Program
{
    private static readonly string[] Properties = ["AvoidGeometryAntialias", "UseGdiPlusTextRendering", "IsSelectionEnabled", "IsContextMenuEnabled"];
    private static readonly List<object> Checks = [];
    private static int failures;

    [STAThread] static int Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Pass the application DLL and results JSON path.");
        string application = Path.GetFullPath(args[0]);
        var resolver = new AssemblyDependencyResolver(application);
        AssemblyLoadContext.Default.Resolving += (_, name) => resolver.ResolveAssemblyToPath(name) is { } path
            ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(application);
        LicenseManager.CurrentContext = new DesignContext();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        ApplicationConfiguration.Initialize();

        try
        {
            Type safeType = assembly.GetType("University_Timetable_and_Classroom_Management_System.DesignerHtmlLabel", throwOnError: true)!;
            Type gunaType = safeType.BaseType!;
            using (var original = (Control)Activator.CreateInstance(gunaType)!)
            {
                original.Dispose();
                int nullReferences = Properties.Count(name => ReadThrowsNullReference(original, name));
                Check("Original Guna label reproduces four disposed getter failures", nullReferences == 4);
            }

            using (var safe = (Control)Activator.CreateInstance(safeType)!)
            {
                var descriptors = Properties.Select(name => TypeDescriptor.GetProperties(safe)[name]!).ToArray();
                bool[] defaults = [false, false, true, true];
                Check("All four properties remain editable in the Properties window",
                    descriptors.All(property => property.IsBrowsable && !property.IsReadOnly && property.Category == "Behavior" &&
                        property.SerializationVisibility == DesignerSerializationVisibility.Visible));
                Check("Designer defaults remain unchanged", descriptors.Select((property, index) =>
                    property.Attributes[typeof(DefaultValueAttribute)] is DefaultValueAttribute attribute && Equals(attribute.Value, defaults[index]) &&
                    Equals(property.GetValue(safe), defaults[index])).All(value => value));
                bool[] changed = [true, true, false, false];
                for (int i = 0; i < descriptors.Length; i++) descriptors[i].SetValue(safe, changed[i]);
                Check("Designer edits update the live Guna renderer", descriptors.Select((property, index) =>
                    Equals(property.GetValue(safe), changed[index])).All(value => value));
                int disposeNotifications = 0;
                bool disposeCallbackValuesMatch = true;
                safe.Disposed += (_, _) =>
                {
                    disposeNotifications++;
                    disposeCallbackValuesMatch &= MatchValues(safe, changed);
                };
                safe.Dispose();
                Check("Property requests during teardown retain their values", disposeNotifications > 0 && disposeCallbackValuesMatch);
                Check("Property requests after teardown retain their values", MatchValues(safe, changed));
                safe.Dispose();
                Check("Repeated disposal remains safe", safe.IsDisposed && disposeNotifications >= 2 && disposeCallbackValuesMatch && MatchValues(safe, changed));
            }

            int labelCount = 0;
            foreach (string name in new[] { "LoginForm", "RegisterForm", "MainShellForm", "BranchesForm", "StudyYearsForm", "SectionsForm", "SubjectsForm", "ClassroomsForm", "TimeSlotsForm", "FacultyMembersForm", "FacultyMemberSubjectsForm", "SchedulesForm" })
            {
                using var form = (Control)Activator.CreateInstance(assembly.GetType("University_Timetable_and_Classroom_Management_System." + name)!)!;
                var labels = Descendants(form).Where(control => gunaType.IsInstanceOfType(control)).ToArray();
                labelCount += labels.Length;
                Check(name + " uses the safe designer label for every HTML label", labels.Length > 0 && labels.All(safeType.IsInstanceOfType));
                Check(name + " properties are readable before handles are created", ReadAll(labels));
                form.CreateControl();
                foreach (Control label in labels) label.CreateControl();
                Check(name + " properties are readable with handles", ReadAll(labels));
                bool callbacksPassed = true;
                foreach (Control label in labels) label.Disposed += (_, _) => callbacksPassed &= ReadAll([label]);
                form.Dispose();
                Check(name + " properties are readable during disposal callbacks", callbacksPassed);
                Check(name + " properties are readable after the designer form closes", ReadAll(labels));
                form.Dispose();
                Check(name + " repeated form disposal remains safe", ReadAll(labels));
            }
            Check("The lifecycle sweep covers all 118 labels in all 12 screens", labelCount == 118);
        }
        catch (Exception exception)
        {
            Check("Lifecycle harness completed", false, exception.ToString());
        }

        string results = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(Path.GetDirectoryName(results)!);
        File.WriteAllText(results, JsonSerializer.Serialize(new { count = Checks.Count, failures, checks = Checks }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"SUMMARY {Checks.Count} checks; {failures} failures");
        return failures == 0 ? 0 : 1;
    }

    private static bool ReadThrowsNullReference(Control control, string name)
    {
        try { TypeDescriptor.GetProperties(control)[name]!.GetValue(control); return false; }
        catch (Exception exception) { return exception is NullReferenceException || exception is TargetInvocationException { InnerException: NullReferenceException }; }
    }
    private static bool ReadAll(IEnumerable<Control> controls)
    {
        try
        {
            foreach (Control control in controls)
                foreach (string name in Properties) TypeDescriptor.GetProperties(control)[name]!.GetValue(control);
            return true;
        }
        catch (Exception exception) { Console.WriteLine(exception); return false; }
    }
    private static bool MatchValues(Control control, bool[] expected)
    {
        try { return Properties.Select((name, index) => Equals(TypeDescriptor.GetProperties(control)[name]!.GetValue(control), expected[index])).All(value => value); }
        catch (Exception exception) { Console.WriteLine(exception); return false; }
    }
    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (Control child in control.Controls)
        {
            yield return child;
            foreach (Control descendant in Descendants(child)) yield return descendant;
        }
    }
    private static void Check(string name, bool passed, string? error = null)
    {
        Checks.Add(new { name, passed, error });
        if (!passed) failures++;
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name);
    }
    private sealed class DesignContext : LicenseContext
    {
        public override LicenseUsageMode UsageMode => LicenseUsageMode.Designtime;
    }
}
