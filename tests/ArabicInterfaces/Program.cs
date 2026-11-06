using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;

internal static class Program
{
    private static int failures, checks;
    private const string Ns = "University_Timetable_and_Classroom_Management_System.";

    [STAThread]
    private static int Main(string[] args)
    {
        string app = Path.GetFullPath(args[0]), output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        var resolver = new AssemblyDependencyResolver(app);
        AssemblyLoadContext.Default.Resolving += (_, name) => resolver.ResolveAssemblyToPath(name) is { } p
            ? AssemblyLoadContext.Default.LoadFromAssemblyPath(p) : null;
        AssemblyLoadContext.Default.ResolvingUnmanagedDll += (_, name) => resolver.ResolveUnmanagedDllToPath(name) is { } path
            ? System.Runtime.InteropServices.NativeLibrary.Load(path) : IntPtr.Zero;
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(app);
        // No database is accessed or changed: OnLoad stays in designer mode.
        LicenseManager.CurrentContext = new DesignContext();
        ApplicationConfiguration.Initialize();
        var ui = assembly.GetType(Ns + "ArabicUi")!;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-IQ");
        ui.GetMethod("InitializeCulture")!.Invoke(null, null);
        Check("Numbers use Western digits and the existing decimal separator", 1234.5.ToString("0.0") == "1234.5");

        foreach (string name in new[] { "LoginForm", "RegisterForm", "MainShellForm", "BranchesForm", "StudyYearsForm", "SectionsForm", "SubjectsForm", "ClassroomsForm", "TimeSlotsForm", "FacultyMembersForm", "FacultyMemberSubjectsForm", "SchedulesForm" })
        {
            using var control = (Control)Activator.CreateInstance(assembly.GetType(Ns + name)!)!;
            ui.GetMethod("Apply")!.Invoke(null, [control]);
            var all = Descendants(control).Prepend(control).ToArray();
            Check(name + " uses RTL", control.RightToLeft == RightToLeft.Yes);
            var untranslated = all.Where(c => c is Label || c.GetType().Name == "DesignerHtmlLabel" || c.GetType().Name == "Guna2Button")
                .Where(c => Regex.IsMatch(Regex.Replace(c.Text, "<[^>]+>", "").Replace("PDF", "").Replace("Excel", "").Replace("CSV", ""), "[A-Za-z]{2}")).Select(c => c.Name + ": " + c.Text).ToArray();
            Check(name + " captions are Arabic", untranslated.Length == 0, string.Join("; ", untranslated));
            Check(name + " column headings are Arabic", all.OfType<DataGridView>().SelectMany(g => g.Columns.Cast<DataGridViewColumn>())
                .All(c => !Regex.IsMatch(c.HeaderText, "[A-Za-z]{2}")));

            using var host = control as Form ?? new Form { ClientSize = new Size(1180, 820), RightToLeft = RightToLeft.Yes, RightToLeftLayout = true };
            if (control != host) { host.Controls.Add(control); control.Dock = DockStyle.Fill; }
            host.Show();
            Application.DoEvents();

            foreach (var (comboName, value, expected) in new[] {
                ("cmbDayOfWeek", "Sunday", "الأحد"), ("cmbLectureType", "Practical", "عملي"),
                ("cmbRoomType", "Shared Lab", "مختبر مشترك") })
            {
                if (all.FirstOrDefault(c => c.Name == comboName) is not ComboBox combo) continue;
                combo.SelectedItem = value;
                Check(name + " " + comboName + " shows Arabic", combo.GetItemText(combo.SelectedItem) == expected);
                Check(name + " " + comboName + " keeps stored value", Equals(combo.SelectedItem, value));
            }

            if (name == "FacultyMembersForm")
            {
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                var type = control.GetType();
                var rowsField = type.GetField("facultyRows", fields)!;
                var members = (System.Collections.IList)rowsField.GetValue(control)!;
                var memberType = rowsField.FieldType.GetGenericArguments()[0];
                foreach (var (id, title) in new (int, string?)[] { (1, "أ.د"), (2, "م.م"), (3, null) })
                {
                    var member = Activator.CreateInstance(memberType)!;
                    memberType.GetProperty("FacultyMemberID")!.SetValue(member, id);
                    memberType.GetProperty("FullName")!.SetValue(member, $"تدريسي {id}");
                    memberType.GetProperty("AcademicTitle")!.SetValue(member, title);
                    members.Add(member);
                }
                type.GetMethod("BindTitleOptions", fields)!.Invoke(control, null);
                var editor = all.Single(c => c.Name == "txtAcademicTitle");
                var choose = all.Single(c => c.Name == "btnChooseAcademicTitle");
                var storedTitles = (string[])type.GetField("academicTitles", fields)!.GetValue(control)!;
                Check("Title filter is removed", all.All(c => c.Name != "cmbTitleFilter"));
                Check("Title field has visible borders, a placeholder, and a title picker",
                    editor.Visible && editor.Height >= 40 && choose.Visible && choose.Parent == editor.Parent &&
                    editor.GetType().GetProperty("PlaceholderText")!.GetValue(editor) is string hint && hint.Contains("اختياري"));
                Check("Title picker offers the stored abbreviations", storedTitles.Order().SequenceEqual(new[] { "أ.د", "م.م" }.Order()));
                var grid = all.OfType<DataGridView>().Single(g => g.Name == "dgvFacultyMembers");
                grid.AutoGenerateColumns = false;
                type.GetMethod("ShowFacultyRows", fields)!.Invoke(control, null);
                Check("Faculty list displays all rows", grid.DataSource is System.Collections.IList visible && visible.Count == members.Count);
                type.GetMethod("ChooseAcademicTitle", fields)!.Invoke(control, null);
                var menu = (ContextMenuStrip)type.GetField("academicTitleMenu", fields)!.GetValue(control)!;
                menu.Items.Cast<ToolStripItem>().Single(item => item.Text == "أ.د").PerformClick();
                menu.Close();
                Check("Choosing a stored title fills the field", editor.Text == "أ.د");
                type.GetMethod("SetAcademicTitle", fields)!.Invoke(control, ["Professor"]);
                Check("An existing translated title keeps its stored value",
                    editor.Text == "أستاذ" && Equals(type.GetMethod("ReadAcademicTitle", fields)!.Invoke(control, null), "Professor"));
                editor.Text = "لقب تجريبي";
                Check("The title editor accepts a new typed title", Equals(type.GetMethod("ReadAcademicTitle", fields)!.Invoke(control, null), "لقب تجريبي"));
                type.GetMethod("ClearFacultyMemberForm", fields)!.Invoke(control, null);
                Check("Clearing the editor removes the previous title", editor.Text.Length == 0);
            }

            if (name == "SchedulesForm")
            {
                var schedule = control.GetType().GetMethod("ReadScheduleFromForm", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, null)!;
                Check("Schedule editor reads the canonical day", (string)schedule.GetType().GetProperty("DayOfWeek")!.GetValue(schedule)! == "Sunday");
                Check("Schedule editor reads the canonical lecture type", (string)schedule.GetType().GetProperty("LectureType")!.GetValue(schedule)! == "Practical");
                var grid = all.OfType<DataGridView>().First(g => g.Name == "dgvSchedules");
                int row = grid.Rows.Add();
                var day = grid.Columns.Cast<DataGridViewColumn>().First(c => c.DataPropertyName == "DayOfWeek");
                grid.Rows[row].Cells[day.Index].Value = "Sunday";
                Check("Grid formats Arabic without changing underlying data", Equals(grid.Rows[row].Cells[day.Index].FormattedValue, "الأحد") && Equals(grid.Rows[row].Cells[day.Index].Value, "Sunday"));
                const BindingFlags methods = BindingFlags.Instance | BindingFlags.NonPublic;
                var type = control.GetType();
                type.GetMethod("ConfigureCompactScheduleWorkspace", methods)!.Invoke(control, null);
                host.ClientSize = new Size(960, 660);
                type.GetMethod("ResizeScheduleWorkspace", methods)!.Invoke(control, null);
                var editor = control.Controls.Find("pnlScheduleEditor", true).Single();
                var toolbar = control.Controls.Find("pnlScheduleCommands", true).Single();
                var manual = control.Controls.Find("btnAddSchedule", true).Single();
                Check("Compact schedule keeps the table and toolbar visible and hides the editor",
                    !editor.Visible && toolbar.Visible && manual.Visible && grid.Visible && grid.Height >= 230);
                bool modalOpened = false;
                using var closeDialog = new System.Windows.Forms.Timer { Interval = 80 };
                closeDialog.Tick += (_, _) =>
                {
                    var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Name == "manualScheduleDialog");
                    if (dialog is null) return;
                    closeDialog.Stop();
                    var cancel = (Button)dialog.Controls.Find("btnCancelManualSchedule", true).Single();
                    modalOpened = editor.FindForm() == dialog && editor.Visible && cancel.Visible &&
                        dialog.Controls.Find("btnSaveManualSchedule", true).Single().Visible;
                    using var preview = new Bitmap(dialog.Width, dialog.Height);
                    dialog.DrawToBitmap(preview, dialog.ClientRectangle);
                    preview.Save(Path.Combine(output, "ManualScheduleDialog.png"));
                    cancel.PerformClick();
                };
                closeDialog.Start();
                ((Task)type.GetMethod("ShowManualScheduleEditorAsync", methods)!.Invoke(control, [false])!).GetAwaiter().GetResult();
                Check("The manual dialog opens and cancel restores the table", modalOpened && !editor.Visible && grid.Visible);
            }

            if (name is "LoginForm" or "RegisterForm" or "BranchesForm" or "SchedulesForm" or "FacultyMembersForm")
            {
                using var image = new Bitmap(host.Width, host.Height);
                host.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                image.Save(Path.Combine(output, name + ".png"));
            }
            host.Close();
        }

