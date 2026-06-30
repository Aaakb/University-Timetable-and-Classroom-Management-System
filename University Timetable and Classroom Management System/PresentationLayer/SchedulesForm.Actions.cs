using System.Windows.Forms;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;

namespace University_Timetable_and_Classroom_Management_System
{
    public partial class SchedulesForm
    {
        private async Task GenerateScheduleAsync()
        {
            if (!ConfirmScheduleGeneration())
            {
                return;
            }

            SetScheduleActionsEnabled(false);

            try
            {
                var result = await scheduleService.GenerateAsync();
                await LoadSchedulesAsync();
                ShowInformation(BuildGenerationMessage(result));
            }
            catch (Exception ex)
            {
                ShowError("Unable to generate schedule.", ex);
            }
            finally
            {
                SetScheduleActionsEnabled(true);
            }
        }

        private bool ConfirmScheduleGeneration()
        {
            var confirmation = UiMessages.Confirm(
                this,
                "This will generate schedule records automatically. Continue?",
                "Generate Timetable");

            return confirmation == DialogResult.Yes;
        }

        private static string BuildGenerationMessage(ScheduleGenerationResult result)
        {
            var message = new List<string>
            {
                $"Generation completed. Created: {result.CreatedCount}, Skipped: {result.SkippedCount}.",
                $"Required subject-section lessons: {result.RequiredCount}.",
                $"Available setup: {result.TimeSlotCount} time slots, {result.ClassroomCount} classrooms, {result.SectionCount} sections."
            };

            if (result.AddedTimeSlotCount > 0 || result.AddedClassroomCount > 0)
            {
                message.Add($"Auto-added resources: {result.AddedTimeSlotCount} time slot(s), {result.AddedClassroomCount} classroom/lab(s).");
            }

            if (result.AddedFacultyMemberCount > 0 || result.AddedFacultySubjectAssignmentCount > 0)
            {
                message.Add($"Auto-added teaching coverage: {result.AddedFacultyMemberCount} faculty member(s), {result.AddedFacultySubjectAssignmentCount} subject assignment(s).");
            }

            if (result.SkippedCount == 0 && result.UnassignedSubjectsCount == 0)
            {
                message.Add("The generated schedule is complete.");
                return string.Join(Environment.NewLine, message);
            }

            message.Add("");
            message.Add("To create a complete schedule, check:");

            if (result.UnassignedSubjectsCount > 0)
            {
                message.Add($"- Assign faculty members to {result.UnassignedSubjectsCount} subject(s).");
            }

            if (result.MissingSectionCount > 0)
            {
                message.Add($"- Add matching sections for {result.MissingSectionCount} subject assignment(s).");
            }

            if (result.NoClassroomCount > 0)
            {
                message.Add($"- Add classrooms or increase classroom capacity for {result.NoClassroomCount} lesson(s).");
            }

            if (result.ConflictCount > 0)
            {
                message.Add($"- Add more non-break time slots, classrooms, or faculty coverage for {result.ConflictCount} conflicted lesson(s).");
            }

            if (result.DuplicateAssignmentCount > 0)
            {
                message.Add($"- Remove {result.DuplicateAssignmentCount} duplicate faculty-subject assignment(s).");
            }

            return string.Join(Environment.NewLine, message);
        }

        private async Task AddScheduleAsync()
        {
            if (!TryBuildSchedule(out var schedule))
            {
                return;
            }

            await ExecuteScheduleActionAsync(
                async () => await scheduleService.AddAsync(schedule),
                UiMessages.RecordAdded);
        }

        private async Task UpdateScheduleAsync()
        {
            if (!TryGetSelectedScheduleId(out int scheduleId) || !TryBuildSchedule(out var schedule))
            {
                ShowInformation("Select a schedule row before updating.");
                return;
            }

            schedule.ScheduleID = scheduleId;

            await ExecuteScheduleActionAsync(
                async () => await scheduleService.UpdateAsync(schedule),
                UiMessages.RecordUpdated);
        }

        private async Task DeleteScheduleAsync()
        {
            if (!TryGetSelectedScheduleId(out int scheduleId))
            {
                ShowInformation("Select a schedule row before deleting.");
                return;
            }

            var confirmation = UiMessages.ConfirmDeletion(this, "Delete Schedule");

            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            await ExecuteScheduleActionAsync(
                async () => await scheduleService.DeleteAsync(scheduleId),
                UiMessages.RecordDeleted);
        }

