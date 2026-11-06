using System.Globalization;
using Guna.UI2.WinForms;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;

namespace University_Timetable_and_Classroom_Management_System;

internal sealed class StudentListPage : UserControl
{
    private static readonly string[] StageNames = ["الأولى", "الثانية", "الثالثة", "الرابعة"];
    private static readonly Color Ink = DesktopPalette.Ink;
    private static readonly Color Accent = DesktopPalette.Primary;
    private static readonly Color Border = DesktopPalette.Border;
    private static readonly StringComparer StudentNameOrder = StringComparer.Create(CultureInfo.GetCultureInfo("ar-IQ"), ignoreCase: true);
    private readonly StudentService service = new();
    private readonly Guna2TextBox nameEditor = new()
    {
        AccessibleName = "اسم الطالب", Size = new Size(225, 42), MaxLength = 150, Margin = new Padding(0),
        BorderRadius = DesktopCorners.Field, BorderColor = DesktopPalette.FieldBorder, PlaceholderText = "أدخل اسم الطالب",
        ForeColor = Ink, PlaceholderForeColor = Accent, FocusedState = { BorderColor = Accent },
        Font = new Font("Segoe UI", 11F)
    };
    private readonly DataGridView students = new();
    private readonly DataGridViewCheckBoxColumn promotionColumn = new() { Name = "colStudentPromotion", HeaderText = "الترقية", Width = 92, AutoSizeMode = DataGridViewAutoSizeColumnMode.None };
    private readonly Label heading = new() { AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 0, 0, 12) };
    private readonly Label summary = new() { Name = "lblStudentSummary", AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 4, 0, 10) };
    private readonly Guna2Button editName = MakeButton("تعديل", 92, Accent);
    private readonly Guna2Button delete = MakeButton("حذف", 92, DesktopPalette.Danger);
    private readonly Guna2Button split = MakeButton("تقسيم الشعبة", 150);
    private readonly Guna2Button export = MakeButton("تصدير الطلبة", 130);
    private readonly Guna2Button add = MakeButton("إضافة طالب", 112, DesktopPalette.Primary);
    private readonly Guna2Button import = MakeButton("استيراد Excel أو CSV", 163);
    private readonly Guna2Button checkAll = MakeButton("تحديد الكل", 104);
    private readonly Guna2Button clearAll = MakeButton("إلغاء تحديد الكل", 128);
    private readonly Guna2Button promote = MakeButton("ترقية المحددين", 145, DesktopPalette.Primary);
    private readonly Guna2Button assign = MakeButton("توزيع المحددين", 137, DesktopPalette.Primary);
    private readonly Guna2ComboBox stageFilter = MakeCombo("cmbStudentStage", "المرحلة الدراسية");
    private readonly Guna2ComboBox groupFilter = MakeCombo("cmbStudentGroup", "الشعبة أو التخصص");
    private readonly Guna2ComboBox destination = MakeCombo("cmbStudentDestination", "شعبة أو تخصص الوجهة");
    private readonly Label destinationLabel = Caption("تخصص الطلبة بعد الترقية");
    private readonly TableLayoutPanel promotionActions = new()
    {
        Name = "pnlStudentPromotion", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 2, 10, 4), Padding = new Padding(8, 5, 8, 2),
        BackColor = DesktopPalette.SoftAccent
    };
    private readonly HashSet<int> excluded = [];
    private List<StudentService.Group> groups = [];
    private List<StudentService.Group> nextGroups = [];
    private string subgroupSummary = "";
    private bool loaded;
    private bool busy;
    private bool changingChecks;
    private int stage = 1;

    public StudentListPage(Control navigation)
    {
        RightToLeft = RightToLeft.Yes;
        BackColor = DesktopPalette.Canvas;
        ForeColor = Ink;
        Font = new Font("Segoe UI", 12F);
        Padding = new Padding(28, 24, 28, 28);
        ConfigureStudentGrid();
        heading.Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold);
        summary.Font = new Font("Segoe UI", 10F);
        summary.ForeColor = DesktopPalette.Primary;
        stageFilter.Width = 160;
        stageFilter.Items.AddRange(StageNames.Select(name => $"المرحلة {name}").ToArray());
        stageFilter.SelectedIndex = Stage - 1;
        var editorLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1, RowCount = 3, BackColor = Color.White, Margin = new Padding(0)
        };
        editorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++) editorLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editorLayout.Controls.Add(heading, 0, 0);
        foreach (var button in new[] { add, import }) button.Margin = new Padding(6, 28, 6, 0);
        editorLayout.Controls.Add(MakeBar(Field("اسم الطالب", nameEditor), add, import), 0, 1);
        promote.Name = "btnPromoteStudents";
        destinationLabel.Name = "lblStudentPromotionDestination";
        destinationLabel.Dock = DockStyle.Fill;
        destinationLabel.TextAlign = ContentAlignment.MiddleLeft;
        promotionActions.Controls.Add(destinationLabel, 0, 0);
        var promotionControls = MakeBar(destination, promote, assign);
        promotionControls.BackColor = Color.Transparent;
        promotionActions.Controls.Add(promotionControls, 0, 1);
        editorLayout.Controls.Add(MakeBar(promotionActions, checkAll, clearAll), 0, 2);
        var editor = MakeCard("pnlStudentEditor");
        editor.AutoSize = true;
        editor.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        editor.Margin = new Padding(0, 0, 0, 10);
        editor.Controls.Add(editorLayout);

        var table = MakeCard("pnlStudentsTable");
        var tableLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.White, Margin = new Padding(0)
        };
        tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        editName.Name = "btnEditStudentName";
        editName.AccessibleName = "تعديل اسم الطالب";
        delete.Name = "btnDeleteStudent";
        delete.AccessibleName = "حذف الطالب المحدد";
        delete.HoverState.FillColor = DesktopPalette.DangerHover;
        split.Name = "btnSplitStudentSection";
        export.Name = "btnExportStudents";
        editName.Margin = delete.Margin = split.Margin = export.Margin = new Padding(6, 28, 6, 0);
        var filterRow = MakeBar(Field("المرحلة الدراسية", stageFilter), Field("الشعبة أو التخصص", groupFilter), split, export, editName, delete);
        filterRow.Name = "pnlStudentListFilters";
        tableLayout.Controls.Add(filterRow, 0, 0);
        tableLayout.Controls.Add(summary, 0, 1);
        tableLayout.Controls.Add(students, 0, 2);
        table.Controls.Add(tableLayout);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(editor, 0, 0);
        layout.Controls.Add(navigation, 0, 1);
        layout.Controls.Add(table, 0, 2);
        Controls.Add(layout);

        students.SelectionChanged += (_, _) => UpdateRowActionsAvailability();
        editName.Click += async (_, _) => await EditNameAsync();
        delete.Click += async (_, _) => await DeleteStudentAsync();
        split.Click += async (_, _) => await SplitSectionAsync();
        export.Click += async (_, _) => await RunAsync(ExportStudentsAsync);
        students.CellClick += async (_, cell) =>
        {
            if (cell.ColumnIndex == students.Columns["colStudentSection"].Index)
                await ChangeSectionAsync(cell.RowIndex);
        };
        students.KeyDown += async (_, key) =>
        {
            if (key.KeyCode != Keys.Enter || students.CurrentCell is not DataGridViewLinkCell cell) return;
            key.SuppressKeyPress = true;
            await ChangeSectionAsync(cell.RowIndex);
        };
        students.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (students.IsCurrentCellDirty && students.CurrentCell?.OwningColumn == promotionColumn)
                students.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        students.CellValueChanged += (_, change) =>
        {
            if (changingChecks || busy || change.RowIndex < 0 || change.ColumnIndex != promotionColumn.Index) return;
            var row = students.Rows[change.RowIndex];
            if (row.Tag is not int studentId) return;
            if (row.Cells[promotionColumn.Index].Value is true) excluded.Remove(studentId);
            else excluded.Add(studentId);
            UpdateSummary(SelectedIds().Length);
        };
        add.Click += async (_, _) => await RunAsync(async () =>
        {
            await service.AddAsync(nameEditor.Text, Stage, SelectedGroup);
            if (IsDisposed || Disposing) return;
            nameEditor.Clear();
            await ReloadAsync();
        });
        stageFilter.SelectedIndexChanged += async (_, _) =>
        {
            int selectedStage = stageFilter.SelectedIndex + 1;
            if (!busy && selectedStage > 0 && selectedStage != Stage)
                await OpenStageAsync(selectedStage, null);
        };
        groupFilter.SelectedIndexChanged += async (_, _) =>
        {
            if (busy || !loaded) return;
            BindDestination();
            await RunAsync(() => ReloadAsync());
        };
        destination.SelectedIndexChanged += (_, _) => UpdateSummary(SelectedIds().Length);
        assign.Click += async (_, _) => await AssignAsync();
        checkAll.Click += (_, _) => SetChecks(true);
        clearAll.Click += (_, _) => SetChecks(false);
        import.Click += async (_, _) => await ImportAsync();
        promote.Click += async (_, _) => await PromoteAsync();
        SetBusy(false);
    }

    private void ConfigureStudentGrid()
    {
        students.Name = "dgvStudents";
        students.AccessibleName = "جدول الطلبة وعلامات الترقية";
        students.Dock = DockStyle.Fill;
        students.Margin = new Padding(0);
        students.BackgroundColor = Color.White;
        students.BorderStyle = BorderStyle.None;
        students.GridColor = DesktopPalette.Border;
        students.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        students.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        students.EnableHeadersVisualStyles = false;
        students.RowHeadersVisible = false;
        students.AllowUserToAddRows = students.AllowUserToDeleteRows = students.AllowUserToResizeRows = false;
        students.MultiSelect = false;
        students.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        students.EditMode = DataGridViewEditMode.EditOnEnter;
        students.ColumnHeadersHeight = students.RowTemplate.Height = 48;
        students.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        students.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = DesktopPalette.Header, ForeColor = Color.White,
            SelectionBackColor = DesktopPalette.Header, SelectionForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold)
        };
        students.DefaultCellStyle = new DataGridViewCellStyle
        {
            Font = new Font("Segoe UI", 11F), BackColor = Color.White, ForeColor = Ink,
            SelectionBackColor = DesktopPalette.Selection, SelectionForeColor = Ink
        };
        students.AlternatingRowsDefaultCellStyle.BackColor = DesktopPalette.Canvas;
        promotionColumn.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        students.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "ت", Width = 46, ReadOnly = true },
            promotionColumn,
            new DataGridViewTextBoxColumn
            {
                Name = "colStudentName", HeaderText = "اسم الطالب", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 65, MinimumWidth = 140, ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle { Font = new Font("Segoe UI", 12F) }
            },
            new DataGridViewTextBoxColumn { Name = "colStudentSection", HeaderText = "الشعبة أو التخصص", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 35, MinimumWidth = 130, ReadOnly = true },
            new DataGridViewTextBoxColumn { HeaderText = "المعرّف", Width = 86, ReadOnly = true });
        foreach (DataGridViewColumn column in students.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
    }

    private int Stage => stage;
    private int SelectedGroup => (groupFilter.SelectedItem as StudentService.Group)?.Id ?? 0;
    private bool IsSectionSelected => Stage <= 2 && SelectedGroup > 0;
    private StudentService.Group? OtherSection => IsSectionSelected
        ? groups.FirstOrDefault(group => group.Id != SelectedGroup) : null;
    private StudentService.Group? SelectedDestination => destination.SelectedItem is StudentService.Group { Id: > 0 } group
        ? group : null;

    public Task LoadOnceAsync(bool refresh = false) => loaded && !refresh
        ? Task.CompletedTask : RunAsync(() => LoadStageAsync(SelectedGroup));

    public Task OpenStageAsync(int selectedStage, int? groupId)
    {
        if (busy) return Task.CompletedTask;
        if (stage != selectedStage) excluded.Clear();
        stage = selectedStage;
        loaded = false;
        return RunAsync(async () =>
        {
            stageFilter.SelectedIndex = Stage - 1;
            await LoadStageAsync(groupId);
        });
    }

    private async Task LoadStageAsync(int? groupId)
    {
        groups = await service.GroupsAsync(Stage);
        nextGroups = Stage == 2 ? await service.GroupsAsync(3) : [];
        if (IsDisposed || Disposing) return;
        groupFilter.Items.Clear();
        groupFilter.Items.AddRange([.. groups, new StudentService.Group(-1, "غير موزّعين")]);
        groupFilter.SelectedItem = groupFilter.Items.Cast<StudentService.Group>().FirstOrDefault(group => group.Id == groupId)
            ?? groupFilter.Items[0];
        BindDestination();
        await ReloadAsync();
        if (IsDisposed || Disposing) return;
        loaded = true;
    }

    private void BindDestination()
    {
        destination.Items.Clear();
        bool distributing = SelectedGroup == -1;
        destination.Items.AddRange([new StudentService.Group(0, distributing ? "اختر شعبة أو تخصصًا" : "اختر تخصص الترقية"),
            .. (distributing ? groups : nextGroups)]);
        destination.SelectedIndex = 0;
    }

    private async Task ReloadAsync()
    {
        students.Rows.Clear();
        subgroupSummary = "";
        var rows = await service.ListAsync(Stage);
        if (IsDisposed || Disposing) return;
        var visibleRows = rows.Where(row => SelectedGroup == -1 ? row.SectionID == null : row.SectionID == SelectedGroup)
            .OrderBy(row => row.PracticalGroupNumber ?? 3).ThenBy(row => row.FullName, StudentNameOrder)
            .ThenBy(row => row.StudentID).ToArray();
        string label = groupFilter.SelectedItem is StudentService.Group { Id: > 0 } group ? group.Label : "غير موزّع";
        string sectionCode = SectionCode(label);
        if (IsSectionSelected && visibleRows.Any(row => row.PracticalGroupNumber.HasValue))
        {
            int first = visibleRows.Count(row => row.PracticalGroupNumber == 1);
            int second = visibleRows.Count(row => row.PracticalGroupNumber == 2);
            int unassigned = visibleRows.Length - first - second;
            subgroupSummary = $"   |   {sectionCode}1: {first}   |   {sectionCode}2: {second}";
            if (unassigned > 0) subgroupSummary += $"   |   دون مجموعة: {unassigned}";
        }
        var otherSection = OtherSection;
        students.SuspendLayout();
        try
        {
            foreach (var row in visibleRows)
            {
                string rowLabel = row.PracticalGroupNumber is int number ? $"{sectionCode}{number}" : label;
                int index = students.Rows.Add(students.Rows.Count + 1, !excluded.Contains(row.StudentID), row.FullName, rowLabel, row.StudentID);
                var gridRow = students.Rows[index];
                gridRow.Tag = row.StudentID;
                if (otherSection is not null)
                {
                    gridRow.Cells["colStudentSection"] = new DataGridViewLinkCell
                    {
                        Value = rowLabel, LinkColor = Accent, ActiveLinkColor = Accent, TrackVisitedState = false,
                        ToolTipText = "اضغط لتغيير شعبة الطالب"
                    };
                }
            }
            students.ClearSelection();
        }
        finally { students.ResumeLayout(); }
    }

    private int[] SelectedIds() => students.Rows.Cast<DataGridViewRow>()
        .Where(row => row.Cells[promotionColumn.Index].Value is true)
        .Select(row => (int)row.Tag!).ToArray();

    private async Task ChangeSectionAsync(int rowIndex)
    {
        if (busy || rowIndex < 0 || OtherSection is not { } target) return;
        var row = students.Rows[rowIndex];
        int id = (int)row.Tag!;
        int source = SelectedGroup;
        var current = (StudentService.Group)groupFilter.SelectedItem!;
        if (UiMessages.Confirm(this, $"نقل {row.Cells["colStudentName"].Value} من {current.Label} إلى {target.Label}؟ سيظهر في الشعبة الجديدة دون مجموعة فرعية.",
                "تغيير شعبة الطالب") != DialogResult.Yes) return;

        await RunAsync(async () =>
        {
            await service.ChangeSectionAsync(Stage, id, source, target.Id);
            if (IsDisposed || Disposing) return;
            await ReloadAsync();
        });
    }

    private async Task SplitSectionAsync()
    {
        if (busy || !IsSectionSelected || students.Rows.Count < 2) return;
        var section = (StudentService.Group)groupFilter.SelectedItem!;
        string sectionCode = SectionCode(section.Label);
        if (UiMessages.Confirm(this,
                $"توزيع جميع طلبة {section.Label} عشوائيًا على {sectionCode}1 و{sectionCode}2؟ سيُستبدل التقسيم السابق لهذه الشعبة إن وُجد.",
                "تقسيم الشعبة") != DialogResult.Yes) return;

        await RunAsync(async () =>
        {
            await service.SplitSectionAsync(Stage, section.Id);
            if (IsDisposed || Disposing) return;
            await ReloadAsync();
        });
    }

    private static string SectionCode(string label) => label.StartsWith("شعبة ", StringComparison.Ordinal) ? label["شعبة ".Length..] : label;

    private async Task EditNameAsync()
    {
        if (busy || students.SelectedRows.Count != 1) return;
        var row = students.SelectedRows[0];
        int id = (int)row.Tag!;
        string currentName = (string)row.Cells["colStudentName"].Value;
        using var dialog = new Form
        {
            Name = "dlgEditStudentName", Text = "تعديل اسم الطالب", Font = Font, ForeColor = Ink, BackColor = Color.White,
            RightToLeft = RightToLeft.Yes, RightToLeftLayout = true, StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog, ShowInTaskbar = false, MaximizeBox = false, MinimizeBox = false,
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(20)
        };
        var input = new Guna2TextBox
        {
            Name = "txtEditedStudentName", AccessibleName = "اسم الطالب", Text = currentName, Font = nameEditor.Font,
            Size = new Size(340, 42), MaxLength = nameEditor.MaxLength, BorderRadius = DesktopCorners.Field,
            BorderColor = DesktopPalette.FieldBorder, ForeColor = Ink, FocusedState = { BorderColor = Accent }
        };
        var save = new Button { Name = "save", Text = "حفظ الاسم", Size = new Size(112, 42), DialogResult = DialogResult.OK, Enabled = false };
        var cancel = new Button { Text = "إلغاء", Size = new Size(90, 42), DialogResult = DialogResult.Cancel };
        input.TextChanged += (_, _) => save.Enabled = input.Text.Trim().Length > 0 && input.Text.Trim() != currentName;
        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 2 };
        fields.Controls.Add(Field("اسم الطالب", input), 0, 0);
        fields.Controls.Add(MakeBar(save, cancel), 0, 1);
        dialog.Controls.Add(fields);
        dialog.AcceptButton = save;
        dialog.CancelButton = cancel;
        DialogAppearance.Apply(dialog);
        dialog.Shown += (_, _) => { input.Focus(); input.SelectAll(); };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        await RunAsync(async () =>
        {
            await service.RenameAsync(id, input.Text);
            if (IsDisposed || Disposing) return;
            await ReloadAsync();
            var edited = students.Rows.Cast<DataGridViewRow>().FirstOrDefault(candidate => Equals(candidate.Tag, id));
            if (edited is not null) students.CurrentCell = edited.Cells["colStudentName"];
        });
    }

    private async Task DeleteStudentAsync()
    {
        if (busy || !loaded || students.SelectedRows.Count != 1) return;
        var row = students.SelectedRows[0];
        int id = (int)row.Tag!;
        int currentStage = Stage;
        int? sectionId = SelectedGroup == -1 ? null : SelectedGroup;
        string name = (string)row.Cells["colStudentName"].Value;
        if (UiMessages.Confirm(this, $"هل تريد حذف الطالب «{name}» نهائيًا؟", "حذف الطالب", MessageBoxDefaultButton.Button2)
            != DialogResult.Yes) return;

        await RunAsync(async () =>
        {
            await service.DeleteAsync(id, currentStage, sectionId);
            excluded.Remove(id);
            if (IsDisposed || Disposing) return;
            await ReloadAsync();
        });
    }

    private async Task ImportAsync()
    {
        int stage = Stage;
        var group = (StudentService.Group)groupFilter.SelectedItem!;
        using var dialog = new OpenFileDialog
        {
            Title = $"استيراد طلبة المرحلة {StageNames[stage - 1]} – {group.Label}",
            Filter = "Excel وCSV (*.xlsx;*.csv)|*.xlsx;*.csv",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        await RunAsync(async () =>
        {
            var (added, skipped) = await service.ImportAsync(dialog.FileName, stage, group.Id);
            if (IsDisposed || Disposing) return;
            await ReloadAsync();
            UiMessages.ShowInformation(this, $"أُضيف {added} إلى المرحلة {StageNames[stage - 1]}. تم تجاوز {skipped} من الأسماء المكررة.", "الطلبة");
        });
    }

    private Task ExportStudentsAsync()
    {
        var rows = students.Rows.Cast<DataGridViewRow>().Select(row => new RosterRow(
            (string)row.Cells["colStudentName"].Value, (string)row.Cells["colStudentSection"].Value)).ToArray();
        return RosterExportDialog.ShowAsync(this, $"{heading.Text} - {groupFilter.SelectedItem}", "اسم الطالب", "الشعبة أو التخصص", rows);
    }

    private async Task PromoteAsync()
    {
        int stage = Stage;
        if (stage is < 1 or >= 4) return;
        var ids = SelectedIds();
        if (ids.Length == 0) return;
        var target = stage == 2 ? SelectedDestination : null;
        if (stage == 2 && target is null) return;
        string targetLabel = target is null ? "" : $" ({target.Label})";
        var confirmation = UiMessages.Confirm(this,
            $"ترقية {ids.Length} من المرحلة {StageNames[stage - 1]} إلى {StageNames[stage]}{targetLabel}؟ سيبقى {students.Rows.Count - ids.Length} من القائمة المعروضة في المرحلة الحالية.",
            "تأكيد ترقية الطلبة");
        if (confirmation != DialogResult.Yes) return;

        await RunAsync(async () =>
        {
            int moved = await service.PromoteAsync(stage, ids, target?.Id);
            if (IsDisposed || Disposing) return;
            await ReloadAsync();
            UiMessages.ShowInformation(this, $"تمت ترقية {moved} إلى المرحلة {StageNames[stage]}.", "الطلبة");
        });
    }

    private async Task AssignAsync()
    {
        var target = SelectedDestination;
        var ids = SelectedIds();
        if (SelectedGroup != -1 || target is null || ids.Length == 0) return;
        if (UiMessages.Confirm(this, $"توزيع {ids.Length} طالبًا إلى {target.Label}؟", "توزيع الطلبة") != DialogResult.Yes) return;
        await RunAsync(async () =>
        {
            int assigned = await service.AssignGroupAsync(Stage, ids, target.Id);
            if (IsDisposed || Disposing) return;
            await ReloadAsync();
            UiMessages.ShowInformation(this, $"تم توزيع {assigned} طالبًا إلى {target.Label}.", "الطلبة");
        });
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (busy || IsDisposed || Disposing) return;
        SetBusy(true);
        try { await action(); }
        catch (Exception ex) { UiMessages.ShowError(this, "تعذرت العملية على سجل الطلبة.", "الطلبة", ex); }
        finally
        {
            if (!IsDisposed && !Disposing) SetBusy(false);
        }
    }

    private void SetBusy(bool value)
    {
        busy = value;
        stageFilter.Enabled = !value;
        groupFilter.Enabled = !value && loaded;
        import.Enabled = add.Enabled = nameEditor.Enabled = !value && loaded && SelectedGroup > 0;
        students.Enabled = !value;
        UpdateRowActionsAvailability();
        UpdateSummary(SelectedIds().Length);
    }

    private void UpdateRowActionsAvailability() => editName.Enabled = delete.Enabled = !busy && loaded && students.SelectedRows.Count == 1;

    private void SetChecks(bool value)
    {
        changingChecks = true;
        try
        {
            foreach (DataGridViewRow row in students.Rows)
            {
                int id = (int)row.Tag!;
                if (value) excluded.Remove(id);
                else excluded.Add(id);
                row.Cells[promotionColumn.Index].Value = value;
            }
        }
        finally { changingChecks = false; }
        UpdateSummary(value ? students.Rows.Count : 0);
    }

    private void UpdateSummary(int checkedCount)
    {
        bool distributing = SelectedGroup == -1;
        heading.Text = $"طلبة المرحلة {StageNames[Stage - 1]}";
        split.Visible = IsSectionSelected;
        split.Enabled = !busy && loaded && IsSectionSelected && students.Rows.Count >= 2;
        export.Enabled = !busy && loaded && students.Rows.Count > 0;
        split.Text = IsSectionSelected ? $"تقسيم {groupFilter.SelectedItem}" : "تقسيم الشعبة";
        bool choosingPromotionMajor = Stage == 2 && !distributing;
        promotionActions.Visible = Stage < 4 || distributing;
        promotionActions.BackColor = choosingPromotionMajor || distributing ? DesktopPalette.SoftAccent : Color.White;
        destination.Visible = destinationLabel.Visible = distributing || choosingPromotionMajor;
        destinationLabel.Text = distributing ? "توزيع الطلبة في المرحلة الحالية" : "الترقية إلى المرحلة الثالثة · تخصص الطلبة بعد الترقية";
        destination.AccessibleName = distributing ? "شعبة أو تخصص التوزيع" : "تخصص الطلبة بعد الترقية";
        destination.Enabled = !busy && destination.Items.Count > 1;
        assign.Visible = distributing;
        assign.Enabled = !busy && distributing && checkedCount > 0 && SelectedDestination is not null;
        checkAll.Enabled = clearAll.Enabled = !busy && (Stage < 4 || distributing) && students.Rows.Count > 0;
        promote.Visible = Stage < 4 && !(Stage == 2 && distributing);
        promote.Text = choosingPromotionMajor ? "ترقية إلى الثالثة" : "ترقية المحددين";
        promote.Enabled = !busy && Stage < 4 && !(Stage == 2 && distributing) &&
            checkedCount > 0 && (!choosingPromotionMajor || SelectedDestination is not null);
        summary.Text = busy ? $"جارٍ تحميل بيانات المرحلة {StageNames[Stage - 1]}…" : Stage == 4
            ? $"عدد الطلبة: {students.Rows.Count}. المرحلة الرابعة هي المرحلة الأخيرة."
            : $"الطلبة: {students.Rows.Count}   |   المحددون: {checkedCount}{subgroupSummary}" +
                (distributing ? "   |   اختر وجهة لتوزيع الطلبة المحددين." : "   |   أزل علامة الصح أمام الراسبين قبل الترقية.");
    }

    private static ResponsiveFlowPanel MakeBar(params Control[] controls)
    {
        var bar = new ResponsiveFlowPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, WrapContents = true,
            BackColor = Color.White, Margin = new Padding(0, 0, 0, 4)
        };
        bar.Controls.AddRange(controls);
        return bar;
    }

    private static Label Caption(string text) => new()
    {
        Text = text, AutoSize = true, ForeColor = Ink, Margin = new Padding(0, 0, 0, 5),
        Font = new Font("Segoe UI Semibold", 10F)
    };

    private static Control Field(string text, Control input)
    {
        var field = new TableLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 2,
            Margin = new Padding(0, 0, 8, 8)
        };
        var label = Caption(text);
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleLeft;
        field.Controls.Add(label, 0, 0);
        field.Controls.Add(input, 0, 1);
        return field;
    }

    private static ResponsiveCardPanel MakeCard(string name) => new()
    {
        Name = name, Dock = DockStyle.Fill, BackColor = Color.Transparent, FillColor = Color.White,
        BorderColor = DesktopPalette.Border, BorderRadius = DesktopCorners.Card, BorderThickness = 1,
        Margin = new Padding(0), Padding = new Padding(24, 18, 24, 18)
    };

    private static Guna2ComboBox MakeCombo(string name, string label) => new()
    {
        Name = name, AccessibleName = label, DropDownStyle = ComboBoxStyle.DropDownList,
        Size = new Size(208, 42), ItemHeight = 36, Margin = new Padding(0),
        Font = new Font("Segoe UI", 11F), BorderColor = DesktopPalette.FieldBorder, BorderRadius = DesktopCorners.Field, ForeColor = Ink,
        FocusedState = { BorderColor = Accent }
    };

    private static Guna2Button MakeButton(string text, int width, Color? fill = null) => new()
    {
        Text = text, Size = new Size(width, 42), Margin = new Padding(4, 0, 4, 6), Cursor = Cursors.Hand,
        BorderRadius = DesktopCorners.Button, BorderThickness = fill.HasValue ? 0 : 1, BorderColor = Border,
        FillColor = fill ?? Color.White, ForeColor = fill.HasValue ? Color.White : Ink,
        DisabledState = { FillColor = DesktopPalette.DisabledSurface, ForeColor = Accent, BorderColor = Border },
        Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold)
    };
}
