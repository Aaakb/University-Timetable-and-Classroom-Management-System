using System.Windows.Forms;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;

namespace University_Timetable_and_Classroom_Management_System
{
    public partial class SchedulesForm
    {
        private async Task GenerateScheduleAsync()
        {
            if (IsScheduleUiUnavailable || !ConfirmScheduleGeneration())
            {
                return;
            }

            SetScheduleActionsEnabled(false);

            try
            {
                var result = await scheduleService.RegenerateAsync();
                if (IsScheduleUiUnavailable) return;
                await LoadSchedulesAsync();
                if (IsScheduleUiUnavailable) return;
                ClearScheduleForm();
                ShowInformation(BuildGenerationMessage(result));
            }
            catch (Exception ex)
            {
                ShowError("تعذر توليد الجدول.", ex);
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
                "سيُنشأ جدول جديد للفصلين من المواد والتكليفات الحالية، بغض النظر عن الفلاتر. ستبقى المحاضرات المثبتة في مواقعها.\n\nإذا فشل التوليد يبقى الجدول الحالي كما هو.\n\nهل تريد توليد جدول جديد؟",
                "توليد جدول جديد");

            return confirmation == DialogResult.Yes;
        }

        private static string BuildGenerationMessage(ScheduleGenerationResult result)
        {
            if (result.IsRegenerated)
                return $"اكتمل توليد جدول جديد للفصلين. إجمالي المحاضرات: {result.CreatedCount}.\n" +
                    $"المحاضرات الجديدة: {result.AddedCount}، المحاضرات المثبتة المحتفظ بها: {result.PreservedCount}." +
                    (result.SafetyBackupPath is null ? "" : "\n\nنسخة الجدول السابق:\n" + result.SafetyBackupPath +
                        "\nيمكنك الرجوع إليها باستخدام «استعادة نسخة».");
            var message = new List<string>
            {
                $"اكتمل التوليد. إجمالي المحاضرات: {result.CreatedCount}.",
                $"المضافة: {result.AddedCount}، المحتفظ بها: {result.PreservedCount}، المصححة: {result.RepairedCount}، المتبقية: {result.SkippedCount}.",
                $"الموارد المتاحة: {result.TimeSlotCount} فترة، {result.ClassroomCount} قاعة، {result.SectionCount} شعبة."
            };

            if (result.AddedTimeSlotCount > 0 || result.AddedClassroomCount > 0)
            {
                message.Add($"الموارد المضافة تلقائياً: {result.AddedTimeSlotCount} فترة، {result.AddedClassroomCount} قاعة أو مختبر.");
            }

            if (result.AddedFacultyMemberCount > 0 || result.AddedFacultySubjectAssignmentCount > 0)
            {
                message.Add($"التغطية التدريسية المضافة تلقائياً: {result.AddedFacultyMemberCount} تدريسي، {result.AddedFacultySubjectAssignmentCount} تكليف تدريسي.");
            }

            if (result.SkippedCount == 0 && result.UnassignedSubjectsCount == 0)
            {
                message.Add("الجدول الناتج مكتمل.");
                return string.Join(Environment.NewLine, message);
            }

            message.Add("");
            message.Add("لإكمال الجدول، راجع ما يأتي:");

            if (result.UnassignedSubjectsCount > 0)
            {
                message.Add($"- كلّف تدريسيين بتدريس {result.UnassignedSubjectsCount} مادة.");
            }

            if (result.MissingSectionCount > 0)
            {
                message.Add($"- أضف شعباً مناسبة لعدد {result.MissingSectionCount} من التكليفات.");
            }

            if (result.NoClassroomCount > 0)
            {
                message.Add($"- أضف قاعات أو زِد سعتها لتغطية {result.NoClassroomCount} محاضرة.");
            }

            if (result.ConflictCount > 0)
            {
                message.Add($"- أضف فترات أو قاعات أو تكليفات تدريسية لحل تعارض {result.ConflictCount} محاضرة.");
            }

            if (result.DuplicateAssignmentCount > 0)
            {
                message.Add($"- احذف {result.DuplicateAssignmentCount} تكليفاً تدريسياً مكرراً.");
            }

            return string.Join(Environment.NewLine, message);
        }

        private async Task AddScheduleAsync()
        {
            if (IsScheduleUiUnavailable) return;
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
            if (IsScheduleUiUnavailable) return;
            if (!TryGetSelectedScheduleId(out int scheduleId) || !TryBuildSchedule(out var schedule))
            {
                ShowInformation("اختر محاضرة من الجدول قبل التعديل.");
                return;
            }

            schedule.ScheduleID = scheduleId;

            await ExecuteScheduleActionAsync(
                async () => await scheduleService.UpdateAsync(schedule),
                UiMessages.RecordUpdated);
        }

