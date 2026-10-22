using University_Timetable_and_Classroom_Management_System.Models;

using System.Drawing;
using System.Windows.Forms;

namespace University_Timetable_and_Classroom_Management_System
{
    public partial class SchedulesForm
    {
        private void ConfigureScheduleGrid()
        {
            dgvSchedules.AutoGenerateColumns = false;
            dgvSchedules.ReadOnly = false;
            foreach (DataGridViewColumn column in dgvSchedules.Columns) column.ReadOnly = column != colLockStatus;
            GridRowNumbers.Attach(dgvSchedules, colRowNumber);
            dgvSchedules.DataSource = scheduleBindingSource;
        }

        private void StyleScheduleRows(HashSet<int> conflictingScheduleIds)
        {
            foreach (DataGridViewRow row in dgvSchedules.Rows)
            {
                if (row.DataBoundItem is not ScheduleRow scheduleRow)
                {
                    continue;
                }

                bool isPractical = scheduleRow.LectureType == "Practical";
                bool hasConflict = conflictingScheduleIds.Contains(scheduleRow.ScheduleID);
                row.DefaultCellStyle.BackColor = hasConflict
                    ? Color.FromArgb(254, 242, 242)
                    : isPractical
                        ? Color.FromArgb(240, 253, 244)
                        : Color.White;
                row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(37, 99, 235);
                row.DefaultCellStyle.SelectionForeColor = Color.White;

                row.Cells["colDayOfWeek"].Style.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
                row.Cells["colTimeSlot"].Style.ForeColor = Color.FromArgb(37, 99, 235);

                if (row.Cells["colSubject"] is DataGridViewCell subjectCell)
                {
                    subjectCell.ToolTipText = scheduleRow.SubjectName;
                }
            }
        }

        private void ApplyScheduleFilters()
        {
            if (IsScheduleUiUnavailable) return;
            var visibleRows = GetFilteredRows()
                .OrderBy(row => row.SemesterNumber)
                .ThenBy(row => DayOrder(row.DayOfWeek))
                .ThenBy(row => row.StartTime)
                .ThenBy(row => StudyYearOrder(row.StudyYearName))
                .ThenBy(row => row.BranchName)
                .ThenBy(row => row.SectionName)
                .ThenBy(row => row.GroupName)
                .ThenBy(row => row.ScheduleID)
                .ToList();

            var allConflictingScheduleIds = GetConflictingScheduleIds(scheduleRows);

            scheduleBindingSource.DataSource = visibleRows;
            dgvSchedules.ClearSelection();
            StyleScheduleRows(allConflictingScheduleIds);
            RenderScheduleTimetable(visibleRows, allConflictingScheduleIds);
        }

        private void ApplySectionFilterSelection()
        {
            if (IsScheduleUiUnavailable || isUpdatingScheduleLookups)
            {
                return;
            }

            int? sectionId = GetSelectedOptionalId(cmbSectionFilter);

            if (sectionId.HasValue)
            {
                var section = sectionsLookup.FirstOrDefault(item => item.SectionID == sectionId.Value);

                if (section is not null)
                {
                    isUpdatingScheduleLookups = true;
                    SelectComboValue(cmbStudyYearFilter, section.StudyYearID);
                    BindGroupFilterCombo();
                    isUpdatingScheduleLookups = false;
                }
            }

            ApplyScheduleFilters();
        }

        private void ApplyStudyYearFilterSelection()
        {
            if (IsScheduleUiUnavailable || isUpdatingScheduleLookups)
            {
                return;
            }

            int? selectedStudyYearId = GetSelectedOptionalId(cmbStudyYearFilter);
            int? selectedSectionId = GetSelectedOptionalId(cmbSectionFilter);
            var selectedSection = selectedSectionId.HasValue
                ? sectionsLookup.FirstOrDefault(section => section.SectionID == selectedSectionId.Value)
                : null;

            if (selectedStudyYearId.HasValue &&
                selectedSection is not null &&
                selectedSection.StudyYearID != selectedStudyYearId.Value)
            {
                selectedSectionId = null;
            }

            isUpdatingScheduleLookups = true;
            BindSectionFilterCombo(selectedStudyYearId, selectedSectionId);
            BindGroupFilterCombo();
            isUpdatingScheduleLookups = false;

            ApplyScheduleFilters();
        }

