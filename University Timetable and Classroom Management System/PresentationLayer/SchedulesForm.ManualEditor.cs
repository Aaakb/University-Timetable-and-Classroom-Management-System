namespace University_Timetable_and_Classroom_Management_System;

public partial class SchedulesForm
{
    private Form? manualScheduleDialog;

    private void ConfigureCompactScheduleWorkspace()
    {
        using var layout = new LayoutUpdateScope(this);
        pnlWorkspace.Controls.Add(pnlScheduleCommands);
        pnlScheduleCommands.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        pnlScheduleEditor.Visible = false;
        lblAutomationTitle.Visible = lblManualRecordsTitle.Visible = btnClearScheduleForm.Visible = false;
        btnAddSchedule.Text = "إضافة محاضرة يدويًا";
        btnAddSchedule.Width = WindowSizing.Pixels(this, 162);
    }

    private async Task ShowManualScheduleEditorAsync(bool edit)
    {
        if (IsScheduleUiUnavailable || manualScheduleDialog is not null) return;
        int scheduleId = 0;
        if (edit)
        {
            if (!TryGetSelectedScheduleId(out scheduleId))
            {
                ShowInformation("اختر محاضرة من الجدول قبل التعديل.");
                return;
            }
            txtScheduleId.Clear();
            SetScheduleActionsEnabled(false);
            try { await PopulateScheduleEditorFromSelectionAsync(); }
            finally { SetScheduleActionsEnabled(true); }
            if (IsScheduleUiUnavailable || txtScheduleId.Text != scheduleId.ToString()) return;
        }
        else
        {
            ClearScheduleForm();
        }

        using var dialog = new Form
        {
            Name = "manualScheduleDialog",
            Text = edit ? "تعديل محاضرة" : "إضافة محاضرة يدويًا",
            StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false,
            RightToLeft = RightToLeft.Yes, RightToLeftLayout = true,
            MinimizeBox = false, MaximizeBox = false, BackColor = DesktopPalette.Canvas,
            Font = new Font("Segoe UI", 10F)
        };
        var area = Screen.FromControl(this).WorkingArea;
        dialog.Size = new Size(Math.Min(WindowSizing.Pixels(this, 960), area.Width),
            Math.Min(WindowSizing.Pixels(this, 560), area.Height));
        WindowSizing.Attach(dialog);
        var viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = WindowSizing.Pixels(this, 64), Padding = new Padding(12, 8, 12, 8),
            BackColor = Color.White
        };
        var save = new Button { Name = "btnSaveManualSchedule", Text = edit ? "حفظ التعديل" : "إضافة المحاضرة", AutoSize = true, Height = 36 };
        var cancel = new Button { Name = "btnCancelManualSchedule", Text = "إلغاء", DialogResult = DialogResult.Cancel, AutoSize = true, Height = 36 };
        footer.Controls.AddRange([save, cancel]);
        dialog.Controls.Add(viewport);
        dialog.Controls.Add(footer);
        dialog.CancelButton = cancel;
        DialogAppearance.Apply(dialog);

        bool saving = false;
        dialog.FormClosing += (_, e) => e.Cancel = saving;
        save.Click += async (_, _) =>
        {
            if (saving || !TryBuildSchedule(out var schedule)) return;
            schedule.ScheduleID = scheduleId;
            saving = true;
            save.Enabled = cancel.Enabled = pnlScheduleEditor.Enabled = false;
            try
            {
                if (edit) await scheduleService.UpdateAsync(schedule);
                else await scheduleService.AddAsync(schedule);
                saving = false;
                dialog.DialogResult = DialogResult.OK;
            }
            catch (Exception ex) { ShowError("تعذر حفظ المحاضرة.", ex); }
            finally
            {
                saving = false;
                save.Enabled = cancel.Enabled = pnlScheduleEditor.Enabled = true;
            }
        };

        manualScheduleDialog = dialog;
        lblEditorTitle.Text = edit ? "بيانات المحاضرة المحددة" : "بيانات المحاضرة الجديدة";
        pnlScheduleEditor.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        viewport.BindingContext = pnlScheduleEditor.BindingContext;
        viewport.Controls.Add(pnlScheduleEditor);
        pnlScheduleEditor.Visible = true;
        viewport.Resize += (_, _) => LayoutManualScheduleEditor(viewport);
        LayoutManualScheduleEditor(viewport);
        DialogResult result;
        try { result = dialog.ShowDialog(this); }
        finally
        {
            manualScheduleDialog = null;
            pnlScheduleEditor.Visible = false;
            if (!IsScheduleUiUnavailable) pnlWorkspace.Controls.Add(pnlScheduleEditor);
        }
        if (result != DialogResult.OK || IsScheduleUiUnavailable) return;

        SetScheduleActionsEnabled(false);
        try
        {
            await LoadSchedulesAsync();
            ClearScheduleForm();
            ShowInformation(edit ? UiMessages.RecordUpdated : UiMessages.RecordAdded);
        }
        catch (Exception ex) { ShowError("حُفظت المحاضرة، لكن تعذر تحديث عرض الجدول.", ex); }
        finally { SetScheduleActionsEnabled(true); }
    }

    private void LayoutManualScheduleEditor(Panel viewport)
    {
        if (viewport.ClientSize.Width <= 0) return;
        int Px(int value) => WindowSizing.Pixels(viewport, value);
        int width = Math.Max(Px(300), viewport.ClientSize.Width - Px(40));
        int fieldsWidth = width - Px(36);
        pnlCourseStaff.Anchor = pnlAcademicPlacement.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        pnlCourseStaff.SetBounds(Px(18), Px(48), fieldsWidth, pnlCourseStaff.Height);
        LayoutScheduleFields(pnlCourseStaff, [(lblSubject, cmbSubject), (lblFacultyMember, cmbFacultyMember),
            (lblLectureType, cmbLectureType), (lblGroupName, cmbGroupName)]);
        pnlAcademicPlacement.SetBounds(Px(18), pnlCourseStaff.Bottom + Px(8), fieldsWidth, pnlAcademicPlacement.Height);
        LayoutScheduleFields(pnlAcademicPlacement, [(lblStudyYear, cmbStudyYear), (lblBranch, cmbBranch),
            (lblSection, cmbSection), (lblDayOfWeek, cmbDayOfWeek), (lblTimeSlot, cmbTimeSlot), (lblClassroom, cmbClassroom)]);
        var scroll = viewport.AutoScrollPosition;
        pnlScheduleEditor.SetBounds(Px(12) + scroll.X, Px(12) + scroll.Y, width, pnlAcademicPlacement.Bottom + Px(18));
        lblEditorTitle.Left = width - Px(18) - lblEditorTitle.Width;
    }
}
