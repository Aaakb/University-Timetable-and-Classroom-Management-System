using University_Timetable_and_Classroom_Management_System.BusinessLayer;

namespace University_Timetable_and_Classroom_Management_System
{
    internal sealed class ScheduleFilterService
    {
        public IEnumerable<ScheduleRow> Apply(
            IEnumerable<ScheduleRow> rows,
            ScheduleFilterCriteria criteria)
        {
            return rows.Where(row =>
                MatchesSemester(row, criteria.SemesterNumber) &&
                MatchesLectureType(row, criteria.LectureType) &&
                MatchesGroup(row, criteria.GroupName) &&
                MatchesFaculty(row, criteria.FacultyId) &&
                MatchesSection(row, criteria.SectionId) &&
                MatchesStudyYear(row, criteria.StudyYearId) &&
                MatchesDay(row, criteria.DayOfWeek));
        }

        private static bool MatchesSemester(ScheduleRow row, int? semesterNumber)
        {
            return !semesterNumber.HasValue || row.SemesterNumber == semesterNumber.Value;
        }

        private static bool MatchesLectureType(ScheduleRow row, string? lectureType)
        {
            return string.IsNullOrWhiteSpace(lectureType) ||
                lectureType == "All" ||
                string.Equals(row.LectureType, lectureType, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesGroup(ScheduleRow row, string? groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName) || groupName == "All")
            {
                return true;
            }

            string normalizedGroup = groupName.Trim().ToUpperInvariant();

            if (string.Equals(row.LectureType, "Practical", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(row.GroupName, normalizedGroup, StringComparison.OrdinalIgnoreCase);
            }

            string baseSectionName = AcademicStructureRules.GetBaseSectionName(normalizedGroup);
            return string.Equals(row.SectionName, baseSectionName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesFaculty(ScheduleRow row, int? facultyId)
        {
            return !facultyId.HasValue || row.FacultyMemberID == facultyId.Value;
        }

        private static bool MatchesSection(ScheduleRow row, int? sectionId)
        {
            return !sectionId.HasValue || row.SectionID == sectionId.Value;
        }

        private static bool MatchesStudyYear(ScheduleRow row, int? studyYearId)
        {
            return !studyYearId.HasValue || row.StudyYearID == studyYearId.Value;
        }

        private static bool MatchesDay(ScheduleRow row, string? dayOfWeek)
        {
            return string.IsNullOrWhiteSpace(dayOfWeek) ||
                dayOfWeek == "All days" ||
                row.DayOfWeek == dayOfWeek;
        }
    }
}
