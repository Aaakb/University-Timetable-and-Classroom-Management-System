using System.Data;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;

namespace University_Timetable_and_Classroom_Management_System;

public sealed class FacultyPreferencesForm : Form
{
    public bool HasSavedChanges { get; private set; }
    private readonly FacultyScheduleEditorService service;
    private readonly ComboBox faculty = Combo("preferenceFaculty", 300), semester = Combo("preferenceSemester", 140);
    private readonly CheckBox enabled = new() { Name = "enableDayPreferences", Text = "تحديد تفضيلات أيام لهذا التدريسي", AutoSize = true, Margin = new(4, 12, 4, 10) };
    private readonly NumericUpDown seconds = new() { Name = "preferenceSeconds", Minimum = 1, Maximum = 120, Value = 20, Width = 72 };
    private readonly Dictionary<string, ComboBox> days = [];
    private readonly TableLayoutPanel dayCards = new() { Name = "dayPreferenceCards", Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1, Height = 112, Margin = new(0, 6, 0, 8) };
    private readonly FlowLayoutPanel filters = new() { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = Padding.Empty };
    private readonly TabControl tabs = new() { Dock = DockStyle.Fill, Padding = new(18, 7) };
    private readonly DataGridView current = Grid("preferenceCurrent"), changes = Grid("preferenceChanges");
    private readonly Label status = new() { Name = "preferenceStatus", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Padding = new(12, 4, 12, 4), BackColor = Color.FromArgb(231, 241, 238), ForeColor = Color.FromArgb(32, 92, 76) };
    private readonly Label profileStatus = new() { AutoSize = true, ForeColor = Color.FromArgb(91, 108, 122), Margin = new(4, 3, 4, 10), MaximumSize = new(1000, 0) };
    private readonly Button save = Action("حفظ التفضيلات وتحديث الجدول", "savePreferences"), reload = Action("تحديث البيانات", "reloadPreferences"), cancel = Action("إلغاء البحث", "cancelPreferences"), close = Action("إغلاق", "closePreferences");
    private FacultyEditorLookups? lookups;
    private CancellationTokenSource? cancellation;
    private int activeFaculty, activeSemester = 1;
    private long revision;
    private bool binding, dirty, busy;
    private sealed record FacultyChoice(int Id, string Text);
    private sealed record LevelChoice(FacultyPreferenceLevel Value, string Text);
    private static readonly LevelChoice[] levels = [new(FacultyPreferenceLevel.StronglyPreferred, "مفضّل"), new(FacultyPreferenceLevel.Preferred, "مسموح"), new(FacultyPreferenceLevel.Prohibited, "غير متاح")];

