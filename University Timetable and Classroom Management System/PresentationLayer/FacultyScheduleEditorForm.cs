using University_Timetable_and_Classroom_Management_System.BusinessLayer;

namespace University_Timetable_and_Classroom_Management_System;

public sealed class FacultyScheduleEditorForm : Form
{
    public bool HasSavedChanges { get; private set; }
    private readonly FacultyScheduleEditorService service;
    private readonly ComboBox faculty = new() { Width = 270, DropDownStyle = ComboBoxStyle.DropDown, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
    private readonly ComboBox semester = new() { Width = 90, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox mode = new() { Width = 210, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckedListBox preferredDays = List("preferredDays");
    private readonly CheckedListBox unavailableDays = List("unavailableDays");
    private readonly CheckedListBox preferredPeriods = List("preferredPeriods");
    private readonly NumericUpDown timeLimit = new() { Minimum = 1, Maximum = 120, Value = 20, Width = 65 };
    private readonly DataGridView current = Grid("currentTimetable");
    private readonly DataGridView proposed = Grid("proposedTimetable");
    private readonly DataGridView slots = Grid("availableSlots");
    private readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    private readonly TextBox notes = new() { Dock = DockStyle.Bottom, Height = 120, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    private readonly Label status = new() { AutoSize = false, AutoEllipsis = true, Dock = DockStyle.Fill, Text = "اختر التدريسي والفصل، ثم حدد الأيام ونمط الجدولة." };
    private readonly Button inspect = Button("فحص الفترات المتاحة", "inspectSlots");
    private readonly Button generate = Button("إنشاء المقترح", "generateProposal");
    private readonly Button save = Button("حفظ التغييرات…", "saveProposal");
    private readonly Button cancel = Button("إلغاء العملية", "cancelOperation");
    private readonly Button reload = Button("تحديث الجدول", "reloadTimetable");
    private readonly FlowLayoutPanel filters = new() { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
    private readonly FlowLayoutPanel preferences = new() { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
    private FacultySchedulePreview? preview;
    private bool busy, binding;
    private CancellationTokenSource? cancellation;
    private sealed record Choice(string Value, string Label) { public override string ToString() => Label; }
    public FacultyScheduleEditorForm() : this(new FacultyScheduleEditorService()) { }
    public FacultyScheduleEditorForm(FacultyScheduleEditorService service)
    {
        this.service = service;
        Name = "FacultyScheduleEditorForm"; Text = "تغيير أيام التدريسي";
        StartPosition = FormStartPosition.CenterParent; MinimumSize = new(800, 600); Size = new(1180, 820);
        AutoScaleMode = AutoScaleMode.Dpi; Font = new("Segoe UI", 10); BackColor = Color.FromArgb(245, 247, 251);
        RightToLeft = RightToLeft.Yes;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(14), ColumnCount = 1, RowCount = 6 };
        root.ColumnStyles.Add(new(SizeType.Percent, 100));
        root.RowStyles.Add(new(SizeType.AutoSize)); root.RowStyles.Add(new(SizeType.AutoSize)); root.RowStyles.Add(new(SizeType.AutoSize));
        root.RowStyles.Add(new(SizeType.Percent, 100)); root.RowStyles.Add(new(SizeType.Absolute, 36)); root.RowStyles.Add(new(SizeType.Absolute, 92));
        Controls.Add(root);
        root.Controls.Add(new Label { Text = "أيام التدريسي — معاينة قبل الحفظ", Font = new("Segoe UI", 17, FontStyle.Bold), AutoSize = true, Margin = new(0,0,0,10) }, 0, 0);
        preferredPeriods.RightToLeft = RightToLeft.No;
        faculty.Name = "facultySelection"; faculty.DisplayMember = nameof(FacultyEditorOption.Name);
        semester.Name = "semesterSelection"; semester.Items.AddRange(["الفصل الأول", "الفصل الثاني"]); semester.SelectedIndex = 0;
        mode.Name = "availabilityMode"; mode.Items.AddRange(["صارم: الأيام المحددة فقط", "تفضيل: المحددة أولًا ثم البدائل"]); mode.SelectedIndex = -1;
        filters.Controls.AddRange([Field("التدريسي", faculty), Field("الفصل", semester), Field("النمط (مطلوب)", mode), Field("مهلة البحث بالثواني", timeLimit)]);
        root.Controls.Add(filters, 0, 1);
        preferences.Controls.AddRange([Group("أيام التدريس المختارة", preferredDays), Group("أيام غير متاحة (اختياري)", unavailableDays), Group("فترات مفضلة (اختياري)", preferredPeriods)]);
        root.Controls.Add(preferences, 0, 2);
        Tab("الجدول الحالي", current); Tab("المقترح: قبل وبعد", proposed);
        var result = new Panel { Dock = DockStyle.Fill }; result.Controls.Add(slots); result.Controls.Add(notes); Tab("الخيارات والتوضيحات", result);
        root.Controls.Add(tabs, 0, 3);
        root.Controls.Add(status, 0, 4);
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2 };
        for (int i = 0; i < 3; i++) actions.ColumnStyles.Add(new(SizeType.Percent, 100f / 3));
        actions.RowStyles.Add(new(SizeType.Percent, 50)); actions.RowStyles.Add(new(SizeType.Percent, 50));
        var close = Button("إغلاق", "closeEditor");
        var buttons = new[] { inspect, generate, save, reload, cancel, close };
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].AutoSize = false; buttons[i].Dock = DockStyle.Fill;
            actions.Controls.Add(buttons[i], i % 3, i / 3);
        }
        root.Controls.Add(actions, 0, 5);
        reload.Click += async (_, _) => await RunAsync(LoadCurrentAsync);
        inspect.Click += async (_, _) => await MakePreviewAsync(true);
        generate.Click += async (_, _) => await MakePreviewAsync(false);
        save.Click += async (_, _) =>
        {
            if (preview?.CanSave != true) return;
            var approved = preview;
            if (MessageBox.Show(this, $"حفظ {approved.Changes.Count(c => c.Changed)} تغييرًا لمحاضرات التدريسي المحدد فقط؟\n" +
                $"المحاضرات خارج الأيام المفضلة: {approved.Changes.Count(c => c.OutsidePreferredDays)}\nتبقى هويات المحاضرات وجداول الآخرين محفوظة.",
                "تأكيد الحفظ", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            await RunAsync(async token =>
            {
                int count = await service.SaveAsync(approved, true, token);
                HasSavedChanges |= count > 0;
                await LoadCurrentAsync(token);
                status.Text = $"تم حفظ {count} تغييرًا. بقيت محاضرات الآخرين دون تعديل.";
            });
        };
        cancel.Click += (_, _) => { if (busy) cancellation?.Cancel(); else InvalidatePreview(); };
        close.Click += (_, _) => Close();
        faculty.SelectedIndexChanged += async (_, _) => { if (!binding && !busy) await RunAsync(LoadCurrentAsync); };
        faculty.TextChanged += (_, _) => { if (!binding && !busy) InvalidatePreview(); };
        semester.SelectedIndexChanged += async (_, _) => { if (!binding && !busy) await RunAsync(LoadCurrentAsync); };
        mode.SelectedIndexChanged += (_, _) => InvalidatePreview();
        timeLimit.ValueChanged += (_, _) => InvalidatePreview();
        preferredDays.ItemCheck += (_, _) => InvalidatePreview(); unavailableDays.ItemCheck += (_, _) => InvalidatePreview(); preferredPeriods.ItemCheck += (_, _) => InvalidatePreview();
        FormClosing += (_, e) => { if (busy) { e.Cancel = true; cancellation?.Cancel(); status.Text = "جارٍ إيقاف العملية دون حفظ جزئي…"; } };
        Shown += async (_, _) => await RunAsync(async token =>
        {
            var lookups = await service.GetLookupsAsync(token);
            binding = true;
            try
            {
                faculty.Items.AddRange(lookups.Faculty.Cast<object>().ToArray());
                foreach (string day in lookups.Days) { preferredDays.Items.Add(new Choice(day, Day(day))); unavailableDays.Items.Add(new Choice(day, Day(day))); }
                preferredPeriods.DisplayMember = nameof(FacultyEditorOption.Name); preferredPeriods.Items.AddRange(lookups.Periods.Cast<object>().ToArray());
                if (faculty.Items.Count > 0) faculty.SelectedIndex = 0;
            }
            finally { binding = false; }
            await LoadCurrentAsync(token);
        });
        UpdateActions();
    }
    private async Task LoadCurrentAsync(CancellationToken token)
    {
        InvalidatePreview();
        if (faculty.SelectedItem is not FacultyEditorOption selected) { current.DataSource = null; return; }
        var rows = await service.GetCurrentAsync(selected.Id, semester.SelectedIndex + 1, token);
        current.DataSource = rows.ToList(); Headers(current);
        binding = true;
        try
        {
            for (int i = 0; i < preferredDays.Items.Count; i++) preferredDays.SetItemChecked(i, rows.Any(r => r.Day == ((Choice)preferredDays.Items[i]).Value));
        }
        finally { binding = false; }
        status.Text = $"{rows.Count} محاضرة حالية. حدد الأيام ونمط الجدولة؛ لا يتغير الجدول عند المعاينة.";
    }
    private async Task MakePreviewAsync(bool inspectOnly)
    {
        await RunAsync(async token =>
        {
            if (faculty.SelectedItem is not FacultyEditorOption selected || mode.SelectedIndex < 0) throw new ArgumentException("اختر التدريسي ونمط الجدولة أولًا.");
            var request = new FacultyScheduleRequest(selected.Id, semester.SelectedIndex + 1, (FacultyAvailabilityMode)mode.SelectedIndex,
                preferredDays.CheckedItems.Cast<Choice>().Select(x => x.Value).ToArray(), unavailableDays.CheckedItems.Cast<Choice>().Select(x => x.Value).ToArray(),
                preferredPeriods.CheckedItems.Cast<FacultyEditorOption>().Select(x => x.Id).ToArray(), (double)timeLimit.Value);
            preview = await service.PreviewAsync(request, inspectOnly, token);
            proposed.DataSource = preview.Changes.ToList(); Headers(proposed);
            foreach (DataGridViewRow row in proposed.Rows)
                if (row.DataBoundItem is FacultyLectureChange change)
                    row.DefaultCellStyle.BackColor = change.OutsidePreferredDays ? Color.FromArgb(255, 237, 213) : change.Changed ? Color.FromArgb(219, 234, 254) : Color.White;
            slots.DataSource = preview.SlotOptions.ToList(); Headers(slots);
            notes.Text = preview.Message + Environment.NewLine + "حالة البحث: " + preview.Status + Environment.NewLine + string.Join(Environment.NewLine, preview.Suggestions);
            status.Text = preview.Message;
            tabs.SelectedIndex = preview.CanSave ? 1 : 2;
        });
    }
    private void InvalidatePreview()
    {
        if (binding) return;
        preview = null; proposed.DataSource = null; slots.DataSource = null; notes.Clear();
        status.Text = "تغيرت الخيارات؛ أنشئ مقترحًا جديدًا قبل الحفظ."; UpdateActions();
    }
    private async Task RunAsync(Func<CancellationToken, Task> work)
    {
        if (busy) return;
        busy = true; cancellation = new(); filters.Enabled = preferences.Enabled = false;
        UseWaitCursor = true; status.Text = "جارٍ الفحص… الجدول محفوظ حتى تأكيد الحفظ."; UpdateActions();
        try { await work(cancellation.Token); }
        catch (OperationCanceledException) { InvalidatePreview(); status.Text = "أُلغيت العملية دون حفظ تغييرات جزئية."; }
        catch (Exception ex) { InvalidatePreview(); status.Text = ex.Message; MessageBox.Show(this, ex.Message, "تغيير أيام التدريسي", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { busy = false; cancellation.Dispose(); cancellation = null; filters.Enabled = preferences.Enabled = true; UseWaitCursor = false; UpdateActions(); }
    }
    private void UpdateActions() { inspect.Enabled = generate.Enabled = reload.Enabled = !busy; save.Enabled = !busy && preview?.CanSave == true; cancel.Enabled = busy || preview is not null; }
    private void Tab(string title, Control control) { var tab = new TabPage(title) { Padding = new(6) }; tab.Controls.Add(control); tabs.TabPages.Add(tab); }
    private static CheckedListBox List(string name) => new() { Name = name, CheckOnClick = true, Dock = DockStyle.Fill, IntegralHeight = false };
    private static DataGridView Grid(string name)
    {
        var grid = new DataGridView { Name = name, Dock = DockStyle.Fill, AutoGenerateColumns = true, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells, RowHeadersVisible = false, BackgroundColor = Color.White, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
        grid.DataBindingComplete += (_, _) =>
        {
            Headers(grid);
            foreach (DataGridViewRow row in grid.Rows)
                if (row.DataBoundItem is FacultyLectureChange change)
                    row.DefaultCellStyle.BackColor = change.OutsidePreferredDays ? Color.FromArgb(255, 237, 213) : change.Changed ? Color.FromArgb(219, 234, 254) : Color.White;
            grid.ClearSelection();
        };
        grid.CellFormatting += (_, e) =>
        {
            if (e.ColumnIndex < 0 || e.Value is not string text) return;
            string field = grid.Columns[e.ColumnIndex].DataPropertyName;
            if (field.EndsWith("Period", StringComparison.Ordinal)) { e.Value = "\u200e" + text + "\u200e"; e.FormattingApplied = true; }
            else if (field.EndsWith("Day", StringComparison.Ordinal)) { e.Value = Day(text); e.FormattingApplied = true; }
            else if (field == "LectureType") { e.Value = text == "Practical" ? "عملي" : "نظري"; e.FormattingApplied = true; }
        };
        return grid;
    }
    private static Button Button(string text, string name) => new() { Name = name, Text = text, AutoSize = true, Padding = new(8,5,8,5), Margin = new(3) };
    private static Control Field(string title, Control control) { var panel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new(3) }; panel.Controls.Add(new Label { Text = title, AutoSize = true, Margin = new(3,6,3,0) }); panel.Controls.Add(control); return panel; }
    private static Control Group(string title, Control control) { var group = new GroupBox { Text = title, Width = 240, Height = 145, Padding = new(8), Margin = new(4) }; group.Controls.Add(control); return group; }
    private static string Day(string day) => day switch { "Sunday" => "الأحد", "Monday" => "الاثنين", "Tuesday" => "الثلاثاء", "Wednesday" => "الأربعاء", "Thursday" => "الخميس", "Friday" => "الجمعة", "Saturday" => "السبت", _ => day };
    private static void Headers(DataGridView grid)
    {
        var labels = new Dictionary<string,string> { ["ScheduleID"]="المعرّف", ["Subject"]="المادة", ["Section"]="الشعبة", ["LectureType"]="النوع", ["GroupName"]="المجموعة", ["Day"]="اليوم", ["Period"]="الفترة", ["Room"]="القاعة", ["OldDay"]="اليوم السابق", ["OldPeriod"]="الفترة السابقة", ["OldRoom"]="القاعة السابقة", ["NewDay"]="اليوم المقترح", ["NewPeriod"]="الفترة المقترحة", ["NewRoom"]="القاعة المقترحة", ["Changed"]="تغيرت", ["OutsidePreferredDays"]="خارج الأيام المفضلة", ["PreferredDay"]="ضمن الأيام المختارة", ["PreferredSlots"]="خيارات الأيام المختارة", ["OtherSlots"]="خيارات الأيام الأخرى" };
        foreach (DataGridViewColumn col in grid.Columns) if (labels.TryGetValue(col.Name, out var label)) col.HeaderText = label;
        if (grid.Columns.Contains("NewDay"))
        {
            string[] order = ["Subject", "OldDay", "OldPeriod", "NewDay", "NewPeriod", "OldRoom", "NewRoom", "GroupName", "LectureType", "Section", "Changed", "OutsidePreferredDays", "ScheduleID"];
            for (int i = 0; i < order.Length; i++) grid.Columns[order[i]].DisplayIndex = i;
        }
    }
}
