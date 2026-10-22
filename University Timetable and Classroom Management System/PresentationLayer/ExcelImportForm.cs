using System.Data;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;

namespace University_Timetable_and_Classroom_Management_System;

public sealed class ExcelImportForm : Form
{
    public bool HasSavedChanges { get; private set; }
    private readonly ExcelImportService service = new();
    private readonly ComboBox mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
    private readonly ComboBox sheet = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly ComboBox entity = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
    private readonly NumericUpDown semester = new() { Minimum = 1, Maximum = 2, Value = 1, Width = 55 };
    private readonly NumericUpDown repairTimeLimit = new() { Minimum = 1, Maximum = 120, Value = 20, Width = 60 };
    private readonly ComboBox studySession = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly CheckBox allSheets = new() { Text = "استيراد جميع الجداول الأكاديمية", AutoSize = true, Checked = true };
    private readonly CheckBox updates = new() { Text = "السماح بتحديث السجلات الموجودة", AutoSize = true };
    private readonly DataGridView data = Grid(false);
    private readonly DataGridView mapping = Grid(false);
    private readonly DataGridView report = Grid(true);
    private readonly DataGridView result = Grid(true);
    private readonly Label status = new() { Text = "افتح ملف Excel أو PDF، ثم راجع البيانات وتحقق منها قبل الحفظ.", AutoSize = true, Dock = DockStyle.Fill };
    private readonly Label fileSummary = new() { Text = "لم يُحدد ملف بعد", AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139), Margin = new(6, 8, 6, 8) };
    private readonly ProgressBar progress = new() { Dock = DockStyle.Fill, Style = ProgressBarStyle.Marquee, Visible = false };
    private readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    private readonly Guna.UI2.WinForms.Guna2Button validate = Button("التحقق من البيانات");
    private readonly Guna.UI2.WinForms.Guna2Button generate = Button("توليد المحاضرات المتبقية");
    private readonly Guna.UI2.WinForms.Guna2Button save = Button("تأكيد الاستيراد…");
    private readonly List<Control> inputs = [];
    private Control entityField = null!, semesterField = null!, repairTimeField = null!, studySessionField = null!;
    private readonly Dictionary<string, Dictionary<string, string>> maps = [];
    private ExcelWorkbookData? workbook;
    private ExcelImportPreview? preview;
    private bool busy, binding;
    private CancellationTokenSource? cancellation;
    private ExcelImportMode Mode => (ExcelImportMode)Math.Max(0, mode.SelectedIndex);
    private ExcelSheetData? SelectedSheet => workbook?.Sheets.FirstOrDefault(s => s.Name == sheet.SelectedItem?.ToString());

    public ExcelImportForm()
    {
        Text = "استيراد Excel وPDF والجدولة الهجينة";
        Name = "ExcelImportForm";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new(800, 600);
        Size = new(1180, 800);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new("Segoe UI", 10);
        BackColor = Color.FromArgb(245, 247, 250);
        ForeColor = Color.FromArgb(15, 23, 42);
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new(16) };
        root.ColumnStyles.Add(new(SizeType.Percent, 100));
        root.RowStyles.Add(new(SizeType.AutoSize));
        root.RowStyles.Add(new(SizeType.AutoSize));
        root.RowStyles.Add(new(SizeType.Percent, 100));
        root.RowStyles.Add(new(SizeType.Absolute, 12));
        root.RowStyles.Add(new(SizeType.Absolute, 100));
        Controls.Add(root);
        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, Padding = new(0, 0, 0, 12) };
        heading.Controls.Add(new Label { Text = "استيراد البيانات والجداول", Font = new("Segoe UI", 19, FontStyle.Bold), AutoSize = true, Margin = new(0, 0, 0, 4) });
        heading.Controls.Add(new Label { Text = "Excel وPDF  •  معاينة البيانات ← ربط الأعمدة ← التحقق ← الحفظ", AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) });
        root.Controls.Add(heading, 0, 0);
        var toolbar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true, Padding = new(8), BackColor = Color.White };
        var open = Button("فتح ملف Excel أو PDF…");
        open.Name = "openImportFile";
        var template = Button("تنزيل قالب Excel…");
        template.Name = "saveImportTemplate";
        mode.Items.AddRange(["البيانات الأكاديمية", "جدول دراسي جاهز", "الجدولة الهجينة", "إصلاح الجدول المستورد"]);
        mode.SelectedIndex = 0;
        entity.FormattingEnabled = sheet.FormattingEnabled = true;
        entity.Format += (_, e) => e.Value = ImportLabels.Display(e.ListItem?.ToString() ?? "");
        sheet.Format += (_, e) => e.Value = ImportLabels.Display(e.ListItem?.ToString() ?? "");
        entity.Items.AddRange(ExcelImportSchema.Academic.Select(x => (object)x.Name).ToArray());
        entity.SelectedIndex = 0;
        entityField = Field("نوع البيانات", entity);
        semesterField = Field("الفصل الدراسي", semester);
        repairTimeField = Field("مهلة الإصلاح / ثانية", repairTimeLimit);
        studySession.Items.AddRange(["كل الدراسات", "صباحي", "مسائي"]); studySession.SelectedIndex = 0;
        studySessionField = Field("نطاق الدراسة", studySession);
        toolbar.Controls.AddRange([open, template, Field("نوع الاستيراد", mode), Field("الورقة / الصفحة", sheet),
            entityField, allSheets, semesterField, repairTimeField, studySessionField, updates, fileSummary]);
        inputs.AddRange(toolbar.Controls.Cast<Control>().Where(c => c is not Label));
        root.Controls.Add(toolbar, 0, 1);
        AddTab("١. معاينة البيانات وتعديلها", data);
        AddTab("٢. ربط الأعمدة", mapping);
        AddTab("٣. التحقق والتغييرات", report);
        AddTab("٤. الجدول المكتمل", result);
        tabs.Font = new("Segoe UI", 10, FontStyle.Bold);
        tabs.Padding = new(12, 8);
        tabs.RightToLeftLayout = true;
        root.Controls.Add(tabs, 0, 2);
        root.Controls.Add(progress, 0, 3);
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        footer.ColumnStyles.Add(new(SizeType.Percent, 100));
        footer.RowStyles.Add(new(SizeType.Absolute, 44));
        footer.RowStyles.Add(new(SizeType.Percent, 100));
        status.AutoSize = false;
        status.AutoEllipsis = true;
        footer.Controls.Add(status, 0, 0);
        var actions = new FlowLayoutPanel { WrapContents = true, Dock = DockStyle.Fill, MinimumSize = new(0, 44) };
        var cancel = Button("إلغاء العملية");
        validate.Name = "validateImport"; generate.Name = "generateRemaining"; save.Name = "confirmImport";
        save.FillColor = Color.FromArgb(22, 163, 74);
        cancel.FillColor = Color.FromArgb(100, 116, 139);
        actions.Controls.AddRange([validate, generate, save, cancel]);
        footer.Controls.Add(actions, 0, 1);
        root.Controls.Add(footer, 0, 4);
        mapping.Columns.Add(new DataGridViewTextBoxColumn { Name = "Source", HeaderText = "عمود الملف", ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        mapping.Columns.Add(new DataGridViewComboBoxColumn { Name = "Target", HeaderText = "الحقل المقابل في البرنامج", FlatStyle = FlatStyle.Flat, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, DisplayMember = "Label", ValueMember = "Value" });
        foreach (var grid in new[] { data, report, result })
        {
            grid.DataBindingComplete += (_, _) => TranslateHeaders(grid);
            grid.CellFormatting += (_, e) =>
            {
                if (e.Value is string value && (grid != data || grid.Columns[e.ColumnIndex].DataPropertyName is "Day" or "LectureType"))
                { e.Value = ImportLabels.Display(value); e.FormattingApplied = true; }
            };
        }
        open.Click += async (_, _) => await OpenAsync();
        template.Click += async (_, _) => await TemplateAsync();
        validate.Click += async (_, _) => await RunAsync(ValidateWorkbookAsync);
        generate.Click += async (_, _) => await RunAsync(async token =>
        {
            if (preview is null) return;
            var validated = preview;
            var selectedMode = Mode;
            double seconds = (double)repairTimeLimit.Value;
            preview = await Task.Run(() => selectedMode == ExcelImportMode.RepairTimetable
                ? service.RepairTimetableAsync(validated, seconds, token) : service.GenerateRemainingAsync(validated, token), token);
            ShowPreview();
            tabs.SelectedIndex = preview.IsValid ? 3 : 2;
        });
        save.Click += async (_, _) =>
        {
            if (preview is null) return;
            if (MessageBox.Show(this,
                $"هل تريد حفظ البيانات التي تم التحقق منها؟\n\nإضافات: {preview.Changes.Count(c => c.Action == "Add")}\nتحديثات أو تثبيت: {preview.Changes.Count(c => c.Action is "Update" or "Lock existing")}\nدون تغيير: {preview.Changes.Count(c => c.Action == "Unchanged")}\nالمحاضرات المعاد توزيعها: {preview.PlacementChanges.Count(c => c.Changed)}\n\nتُحفظ جميع التغييرات معاً.",
                "تأكيد الاستيراد", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            await RunAsync(async token =>
            {
                var validated = preview;
                var reporter = new Progress<string>(_ => status.Text = "جارٍ حفظ البيانات التي تم التحقق منها…");
                var saved = await Task.Run(() => service.SaveAsync(validated, true, reporter, token), token);
                HasSavedChanges |= saved.Added + saved.Updated > 0;
                preview = null;
                status.Text = $"اكتمل الاستيراد: {saved.Added} إضافة، {saved.Updated} تحديث، {saved.Unchanged} دون تغيير.";
                MessageBox.Show(this, status.Text, "استيراد البيانات", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        };
        cancel.Click += (_, _) => cancellation?.Cancel();
        mode.SelectedIndexChanged += (_, _) => { InvalidatePreview(); maps.Clear(); UpdateMode(); BindSheet(); };
        sheet.SelectedIndexChanged += (_, _) => { InvalidatePreview(); BindSheet(); };
        entity.SelectedIndexChanged += (_, _) => { if (!binding) { InvalidatePreview(); BindMapping(); } };
        allSheets.CheckedChanged += (_, _) => { InvalidatePreview(); UpdateMode(); };
        updates.CheckedChanged += (_, _) => InvalidatePreview();
        semester.ValueChanged += (_, _) => InvalidatePreview();
        repairTimeLimit.ValueChanged += (_, _) => InvalidatePreview();
        studySession.SelectedIndexChanged += (_, _) => { if (!binding) { InvalidatePreview(); BindWorkbookSheets(); } };
        data.CellValueChanged += (_, _) => InvalidatePreview();
        data.CurrentCellDirtyStateChanged += (_, _) => { if (data.IsCurrentCellDirty) data.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        mapping.CurrentCellDirtyStateChanged += (_, _) => { if (mapping.IsCurrentCellDirty) mapping.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        mapping.CellValueChanged += (_, e) =>
        {
            if (binding || e.RowIndex < 0 || SelectedSheet is null) return;
            var row = mapping.Rows[e.RowIndex];
            maps[SelectedSheet.Name][(string)row.Cells[0].Value] = Convert.ToString(row.Cells[1].Value) ?? "";
            InvalidatePreview();
        };
        data.DataError += (_, e) => { e.ThrowException = false; status.Text = "صحّح قيمة الخلية غير الصالحة قبل التحقق."; };
        FormClosing += (_, e) =>
        {
            if (busy) { e.Cancel = true; cancellation?.Cancel(); status.Text = "جارٍ إيقاف العملية الحالية…"; }
        };
        UpdateMode(); UpdateActions();
    }
    private static Control Field(string text, Control input)
    {
        var field = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new(3), Padding = new(0, 4, 0, 0) };
        field.Controls.Add(new Label { Text = text, AutoSize = true, Margin = new(4, 5, 2, 0) });
        field.Controls.Add(input);
        return field;
    }
    private static Guna.UI2.WinForms.Guna2Button Button(string text)
    {
        var font = new Font("Segoe UI", 10, FontStyle.Bold);
        return new()
        {
            Text = text, Size = new(Math.Max(112, TextRenderer.MeasureText(text, font).Width + 32), 40), Margin = new(4),
            BorderRadius = 6, FillColor = Color.FromArgb(37, 99, 235), ForeColor = Color.White,
            Font = font, Cursor = Cursors.Hand,
            DisabledState = { FillColor = Color.FromArgb(226, 232, 240), ForeColor = Color.FromArgb(100, 116, 139) }
        };
    }
    private static DataGridView Grid(bool readOnly) => new()
    {
        Dock = DockStyle.Fill, ReadOnly = readOnly, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
        BackgroundColor = Color.White, BorderStyle = BorderStyle.None, RowHeadersVisible = true,
        AutoGenerateColumns = true, SelectionMode = DataGridViewSelectionMode.CellSelect,
        EnableHeadersVisualStyles = false,
        ColumnHeadersHeight = 42, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
        ColumnHeadersDefaultCellStyle = new() { BackColor = Color.FromArgb(15, 23, 42), ForeColor = Color.White, Font = new("Segoe UI", 10, FontStyle.Bold) },
        DefaultCellStyle = new() { Font = new("Segoe UI", 10), ForeColor = Color.FromArgb(15, 23, 42), SelectionBackColor = Color.FromArgb(37, 99, 235), SelectionForeColor = Color.White },
        AlternatingRowsDefaultCellStyle = new() { BackColor = Color.FromArgb(241, 245, 249) },
        GridColor = Color.FromArgb(226, 232, 240), RowTemplate = { Height = 34 }, RightToLeft = RightToLeft.Yes
    };
    private void AddTab(string title, Control control)
    {
        var page = new TabPage(title) { Padding = new(8), BackColor = Color.White };
        page.Controls.Add(control); tabs.TabPages.Add(page);
    }
    private void UpdateMode()
    {
        entity.Enabled = Mode == ExcelImportMode.AcademicData && !allSheets.Checked && !busy;
        updates.Visible = entity.Visible = Mode == ExcelImportMode.AcademicData;
        allSheets.Visible = Mode is ExcelImportMode.AcademicData or ExcelImportMode.RepairTimetable;
        allSheets.Text = Mode == ExcelImportMode.RepairTimetable ? "إصلاح جميع الأوراق / الصفحات" : "استيراد جميع الجداول الأكاديمية";
        entityField.Visible = Mode == ExcelImportMode.AcademicData;
        semesterField.Visible = Mode != ExcelImportMode.AcademicData;
        semester.Enabled = Mode != ExcelImportMode.AcademicData && !busy;
        repairTimeField.Visible = Mode == ExcelImportMode.RepairTimetable;
        studySessionField.Visible = Mode != ExcelImportMode.AcademicData && workbook?.Sheets.Any(s => s.IsWeeklyTimetable) == true;
        generate.Visible = Mode is ExcelImportMode.Hybrid or ExcelImportMode.RepairTimetable;
        generate.Text = Mode == ExcelImportMode.RepairTimetable ? "إصلاح الجدول المستورد" : "توليد المحاضرات المتبقية";
        tabs.TabPages[3].Text = Mode == ExcelImportMode.RepairTimetable ? "٤. المقارنة قبل وبعد الإصلاح" : "٤. الجدول المكتمل";
        UpdateActions();
    }
    private void InvalidatePreview()
    {
        if (binding || busy) return;
        preview = null; report.DataSource = null; result.DataSource = null;
        status.Text = Mode == ExcelImportMode.RepairTimetable
            ? "حدّد المحاضرات المثبتة في المعاينة، ثم تحقق واضغط إصلاح الجدول المستورد. المحاضرات الأخرى تسمح بتغيير اليوم والفترة والقاعة."
            : "تغيّرت البيانات أو الخيارات. أعد التحقق قبل الحفظ.";
        UpdateActions();
    }
    private void UpdateActions()
    {
        validate.Enabled = !busy && workbook is not null;
        generate.Enabled = !busy && preview?.IsValid == true && !preview.GenerationCompleted;
        save.Enabled = !busy && preview?.IsValid == true && (Mode is not (ExcelImportMode.Hybrid or ExcelImportMode.RepairTimetable) || preview.GenerationCompleted);
    }
    private async Task RunAsync(Func<CancellationToken, Task> operation)
    {
        if (busy) return;
        busy = true; cancellation = new();
        foreach (var input in inputs) input.Enabled = false;
        data.ReadOnly = mapping.ReadOnly = true;
        progress.Visible = true; UpdateActions();
        status.Text = "جارٍ تنفيذ العملية…";
        try { await operation(cancellation.Token); }
        catch (OperationCanceledException) { preview = null; status.Text = "أُلغيت العملية دون حفظ تغييرات الاستيراد."; }
        catch (Exception ex)
        {
            preview = null; status.Text = "تعذّر إكمال العملية: " + ImportUiText.Message(ex.Message);
            MessageBox.Show(this, ImportUiText.Message(ex.Message), "استيراد البيانات", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            busy = false; cancellation.Dispose(); cancellation = null;
            foreach (var input in inputs) input.Enabled = true;
            data.ReadOnly = mapping.ReadOnly = false;
            progress.Visible = false; UpdateMode(); UpdateActions();
        }
    }
    private async Task OpenAsync()
    {
        using var dialog = new OpenFileDialog { Title = "اختر ملف Excel أو PDF للاستيراد", Filter = "الملفات المدعومة (*.xlsx;*.pdf)|*.xlsx;*.pdf|ملفات Excel (*.xlsx)|*.xlsx|ملفات PDF (*.pdf)|*.pdf", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        await RunAsync(async token =>
        {
            var loaded = await Task.Run(() => ImportFileReader.Read(dialog.FileName, token), token);
            LoadWorkbook(loaded, dialog.FileName);
        });
    }
    private void LoadWorkbook(ExcelWorkbookData loaded, string path)
    {
        workbook = loaded; preview = null; maps.Clear();
        report.DataSource = result.DataSource = null;
        if (Mode == ExcelImportMode.AcademicData && loaded.Sheets.All(s => s.SuggestedSchemaName == "Timetable"))
            mode.SelectedIndex = loaded.Sheets.Any(s => s.IsWeeklyTimetable) ? 3 : 1;
        if (Mode == ExcelImportMode.AcademicData && loaded.Sheets.Any(s => s.SuggestedSchemaName is null && ImportLabels.MatchSheet(s.Name) is null)) allSheets.Checked = false;
        binding = true; studySession.SelectedIndex = 0; binding = false;
        BindWorkbookSheets();
        fileSummary.Text = $"{Path.GetFileName(path)}  •  {loaded.Sheets.Count} جدول  •  {loaded.Sheets.Sum(s => s.Table.Rows.Count)} صف";
        status.Text = loaded.Sheets.Any(s => s.IsWeeklyTimetable)
            ? "تم تحويل الجدول الأسبوعي إلى محاضرات. اختر نطاق الدراسة، وراجع أسماء التدريسيين والقاعات وأوقات الانتهاء والملاحظات قبل التحقق."
            : "تمت قراءة الملف. راجع البيانات واربط الأعمدة عند الحاجة، ثم اضغط التحقق من البيانات.";
        UpdateMode();
        UpdateActions();
    }
    private void BindWorkbookSheets()
    {
        if (workbook is null) return;
        binding = true;
        try
        {
            var visible = workbook.Sheets.Where(s => studySession.SelectedIndex <= 0 || s.StudySession.Length == 0 || s.StudySession == studySession.SelectedItem?.ToString()).ToList();
            sheet.Items.Clear(); sheet.Items.AddRange(visible.Select(s => (object)s.Name).ToArray());
            if (sheet.Items.Count > 0) sheet.SelectedIndex = 0;
            else { data.DataSource = null; mapping.Rows.Clear(); }
        }
        finally { binding = false; }
        BindSheet();
    }
    private void BindSheet()
    {
        if (binding || SelectedSheet is not { } selected) return;
        binding = true;
        try
        {
            var lockColumn = selected.Table.Columns.Cast<DataColumn>().FirstOrDefault(c => ImportLabels.MatchColumn(c.ColumnName, ["Locked"]) is not null);
            if (lockColumn is not null && !selected.Table.Columns.Contains("Locked")) lockColumn.ColumnName = "Locked";
            if (Mode is ExcelImportMode.Hybrid or ExcelImportMode.RepairTimetable && !selected.Table.Columns.Contains("Locked"))
            {
                selected.Table.Columns.Add("Locked", typeof(string));
                foreach (DataRow row in selected.Table.Rows) row["Locked"] = "FALSE";
            }
            data.DataSource = null;
            data.Columns.Clear();
            data.DataSource = selected.Table;
            if (Mode is ExcelImportMode.Hybrid or ExcelImportMode.RepairTimetable && data.Columns.Contains("Locked"))
            {
                foreach (DataRow row in selected.Table.Rows)
                {
                    try { row["Locked"] = new ImportValuesForUi(row).LockText(false); }
                    catch (ArgumentException) { /* Leave invalid text for row validation. */ }
                }
                int index = data.Columns["Locked"].Index;
                data.Columns.RemoveAt(index);
                data.Columns.Insert(index, new DataGridViewCheckBoxColumn
                {
                    Name = "Locked", HeaderText = "تثبيت المحاضرة", DataPropertyName = "Locked",
                    TrueValue = "TRUE", FalseValue = "FALSE", Width = 70
                });
                data.Columns["Locked"].DisplayIndex = 0;
                data.Columns["Locked"].Frozen = true;
            }
            data.AllowUserToOrderColumns = true;
            data.AllowUserToResizeColumns = true;
            if (selected.IsWeeklyTimetable)
            {
                int displayIndex = 0;
                foreach (string name in new[] { "Locked", "Day", "TimeSlot", "EndTime", "Subject", "Faculty", "Classroom", "LectureType", "Section", "GroupName", "StudyYear", "Branch", "StudySession", "ImportNote", "SourceText", "SemesterNumber", "ScheduleID" })
                    if (data.Columns.Contains(name)) data.Columns[name].DisplayIndex = displayIndex++;
                foreach (DataGridViewRow row in data.Rows)
                    if (row.DataBoundItem is DataRowView source)
                        row.Cells["Subject"].ToolTipText = Convert.ToString(source.Row["SourceText"]);
            }
            data.RowPostPaint -= PaintSourceRow;
            data.RowPostPaint += PaintSourceRow;
            var known = ExcelImportSchema.Academic.FirstOrDefault(s => s.Name.Equals(selected.SuggestedSchemaName ?? ImportLabels.MatchSheet(selected.Name), StringComparison.OrdinalIgnoreCase));
            if (known is not null) entity.SelectedItem = known.Name;
            TranslateHeaders(data);
        }
        finally { binding = false; }
        BindMapping();
    }
    private void PaintSourceRow(object? sender, DataGridViewRowPostPaintEventArgs e)
    {
        if (SelectedSheet is not { } selected || e.RowIndex >= selected.SourceRows.Count) return;
        int sourceIndex = data.Rows[e.RowIndex].DataBoundItem is DataRowView view ? selected.Table.Rows.IndexOf(view.Row) : e.RowIndex;
        TextRenderer.DrawText(e.Graphics, selected.SourceRows[sourceIndex].ToString(), data.Font,
            new Rectangle(e.RowBounds.Right - data.RowHeadersWidth, e.RowBounds.Top, data.RowHeadersWidth - 4, e.RowBounds.Height), Color.DimGray,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
    }
    private void BindMapping()
    {
        if (SelectedSheet is not { } selected) return;
        binding = true;
        try
        {
            mapping.Rows.Clear();
            var schema = Mode == ExcelImportMode.AcademicData
                ? ExcelImportSchema.Academic.First(s => s.Name == entity.SelectedItem?.ToString()) : ExcelImportSchema.Timetable;
            var target = (DataGridViewComboBoxColumn)mapping.Columns[1];
            var choices = new[] { new MappingChoice("", "— تجاهل العمود —") }.Concat(schema.Columns.Select(c => new MappingChoice(c, ImportLabels.Display(c)))).ToList();
            target.DataSource = choices;
            if (!maps.TryGetValue(selected.Name, out var saved)) maps[selected.Name] = saved = [];
            foreach (DataColumn column in selected.Table.Columns)
            {
                string? match = ImportLabels.MatchColumn(column.ColumnName, schema.Columns);
                if (!saved.TryGetValue(column.ColumnName, out string? mapped) || !choices.Any(c => c.Value == mapped)) saved[column.ColumnName] = mapped = match ?? "";
                mapping.Rows.Add(column.ColumnName, mapped);
            }
        }
        finally { binding = false; }
    }
    private List<ExcelSheetData> PrepareSheets()
    {
        data.EndEdit(); mapping.EndEdit();
        if (workbook is null || SelectedSheet is null) return [];
        IEnumerable<ExcelSheetData> selected = Mode is ExcelImportMode.AcademicData or ExcelImportMode.RepairTimetable && allSheets.Checked ? workbook.Sheets : [SelectedSheet];
        if (Mode != ExcelImportMode.AcademicData && studySession.SelectedIndex > 0)
            selected = selected.Where(s => s.StudySession.Length == 0 || s.StudySession == studySession.SelectedItem?.ToString());
        return selected.Select(s =>
        {
            string schemaName = allSheets.Checked ? s.SuggestedSchemaName ?? ImportLabels.MatchSheet(s.Name) ?? s.Name : entity.SelectedItem!.ToString()!;
            var schema = Mode == ExcelImportMode.AcademicData ? ExcelImportSchema.Academic.FirstOrDefault(x => x.Name == schemaName) : ExcelImportSchema.Timetable;
            var autoMap = s.Table.Columns.Cast<DataColumn>().ToDictionary(c => c.ColumnName,
                c => schema is null ? c.ColumnName : ImportLabels.MatchColumn(c.ColumnName, schema.Columns) ?? "");
            var chosenMap = maps.TryGetValue(s.Name, out var map) ? map : autoMap;
            if (chosenMap.Values.Where(v => !string.IsNullOrEmpty(v)).GroupBy(v => v, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                throw new ArgumentException("لا يمكن ربط عمودين بالحقل نفسه. اختر حقلاً مختلفاً أو تجاهل أحد العمودين.");
            var mapped = s.MapColumns(chosenMap);
            return Mode == ExcelImportMode.AcademicData
                ? new ExcelSheetData(schemaName, mapped.Table, mapped.SourceRows) { SourceName = s.SourceName } : mapped;
        }).ToList();
    }
    private async Task ValidateWorkbookAsync(CancellationToken token)
    {
        var sheets = PrepareSheets();
        var selectedMode = Mode;
        bool allowUpdates = updates.Checked;
        int selectedSemester = (int)semester.Value;
        preview = await Task.Run(() => service.ValidateAsync(selectedMode, sheets, allowUpdates, selectedSemester, token), token);
        ShowPreview(); tabs.SelectedIndex = 2;
    }
    private void ShowPreview()
    {
        if (preview is null) return;
        var lines = preview.Issues.Select(i => new { i.Sheet, i.Row, Status = "Error", Detail = ImportLabels.Display(i.Column) + " " + ImportUiText.Message(i.Message) })
            .Concat(preview.RepairableConflicts.Select(i => new { i.Sheet, i.Row, Status = "Needs repair", Detail = ImportUiText.Message(i.Message) }))
            .Concat(preview.ReadingNotes.Select(i => new { i.Sheet, i.Row, Status = "Review source", Detail = i.Message }))
            .Concat(preview.Changes.Select(c => new { c.Sheet, c.Row, Status = c.Action, Detail = c.Key }))
            .Concat(preview.UnscheduledLectures.Select(x => new { Sheet = "Generated", Row = 0, Status = "Unscheduled", Detail = ImportUiText.Message(x) })).ToList();
        report.DataSource = lines;
        if (Mode == ExcelImportMode.RepairTimetable && preview.GenerationCompleted)
        {
            result.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            result.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            result.RowTemplate.Height = 60;
            result.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            result.RowHeadersVisible = false;
            result.DataSource = preview.PlacementChanges.Select(c => new
            {
                c.SubjectName, c.FacultyName, c.SectionName, c.GroupName, c.Locked,
                BeforePlacement = $"{ImportLabels.Display(c.BeforeDay)}، \u200e{c.BeforePeriod}\u200e\nالقاعة: {c.BeforeClassroom}",
                AfterPlacement = $"{ImportLabels.Display(c.AfterDay)}، \u200e{c.AfterPeriod}\u200e\nالقاعة: {c.AfterClassroom}", c.Changed
            }).ToList();
            foreach (DataGridViewColumn column in result.Columns)
            {
                int weight = column.Name is "BeforePlacement" or "AfterPlacement" ? 25 : column.Name is "SubjectName" or "FacultyName" ? 13 : 6;
                int minimum = column.Name is "BeforePlacement" or "AfterPlacement" ? 180 : column.Name is "SubjectName" or "FacultyName" ? 100 : 55;
                column.Width = Math.Max(minimum, Math.Max(700, result.ClientSize.Width - 4) * weight / 100);
                column.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            }
            result.Columns["SectionName"].HeaderText = "الشعبة";
            result.Columns["GroupName"].HeaderText = "المجموعة";
            result.Columns["Locked"].HeaderText = "مثبتة";
            result.Columns["Changed"].HeaderText = "تغيّر";
            foreach (DataGridViewRow row in result.Rows)
            {
                row.Height = 60;
                if (row.Cells["Changed"].Value is true) row.DefaultCellStyle.BackColor = Color.FromArgb(220, 252, 231);
            }
        }
        else
        {
            result.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            result.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            result.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            result.RowHeadersVisible = true;
            result.DataSource = preview.Lectures.Select(l => new
            {
                l.Locked, l.Sheet, l.Row, l.SubjectName, l.FacultyName, l.ClassroomName,
                l.Schedule.DayOfWeek, l.Period, l.SectionName, l.Schedule.LectureType, l.Schedule.GroupName, l.Schedule.SemesterNumber
            }).ToList();
        }
        if (Mode == ExcelImportMode.RepairTimetable)
        {
            status.Text = preview.GenerationCompleted
                ? $"اكتمل الإصلاح: {preview.Lectures.Count} محاضرة، {preview.PlacementChanges.Count(c => c.Changed)} تغييراً. راجع المقارنة قبل الحفظ."
                : $"الأخطاء المانعة: {preview.Issues.Count} • تعارضات قابلة للإصلاح: {preview.RepairableConflicts.Count}. ثبّت الصفوف المطلوبة ثم اضغط إصلاح الجدول المستورد.";
            return;
        }
        status.Text = $"الأخطاء: {preview.Issues.Count}  •  السجلات المراجعة: {preview.Changes.Count}. " +
            (Mode == ExcelImportMode.Hybrid ? $"المحاضرات المتبقية: {preview.RemainingLectureCount}. يمكنك تعديل تثبيت المحاضرات في المعاينة ثم إعادة التحقق." : "راجع التغييرات قبل تأكيد الاستيراد.");
    }
    private async Task TemplateAsync()
    {
        using var dialog = new SaveFileDialog { Title = "حفظ قالب الاستيراد", Filter = "ملفات Excel (*.xlsx)|*.xlsx", FileName = Mode + "-template.xlsx", AddExtension = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var selectedMode = Mode;
        await RunAsync(async token =>
        {
            await Task.Run(() => ExcelImportTemplates.Create(dialog.FileName, selectedMode), token);
            status.Text = "تم حفظ القالب: " + dialog.FileName;
        });
    }

    private sealed record MappingChoice(string Value, string Label);
    private static void TranslateHeaders(DataGridView grid)
    {
        bool comparison = grid.Columns.Contains("BeforePlacement");
        foreach (DataGridViewColumn column in grid.Columns)
            column.HeaderText = comparison ? column.Name switch
            {
                "SectionName" => "الشعبة", "GroupName" => "المجموعة", "Locked" => "مثبتة", "Changed" => "تغيّر",
                _ => ImportLabels.Display(column.Name)
            } : ImportLabels.Display(column.DataPropertyName.Length > 0 ? column.DataPropertyName : column.Name);
    }

    private sealed class ImportValuesForUi(DataRow row)
    {
        public string LockText(bool blankLocked) => ImportLabels.NormalizeReference(Convert.ToString(row["Locked"]) ?? "").ToLowerInvariant() switch
        {
            "" or null => blankLocked ? "TRUE" : "FALSE",
            "true" or "1" or "yes" or "نعم" => "TRUE",
            "false" or "0" or "no" or "لا" => "FALSE",
            _ => throw new ArgumentException("اختر نعم أو لا لتثبيت المحاضرة.")
        };
    }
}