        private async Task DeleteScheduleAsync()
        {
            if (IsScheduleUiUnavailable) return;
            if (!TryGetSelectedScheduleId(out int scheduleId))
            {
                ShowInformation("اختر محاضرة من الجدول قبل الحذف.");
                return;
            }

            var confirmation = UiMessages.ConfirmDeletion(this, "حذف المحاضرة");

            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            await ExecuteScheduleActionAsync(
                async () => await scheduleService.DeleteAsync(scheduleId),
                UiMessages.RecordDeleted);
        }

        private Task ExecuteScheduleActionAsync(Func<Task> action, string successMessage)
        {
            return EditorAction.RunAsync(this, action, LoadSchedulesAsync, SetScheduleActionsEnabled,
                () =>
                {
                    ClearScheduleForm();
                    ShowInformation(successMessage);
                },
                ex => ShowError("تعذر إتمام العملية على المحاضرة.", ex));
        }

        private async Task ExportSchedulePdfAsync()
        {
            if (IsScheduleUiUnavailable) return;
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
                Filter = "ملفات PDF (*.pdf)|*.pdf",
                FileName = $"Schedule-{DateTime.Now:yyyyMMdd-HHmm}.pdf",
                Title = "تصدير الجدول إلى PDF"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                string? facultyName = GetSelectedOptionalId(cmbFacultyFilter) is int facultyId
                    ? facultyMembersLookup.FirstOrDefault(faculty => faculty.FacultyMemberID == facultyId)?.FullName
                    : null;
                await schedulePdfExportService.ExportAsync(dialog.FileName, rows, new SchedulePdfExportOptions(facultyName));
                ShowInformation("تم تصدير الجدول بنجاح.");
            }
            catch (Exception ex)
            {
                ShowError("تعذر تصدير الجدول إلى PDF.", ex);
            }
        }

        private async Task PopulateScheduleEditorFromSelectionAsync()
        {
            if (IsDisposed || Disposing || isUpdatingScheduleLookups || dgvSchedules.IsDisposed || dgvSchedules.Disposing)
            {
                return;
            }
            int loadVersion = ++scheduleEditorLoadVersion;

            // A BindingSource is disposed before its grid by the standard Designer
            // Dispose method. SelectionChanged can still fire while its list is empty.
            var selectedRow = dgvSchedules.CurrentRow;
            if (selectedRow is null || !selectedRow.Selected || selectedRow.Index < 0 ||
                selectedRow.Index >= scheduleBindingSource.Count ||
                scheduleBindingSource[selectedRow.Index] is not ScheduleRow row)
            {
                return;
            }

            try
            {
                var schedule = await scheduleService.GetByIdAsync(row.ScheduleID);

                if (schedule is null || loadVersion != scheduleEditorLoadVersion || IsDisposed || Disposing || dgvSchedules.IsDisposed ||
                    scheduleBindingSource.Count == 0 || !ReferenceEquals(dgvSchedules.CurrentRow, selectedRow))
                {
                    return;
                }

                isUpdatingScheduleLookups = true;
                try
                {
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
                    BindGroupNameCombo(schedule.GroupName ?? "الشعبة كاملة");
                    cmbBranch.Enabled = !AcademicStructureRules.UsesGeneralSections(schedule.StudyYearID ?? 0);
                }
                finally { isUpdatingScheduleLookups = false; }
            }
            catch (Exception ex)
            {
                if (!IsDisposed && !Disposing)
                    ShowError("تعذر تحميل المحاضرة المحددة.", ex);
            }
        }

        private void ClearScheduleForm()
        {
            if (IsScheduleUiUnavailable) return;
            scheduleEditorLoadVersion++;
            isUpdatingScheduleLookups = true;
            try
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
            finally { isUpdatingScheduleLookups = false; }
        }

        private void SetScheduleActionsEnabled(bool enabled)
        {
            if (IsScheduleUiUnavailable) return;
            btnGenerateSchedule.Enabled = enabled;
            btnAddSchedule.Enabled = enabled;
            btnUpdateSchedule.Enabled = enabled;
            btnDeleteSchedule.Enabled = enabled;
            btnClearScheduleForm.Enabled = enabled;
            btnExportSchedulePdf.Enabled = enabled;
            btnBackupTimetable.Enabled = enabled;
            btnRestoreTimetable.Enabled = enabled;
            btnScheduleGridView.Enabled = enabled;
            btnScheduleTimetableView.Enabled = enabled;
            dgvSchedules.Enabled = enabled;
            pnlTimetableHost.Enabled = enabled;
        }
    }
}
