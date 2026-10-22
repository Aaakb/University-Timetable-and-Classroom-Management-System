using System.Data;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;

namespace University_Timetable_and_Classroom_Management_System;

public partial class FacultyPreferencesForm : Form
{
    public bool HasSavedChanges { get; private set; }
    private readonly FacultyScheduleEditorService service = null!;
    private readonly Dictionary<string, ComboBox> days = [];
    private FacultyEditorLookups? lookups;
    private CancellationTokenSource? cancellation;
    private int activeFaculty, activeSemester = 1;
    private long revision;
    private bool binding, dirty, busy;
    private sealed record FacultyChoice(int Id, string Text);
    private sealed record LevelChoice(FacultyPreferenceLevel Value, string Text);
    private static readonly LevelChoice[] levels = [new(FacultyPreferenceLevel.StronglyPreferred, "مفضّل"), new(FacultyPreferenceLevel.Preferred, "مسموح"), new(FacultyPreferenceLevel.Prohibited, "غير متاح")];

    public FacultyPreferencesForm() : this(null) { }

    public FacultyPreferencesForm(FacultyScheduleEditorService? service)
    {
        InitializeComponent();
        semester.SelectedIndex = 0;
        if (DesignerEnvironment.IsDesignTime(this)) return;

        this.service = service ?? new FacultyScheduleEditorService();
        ConfigureDayPreferences();
        WireEvents();
        UpdateActions();
    }

    private void ConfigureDayPreferences()
    {
        days.Add("Sunday", dayPreferenceSunday);
        days.Add("Monday", dayPreferenceMonday);
        days.Add("Tuesday", dayPreferenceTuesday);
        days.Add("Wednesday", dayPreferenceWednesday);
        days.Add("Thursday", dayPreferenceThursday);
    }

    private void BindDayPreferences()
    {
        foreach (var choice in days.Values)
        {
            choice.DisplayMember = nameof(LevelChoice.Text);
            choice.ValueMember = nameof(LevelChoice.Value);
            choice.DataSource = levels.ToArray();
            choice.SelectedValue = FacultyPreferenceLevel.Preferred;
            choice.SelectedIndexChanged += (_, _) => { PaintDay(choice); Edited(); };
        }
    }

    private void WireEvents()
    {
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
            BindDayPreferences();
            await RefreshFacultyListAsync(token);
            if (faculty.Items.Count > 0) { binding = true; faculty.SelectedIndex = 0; activeFaculty = ((FacultyChoice)faculty.SelectedItem!).Id; binding = false; }
            await LoadFacultyAsync(token);
        });
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
    private static DataTable Table(string[] columns, IEnumerable<object[]> rows) { var table = new DataTable(); foreach (string column in columns) table.Columns.Add(column); foreach (var row in rows) table.Rows.Add(row); return table; }
}
