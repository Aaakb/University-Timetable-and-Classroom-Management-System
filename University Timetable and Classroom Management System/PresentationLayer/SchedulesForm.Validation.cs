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
                DayOfWeek = GetSelectedPlainText(cmbDayOfWeek)?.Trim() ?? string.Empty,
                SubjectID = GetSelectedRequiredId(cmbSubject),
                FacultyMemberID = GetSelectedRequiredId(cmbFacultyMember),
                ClassroomID = GetSelectedRequiredId(cmbClassroom),
                TimeSlotID = GetSelectedRequiredId(cmbTimeSlot),
                LectureType = GetSelectedPlainText(cmbLectureType) ?? "Theory",
                GroupName = GetSelectedPlainText(cmbGroupName) is "الشعبة كاملة" ? null : GetSelectedPlainText(cmbGroupName),
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
                string.IsNullOrWhiteSpace(schedule.GroupName) &&
                !sectionsLookup.Any(section => section.SectionID == schedule.SectionID &&
                    AcademicStructureRules.AllowsWholeSectionPractical(section.StudyYearID)))
            {
                ShowInformation("اختر مجموعة للمحاضرات العملية.");
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
                ShowInformation("المادة المحددة لا تنتمي إلى مرحلة الشعبة المحددة.");
                return false;
            }

            if (subject?.BranchID.HasValue == true &&
                schedule.BranchID.HasValue &&
                subject.BranchID.Value != schedule.BranchID.Value)
            {
                ShowInformation("المادة المحددة لا تنتمي إلى فرع الشعبة المحددة.");
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
                ShowInformation("الشعبة المحددة غير مناسبة لهذه المادة.");
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
