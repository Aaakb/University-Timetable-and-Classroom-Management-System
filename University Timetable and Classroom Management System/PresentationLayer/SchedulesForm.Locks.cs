namespace University_Timetable_and_Classroom_Management_System;

public partial class SchedulesForm
{
    private bool isSavingScheduleLock;

    private async Task SaveScheduleLockCheckboxAsync()
    {
        if (IsScheduleUiUnavailable || isSavingScheduleLock || !dgvSchedules.IsCurrentCellDirty ||
            dgvSchedules.CurrentCell is not DataGridViewCheckBoxCell cell || cell.OwningColumn != colLockStatus ||
            cell.OwningRow.DataBoundItem is not ScheduleRow row) return;
        int scheduleId = row.ScheduleID;
        bool locked = Convert.ToBoolean(cell.EditedFormattedValue);
        isSavingScheduleLock = true;
        try
        {
            dgvSchedules.CommitEdit(DataGridViewDataErrorContexts.Commit);
            SetScheduleActionsEnabled(false);
            await scheduleService.SetLockedAsync(scheduleId, locked);
            if (IsScheduleUiUnavailable) return;
            await LoadSchedulesAsync();
            if (IsScheduleUiUnavailable) return;
            ClearScheduleForm();
        }
        catch (Exception ex)
        {
            if (!IsScheduleUiUnavailable)
            {
                try { await LoadSchedulesAsync(); }
                catch { /* Show the original failure even if reloading is unavailable. */ }
            }
            ShowError("تعذّر تغيير تثبيت المحاضرة.", ex);
        }
        finally { isSavingScheduleLock = false; SetScheduleActionsEnabled(true); }
    }
}