        private async Task ExecuteScheduleActionAsync(Func<Task> action, string successMessage)
        {
            SetScheduleActionsEnabled(false);

            try
            {
                await action();
                await LoadSchedulesAsync();
                ClearScheduleForm();
                ShowInformation(successMessage);
            }
            catch (Exception ex)
            {
                ShowError("Unable to complete the schedule operation.", ex);
            }
            finally
            {
                SetScheduleActionsEnabled(true);
            }
        }

        private async Task ExportSchedulePdfAsync()
        {
            var rows = GetFilteredRows()
                .Select(row => new SchedulePdfRow(
                    row.SemesterNumber,
                    row.StudyYearName,
                    row.BranchName,
                    row.SectionName,
                    row.GroupName == "All" ? "-" : row.GroupName,
                    row.LectureType,
                    row.SubjectName,
                    row.FacultyMemberName,
                    row.ClassroomName,
                    row.DayOfWeek,
                    row.StartTimeText,
                    row.EndTimeText))
                .ToList();

            using var dialog = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                FileName = $"Schedule-{DateTime.Now:yyyyMMdd-HHmm}.pdf",
                Title = "Export schedule to PDF"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                await schedulePdfExportService.ExportAsync(dialog.FileName, rows);
                ShowInformation("Schedule exported successfully.");
            }
            catch (Exception ex)
            {
                ShowError("Unable to export schedule PDF.", ex);
            }
        }

        private async Task PopulateScheduleEditorFromSelectionAsync()
        {
            if (dgvSchedules.CurrentRow?.DataBoundItem is not ScheduleRow row)
            {
                return;
            }

            try
            {
                var schedule = await scheduleService.GetByIdAsync(row.ScheduleID);

                if (schedule is null)
                {
                    return;
                }

                txtScheduleId.Text = schedule.ScheduleID.ToString();
                cmbDayOfWeek.SelectedItem = schedule.DayOfWeek;
                SelectComboValue(cmbClassroom, schedule.ClassroomID);
                SelectComboValue(cmbTimeSlot, schedule.TimeSlotID);
                SelectComboValue(cmbStudyYear, schedule.StudyYearID);
                SelectComboValue(cmbBranch, schedule.BranchID);
                BindSectionsCombo(schedule.StudyYearID, schedule.BranchID, schedule.SectionID);
                BindSubjectsCombo(schedule.StudyYearID, schedule.BranchID, schedule.SubjectID);
                BindFacultyMembersForSubject(schedule.SubjectID, schedule.FacultyMemberID);
                SelectComboValue(cmbSection, schedule.SectionID);
                ConfigureLectureOptionsForSubject(
                    subjectsLookup.FirstOrDefault(subject => subject.SubjectID == schedule.SubjectID),
                    schedule.LectureType);
                BindGroupNameCombo(schedule.GroupName);
            }
            catch (Exception ex)
            {
                ShowError("Unable to load the selected schedule.", ex);
            }
        }

        private void ClearScheduleForm()
        {
            txtScheduleId.Clear();
            ClearCombo(cmbSubject);
            ClearCombo(cmbFacultyMember);
            ClearCombo(cmbClassroom);
            ClearCombo(cmbTimeSlot);
            ClearCombo(cmbDayOfWeek);
            ClearCombo(cmbStudyYear);
            ClearCombo(cmbBranch);
            cmbBranch.Enabled = true;
            BindSectionsCombo();
            BindSubjectsCombo();
            ClearCombo(cmbSection);
            BindFacultyMembersForSubject(null);
            ConfigureLectureOptionsForSubject(null, "Theory");
            BindGroupNameCombo();
            ClearCombo(cmbGroupName);
            dgvSchedules.ClearSelection();
        }

        private void SetScheduleActionsEnabled(bool enabled)
        {
            btnGenerateSchedule.Enabled = enabled;
            btnAddSchedule.Enabled = enabled;
            btnUpdateSchedule.Enabled = enabled;
            btnDeleteSchedule.Enabled = enabled;
            btnClearScheduleForm.Enabled = enabled;
            btnExportSchedulePdf.Enabled = enabled;
            btnScheduleGridView.Enabled = enabled;
            btnScheduleTimetableView.Enabled = enabled;
            dgvSchedules.Enabled = enabled;
            pnlTimetableHost.Enabled = enabled;
        }
    }
}
