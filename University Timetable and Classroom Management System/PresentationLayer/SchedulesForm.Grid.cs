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
            dgvSchedules.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvSchedules.BackgroundColor = Color.White;
            dgvSchedules.BorderStyle = BorderStyle.None;
            dgvSchedules.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvSchedules.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dgvSchedules.ColumnHeadersHeight = 48;
            dgvSchedules.EnableHeadersVisualStyles = false;
            dgvSchedules.RowTemplate.Height = 46;
            dgvSchedules.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            GridStyle.Apply(dgvSchedules);
            dgvSchedules.DataSource = scheduleBindingSource;
            colScheduleId.DataPropertyName = nameof(ScheduleRow.ScheduleID);
            colScheduleId.Visible = false;
            EnsureSemesterColumn();
            colDayOfWeek.DataPropertyName = nameof(ScheduleRow.DayOfWeek);
            colSubject.DataPropertyName = nameof(ScheduleRow.SubjectName);
            colFacultyMember.DataPropertyName = nameof(ScheduleRow.FacultyMemberName);
            colClassroom.DataPropertyName = nameof(ScheduleRow.ClassroomName);
            colTimeSlot.DataPropertyName = nameof(ScheduleRow.TimeSlotName);
            colTimeSlot.HeaderText = "Time";
            colStudyYear.DataPropertyName = nameof(ScheduleRow.StudyYearName);
            colBranch.DataPropertyName = nameof(ScheduleRow.BranchName);
            colSection.DataPropertyName = nameof(ScheduleRow.SectionName);
            ApplyScheduleGridColumnLayout();
        }

        private void EnsureSemesterColumn()
        {
            if (dgvSchedules.Columns.Contains("colSemester"))
            {
                return;
            }

            var column = new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(ScheduleRow.SemesterNumber),
                FillWeight = 58F,
                HeaderText = "Semester",
                Name = "colSemester",
                ReadOnly = true
            };

            dgvSchedules.Columns.Insert(1, column);

            dgvSchedules.Columns.Insert(4, new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(ScheduleRow.GroupName),
                FillWeight = 56F,
                HeaderText = "Group",
                Name = "colGroupName",
                ReadOnly = true
            });

            dgvSchedules.Columns.Insert(5, new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(ScheduleRow.LectureType),
                FillWeight = 64F,
                HeaderText = "Type",
                Name = "colLectureType",
                ReadOnly = true
            });

            colDayOfWeek.DisplayIndex = 0;
            colTimeSlot.DisplayIndex = 1;
            colSubject.DisplayIndex = 2;
            colFacultyMember.DisplayIndex = 3;
            colClassroom.DisplayIndex = 4;
            dgvSchedules.Columns["colLectureType"].DisplayIndex = 5;
            colSection.DisplayIndex = 6;
            dgvSchedules.Columns["colGroupName"].DisplayIndex = 7;
            colStudyYear.DisplayIndex = 8;
            colBranch.DisplayIndex = 9;
            column.DisplayIndex = 10;
        }

        private void ApplyScheduleGridColumnLayout()
        {
            SetGridColumn(colDayOfWeek, "Day", 78);
            SetGridColumn(colTimeSlot, "Time", 138);
            SetGridColumn(colSubject, "Subject", 230);
            SetGridColumn(colFacultyMember, "Teacher", 170);
            SetGridColumn(colClassroom, "Room", 110);
            SetGridColumn(dgvSchedules.Columns["colLectureType"], "Type", 92);
            SetGridColumn(colSection, "Section", 86);
            SetGridColumn(dgvSchedules.Columns["colGroupName"], "Group", 64);
            SetGridColumn(colStudyYear, "Year", 90);
            SetGridColumn(colBranch, "Branch", 105);
            SetGridColumn(dgvSchedules.Columns["colSemester"], "Semester", 70);

            colStudyYear.Visible = false;
            colBranch.Visible = false;
            dgvSchedules.Columns["colSemester"].Visible = false;

            dgvSchedules.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
            dgvSchedules.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
            dgvSchedules.DefaultCellStyle.Font = new Font("Segoe UI", 11F);
            dgvSchedules.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
            dgvSchedules.RowsDefaultCellStyle.BackColor = Color.White;
            dgvSchedules.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
            dgvSchedules.DefaultCellStyle.SelectionBackColor = Color.FromArgb(37, 99, 235);
            dgvSchedules.DefaultCellStyle.SelectionForeColor = Color.White;
            dgvSchedules.ColumnHeadersHeight = 48;
            dgvSchedules.RowTemplate.Height = 46;
        }

        private static void SetGridColumn(DataGridViewColumn column, string headerText, float fillWeight)
        {
            column.HeaderText = headerText;
            column.FillWeight = fillWeight;
            column.MinimumWidth = 48;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
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
            var visibleRows = GetFilteredRows()
                .OrderBy(row => row.SemesterNumber)
                .ThenBy(row => StudyYearOrder(row.StudyYearName))
                .ThenBy(row => row.BranchName)
                .ThenBy(row => row.SectionName)
                .ThenBy(row => DayOrder(row.DayOfWeek))
                .ThenBy(row => row.StartTime)
                .ThenBy(row => row.GroupName)
                .ToList();

            var allConflictingScheduleIds = GetConflictingScheduleIds(scheduleRows);

            scheduleBindingSource.DataSource = visibleRows;
            dgvSchedules.ClearSelection();
            StyleScheduleRows(allConflictingScheduleIds);
            RenderScheduleTimetable(visibleRows, allConflictingScheduleIds);
        }

        private void ApplySectionFilterSelection()
        {
            if (isUpdatingScheduleLookups)
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
                    isUpdatingScheduleLookups = false;
                }
            }

            ApplyScheduleFilters();
        }

        private void ApplyStudyYearFilterSelection()
        {
            if (isUpdatingScheduleLookups)
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
            string year = section.StudyYear?.YearName ?? "Year";

            return section.Branch is null
                ? $"{section.SectionName} - {year}"
                : $"{section.SectionName} - {year} - {section.Branch.BranchName}";
        }

        private static int DayOrder(string day)
        {
            return day switch
            {
                "Sunday" => 1,
                "Monday" => 2,
                "Tuesday" => 3,
                "Wednesday" => 4,
                "Thursday" => 5,
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
