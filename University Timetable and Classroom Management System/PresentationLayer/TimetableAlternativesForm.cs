using University_Timetable_and_Classroom_Management_System.BusinessLayer;

namespace University_Timetable_and_Classroom_Management_System;

public partial class TimetableAlternativesForm : Form
{
    public bool HasSavedChanges { get; private set; }
    private readonly TimetableAlternativesService service = null!;
    private TimetableAlternative? current, selected;
    private bool busy, binding;
    private CancellationTokenSource? cancellation;
    public TimetableAlternativesForm() : this(null) { }

    public TimetableAlternativesForm(TimetableAlternativesService? service)
    {
        InitializeComponent();
        semester.SelectedIndex = 0;
        if (DesignerEnvironment.IsDesignTime(this)) return;

        this.service = service ?? new TimetableAlternativesService();
        ClientSizeChanged += (_, _) => FitLayout();
        FitLayout();
        WireEvents();
        UpdateActions();
    }

    private void FitLayout()
    {
        root.RowStyles[2].Height = ClientSize.Height < 760 ? 210 : 310;
        root.RowStyles[4].Height = ClientSize.Height < 760 ? 64 : 86;
        root.RowStyles[5].Height = ClientSize.Height < 760 ? 90 : 100;
    }

    private void WireEvents()
    {
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
        Shown += async (_, _) => await RunAsync(LoadAsync);
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
                    option.Status == "Current" ? "للمقارنة" : option.Status == "Optimal" ? "أمثل ضمن النطاق" : "صالح؛ الأمثلية غير مثبتة");
                comparison.Rows[row].Tag = option;
                comparison.Rows[row].Cells[7].ToolTipText = option.Status == "Optimal" ? "أمثل ضمن نطاق البدائل والهدف المحدد للبحث" : Convert.ToString(comparison.Rows[row].Cells[7].Value);
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
        int[] widths = [75, 160, 210, 240, 85, 80, 95, 130, 110, 75, 75];
        for (int c = 0; c < widths.Length; c++) { grid.Columns[c].MinimumWidth = widths[c]; grid.Columns[c].FillWeight = widths[c]; }
        grid.Columns[7].DefaultCellStyle.WrapMode = DataGridViewTriState.False;
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
}