        private IEnumerable<ScheduleRow> GetFilteredRows()
        {
            var criteria = new ScheduleFilterCriteria
            {
                FacultyId = GetSelectedOptionalId(cmbFacultyFilter),
                SectionId = GetSelectedOptionalId(cmbSectionFilter),
                StudyYearId = GetSelectedOptionalId(cmbStudyYearFilter),
                SemesterNumber = GetSelectedOptionalId(cmbSemesterFilter),
                GroupName = cmbGroupFilter.SelectedItem is ComboOption groupOption ? groupOption.Text : null
            };

            return scheduleFilterService.Apply(scheduleRows, criteria);
        }

        private bool TryGetSelectedScheduleId(out int scheduleId)
        {
            if (int.TryParse(txtScheduleId.Text, out scheduleId))
            {
                return true;
            }

            var selectedSchedule = dgvSchedules.CurrentRow?.DataBoundItem as ScheduleRow;
            scheduleId = selectedSchedule?.ScheduleID ?? 0;
            return scheduleId > 0;
        }

        private static string FormatTimeSlot(TimeSlot timeSlot)
        {
            return TimeDisplay.FormatRange(timeSlot.StartTime, timeSlot.EndTime);
        }

        private static string FormatSection(Section section)
        {
            string year = ArabicUi.Display(section.StudyYear?.YearName ?? "المرحلة");

            return section.Branch is null
                ? $"{section.SectionName} - {year}"
                : $"{section.SectionName} - {year} - {section.Branch.BranchName}";
        }

        private static int DayOrder(string day)
        {
            return day.Trim().ToLowerInvariant() switch
            {
                "sunday" => 1,
                "monday" => 2,
                "tuesday" => 3,
                "wednesday" => 4,
                "thursday" => 5,
                _ => 99
            };
        }

        private static int StudyYearOrder(string studyYearName)
        {
            return studyYearName switch
            {
                "First Year" => 1,
                "Second Year" => 2,
                "Third Year" => 3,
                "Fourth Year" => 4,
                _ => 99
            };
        }

        private static HashSet<int> GetConflictingScheduleIds(IReadOnlyCollection<ScheduleRow> rows)
        {
            var conflicts = new HashSet<int>();
            var rowList = rows.ToList();

            for (int i = 0; i < rowList.Count; i++)
            {
                for (int j = i + 1; j < rowList.Count; j++)
                {
                    if (!RowsOverlap(rowList[i], rowList[j]))
                    {
                        continue;
                    }

                    conflicts.Add(rowList[i].ScheduleID);
                    conflicts.Add(rowList[j].ScheduleID);
                }
            }

            return conflicts;
        }

        private static bool RowsOverlap(ScheduleRow first, ScheduleRow second)
        {
            if (first.SemesterNumber != second.SemesterNumber ||
                first.TimeSlotID != second.TimeSlotID ||
                !string.Equals(first.DayOfWeek, second.DayOfWeek, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return first.ClassroomID == second.ClassroomID ||
                first.FacultyMemberID == second.FacultyMemberID ||
                HasSectionOrGroupOverlap(first, second);
        }

        private static bool HasSectionOrGroupOverlap(ScheduleRow first, ScheduleRow second)
        {
            if (!first.SectionID.HasValue ||
                !second.SectionID.HasValue ||
                first.SectionID.Value != second.SectionID.Value)
            {
                return false;
            }

            if (IsWholeSection(first) || IsWholeSection(second))
            {
                return true;
            }

            return string.Equals(first.GroupName, second.GroupName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWholeSection(ScheduleRow row)
        {
            return string.Equals(row.LectureType, "Theory", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(row.GroupName, "All", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(row.GroupName);
        }
    }
}