        var translator = assembly.GetType(Ns + "ArabicUiText")!.GetMethod("Message")!;
        foreach (string message in new[] { "Invalid user name or password.", "Password must be at least 4 characters.", "Branch ID already exists.", "Subject name is required.", "Classroom capacity must be greater than zero.", "This classroom is already booked in the selected semester, day, and time slot." })
            Check("Localized validation: " + message, Regex.IsMatch((string)translator.Invoke(null, [message])!, "[\u0600-\u06ff]"));

        if (args.Length > 2) CheckRuntime(assembly, output, args[2]);

        Console.WriteLine($"SUMMARY {checks} checks; {failures} failures");
        return failures == 0 ? 0 : 1;
    }

    private static void CheckRuntime(Assembly assembly, string output, string database)
    {
        if (!database.StartsWith("UniversityTimetable_FourFixes_", StringComparison.Ordinal))
            throw new ArgumentException("Only an isolated regression database is allowed.");
        Environment.SetEnvironmentVariable("UNIVERSITY_TIMETABLE_CONNECTION_STRING",
            $"Server=localhost;Database={database};Integrated Security=True;Encrypt=False;TrustServerCertificate=True");
        LicenseManager.CurrentContext = new LicenseContext();
        using var shell = (Form)Activator.CreateInstance(assembly.GetType(Ns + "MainShellForm")!)!;
        shell.Size = new Size(1440, 1000);
        shell.Shown += async (_, _) =>
        {
            try
            {
                foreach (string page in new[] { "Branches", "StudyYears", "Sections", "Subjects", "Classrooms", "TimeSlots", "FacultyMembers", "FacultyAssignments", "Schedules" })
                {
                    var button = shell.Controls.Find("btnNavigation" + page, true).Single();
                    button.GetType().GetMethod("PerformClick")!.Invoke(button, null);
                    await Task.Delay(100);
                    var host = shell.Controls.Find("pnlPageHost", true).Single();
                    var active = host.Controls.Cast<Control>().Single();
                    var add = Descendants(active).First(c => c.Name.StartsWith("btnAdd", StringComparison.Ordinal));
                    for (int attempt = 0; attempt < 400 && !add.Enabled; attempt++) await Task.Delay(100);
                    Check("Runtime " + page + " loads and uses RTL", active.Visible && active.RightToLeft == RightToLeft.Yes);
                    Check("Runtime " + page + " finishes loading data", add.Enabled);
                    if (page is "Branches" or "Classrooms" or "Schedules")
                    {
                        using var bitmap = new Bitmap(shell.Width, shell.Height);
                        shell.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                        bitmap.Save(Path.Combine(output, "Runtime-" + page + ".png"));
                    }
                }
            }
            catch (Exception exception) { Check("Runtime navigation completes", false, exception.ToString()); }
            finally { shell.Close(); }
        };
        Application.Run(shell);
    }

    private static IEnumerable<Control> Descendants(Control parent) => parent.Controls.Cast<Control>().SelectMany(c => Descendants(c).Prepend(c));
    private static void Check(string name, bool ok, string details = "")
    {
        checks++;
        if (!ok) failures++;
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (details.Length == 0 ? "" : ": " + details));
    }
    private sealed class DesignContext : LicenseContext { public override LicenseUsageMode UsageMode => LicenseUsageMode.Designtime; }
}
