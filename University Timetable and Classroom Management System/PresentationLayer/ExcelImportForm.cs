using System.Data;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;

namespace University_Timetable_and_Classroom_Management_System;

public partial class ExcelImportForm : Form
{
    public bool HasSavedChanges { get; private set; }
    private readonly ExcelImportService service = null!;
    private readonly List<Control> inputs = [];
    private readonly Dictionary<string, Dictionary<string, string>> maps = [];
    private ExcelWorkbookData? workbook;
    private ExcelImportPreview? preview;
    private bool busy, binding;
    private CancellationTokenSource? cancellation;
    private ExcelImportMode Mode => ExcelImportMode.AcademicData;
    private ExcelSheetData? SelectedSheet => workbook?.Sheets.FirstOrDefault(s => s.Name == sheet.SelectedItem?.ToString());

    public ExcelImportForm()
    {
        InitializeComponent();
        mode.SelectedIndex = 0;
        entity.SelectedIndex = 0;
        studySession.SelectedIndex = 0;
        if (DesignerEnvironment.IsDesignTime(this)) return;

        service = new ExcelImportService();
        entity.Format += (_, e) => e.Value = ImportLabels.Display(e.ListItem?.ToString() ?? "");
        sheet.Format += (_, e) => e.Value = ImportLabels.Display(e.ListItem?.ToString() ?? "");
        entity.Items.Clear();
        entity.Items.AddRange(ExcelImportSchema.AcademicInputs.Select(x => (object)x.Name).ToArray());
        mode.SelectedIndex = 0;
        entity.SelectedIndex = 0;
        studySession.SelectedIndex = 0;
        inputs.AddRange(toolbar.Controls.Cast<Control>().Where(c => c is not Label));
        WireEvents();
        UpdateMode();
        UpdateActions();
    }

    private void WireEvents()
    {
        ClientSizeChanged += (_, _) => root.RowStyles[4].Height = ClientSize.Width < 1050 ? 148 : 100;
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
        skipProblems.Click += async (_, _) => await RunAsync(async token =>
        {
            var sheets = PrepareSheets(); bool allowUpdates = updates.Checked;
            preview = await Task.Run(() => service.ValidateSkippingInvalidAcademicRowsAsync(sheets, allowUpdates, token), token);
            ShowPreview(); tabs.SelectedIndex = 2;
        });
        save.Click += async (_, _) =>
        {
            if (preview is null) return;
            if (MessageBox.Show(this,
                $"هل تريد حفظ البيانات التي تم التحقق منها؟\n\nإضافات: {preview.Changes.Count(c => c.Action == "Add")}\nتحديثات: {preview.Changes.Count(c => c.Action is "Update" or "Lock existing")}\nدون تغيير: {preview.Changes.Count(c => c.Action == "Unchanged")}\nصفوف المشاكل المتخطاة: {preview.SkippedRows.Select(i => (i.Sheet, i.Row)).Distinct().Count()}\n\nتُحفظ جميع التغييرات المعروضة معاً؛ لا تُحفظ الصفوف المتخطاة.",
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
    }

    private void UpdateMode()
    {
        entity.Enabled = !allSheets.Checked && !busy;
        updates.Visible = entity.Visible = entityField.Visible = allSheets.Visible = true;
        allSheets.Text = "استيراد التدريسيين والمواد من جميع الأوراق";
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
        skipProblems.Visible = Mode == ExcelImportMode.AcademicData;
        skipProblems.Enabled = !busy && preview?.Issues.Any(i => i.Row > 1) == true;
        save.Enabled = !busy && preview?.IsValid == true && preview.Changes.Count > 0;
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
        using var dialog = new OpenFileDialog { Title = "اختر ملف Excel للتدريسيين والمواد", Filter = "ملفات Excel (*.xlsx)|*.xlsx", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        await RunAsync(async token =>
        {
            var loaded = await Task.Run(() => ExcelWorkbookReader.Read(dialog.FileName, token), token);
            LoadWorkbook(loaded, dialog.FileName);
        });
    }
    private void LoadWorkbook(ExcelWorkbookData loaded, string path)
    {
        workbook = loaded; preview = null; maps.Clear();
        report.DataSource = result.DataSource = null;
        if (Mode == ExcelImportMode.AcademicData && loaded.Sheets.Any(s => s.SuggestedSchemaName is null && ImportLabels.MatchSheet(s.Name) is null)) allSheets.Checked = false;
        binding = true; studySession.SelectedIndex = 0; binding = false;
        BindWorkbookSheets();
        fileSummary.Text = $"{Path.GetFileName(path)}  •  {loaded.Sheets.Count} جدول  •  {loaded.Sheets.Sum(s => s.Table.Rows.Count)} صف";
        status.Text = loaded.Sheets.Any(s => s.IsWeeklyTimetable)
            ? "تم تحويل الجدول الأسبوعي إلى محاضرات. اختر نطاق الدراسة، وراجع أسماء التدريسيين والقاعات وأوقات الانتهاء والملاحظات قبل التحقق."
            : "تمت قراءة الملف. راجع البيانات واربط الأعمدة عند الحاجة، ثم اضغط التحقق من البيانات.";
        if (loaded.Sheets.Any(s => s.SuggestedSchemaName == ExcelImportSchema.NamedAssignments.Name))
            status.Text = "يُستورد اسم التدريسي واسم المادة فقط. تُتخطى الأسماء الموجودة، وتظهر المواد الجديدة كغير مصنفة دون إنشاء تكليفات.";
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
            var known = ExcelImportSchema.AcademicInputs.FirstOrDefault(s => s.Name.Equals(selected.SuggestedSchemaName ?? ImportLabels.MatchSheet(selected.Name), StringComparison.OrdinalIgnoreCase));
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
                ? ExcelImportSchema.AcademicInputs.First(s => s.Name == entity.SelectedItem?.ToString()) : ExcelImportSchema.Timetable;
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
            var schema = Mode == ExcelImportMode.AcademicData ? ExcelImportSchema.AcademicInputs.FirstOrDefault(x => x.Name == schemaName) : ExcelImportSchema.Timetable;
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
            .Concat(preview.SkippedRows.Select(i => new { i.Sheet, i.Row, Status = "Skipped", Detail = ImportUiText.Message(i.Message) }))
            .Concat(preview.Changes.Select(c => new { c.Sheet, c.Row, Status = c.Action, Detail = c.Key }))
            .Concat(preview.UnscheduledLectures.Select(x => new { Sheet = "Generated", Row = 0, Status = "Unscheduled", Detail = ImportUiText.Message(x) })).ToList();
        report.DataSource = lines;
        status.Text = $"الأخطاء: {preview.Issues.Count}  •  صفوف المشاكل المتخطاة: {preview.SkippedRows.Select(i => (i.Sheet, i.Row)).Distinct().Count()}  •  السجلات المراجعة: {preview.Changes.Count}. " +
            (preview.Issues.Any(i => i.Row > 1) ? "صحح الصفوف أو اضغط «تخطي الصفوف ذات المشاكل» ثم راجع البقية قبل الحفظ." : "راجع التغييرات قبل تأكيد الاستيراد.");
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
