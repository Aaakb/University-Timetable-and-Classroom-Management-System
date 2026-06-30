using Guna.UI2.WinForms;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System
{
    public partial class SchedulesForm
    {
        private async Task RefreshSchedulesAsync()
        {
            SetScheduleActionsEnabled(false);

            try
            {
                await LoadLookupsAsync();
                await LoadSchedulesAsync();
                ClearScheduleForm();
            }
            catch (Exception ex)
            {
                ShowError("Unable to refresh schedule data.", ex);
            }
            finally
            {
                SetScheduleActionsEnabled(true);
            }
        }

        private async Task LoadLookupsAsync()
        {
            subjectsLookup = await subjectService.GetAllAsync();
            facultyMembersLookup = await facultyMemberService.GetAllAsync();
            facultySubjectAssignmentsLookup = await facultyMemberSubjectService.GetAllAsync();
            var classrooms = await classroomService.GetAllAsync();
            var timeSlots = await timeSlotService.GetAllAsync();
            studyYearsLookup = await studyYearService.GetAllAsync();
            branchesLookup = await branchService.GetAllAsync();
            sectionsLookup = await sectionService.GetAllAsync();

            BindSubjectsCombo();
            BindFacultyMembersForSubject(null);
            BindCombo(cmbClassroom, classrooms.Select(classroom => new ComboOption(classroom.ClassroomID, classroom.ClassroomNumber)));
            BindCombo(cmbTimeSlot, timeSlots.Select(slot => new ComboOption(slot.TimeSlotID, FormatTimeSlot(slot))));
            BindCombo(cmbStudyYear, studyYearsLookup.Select(studyYear => new ComboOption(studyYear.StudyYearID, studyYear.YearName)));
            BindCombo(cmbBranch, branchesLookup.Select(branch => new ComboOption(branch.BranchID, branch.BranchName)));
            BindSectionsCombo();

            BindFilterCombo(cmbFacultyFilter, facultyMembersLookup.Select(faculty => new ComboOption(faculty.FacultyMemberID, faculty.FullName)), "All faculty");
            BindFilterCombo(cmbStudyYearFilter, studyYearsLookup.Select(studyYear => new ComboOption(studyYear.StudyYearID, studyYear.YearName)), "All study years");
            BindSectionFilterCombo();
            BindDayCombos();
            BindSemesterFilterCombo();
            BindGroupFilterCombo();
            BindGroupNameCombo();
        }

        private async Task LoadSchedulesAsync()
        {
            var schedules = await scheduleService.GetScheduleDetailsAsync();
            scheduleRows = schedules
                .Select(ScheduleRow.FromDetails)
                .OrderBy(row => StudyYearOrder(row.StudyYearName))
                .ThenBy(row => row.BranchName)
                .ThenBy(row => row.SectionName)
                .ThenBy(row => row.SemesterNumber)
                .ThenBy(row => DayOrder(row.DayOfWeek))
                .ThenBy(row => row.StartTime)
                .ToList();

            ApplyScheduleFilters();
        }

        private async Task LoadFormAsync()
        {
            await RefreshSchedulesAsync();
        }

        private void BindSubjectsCombo(int? studyYearId = null, int? branchId = null, int? selectedSubjectId = null)
        {
            var subjects = subjectsLookup
                .Where(subject => SubjectMatchesFilter(subject, studyYearId, branchId))
                .OrderBy(subject => subject.StudyYearID)
                .ThenBy(subject => subject.BranchID ?? 0)
                .ThenBy(subject => subject.SubjectID)
                .Select((subject, index) => new ComboOption(subject.SubjectID, $"{index + 1}. {subject.SubjectName}"));

            BindCombo(cmbSubject, subjects);
            SelectComboValue(cmbSubject, selectedSubjectId);
        }

        private void BindSectionsCombo(int? studyYearId = null, int? branchId = null, int? selectedSectionId = null)
        {
            var sections = sectionsLookup
                .Where(section => SectionMatchesFilter(section, studyYearId, branchId))
                .OrderBy(section => section.StudyYearID)
                .ThenBy(section => section.BranchID ?? 0)
                .ThenBy(section => section.SectionName)
                .Select(section => new ComboOption(section.SectionID, FormatSection(section)));

            BindCombo(cmbSection, sections);
            SelectComboValue(cmbSection, selectedSectionId);
        }

        private void BindSectionFilterCombo(int? studyYearId = null, int? selectedSectionId = null)
        {
            var sections = sectionsLookup
                .Where(section => SectionMatchesFilter(section, studyYearId, null))
                .OrderBy(section => section.StudyYearID)
                .ThenBy(section => section.BranchID ?? 0)
                .ThenBy(section => section.SectionName)
                .Select(section => new ComboOption(section.SectionID, FormatSection(section)));

            BindFilterCombo(cmbSectionFilter, sections, "All sections");
            SelectComboValue(cmbSectionFilter, selectedSectionId);
        }

        private void BindSemesterFilterCombo()
        {
            BindFilterCombo(
                cmbSemesterFilter,
                [
                    new ComboOption(1, "Semester 1"),
                    new ComboOption(2, "Semester 2")
                ],
                "All semesters");
        }

        private void BindGroupFilterCombo()
        {
            BindFilterCombo(
                cmbGroupFilter,
                GetPracticalGroupNames().Select(group => new ComboOption(null, group)),
                "All");
        }

        private void BindGroupNameCombo(string? selectedGroupName = null)
        {
            string? currentSelection = selectedGroupName ?? GetSelectedPlainText(cmbGroupName);
            var section = GetSelectedOptionalId(cmbSection) is int sectionId
                ? sectionsLookup.FirstOrDefault(item => item.SectionID == sectionId)
                : null;

            cmbGroupName.Items.Clear();
            cmbGroupName.Items.AddRange(GetAllowedGroupsForSection(section).Cast<object>().ToArray());
            SelectComboText(cmbGroupName, currentSelection);
            ApplyLectureTypeSelection();
        }

        private void ApplySubjectSelection()
        {
            if (isUpdatingScheduleLookups)
            {
                return;
            }

            int? subjectId = GetSelectedOptionalId(cmbSubject);
            var subject = subjectId.HasValue
                ? subjectsLookup.FirstOrDefault(item => item.SubjectID == subjectId.Value)
                : null;

            if (subject is null)
            {
                BindFacultyMembersForSubject(null);
                ConfigureLectureOptionsForSubject(null);
                return;
            }

            isUpdatingScheduleLookups = true;
            SelectComboValue(cmbStudyYear, subject.StudyYearID);

            if (subject.BranchID.HasValue)
            {
                SelectComboValue(cmbBranch, subject.BranchID);
            }

            BindSectionsCombo(subject.StudyYearID, subject.BranchID);
            BindFacultyMembersForSubject(subject.SubjectID);
            ConfigureLectureOptionsForSubject(subject);
            BindGroupNameCombo();
            isUpdatingScheduleLookups = false;
        }

        private void ApplyStudyYearSelection()
        {
            if (isUpdatingScheduleLookups)
            {
                return;
            }

            int? studyYearId = GetSelectedOptionalId(cmbStudyYear);
            int? branchId = GetSelectedOptionalId(cmbBranch);

            isUpdatingScheduleLookups = true;

            if (studyYearId.HasValue && AcademicStructureRules.UsesGeneralSections(studyYearId.Value))
            {
                ClearCombo(cmbBranch);
                cmbBranch.Enabled = false;
                branchId = null;
            }
            else
            {
                cmbBranch.Enabled = true;
            }

            BindSectionsCombo(studyYearId, branchId);
            BindSubjectsCombo(studyYearId, branchId);
            BindFacultyMembersForSubject(GetSelectedOptionalId(cmbSubject));
            ConfigureLectureOptionsForSubject(GetSelectedSubject());
            BindGroupNameCombo();
            isUpdatingScheduleLookups = false;
        }

        private void ApplyBranchSelection()
        {
            if (isUpdatingScheduleLookups)
            {
                return;
            }

            int? studyYearId = GetSelectedOptionalId(cmbStudyYear);
            int? branchId = GetSelectedOptionalId(cmbBranch);

            isUpdatingScheduleLookups = true;

            if (studyYearId.HasValue && AcademicStructureRules.UsesGeneralSections(studyYearId.Value))
            {
                ClearCombo(cmbBranch);
                branchId = null;
            }

            BindSectionsCombo(studyYearId, branchId);
            BindSubjectsCombo(studyYearId, branchId);
            BindFacultyMembersForSubject(GetSelectedOptionalId(cmbSubject));
            ConfigureLectureOptionsForSubject(GetSelectedSubject());
            BindGroupNameCombo();
            isUpdatingScheduleLookups = false;
        }

        private void ApplySectionSelection()
        {
            if (isUpdatingScheduleLookups)
            {
                return;
            }

            int? sectionId = GetSelectedOptionalId(cmbSection);
            var section = sectionId.HasValue
                ? sectionsLookup.FirstOrDefault(item => item.SectionID == sectionId.Value)
                : null;

            if (section is null)
            {
                return;
            }

            int? selectedSubjectId = GetSelectedOptionalId(cmbSubject);

            isUpdatingScheduleLookups = true;
            SelectComboValue(cmbStudyYear, section.StudyYearID);
            SelectComboValue(cmbBranch, section.BranchID);
            BindSubjectsCombo(section.StudyYearID, section.BranchID, selectedSubjectId);
            BindFacultyMembersForSubject(selectedSubjectId);
            ConfigureLectureOptionsForSubject(GetSelectedSubject());
            cmbBranch.Enabled = !AcademicStructureRules.UsesGeneralSections(section.StudyYearID);
            BindGroupNameCombo();
            isUpdatingScheduleLookups = false;
        }

        private void ApplyLectureTypeSelection()
        {
            var selectedSubject = GetSelectedSubject();
            bool isPractical = string.Equals(
                GetSelectedPlainText(cmbLectureType),
                "Practical",
                StringComparison.OrdinalIgnoreCase);
            bool canChoosePractical = selectedSubject?.PracticalHours > 0;
            bool shouldShowLectureType = selectedSubject is null ||
                (selectedSubject.TheoreticalHours > 0 && selectedSubject.PracticalHours > 0);

            lblLectureType.Visible = shouldShowLectureType;
            cmbLectureType.Visible = shouldShowLectureType;
            lblGroupName.Visible = isPractical && canChoosePractical;
            cmbGroupName.Visible = isPractical && canChoosePractical;
            cmbGroupName.Enabled = isPractical && canChoosePractical && cmbGroupName.Items.Count > 0;

            if (!isPractical || !canChoosePractical)
            {
                ClearCombo(cmbGroupName);
            }
        }

        private void BindFacultyMembersForSubject(int? subjectId, int? selectedFacultyMemberId = null)
        {
            var facultyMembers = subjectId.HasValue
                ? facultySubjectAssignmentsLookup
                    .Where(assignment => assignment.SubjectID == subjectId.Value)
                    .Select(assignment => assignment.FacultyMember)
                    .Where(faculty => faculty is not null)
                    .DistinctBy(faculty => faculty.FacultyMemberID)
                : Enumerable.Empty<FacultyMember>();

            var options = facultyMembers
                .OrderBy(faculty => faculty.FullName)
                .Select(faculty => new ComboOption(faculty.FacultyMemberID, faculty.FullName))
                .ToList();

            BindCombo(cmbFacultyMember, options);

            if (selectedFacultyMemberId.HasValue)
            {
                SelectComboValue(cmbFacultyMember, selectedFacultyMemberId);
            }
            else if (options.Count == 1)
            {
                SelectComboValue(cmbFacultyMember, options[0].Id);
            }
        }

        private void ConfigureLectureOptionsForSubject(Subject? subject, string? selectedLectureType = null)
        {
            var lectureTypes = GetAvailableLectureTypes(subject).ToList();
            cmbLectureType.Items.Clear();
            cmbLectureType.Items.AddRange(lectureTypes.Cast<object>().ToArray());

            string lectureType = selectedLectureType is not null && lectureTypes.Contains(selectedLectureType)
                ? selectedLectureType
                : lectureTypes.FirstOrDefault() ?? "Theory";

            SelectComboText(cmbLectureType, lectureType);
            ApplyLectureTypeSelection();
        }

        private static bool SubjectMatchesFilter(Subject subject, int? studyYearId, int? branchId)
        {
            if (studyYearId.HasValue && subject.StudyYearID != studyYearId.Value)
            {
                return false;
            }

            if (AcademicStructureRules.UsesGeneralSections(subject.StudyYearID))
            {
                return !subject.BranchID.HasValue;
            }

            if (!branchId.HasValue)
            {
                return true;
            }

            return subject.BranchID == branchId.Value;
        }

        private static bool SectionMatchesFilter(Section section, int? studyYearId, int? branchId)
        {
            if (studyYearId.HasValue && section.StudyYearID != studyYearId.Value)
            {
                return false;
            }

            if (AcademicStructureRules.UsesGeneralSections(section.StudyYearID))
            {
                return !section.BranchID.HasValue &&
                    AcademicStructureRules.GetAllowedSectionNames(section.StudyYearID)
                        .Contains(section.SectionName.Trim(), StringComparer.OrdinalIgnoreCase);
            }

            if (!branchId.HasValue)
            {
                return section.BranchID.HasValue;
            }

            return section.BranchID == branchId.Value;
        }

        private void BindDayCombos()
        {
            string[] days = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday"];

            cmbDayOfWeek.Items.Clear();
            cmbDayOfWeek.Items.AddRange(days);
            cmbDayOfWeek.SelectedIndex = -1;
        }

        private static IReadOnlyList<string> GetAllowedGroupsForSection(Section? section)
        {
            if (section is null)
            {
                return [];
            }

            return AcademicStructureRules.GetAllowedPracticalGroupNames(section.SectionName);
        }

        private static IReadOnlyList<string> GetPracticalGroupNames()
        {
            return ["A1", "A2", "B1", "B2"];
        }
        private static IEnumerable<string> GetAvailableLectureTypes(Subject? subject)
        {
            if (subject is null)
            {
                yield return "Theory";
                yield return "Practical";
                yield break;
            }

            if (subject.TheoreticalHours > 0)
            {
                yield return "Theory";
            }

            if (subject.PracticalHours > 0)
            {
                yield return "Practical";
            }

            if (subject.TheoreticalHours <= 0 && subject.PracticalHours <= 0)
            {
                yield return "Theory";
            }
        }

        private Subject? GetSelectedSubject()
        {
            int? subjectId = GetSelectedOptionalId(cmbSubject);

            return subjectId.HasValue
                ? subjectsLookup.FirstOrDefault(subject => subject.SubjectID == subjectId.Value)
                : null;
        }

        private static void BindCombo(Guna2ComboBox combo, IEnumerable<ComboOption> options)
        {
            ComboBoxHelper.Bind(combo, options);
        }

        private static void BindFilterCombo(
            Guna2ComboBox combo,
            IEnumerable<ComboOption> options,
            string allText)
        {
            var items = new List<ComboOption> { new(null, allText) };
            items.AddRange(options);
            ComboBoxHelper.Bind(combo, items, selectedIndex: 0);
        }

        private static int GetSelectedRequiredId(Guna2ComboBox combo)
        {
            return ComboBoxHelper.GetSelectedRequiredId(combo);
        }

        private static int? GetSelectedOptionalId(Guna2ComboBox combo)
        {
            return ComboBoxHelper.GetSelectedOptionalId(combo);
        }

        private static void SelectComboValue(Guna2ComboBox combo, int? id)
        {
            ComboBoxHelper.SelectValue(combo, id, selectNullOption: false);
        }

        private static void ClearCombo(Guna2ComboBox combo)
        {
            ComboBoxHelper.Clear(combo);
        }

        private static string? GetSelectedPlainText(Guna2ComboBox combo)
        {
            return ComboBoxHelper.GetSelectedPlainText(combo);
        }

        private static void SelectComboText(Guna2ComboBox combo, string? text)
        {
            ComboBoxHelper.SelectText(combo, text);
        }
    }
}