    public FacultyPreferencesForm() : this(new FacultyScheduleEditorService()) { }
    public FacultyPreferencesForm(FacultyScheduleEditorService service)
    {
        this.service = service;
        Name = "FacultyPreferencesForm"; Text = "تفضيلات أيام التدريسيين";
        Font = new("Segoe UI", 10); BackColor = Color.FromArgb(243, 246, 249);
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        Size = new(1180, 820); MinimumSize = new(800, 680); StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.Dpi;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(16), ColumnCount = 1, RowCount = 6 };
        root.ColumnStyles.Add(new(SizeType.Percent, 100));
        root.RowStyles.Add(new(SizeType.Absolute, 56)); root.RowStyles.Add(new(SizeType.AutoSize));
        root.RowStyles.Add(new(SizeType.Absolute, 190)); root.RowStyles.Add(new(SizeType.Percent, 100));
        root.RowStyles.Add(new(SizeType.Absolute, 48)); root.RowStyles.Add(new(SizeType.Absolute, 52)); Controls.Add(root);
        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        heading.Controls.Add(new Label { Text = "تفضيلات أيام التدريسيين", AutoSize = true, Font = new("Segoe UI", 19, FontStyle.Bold), ForeColor = Color.FromArgb(25, 52, 67) });
        heading.Controls.Add(new Label { Text = "اختر الأيام، وسنُعيد توزيع الجدول الحالي وفق التفضيلات المتوفرة.", AutoSize = true, ForeColor = Color.FromArgb(91, 108, 122) });
        root.Controls.Add(heading, 0, 0);
        faculty.DisplayMember = nameof(FacultyChoice.Text); faculty.DropDownStyle = ComboBoxStyle.DropDown;
        faculty.AutoCompleteMode = AutoCompleteMode.SuggestAppend; faculty.AutoCompleteSource = AutoCompleteSource.ListItems;
        semester.Items.AddRange(["الفصل الأول", "الفصل الثاني"]); semester.SelectedIndex = 0;
        filters.Controls.AddRange([Field("التدريسي", faculty), Field("الفصل الدراسي", semester), Field("مهلة التحسين / ثانية", seconds)]);
        root.Controls.Add(filters, 0, 1);
        var preferences = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Color.White, Padding = new(10), Margin = new(0, 8, 0, 8) };
        preferences.ColumnStyles.Add(new(SizeType.Percent, 100));
        enabled.Margin = new(4);
        profileStatus.Font = new("Segoe UI", 9); profileStatus.Margin = new(4, 0, 4, 0);
        preferences.RowStyles.Add(new(SizeType.AutoSize)); preferences.RowStyles.Add(new(SizeType.Absolute, 80));
        preferences.RowStyles.Add(new(SizeType.AutoSize)); preferences.RowStyles.Add(new(SizeType.Percent, 100));
        preferences.Controls.Add(enabled, 0, 0); preferences.Controls.Add(dayCards, 0, 1);
        preferences.Controls.Add(new Label { AutoSize = true, Font = new("Segoe UI", 9), Text = "مفضّل: أولوية في التحسين    •    مسموح: متاح عند الحاجة    •    غير متاح: يُمنع وضع المحاضرات فيه", ForeColor = Color.FromArgb(72, 91, 105), Margin = new(4, 2, 4, 4) }, 0, 2);
        preferences.Controls.Add(profileStatus, 0, 3); root.Controls.Add(preferences, 0, 2);
        AddTab("الجدول الحالي للتدريسي", current); AddTab("التغييرات المحفوظة في الفصل", changes); root.Controls.Add(tabs, 0, 3);
        root.Controls.Add(status, 0, 4);
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = new(0, 8, 0, 0) };
        actions.ColumnStyles.Add(new(SizeType.Percent, 43)); actions.ColumnStyles.Add(new(SizeType.Percent, 23)); actions.ColumnStyles.Add(new(SizeType.Percent, 19)); actions.ColumnStyles.Add(new(SizeType.Percent, 15));
        save.BackColor = Color.FromArgb(24, 113, 95); save.ForeColor = Color.White;
        var buttons = new[] { save, reload, cancel, close };
        for (int i = 0; i < buttons.Length; i++) { buttons[i].Dock = DockStyle.Fill; actions.Controls.Add(buttons[i], i, 0); }
        root.Controls.Add(actions, 0, 5);
        faculty.SelectedIndexChanged += async (_, _) => await ChangeSelectionAsync();
        semester.SelectedIndexChanged += async (_, _) => await ChangeSelectionAsync();
        enabled.CheckedChanged += (_, _) => { dayCards.Enabled = enabled.Checked && !busy; Edited(); };
        save.Click += async (_, _) => await SaveAsync();
        reload.Click += async (_, _) => { if (CanDiscard()) await RunAsync(async token => { await RefreshFacultyListAsync(token); await LoadFacultyAsync(token); }); };
        cancel.Click += (_, _) => cancellation?.Cancel(); close.Click += (_, _) => Close();
        FormClosing += (_, e) => { if (busy) { e.Cancel = true; cancellation?.Cancel(); } else if (!CanDiscard()) e.Cancel = true; };
        Shown += async (_, _) => await RunAsync(async token =>
        {
            lookups = await service.GetLookupsAsync(token);
            foreach (var day in lookups.Days)
            {
                dayCards.ColumnStyles.Add(new(SizeType.Percent, 20));
                var card = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new(5), Margin = new(3), BackColor = Color.FromArgb(244, 247, 250) };
                card.ColumnStyles.Add(new(SizeType.Percent, 100)); card.RowStyles.Add(new(SizeType.Absolute, 27)); card.RowStyles.Add(new(SizeType.Percent, 100));
                card.Controls.Add(new Label { Text = ImportLabels.Display(day), Dock = DockStyle.Fill, Font = new("Segoe UI", 11, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter });
                var choice = Combo("dayPreference_" + day, 130); choice.Dock = DockStyle.Top;
                choice.DataSource = levels.ToArray(); choice.DisplayMember = nameof(LevelChoice.Text); choice.ValueMember = nameof(LevelChoice.Value);
                choice.SelectedValue = FacultyPreferenceLevel.Preferred;
                choice.SelectedIndexChanged += (_, _) => { PaintDay(choice); Edited(); };
                days.Add(day, choice); card.Controls.Add(choice); dayCards.Controls.Add(card);
            }
            await RefreshFacultyListAsync(token);
            if (faculty.Items.Count > 0) { binding = true; faculty.SelectedIndex = 0; activeFaculty = ((FacultyChoice)faculty.SelectedItem!).Id; binding = false; }
            await LoadFacultyAsync(token);
        });
        UpdateActions();
    }
    private async Task SaveAsync() => await RunAsync(async token =>
    {
        if (faculty.SelectedItem is not FacultyChoice selected || selected.Id != activeFaculty)
            throw new ArgumentException("اختر اسم التدريسي من القائمة.");
        if (!enabled.Checked)
        {
            await service.Preferences.DeleteAsync(activeFaculty, activeSemester, revision, token);
            revision = 0; dirty = false;
            await RefreshFacultyListAsync(CancellationToken.None); await LoadFacultyAsync(CancellationToken.None);
            status.Text = "حُفظ بدون تفضيلات. أيام التدريسي متاحة وفق قواعد الجدولة العامة.";
            return;
        }
        var request = new FacultyScheduleRequest(activeFaculty, activeSemester, FacultyAvailabilityMode.Strict, [], TimeLimitSeconds: (double)seconds.Value)
        {
            UseDayPreferences = true,
            DayPreferences = days.Select(p => new FacultyDaySetting(p.Key, (FacultyPreferenceLevel)p.Value.SelectedValue!)).ToArray()
        };
        var result = await service.SaveDayPreferencesAndOptimizeAsync(request, revision, token);
        HasSavedChanges |= result.ChangedLectures > 0;
        // Mark clean before refreshing so a refresh failure cannot leave a stale revision.
        revision = result.Profile.Revision; dirty = false;
        await RefreshFacultyListAsync(CancellationToken.None); await LoadFacultyAsync(CancellationToken.None);
        changes.DataSource = Table(["التدريسي", "المادة", "الشعبة", "اليوم السابق", "الوقت السابق", "اليوم الجديد", "الوقت الجديد", "القاعة السابقة", "القاعة الجديدة"], result.Changes.Select(c => new object[]
            { c.Faculty, c.Subject, c.Section, ImportLabels.Display(c.OldDay), "\u200e" + c.OldPeriod, ImportLabels.Display(c.NewDay), "\u200e" + c.NewPeriod, c.OldRoom, c.NewRoom }));
        status.Text = result.Status == "NoTimetable" ? "حُفظت تفضيلات الأيام. ستُستخدم عند إنشاء الجدول؛ لا توجد محاضرات في هذا الفصل حالياً."
            : $"حُفظت التفضيلات وحُدّث الجدول: {result.ChangedLectures} محاضرة تغيّرت. " + (result.Status == "Optimal" ? "اكتمل التحسين ضمن النموذج." : "حُلّ صالح ضمن المهلة؛ لم تثبت الأمثلية.");
        if (result.ChangedLectures > 0) tabs.SelectedIndex = 1;
    });
    private async Task RefreshFacultyListAsync(CancellationToken token)
    {
        var configured = await service.GetConfiguredFacultyAsync(activeSemester, token); binding = true;
        try
        {
            faculty.Items.Clear(); faculty.Items.AddRange(lookups!.Faculty.Select(f => new FacultyChoice(f.Id, f.Name + (configured.Contains(f.Id) ? " — لديه تفضيلات" : " — بدون تفضيلات"))).Cast<object>().ToArray());
            faculty.SelectedItem = faculty.Items.Cast<FacultyChoice>().FirstOrDefault(f => f.Id == activeFaculty);
        }
        finally { binding = false; }
    }
    private bool CanDiscard() => !dirty || MessageBox.Show(this, "توجد تعديلات غير محفوظة. تجاهلها والمتابعة؟", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    private async Task ChangeSelectionAsync()
    {
        if (binding || busy || lookups is null) return;
        int nextFaculty = (faculty.SelectedItem as FacultyChoice)?.Id ?? activeFaculty, nextSemester = semester.SelectedIndex + 1;
        if (!CanDiscard())
        {
            binding = true; faculty.SelectedItem = faculty.Items.Cast<FacultyChoice>().FirstOrDefault(f => f.Id == activeFaculty); semester.SelectedIndex = activeSemester - 1; binding = false; return;
        }
        activeFaculty = nextFaculty; activeSemester = nextSemester;
        await RunAsync(async token => { await RefreshFacultyListAsync(token); await LoadFacultyAsync(token); });
    }
    private async Task LoadFacultyAsync(CancellationToken token)
    {
        if (activeFaculty == 0) { status.Text = "أضف التدريسيين أولًا."; return; }
        var profile = await service.Preferences.GetAsync(activeFaculty, activeSemester, token);
        var rows = await service.GetCurrentAsync(activeFaculty, activeSemester, token);
        binding = true;
        try
        {
            revision = profile?.Revision ?? 0; enabled.Checked = profile is not null;
            foreach (var day in days)
            {
                var settings = profile?.Settings;
                var level = settings?.UseDayPreferences == true ? settings.DayPreferences.Single(p => p.Day == day.Key).Level
                    : settings is null ? FacultyPreferenceLevel.Preferred
                    : settings.UnavailableDays!.Contains(day.Key) || settings.Mode == FacultyAvailabilityMode.Strict && !settings.SelectedDays.Contains(day.Key) ? FacultyPreferenceLevel.Prohibited
                    : settings.UsePreferenceMatrix && settings.PeriodPreferences.Where(p => p.Day == day.Key).Any(p => p.Level == FacultyPreferenceLevel.StronglyPreferred) ? FacultyPreferenceLevel.StronglyPreferred
                    : FacultyPreferenceLevel.Preferred;
                day.Value.SelectedValue = level; PaintDay(day.Value);
            }
            current.DataSource = Table(["المادة", "الشعبة", "نوع المحاضرة", "المجموعة", "اليوم", "الوقت", "القاعة"], rows.Select(r => new object[]
                { r.Subject, r.Section, r.LectureType == "Practical" ? "عملي" : "نظري", r.GroupName ?? "—", ImportLabels.Display(r.Day), "\u200e" + r.Period, r.Room }));
            changes.DataSource = null; dirty = false; tabs.SelectedIndex = 0;
            profileStatus.Text = profile is not null && !profile.Settings.UseDayPreferences
                ? "توجد إعدادات قديمة تتضمن أوقاتاً. عند الحفظ ستُستبدل بتفضيلات الأيام المعروضة فقط؛ راجعها أولاً."
                : "التفضيلات اختيارية، ولا يلزم إدخالها للجميع. الحفظ يُحسّن جدول الفصل ويحافظ على المحاضرات المثبتة.";
            status.Text = profile is null ? "بدون تفضيلات محفوظة. فعّل تحديد التفضيلات لإدخال الأيام؛ لا تُستنتج التفضيلات من الجدول الحالي."
                : "التفضيلات المحفوظة محمّلة. يمكنك تعديل الأيام ثم حفظها لتحديث الجدول.";
        }
        finally { binding = false; }
        UpdateActions();
    }
    private void Edited()
    {
        if (binding || busy) return;
        dirty = true; status.Text = "تعديلات غير محفوظة. عند الحفظ قد تتحرك محاضرات تدريسيين آخرين لتحقيق التفضيلات ومنع التعارض.";
        UpdateActions();
    }
    private async Task RunAsync(Func<CancellationToken, Task> work)
    {
        if (busy) return; busy = true; cancellation = new(); filters.Enabled = enabled.Enabled = false; UseWaitCursor = true; UpdateActions();
        try { await work(cancellation.Token); }
        catch (OperationCanceledException) { status.Text = "أُلغيت العملية. لم يُحفظ تغيير جزئي للتفضيلات أو الجدول."; }
        catch (Exception ex) { status.Text = ex.Message; MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { busy = false; cancellation.Dispose(); cancellation = null; filters.Enabled = enabled.Enabled = true; UseWaitCursor = false; UpdateActions(); }
    }
    private void UpdateActions()
    {
        save.Enabled = reload.Enabled = !busy && activeFaculty > 0; cancel.Enabled = busy; close.Enabled = !busy;
        dayCards.Enabled = !busy && enabled.Checked; save.Text = enabled.Checked ? "حفظ التفضيلات وتحديث الجدول" : "حفظ بدون تفضيلات";
    }
    private static void PaintDay(ComboBox choice)
    {
        choice.BackColor = choice.SelectedValue is FacultyPreferenceLevel level ? level switch
        { FacultyPreferenceLevel.StronglyPreferred => Color.FromArgb(219, 242, 229), FacultyPreferenceLevel.Prohibited => Color.FromArgb(253, 229, 228), _ => Color.White } : Color.White;
    }
    private void AddTab(string title, Control control) { var tab = new TabPage(title) { BackColor = Color.White, Padding = new(6) }; tab.Controls.Add(control); tabs.TabPages.Add(tab); }
    private static ComboBox Combo(string name, int width) => new() { Name = name, Width = width, DropDownStyle = ComboBoxStyle.DropDownList, IntegralHeight = false, DropDownHeight = 240 };
    private static Button Action(string text, string name) => new() { Name = name, Text = text, FlatStyle = FlatStyle.Flat, BackColor = Color.White, Margin = new(4), UseVisualStyleBackColor = false };
    private static Control Field(string title, Control control)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new(6, 0, 6, 5) };
        panel.Controls.Add(new Label { Text = title, AutoSize = true, ForeColor = Color.FromArgb(91, 108, 122), Margin = new(3, 0, 3, 5) }); panel.Controls.Add(control); return panel;
    }
    private static DataGridView Grid(string name) => new()
    {
        Name = name, Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells, BackgroundColor = Color.White,
        BorderStyle = BorderStyle.None, ColumnHeadersHeight = 42, RowTemplate = { Height = 34 }, AutoGenerateColumns = true,
        EnableHeadersVisualStyles = false, ColumnHeadersDefaultCellStyle = new() { BackColor = Color.FromArgb(237, 243, 246), ForeColor = Color.FromArgb(55, 78, 93), Padding = new(6) },
        AlternatingRowsDefaultCellStyle = new() { BackColor = Color.FromArgb(247, 250, 251) }, DefaultCellStyle = new() { SelectionBackColor = Color.FromArgb(220, 238, 231), SelectionForeColor = Color.FromArgb(25, 52, 67), Padding = new(4) }
    };
    private static DataTable Table(string[] columns, IEnumerable<object[]> rows) { var table = new DataTable(); foreach (string column in columns) table.Columns.Add(column); foreach (var row in rows) table.Rows.Add(row); return table; }
}
