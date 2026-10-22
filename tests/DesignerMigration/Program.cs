using System.ComponentModel;
using System.Collections;
using System.Data;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class Program
{
    private const string Ns = "University_Timetable_and_Classroom_Management_System.";
    private static readonly string[] Forms = ["ExcelImportForm", "FacultyPreferencesForm", "FacultyScheduleEditorForm", "TimetableAlternativesForm"];
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static readonly List<object> Checks = [];
    private static int failures;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length < 3) throw new ArgumentException("Use capture|verify application.dll output [baseline] [form].");
        string command = args[0], application = Path.GetFullPath(args[1]), output = Path.GetFullPath(args[2]);
        Directory.CreateDirectory(output);
        var resolver = new AssemblyDependencyResolver(application);
        AssemblyLoadContext.Default.Resolving += (_, name) => resolver.ResolveAssemblyToPath(name) is { } path
            ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        AssemblyLoadContext.Default.ResolvingUnmanagedDll += (_, name) => resolver.ResolveUnmanagedDllToPath(name) is { } path
            ? System.Runtime.InteropServices.NativeLibrary.Load(path) : IntPtr.Zero;
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(application);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        ApplicationConfiguration.Initialize();
        string? only = args.Length > 4 ? args[4] : null;
        foreach (string name in Forms.Where(name => only is null || name == only))
        {
            try
            {
                foreach (bool design in new[] { false, true })
                {
                    LicenseManager.CurrentContext = design ? new DesignContext() : new LicenseContext();
                    using var form = (Form)Activator.CreateInstance(assembly.GetType(Ns + name, true)!)!;
                    // Creating handles and rendering never invokes Form.OnShown. No form is shown,
                    // no action/selection event is submitted, and no database service is called.
                    form.CreateControl();
                    form.PerformLayout();
                    foreach (Control child in Descendants(form)) child.CreateControl();
                    form.PerformLayout();
                    string suffix = design ? "design" : "runtime";
                    File.WriteAllText(Path.Combine(output, name + "-" + suffix + "-initial.json"), Snapshot(form).ToJsonString(Json));
                    RenderWithoutShowingOriginal(form, Path.Combine(output, name + "-" + suffix + ".png"));
                    var snapshot = Snapshot(form);
                    File.WriteAllText(Path.Combine(output, name + "-" + suffix + ".json"), snapshot.ToJsonString(Json));
                    if (command == "diagnose")
                        foreach (var combo in Descendants(form).OfType<ComboBox>())
                            Console.WriteLine($"DIAG {name} {suffix} {combo.Name} actual={combo.Size} preferredHeight={combo.PreferredHeight} preferred={combo.PreferredSize} parent={combo.Parent?.Size} parentPreferred={combo.Parent?.PreferredSize} font={combo.Font}");
                    Check(name + " " + suffix + " constructs and renders without showing", form.Controls.Count > 0);

                    if (command == "verify")
                    {
                        if (args.Length < 4) throw new ArgumentException("verify requires baseline directory.");
                        string baseline = Path.Combine(Path.GetFullPath(args[3]), name + "-runtime.json");
                        var expected = JsonNode.Parse(File.ReadAllText(baseline))!;
                        var comparable = (JsonObject)snapshot.DeepClone();
                        if (name == "FacultyPreferencesForm") RemoveNewDayCards(expected, comparable);
                        var differences = new List<string>();
                        Compare(expected["controls"], comparable["controls"], "$controls", differences, design, name);
                        File.WriteAllText(Path.Combine(output, name + "-" + suffix + "-differences.json"), JsonSerializer.Serialize(differences, Json));
                        Check(name + " " + suffix + " preserves initial UI properties", differences.Count == 0,
                            string.Join("; ", differences.Take(12)));
                        var beforeControls = (JsonArray)expected["controls"]!;
                        var afterControls = (JsonArray)comparable["controls"]!;
                        Check(name + " " + suffix + " preserves every original control bound", beforeControls.Count == afterControls.Count && beforeControls.Select((control, index) =>
                            control!["properties"]!["bounds"]!.GetValue<string>() == afterControls[index]!["properties"]!["bounds"]!.GetValue<string>()).All(value => value));
                        if (name == "ExcelImportForm")
                            Check("Excel " + suffix + " keeps the three original field heights through designer minimums", new[] { "flow", "flow2", "entityField" }
                                .All(field => Descendants(form).Single(c => c.Name == field).MinimumSize == new Size(0, 38)));
                        if (name == "FacultyScheduleEditorForm")
                            Check("Editor " + suffix + " keeps the three original field heights through designer minimums", new[] { "flow", "flow2", "flow3" }
                                .All(field => Descendants(form).Single(c => c.Name == field).MinimumSize == new Size(0, 34)));
                        if (name == "FacultyPreferencesForm")
                            Check("Preferences " + suffix + " keeps the two original field heights through designer minimums", new[] { "flow", "flow2" }
                                .All(field => Descendants(form).Single(c => c.Name == field).MinimumSize == new Size(0, 58)));
                        if (name == "FacultyPreferencesForm") CheckDayCards(form, design);
                        if (design)
                        {
                            Check(name + " exposes InitializeComponent for the designer", form.GetType().GetMethod("InitializeComponent", BindingFlags.NonPublic | BindingFlags.Instance) is not null);
                            var field = form.GetType().GetField("service", BindingFlags.NonPublic | BindingFlags.Instance);
                            Check(name + " designer does not instantiate a database service", field is not null && field.GetValue(form) is null);
                            var handlers = ApplicationHandlers(form).Concat(Descendants(form).SelectMany(ApplicationHandlers)).ToArray();
                            Check(name + " designer has no application event handlers", handlers.Length == 0, string.Join("; ", handlers));
                        }
                        else
                        {
                            var missing = new List<string>();
                            var before = (JsonArray)expected["controls"]!;
                            var after = (JsonArray)comparable["controls"]!;
                            for (int i = 0; i < Math.Min(before.Count, after.Count); i++)
                            {
                                var oldEvents = ((JsonArray)before[i]!["applicationHandlers"]!).Select(v => v!.GetValue<string>()).GroupBy(v => v);
                                var newEvents = ((JsonArray)after[i]!["applicationHandlers"]!).Select(v => v!.GetValue<string>()).ToArray();
                                foreach (var group in oldEvents)
                                    if (newEvents.Count(v => v == group.Key) < group.Count()) missing.Add(before[i]!["path"] + " " + group.Key);
                            }
                            Check(name + " keeps runtime event wiring", missing.Count == 0, string.Join("; ", missing));
                        }
                    }
                }
                if (name == "ExcelImportForm") CheckExcelImportBehavior(assembly);
            }
            catch (Exception exception) { Check(name + " completes", false, exception.ToString()); }
        }
        if (command == "verify" && (only is null || only == "MainShellForm"))
        {
            try { CheckShellDesigner(assembly, output); }
            catch (Exception exception) { Check("Shell designer checks complete", false, exception.ToString()); }
        }
        File.WriteAllText(Path.Combine(output, "checks.json"), JsonSerializer.Serialize(new { count = Checks.Count, failures, checks = Checks }, Json));
        Console.WriteLine($"SUMMARY {Checks.Count} checks; {failures} failures");
        return failures == 0 ? 0 : 1;
    }

    private static void RemoveNewDayCards(JsonNode expected, JsonObject actual)
    {
        var before = (JsonArray)expected["controls"]!;
        var after = (JsonArray)actual["controls"]!;
        var oldCards = before.Single(node => node!["name"]?.GetValue<string>() == "dayPreferenceCards")!;
        string prefix = oldCards["path"]!.GetValue<string>() + "/";
        if (before.Any(node => node!["path"]!.GetValue<string>().StartsWith(prefix, StringComparison.Ordinal))) return;
        foreach (var node in after.Where(node => node!["path"]!.GetValue<string>().StartsWith(prefix, StringComparison.Ordinal)).ToArray()) after.Remove(node);
        var newCards = after.Single(node => node!["name"]?.GetValue<string>() == "dayPreferenceCards")!;
        // Five fixed teaching days formerly appeared only after async data loading.
        // Their new static designer subtree is verified separately, while every
        // pre-existing control remains in the exact baseline comparison.
        if (oldCards["properties"]?["columnStyles"] is JsonArray { Count: 0 } && newCards["properties"]?["columnStyles"] is JsonArray { Count: 5 } styles &&
            styles.All(node => node!["type"]?.GetValue<string>() == "Percent" && node["size"]?.GetValue<float>() == 20))
            newCards["properties"]!["columnStyles"] = new JsonArray();
    }

    private static void CheckDayCards(Form form, bool design)
    {
        string context = design ? "designer" : "runtime";
        // The original dialog binds choices only after its Shown lookup completes.
        // Invoke just the extracted in-memory binder, never the Shown/DB handler.
        if (!design) form.GetType().GetMethod("BindDayPreferences", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(form, null);
        var table = Descendants(form).OfType<TableLayoutPanel>().Single(c => c.Name == "dayPreferenceCards");
        string[] canonicalDays = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday"];
        string[] labels = ["الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس"];
        Check("Preferences " + context + " contains five static teaching-day cards", table.Controls.Count == 5);
        for (int i = 0; i < canonicalDays.Length; i++)
        {
            var matches = Descendants(table).OfType<ComboBox>().Where(c => c.Name == "dayPreference_" + canonicalDays[i]).ToArray();
            Check("Preferences " + context + " maps " + canonicalDays[i] + " to its Arabic day card", matches.Length == 1 && matches[0].Parent is TableLayoutPanel card &&
                card.Parent == table && table.GetColumn(card) == i && table.GetRow(card) == 0 && card.Controls.Count == 2 && card.Controls.OfType<Label>().Single().Text == labels[i]);
            if (matches.Length != 1) continue;
            var combo = matches[0];
            Check("Preferences " + context + " " + canonicalDays[i] + " has all three levels", combo.Items.Cast<object>().Select(combo.GetItemText).SequenceEqual(["مفضّل", "مسموح", "غير متاح"]));
            if (design) Check("Preferences designer " + canonicalDays[i] + " defaults to allowed", combo.SelectedIndex == 1);
            if (!design)
            {
                Type? levelType = combo.Items.Cast<object>().FirstOrDefault()?.GetType().GetProperty("Value")?.PropertyType;
                bool mapsEveryValue = levelType?.IsEnum == true;
                if (mapsEveryValue)
                    foreach (string level in new[] { "StronglyPreferred", "Preferred", "Prohibited" })
                    {
                        combo.SelectedValue = Enum.Parse(levelType!, level);
                        mapsEveryValue &= combo.SelectedValue?.ToString() == level;
                    }
                Check("Preferences runtime " + canonicalDays[i] + " keeps all three canonical availability values", mapsEveryValue);
            }
        }
        if (!design)
        {
            var days = (IDictionary)form.GetType().GetField("days", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(form)!;
            Check("Preferences runtime keeps canonical day mappings", days.Count == 5 && canonicalDays.All(day => days.Contains(day) && days[day] is ComboBox combo && combo.Name == "dayPreference_" + day));
        }
    }

    private static void RenderWithoutShowingOriginal(Form form, string path)
    {
        // Invisible WinForms parents suppress descendant painting. Move only the
        // UI root into a neutral, transparent host: the application Form never
        // receives Load/Shown, so its data-loading handlers cannot run.
        Control[] roots = form.Controls.Cast<Control>().ToArray();
        using var host = new Form
        {
            ClientSize = form.ClientSize, Font = form.Font, BackColor = form.BackColor,
            ForeColor = form.ForeColor, RightToLeft = form.RightToLeft,
            BindingContext = form.BindingContext,
            RightToLeftLayout = form.RightToLeftLayout, ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000), Opacity = 0,
            FormBorderStyle = FormBorderStyle.None, AutoScaleMode = AutoScaleMode.None
        };
        foreach (Control root in roots) form.Controls.Remove(root);
        try
        {
            host.Controls.AddRange(roots);
            host.Show();
            Size clientSize = host.ClientSize;
            host.ClientSize = new Size(clientSize.Width - 1, clientSize.Height);
            host.PerformLayout();
            host.ClientSize = clientSize;
            foreach (Control control in Descendants(host).Reverse()) control.PerformLayout();
            host.PerformLayout();
            host.Invalidate(true);
            host.Update();
            Application.DoEvents();
            using var bitmap = new Bitmap(host.ClientSize.Width, host.ClientSize.Height);
            host.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(path);
            host.Hide();
        }
        finally
        {
            foreach (Control root in roots) host.Controls.Remove(root);
            form.Controls.AddRange(roots);
        }
    }

    private static void CheckShellDesigner(Assembly assembly, string output)
    {
        LicenseManager.CurrentContext = new DesignContext();
        using var shell = (Form)Activator.CreateInstance(assembly.GetType(Ns + "MainShellForm", true)!)!;
        var all = Descendants(shell).ToArray();
        var navigation = all.OfType<TableLayoutPanel>().Single(c => c.Name == "layoutNavigation");
        foreach (var (name, row) in new[] { ("btnExcelImport", 11), ("btnFacultyScheduleEditor", 12), ("btnTimetableAlternatives", 13) })
        {
            var matches = all.Where(c => c.Name == name).ToArray();
            Check("Shell designer contains exactly one " + name, matches.Length == 1);
            if (matches.Length == 1)
                Check("Shell designer places " + name + " in row " + row, matches[0].Parent == navigation && navigation.GetRow(matches[0]) == row);
        }
        var footer = all.Single(c => c.Name == "lblSidebarFooter");
        Check("Shell designer keeps footer after all commands", navigation.GetRow(footer) == 14 && navigation.RowCount == 15);
        Check("Shell designer does not create a runtime page", !all.Any(c => c.GetType().Name == "BranchesForm"));
        Check("Shell designer has no application event handlers", !ApplicationHandlers(shell).Concat(all.SelectMany(ApplicationHandlers)).Any());
        File.WriteAllText(Path.Combine(output, "MainShellForm-design.json"), Snapshot(shell).ToJsonString(Json));
        RenderWithoutShowingOriginal(shell, Path.Combine(output, "MainShellForm-design.png"));
    }

    private static void CheckExcelImportBehavior(Assembly assembly)
    {
        LicenseManager.CurrentContext = new LicenseContext();
        Type formType = assembly.GetType(Ns + "ExcelImportForm", true)!;
        Type sheetType = formType.GetField("workbook", BindingFlags.NonPublic | BindingFlags.Instance)!.FieldType.Assembly
            .GetType(Ns + "BusinessLayer.ExcelSheetData", true)!;
        Type workbookType = sheetType.Assembly.GetType(Ns + "BusinessLayer.ExcelWorkbookData", true)!;
        for (int selectedMode = 0; selectedMode < 4; selectedMode++)
        {
            using var form = (Form)Activator.CreateInstance(formType)!;
            form.BindingContext = new BindingContext();
            _ = form.Handle;
            object Field(string name) => formType.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(form)!;
            object? Call(string name, params object[] values) => formType.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(form, values);
            var mode = (ComboBox)Field("mode");
            var entity = (ComboBox)Field("entity");
            var data = (DataGridView)Field("data");
            var mapping = (DataGridView)Field("mapping");
            var tabs = (TabControl)Field("tabs");
            _ = data.Handle; _ = mapping.Handle;
            mode.SelectedIndex = selectedMode;
            string expectedMode = new[] { "AcademicData", "CompleteTimetable", "Hybrid", "RepairTimetable" }[selectedMode];
            var canonicalMode = formType.GetProperty("Mode", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(form);
            Check("Excel mode " + selectedMode + " keeps canonical enum", canonicalMode?.ToString() == expectedMode);
            Check("Excel mode " + selectedMode + " keeps canonical schema choices", entity.Items.Contains("Branches") && entity.Items.Contains("NamedAssignments") && entity.Items.Cast<object>().All(v => !v.ToString()!.Any(c => c is >= '\u0600' and <= '\u06ff')));

            var table = new DataTable(selectedMode == 0 ? "Branches" : "Timetable");
            string[] columns = selectedMode == 0 ? ["BranchID", "BranchName"] : ["Day", "TimeSlot", "Subject", "Faculty", "Classroom", "Section", "LectureType", "SemesterNumber"];
            foreach (string column in columns) table.Columns.Add(column, typeof(string));
            table.Rows.Add(selectedMode == 0 ? ["51", "التقنيات الطبية"] : ["Sunday", "08:30", "الرياضيات", "تدريسي تجريبي", "قاعة 1", "A", "Theory", "1"]);
            var sheet = Activator.CreateInstance(sheetType, [table.TableName, table, new[] { 7 }])!;
            sheetType.GetProperty("SuggestedSchemaName")!.SetValue(sheet, table.TableName);
            var workbook = Activator.CreateInstance(workbookType)!;
            ((IList)workbookType.GetProperty("Sheets")!.GetValue(workbook)!).Add(sheet);
            Call("LoadWorkbook", workbook, "designer-migration-fixture.xlsx");
            Check("Excel mode " + selectedMode + " loads in-memory workbook", data.Rows.Count == 1 && ((Control)Field("validate")).Enabled && ((Label)Field("fileSummary")).Text.Contains("designer-migration-fixture.xlsx"));
            Check("Excel mode " + selectedMode + " preserves mapping column types", mapping.Columns.Count == 2 && mapping.Columns[0] is DataGridViewTextBoxColumn && mapping.Columns[0].ReadOnly && mapping.Columns[1] is DataGridViewComboBoxColumn { DisplayMember: "Label", ValueMember: "Value" });
            var prepared = ((IEnumerable)Call("PrepareSheets")!).Cast<object>().ToArray();
            var preparedTable = (DataTable)sheetType.GetProperty("Table")!.GetValue(prepared.Single())!;
            var sourceRows = (IEnumerable<int>)sheetType.GetProperty("SourceRows")!.GetValue(prepared.Single())!;
            Check("Excel mode " + selectedMode + " preserves source rows and mapped values", sourceRows.SequenceEqual([7]) && preparedTable.Rows.Count == 1 &&
                (selectedMode == 0 ? preparedTable.TableName == "Branches" && (string)preparedTable.Rows[0]["BranchID"] == "51" : (string)preparedTable.Rows[0]["Day"] == "Sunday" && (string)preparedTable.Rows[0]["LectureType"] == "Theory"));
            Check("Excel mode " + selectedMode + " keeps appropriate fields", OwnVisible((Control)Field("entityField")) == (selectedMode == 0) && OwnVisible((Control)Field("semesterField")) == (selectedMode != 0) && OwnVisible((Control)Field("repairTimeField")) == (selectedMode == 3));
            Check("Excel mode " + selectedMode + " keeps generation and save workflow", OwnVisible((Control)Field("generate")) == (selectedMode >= 2) && !((Control)Field("generate")).Enabled && !((Control)Field("save")).Enabled && tabs.TabPages.Count == 4);
            if (selectedMode >= 2)
            {
                Check("Excel mode " + selectedMode + " preserves editable canonical lock column", data.Columns["Locked"] is DataGridViewCheckBoxColumn { TrueValue: "TRUE", FalseValue: "FALSE", Frozen: true, DisplayIndex: 0 } && (string)preparedTable.Rows[0]["Locked"] == "FALSE");
                Check("Excel mode " + selectedMode + " preserves result tab caption", tabs.TabPages[3].Text == (selectedMode == 3 ? "٤. المقارنة قبل وبعد الإصلاح" : "٤. الجدول المكتمل"));
            }
            // Editing the map exercises the actual migrated CellValueChanged wiring.
            mapping.Rows[0].Cells[1].Value = selectedMode == 0 ? "BranchName" : "Subject";
            mapping.Rows[1].Cells[1].Value = selectedMode == 0 ? "BranchName" : "Subject";
            bool duplicateRejected = false;
            try { Call("PrepareSheets"); }
            catch (TargetInvocationException exception) when (exception.InnerException is ArgumentException) { duplicateRejected = true; }
            Check("Excel mode " + selectedMode + " rejects duplicate target mappings", duplicateRejected);
        }
    }

    private static JsonObject Snapshot(Form form)
    {
        var fields = form.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
            .Where(field => typeof(Control).IsAssignableFrom(field.FieldType)).ToArray();
        var controls = new JsonArray();
        void Visit(Control control, string path)
        {
            var p = new JsonObject
            {
                ["type"] = control.GetType().FullName, ["text"] = control.Text, ["bounds"] = control.Bounds.ToString(),
                ["minimumSize"] = control.MinimumSize.ToString(), ["maximumSize"] = control.MaximumSize.ToString(),
                ["dock"] = control.Dock.ToString(), ["anchor"] = control.Anchor.ToString(), ["padding"] = control.Padding.ToString(),
                ["margin"] = control.Margin.ToString(), ["autoSize"] = control.AutoSize, ["enabled"] = control.Enabled,
                ["ownVisible"] = OwnVisible(control), ["rightToLeft"] = control.RightToLeft.ToString(),
                ["font"] = $"{control.Font.Name}|{control.Font.Size}|{control.Font.Style}|{control.Font.Unit}",
                ["backColor"] = control.BackColor.ToArgb(), ["foreColor"] = control.ForeColor.ToArgb(),
                ["tabIndex"] = control.TabIndex, ["tabStop"] = control.TabStop
            };
            foreach (string property in new[] { "AutoSizeMode", "AutoScroll", "WrapContents", "FlowDirection", "BorderStyle", "TextAlign", "AutoEllipsis", "ReadOnly", "Multiline", "ScrollBars", "DropDownStyle", "AutoCompleteMode", "AutoCompleteSource", "DisplayMember", "ValueMember", "FormattingEnabled", "RightToLeftLayout", "FormBorderStyle", "StartPosition", "AutoScaleMode", "FillColor", "BorderColor", "BorderRadius", "BorderThickness", "ImageSize", "ImageAlign", "TextOffset", "CheckOnClick", "IntegralHeight", "SelectionMode", "RowHeadersVisible", "AllowUserToAddRows", "AllowUserToDeleteRows", "AutoGenerateColumns", "AutoSizeColumnsMode", "AutoSizeRowsMode", "ColumnHeadersHeight", "ColumnHeadersHeightSizeMode", "EnableHeadersVisualStyles", "MultiSelect", "Checked", "Minimum", "Maximum", "Value", "DecimalPlaces", "Increment", "Style" })
            {
                var info = MostDerivedProperty(control.GetType(), property);
                if (info is null || !info.CanRead || info.GetIndexParameters().Length != 0) continue;
                var value = info.GetValue(control);
                p[property] = value is Color color ? color.ToArgb().ToString() : value?.ToString();
            }
            if (control is Form f) p["clientSize"] = f.ClientSize.ToString();
            if (control is TableLayoutPanel table)
            {
                p["columnCount"] = table.ColumnCount; p["rowCount"] = table.RowCount;
                p["columnStyles"] = JsonSerializer.SerializeToNode(table.ColumnStyles.Cast<ColumnStyle>().Select(s => new { type = s.SizeType.ToString(), size = s.Width }));
                p["rowStyles"] = JsonSerializer.SerializeToNode(table.RowStyles.Cast<RowStyle>().Select(s => new { type = s.SizeType.ToString(), size = s.Height }));
            }
            if (control.Parent is TableLayoutPanel parent)
            { p["cell"] = parent.GetPositionFromControl(control).ToString(); p["assignedCell"] = $"{parent.GetColumn(control)},{parent.GetRow(control)}"; p["columnSpan"] = parent.GetColumnSpan(control); p["rowSpan"] = parent.GetRowSpan(control); }
            if (control is ComboBox combo) { p["items"] = JsonSerializer.SerializeToNode(combo.Items.Cast<object>().Select(ItemText)); p["selectedIndex"] = combo.SelectedIndex; }
            if (control is ListBox list) p["items"] = JsonSerializer.SerializeToNode(list.Items.Cast<object>().Select(ItemText));
            if (control is TabControl tabs) { p["selectedIndex"] = tabs.SelectedIndex; p["tabPadding"] = tabs.Padding.ToString(); }
            if (control is DataGridView grid)
            {
                p["defaultCellStyle"] = JsonSerializer.SerializeToNode(Style(grid.DefaultCellStyle));
                p["columnHeadersDefaultCellStyle"] = JsonSerializer.SerializeToNode(Style(grid.ColumnHeadersDefaultCellStyle));
                p["alternatingRowsDefaultCellStyle"] = JsonSerializer.SerializeToNode(Style(grid.AlternatingRowsDefaultCellStyle));
                p["rowTemplateHeight"] = grid.RowTemplate.Height;
                p["columns"] = JsonSerializer.SerializeToNode(grid.Columns.Cast<DataGridViewColumn>().Select(c => new
                {
                    type = c.GetType().Name, c.Name, c.HeaderText, c.DataPropertyName, c.Width, c.MinimumWidth,
                    c.FillWeight, c.ReadOnly, c.Visible, autoSizeMode = c.AutoSizeMode.ToString(), sortMode = c.SortMode.ToString(),
                    defaultCellStyle = Style(c.DefaultCellStyle)
                }));
            }
            controls.Add(new JsonObject { ["path"] = path, ["name"] = control.Name,
                ["fields"] = JsonSerializer.SerializeToNode(fields.Where(field => ReferenceEquals(field.GetValue(form), control)).Select(field => field.Name)),
                ["properties"] = p, ["applicationHandlers"] = JsonSerializer.SerializeToNode(ApplicationHandlers(control)) });
            for (int index = 0; index < control.Controls.Count; index++) Visit(control.Controls[index], path + "/" + index);
        }
        Visit(form, "0");
        return new JsonObject { ["form"] = form.GetType().Name, ["controls"] = controls };
    }

    private static object Style(DataGridViewCellStyle style) => new
    {
        backColor = style.BackColor.ToArgb(), foreColor = style.ForeColor.ToArgb(), selectionBackColor = style.SelectionBackColor.ToArgb(),
        selectionForeColor = style.SelectionForeColor.ToArgb(), font = style.Font is null ? null : $"{style.Font.Name}|{style.Font.Size}|{style.Font.Style}|{style.Font.Unit}",
        alignment = style.Alignment.ToString(), wrapMode = style.WrapMode.ToString(), padding = style.Padding.ToString(), style.Format
    };
    private static PropertyInfo? MostDerivedProperty(Type type, string name)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
            if (current.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly) is { } property) return property;
        return null;
    }
    private static bool OwnVisible(Control control)
    {
        var method = typeof(Control).GetMethod("GetState", BindingFlags.NonPublic | BindingFlags.Instance);
        if (method is null) return control.Visible;
        var state = Enum.Parse(method.GetParameters()[0].ParameterType, "Visible");
        return (bool)method.Invoke(control, [state])!;
    }
    private static string ItemText(object item)
    {
        var type = item.GetType();
        return type.GetProperty("Text")?.GetValue(item)?.ToString() ?? type.GetProperty("Label")?.GetValue(item)?.ToString() ?? item.ToString() ?? "";
    }
    private static IEnumerable<string> ApplicationHandlers(Control control)
    {
        var events = (EventHandlerList)typeof(Component).GetProperty("Events", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(control)!;
        for (Type? type = control.GetType(); type is not null; type = type.BaseType)
            foreach (var key in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Where(f => f.FieldType == typeof(object)))
            {
                if (key.GetValue(null) is not { } value || events[value] is not Delegate handler) continue;
                foreach (var item in handler.GetInvocationList().Where(d => d.Method.DeclaringType?.FullName?.StartsWith(Ns, StringComparison.Ordinal) == true))
                    yield return key.Name;
            }
    }
    private static IEnumerable<Control> Descendants(Control control) => control.Controls.Cast<Control>().SelectMany(c => Descendants(c).Prepend(c));
    private static void Compare(JsonNode? expected, JsonNode? actual, string path, List<string> differences, bool design, string formName)
    {
        if (expected is JsonArray ea && actual is JsonArray aa)
        {
            if (ea.Count != aa.Count) differences.Add(path + $" count {ea.Count} -> {aa.Count}");
            for (int i = 0; i < Math.Min(ea.Count, aa.Count); i++) Compare(ea[i], aa[i], path + "[" + i + "]", differences, design, formName);
        }
        else if (expected is JsonObject eo && actual is JsonObject ao)
        {
            if (formName == "ExcelImportForm" && ao["name"]?.GetValue<string>() is "flow" or "flow2" or "entityField" &&
                eo["properties"]?["type"]?.GetValue<string>() == typeof(FlowLayoutPanel).FullName &&
                eo["properties"]?["minimumSize"]?.GetValue<string>() == "{Width=0, Height=0}" &&
                ao["properties"]?["minimumSize"]?.GetValue<string>() == "{Width=0, Height=38}")
            {
                ao = (JsonObject)ao.DeepClone();
                ao["properties"]!["minimumSize"] = "{Width=0, Height=0}";
            }
            if (formName == "FacultyScheduleEditorForm" && ao["name"]?.GetValue<string>() is "flow" or "flow2" or "flow3" &&
                eo["properties"]?["type"]?.GetValue<string>() == typeof(FlowLayoutPanel).FullName &&
                eo["properties"]?["minimumSize"]?.GetValue<string>() == "{Width=0, Height=0}" &&
                ao["properties"]?["minimumSize"]?.GetValue<string>() == "{Width=0, Height=34}")
            {
                ao = (JsonObject)ao.DeepClone();
                ao["properties"]!["minimumSize"] = "{Width=0, Height=0}";
            }
            if (formName == "FacultyPreferencesForm" && ao["name"]?.GetValue<string>() is "flow" or "flow2" &&
                eo["properties"]?["type"]?.GetValue<string>() == typeof(FlowLayoutPanel).FullName &&
                eo["properties"]?["minimumSize"]?.GetValue<string>() == "{Width=0, Height=0}" &&
                ao["properties"]?["minimumSize"]?.GetValue<string>() == "{Width=0, Height=58}")
            {
                ao = (JsonObject)ao.DeepClone();
                ao["properties"]!["minimumSize"] = "{Width=0, Height=0}";
            }
            // The designer deliberately omits the runtime Format event. Its initial
            // entity item is the same canonical StudyYears value, so only this
            // one derived display string is normalized for appearance comparison.
            if (design && eo["fields"] is JsonArray fieldNames && fieldNames.Any(f => f?.GetValue<string>() == "entity") &&
                eo["properties"]?["type"]?.GetValue<string>() == typeof(ComboBox).FullName &&
                eo["properties"]?["text"]?.GetValue<string>() == "المراحل الدراسية" &&
                ao["properties"]?["text"]?.GetValue<string>() == "StudyYears" && ao["properties"]?["selectedIndex"]?.GetValue<int>() == 0)
            {
                ao = (JsonObject)ao.DeepClone();
                ao["properties"]!["text"] = "المراحل الدراسية";
            }
            foreach (var pair in eo)
            {
                // Control names are added for Designer support; anonymous old layout controls
                // also become fields. Event method names may change when lambdas are extracted.
                if (pair.Key is "fields" or "applicationHandlers") continue;
                if (pair.Key == "name" && string.IsNullOrEmpty(pair.Value?.GetValue<string>())) continue;
                Compare(pair.Value, ao[pair.Key], path + "." + pair.Key, differences, design, formName);
            }
        }
        else if (!JsonNode.DeepEquals(expected, actual)) differences.Add(path + ": " + expected?.ToJsonString() + " -> " + actual?.ToJsonString());
    }
    private static void Check(string name, bool passed, string? detail = null)
    {
        Checks.Add(new { name, passed, detail }); if (!passed) failures++;
        Console.WriteLine((passed ? "PASS " : "FAIL ") + name + (detail is null ? "" : ": " + detail));
    }
    private sealed class DesignContext : LicenseContext { public override LicenseUsageMode UsageMode => LicenseUsageMode.Designtime; }
}
