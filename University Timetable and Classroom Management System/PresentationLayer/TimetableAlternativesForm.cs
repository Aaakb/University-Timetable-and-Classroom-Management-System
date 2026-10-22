using University_Timetable_and_Classroom_Management_System.BusinessLayer;

namespace University_Timetable_and_Classroom_Management_System;

public sealed class TimetableAlternativesForm : Form
{
    public bool HasSavedChanges { get; private set; }
    private readonly TimetableAlternativesService service;
    private readonly ComboBox semester = new() { Name = "alternativeSemester", Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox facultyFilter = new() { Name = "alternativeFacultyFilter", Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown seconds = new() { Name = "alternativeSeconds", Minimum = 1, Maximum = 120, Value = 30, Width = 70 };
    private readonly DataGridView comparison = Grid("alternativeComparison"), original = Grid("alternativeOriginal"), detail = Grid("alternativeDetail"), week = Grid("alternativeWeek");
    private readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    private readonly Label status = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleRight };
    private readonly Button generate = Button("إنشاء حتى 3 بدائل", "generateAlternatives"), save = Button("اعتماد البديل المحدد…", "saveAlternative"), cancel = Button("إلغاء البحث", "cancelAlternatives"), reload = Button("تحديث البيانات", "reloadAlternatives"), showCurrent = Button("عرض الجدول الحالي", "viewCurrentAlternative"), close = Button("إغلاق", "closeAlternatives"), unlock = Button("إلغاء تثبيت المحاضرات المحددة…", "unlockAlternativeRows");
    private readonly FlowLayoutPanel filters = new() { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
    private TimetableAlternative? current, selected;
    private bool busy, binding;
    private CancellationTokenSource? cancellation;
    public TimetableAlternativesForm() : this(new TimetableAlternativesService()) { }
    public TimetableAlternativesForm(TimetableAlternativesService service)
    {
        this.service = service; Name = "TimetableAlternativesForm"; Text = "جداول بديلة — معاينة قبل الاعتماد";
        RightToLeft = RightToLeft.Yes; Font = new("Segoe UI", 10); BackColor = Color.FromArgb(245, 247, 251); Size = new(1180, 850); MinimumSize = new(900, 680); StartPosition = FormStartPosition.CenterParent;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(12), ColumnCount = 1, RowCount = 6 };
        root.ColumnStyles.Add(new(SizeType.Percent, 100)); root.RowStyles.Add(new(SizeType.AutoSize)); root.RowStyles.Add(new(SizeType.AutoSize)); root.RowStyles.Add(new(SizeType.Absolute, 168)); root.RowStyles.Add(new(SizeType.Percent, 100)); root.RowStyles.Add(new(SizeType.Absolute, 62)); root.RowStyles.Add(new(SizeType.Absolute, 90)); Controls.Add(root);
        root.Controls.Add(new Label { AutoSize = true, Text = "قارن الجدول الحالي ببدائل مختلفة قبل الحفظ", Font = new("Segoe UI", 16, FontStyle.Bold), Margin = new(3, 3, 3, 10) }, 0, 0);
        semester.Items.AddRange(["الفصل الأول", "الفصل الثاني"]); semester.SelectedIndex = 0;
        filters.Controls.AddRange([Field("الفصل", semester), Field("مهلة البحث/ثانية", seconds), Field("تصفية المعاينة", facultyFilter)]); root.Controls.Add(filters, 0, 1);
        comparison.MultiSelect = false;
        comparison.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; comparison.ColumnHeadersHeight = 56;
        foreach (string title in new[] { "الاختيار", "عقوبة التفضيل ↓", "فراغات التدريسيين/دقيقة ↓", "فراغات الطلبة/دقيقة ↓", "أعلى عبء يومي/دقيقة ↓", "محاضرات متغيرة", "تدريسيون متأثرون", "الحالة" }) comparison.Columns.Add(title, title);
        foreach (DataGridViewColumn column in comparison.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
        comparison.Columns[0].FillWeight = 170; comparison.Columns[7].FillWeight = 130;
        root.Controls.Add(comparison, 0, 2); root.Controls.Add(tabs, 0, 3); root.Controls.Add(status, 0, 4);
        var originalPage = new Panel { Dock = DockStyle.Fill }; var lockActions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        lockActions.Controls.Add(unlock); lockActions.Controls.Add(new Label { AutoSize = true, Text = "حدّد الصفوف المطلوبة؛ Ctrl+A لتحديد الكل. فك التثبيت لا يحرّك المحاضرات.", Margin = new(5, 10, 5, 3) });
        originalPage.Controls.Add(original); originalPage.Controls.Add(lockActions); AddTab("الجدول الحالي والأقفال", originalPage); AddTab("تفاصيل الاختيار", detail); AddTab("معاينة أسبوعية", week);
        week.DefaultCellStyle.WrapMode = DataGridViewTriState.True; week.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2 };
        for (int i = 0; i < 3; i++) actions.ColumnStyles.Add(new(SizeType.Percent, 100f / 3));
        actions.RowStyles.Add(new(SizeType.Percent, 50)); actions.RowStyles.Add(new(SizeType.Percent, 50));
        var buttons = new[] { generate, save, cancel, reload, showCurrent, close };
        for (int i = 0; i < buttons.Length; i++) { buttons[i].Dock = DockStyle.Fill; actions.Controls.Add(buttons[i], i % 3, i / 3); } root.Controls.Add(actions, 0, 5);
        comparison.CurrentCellChanged += (_, _) => { if (!binding && comparison.CurrentRow?.Tag is TimetableAlternative option) { selected = option; BindSelection(); UpdateActions(); } };
        facultyFilter.SelectedIndexChanged += (_, _) => { if (!binding) BindSelection(); };
        semester.SelectedIndexChanged += async (_, _) => { if (!busy) await RunAsync(LoadAsync); };
        generate.Click += async (_, _) => await RunAsync(async token =>
        {
            var result = await service.GenerateAsync(new(semester.SelectedIndex + 1, 3, (double)seconds.Value), token);
            current = result.Current; BindComparison(result.Alternatives); status.Text = result.Message;
            if (result.Alternatives.Count > 0) { comparison.CurrentCell = comparison.Rows[1].Cells[0]; selected = result.Alternatives[0]; BindSelection(); tabs.SelectedIndex = 1; }
        });
        save.Click += async (_, _) =>
        {
            if (selected is null || ReferenceEquals(selected, current)) return; var approved = selected;
            if (MessageBox.Show(this, $"اعتماد «{approved.Title}» وتغيير {approved.Metrics.ChangedLectures} محاضرة لدى {approved.Metrics.AffectedFaculty} تدريسيين؟", "اعتماد الجدول", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            await RunAsync(async token => { int count = await service.SaveAsync(approved, true, token); HasSavedChanges |= count > 0; await LoadAsync(token); status.Text = $"تم اعتماد البديل وحفظ {count} تغييرًا، مع الحفاظ على هويات المحاضرات والأقفال."; });
        };
        unlock.Click += async (_, _) =>
        {
            if (current is null) return;
            var ids = original.SelectedRows.Cast<DataGridViewRow>().Where(r => r.Tag is TimetableAlternativeLecture { Locked: true }).Select(r => ((TimetableAlternativeLecture)r.Tag!).Id).ToArray();
            if (ids.Length == 0) { status.Text = "اختر المحاضرات المثبتة التي تسمح بتحريكها من الجدول الحالي."; return; }
            if (MessageBox.Show(this, $"إلغاء تثبيت {ids.Length} محاضرة محددة للسماح بتحريكها في البحث؟\nهذا يحفظ حالة التثبيت فقط ولا يغيّر مواعيدها.", "تعديل صريح للأقفال", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            string version = current.Version;
            await RunAsync(async token => { await service.SetLocksAsync(semester.SelectedIndex + 1, ids, false, version, token); HasSavedChanges = true; await LoadAsync(token); });
        };
        reload.Click += async (_, _) => await RunAsync(LoadAsync);
        showCurrent.Click += (_, _) => { if (current is not null) { comparison.CurrentCell = comparison.Rows[0].Cells[0]; selected = current; BindSelection(); tabs.SelectedIndex = 0; UpdateActions(); } };
        cancel.Click += (_, _) => cancellation?.Cancel(); close.Click += (_, _) => Close();
        FormClosing += (_, e) => { if (busy) { e.Cancel = true; cancellation?.Cancel(); } };
        Shown += async (_, _) => await RunAsync(LoadAsync); UpdateActions();
    }
    private async Task LoadAsync(CancellationToken token)
    {
        current = await service.GetCurrentAsync(semester.SelectedIndex + 1, token); BindComparison([]);
        status.Text = $"{current.Lectures.Count} محاضرة، منها {current.Lectures.Count(l => l.Locked)} مثبتة. إنشاء البدائل لا يحفظ أي تغيير. قد يتوفر أقل من ثلاثة بدائل بحسب القيود.";
    }
    private void BindComparison(IReadOnlyList<TimetableAlternative> alternatives)
    {
        binding = true;
        try
        {
            comparison.Rows.Clear();
            foreach (var option in new[] { current! }.Concat(alternatives))
            {
                var m = option.Metrics; int row = comparison.Rows.Add(option.Title, m.PreferencePenalty, m.FacultyGapMinutes, m.StudentGapMinutes, m.PeakStudentDailyMinutes, m.ChangedLectures, m.AffectedFaculty,
                    option.Status == "Current" ? "للمقارنة" : option.Status == "Optimal" ? "أمثل ضمن نطاق البدائل والهدف" : "صالح؛ الأمثلية غير مثبتة");
                comparison.Rows[row].Tag = option; comparison.Rows[row].Height = 25;
            }
            selected = current; facultyFilter.Items.Clear(); facultyFilter.Items.Add("كل التدريسيين"); facultyFilter.Items.AddRange(current!.Lectures.Select(l => l.Faculty).Distinct().Order().Cast<object>().ToArray()); facultyFilter.SelectedIndex = 0;
            BindRows(original, current.Lectures); comparison.CurrentCell = comparison.Rows[0].Cells[0];
        }
        finally { binding = false; }
        BindSelection(); UpdateActions();
    }
    private void BindSelection()
    {
        if (selected is null) return;
        var rows = selected.Lectures.Where(l => facultyFilter.SelectedIndex <= 0 || l.Faculty == (string?)facultyFilter.SelectedItem).ToList(); BindRows(detail, rows);
        week.Columns.Clear(); week.Rows.Clear(); week.Columns.Add("Period", "الفترة");
        foreach (string day in service.TeachingDays) week.Columns.Add(day, ImportLabels.Display(day));
        foreach (var period in rows.Select(l => l.Period).Distinct().Order())
        {
            int r = week.Rows.Add(); week.Rows[r].Cells[0].Value = "\u200e" + period;
            foreach (DataGridViewColumn day in week.Columns.Cast<DataGridViewColumn>().Skip(1))
                week.Rows[r].Cells[day.Index].Value = string.Join(Environment.NewLine + Environment.NewLine, rows.Where(l => l.Day == day.Name && l.Period == period).Select(l => $"{l.Subject} — {l.Faculty}\n{l.Section} {l.GroupName} / {l.Room}"));
        }
    }
    private static void BindRows(DataGridView grid, IEnumerable<TimetableAlternativeLecture> rows)
    {
        grid.Columns.Clear(); grid.Rows.Clear(); foreach (string name in new[] { "المعرف", "التدريسي", "المادة", "الشعبة", "المجموعة", "النوع", "اليوم", "الفترة", "القاعة", "مثبتة", "تغيّرت" }) grid.Columns.Add(name, name);
        foreach (var row in rows) { int i = grid.Rows.Add(row.Id, row.Faculty, row.Subject, row.Section, row.GroupName, ImportLabels.Display(row.LectureType), ImportLabels.Display(row.Day), "\u200e" + row.Period, row.Room, row.Locked ? "نعم" : "لا", row.Changed ? "نعم" : "لا"); grid.Rows[i].Tag = row; if (row.Changed) grid.Rows[i].DefaultCellStyle.BackColor = Color.FromArgb(219, 234, 254); }
        grid.ClearSelection();
    }
    private async Task RunAsync(Func<CancellationToken, Task> work)
    {
        if (busy) return; busy = true; cancellation = new(); filters.Enabled = comparison.Enabled = false; UseWaitCursor = true; UpdateActions(); status.Text = "جارٍ البحث والتحقق؛ يبقى الجدول محفوظًا حتى الاعتماد…";
        try { await work(cancellation.Token); }
        catch (OperationCanceledException) { status.Text = "أُلغيت العملية دون حفظ جزئي."; }
        catch (Exception ex) { status.Text = ex.Message; MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { busy = false; cancellation.Dispose(); cancellation = null; filters.Enabled = comparison.Enabled = true; UseWaitCursor = false; UpdateActions(); }
    }
    private void UpdateActions() { generate.Enabled = reload.Enabled = showCurrent.Enabled = unlock.Enabled = !busy; save.Enabled = !busy && selected is not null && !ReferenceEquals(selected, current); cancel.Enabled = busy; }
    private void AddTab(string title, Control control) { var tab = new TabPage(title) { Padding = new(5) }; tab.Controls.Add(control); tabs.TabPages.Add(tab); }
    private static DataGridView Grid(string name) => new() { Name = name, Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false, BackgroundColor = Color.White, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells };
    private static Button Button(string text, string name) => new() { Text = text, Name = name, AutoSize = true, Margin = new(3), Padding = new(7, 5, 7, 5), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(235, 241, 250) };
    private static Control Field(string title, Control control) { var panel = new FlowLayoutPanel { AutoSize = true, WrapContents = false }; panel.Controls.Add(new Label { Text = title, AutoSize = true, Margin = new(3, 6, 3, 0) }); panel.Controls.Add(control); return panel; }
}
