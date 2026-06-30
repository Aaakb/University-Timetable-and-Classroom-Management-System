using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System
{
    public partial class SchedulesForm
    {
        private bool TryBuildSchedule(out Schedule schedule)
        {
            ApplySectionSelection();
            schedule = ReadScheduleFromForm();

            return ValidateRequiredScheduleFields(schedule) &&
                ValidateScheduleAcademicRules(schedule);
        }

        private Schedule ReadScheduleFromForm()
        {
            return new Schedule
            {
                DayOfWeek = cmbDayOfWeek.Text.Trim(),
                SubjectID = GetSelectedRequiredId(cmbSubject),
                FacultyMemberID = GetSelectedRequiredId(cmbFacultyMember),
                ClassroomID = GetSelectedRequiredId(cmbClassroom),
                TimeSlotID = GetSelectedRequiredId(cmbTimeSlot),
                LectureType = GetSelectedPlainText(cmbLectureType) ?? "Theory",
                GroupName = GetSelectedPlainText(cmbGroupName),
                StudyYearID = GetSelectedOptionalId(cmbStudyYear),
                BranchID = GetSelectedOptionalId(cmbBranch),
                SectionID = GetSelectedOptionalId(cmbSection)
            };
        }

        private bool ValidateRequiredScheduleFields(Schedule schedule)
        {
            if (string.IsNullOrWhiteSpace(schedule.DayOfWeek))
            {
                ShowInformation(UiMessages.RequiredFields);
                cmbDayOfWeek.Focus();
                return false;
            }

            if (schedule.SubjectID <= 0 || schedule.FacultyMemberID <= 0 ||
                schedule.ClassroomID <= 0 || schedule.TimeSlotID <= 0)
            {
                ShowInformation(UiMessages.RequiredFields);
                return false;
            }

            if (!schedule.SectionID.HasValue)
            {
                ShowInformation(UiMessages.RequiredFields);
                cmbSection.Focus();
                return false;
            }

            if (string.Equals(schedule.LectureType, "Practical", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(schedule.GroupName))
            {
                ShowInformation("Select a group for practical sessions.");
                cmbGroupName.Focus();
                return false;
            }

            return true;
        }

        private bool ValidateScheduleAcademicRules(Schedule schedule)
        {
            var subject = subjectsLookup.FirstOrDefault(item => item.SubjectID == schedule.SubjectID);
            var section = schedule.SectionID.HasValue
                ? sectionsLookup.FirstOrDefault(item => item.SectionID == schedule.SectionID.Value)
                : null;

            ApplySectionDetails(schedule, section);

            if (subject is not null &&
                schedule.StudyYearID.HasValue &&
                subject.StudyYearID != schedule.StudyYearID.Value)
            {
                ShowInformation("The selected subject does not belong to the selected section study year.");
                return false;
            }

            if (subject?.BranchID.HasValue == true &&
                schedule.BranchID.HasValue &&
                subject.BranchID.Value != schedule.BranchID.Value)
            {
                ShowInformation("The selected subject does not belong to the selected section branch.");
                return false;
            }

            if (subject is not null &&
                section is not null &&
                !AcademicStructureRules.SectionMatchesSubject(
                    subject.StudyYearID,
                    subject.BranchID,
                    section.BranchID,
                    section.SectionName))
            {
                ShowInformation("The selected section is not valid for this subject.");
                return false;
            }

            return true;
        }

        private static void ApplySectionDetails(Schedule schedule, Section? section)
        {
            if (section is null)
            {
                return;
            }

            schedule.StudyYearID = section.StudyYearID;
            schedule.BranchID = section.BranchID;
        }
    }
}
