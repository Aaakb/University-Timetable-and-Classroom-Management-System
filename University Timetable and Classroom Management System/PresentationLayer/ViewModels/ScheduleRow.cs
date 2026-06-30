using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System
{
    internal sealed class ScheduleRow
    {
        public int ScheduleID { get; init; }
        public int SubjectID { get; init; }
        public int FacultyMemberID { get; init; }
        public int ClassroomID { get; init; }
        public int TimeSlotID { get; init; }
        public int SemesterNumber { get; init; }
        public int? StudyYearID { get; init; }
        public int? BranchID { get; init; }
        public int? SectionID { get; init; }
        public string DayOfWeek { get; init; } = string.Empty;
        public string SubjectName { get; init; } = string.Empty;
        public string FacultyMemberName { get; init; } = string.Empty;
        public string ClassroomName { get; init; } = string.Empty;
        public string TimeSlotName { get; init; } = string.Empty;
        public TimeSpan StartTime { get; init; }
        public TimeSpan EndTime { get; init; }
        public string StartTimeText { get; init; } = string.Empty;
        public string EndTimeText { get; init; } = string.Empty;
        public string StudyYearName { get; init; } = string.Empty;
        public string BranchName { get; init; } = string.Empty;
        public string SectionName { get; init; } = string.Empty;
        public string GroupName { get; init; } = "All";
        public string LectureType { get; init; } = "Theory";

        public static ScheduleRow FromSchedule(Schedule schedule)
        {
            string timeSlotName = schedule.TimeSlot is null
                ? "-"
                : TimeDisplay.FormatRange(schedule.TimeSlot.StartTime, schedule.TimeSlot.EndTime);

            return new ScheduleRow
            {
                ScheduleID = schedule.ScheduleID,
                SubjectID = schedule.SubjectID,
                FacultyMemberID = schedule.FacultyMemberID,
                ClassroomID = schedule.ClassroomID,
                TimeSlotID = schedule.TimeSlotID,
                SemesterNumber = schedule.SemesterNumber,
                StudyYearID = schedule.StudyYearID,
                BranchID = schedule.BranchID,
                SectionID = schedule.SectionID,
                DayOfWeek = schedule.DayOfWeek,
                SubjectName = schedule.Subject?.SubjectName ?? "-",
                FacultyMemberName = schedule.FacultyMember?.FullName ?? "-",
                ClassroomName = schedule.Classroom?.ClassroomNumber ?? "-",
                TimeSlotName = timeSlotName,
                StartTime = schedule.TimeSlot?.StartTime ?? TimeSpan.Zero,
                EndTime = schedule.TimeSlot?.EndTime ?? TimeSpan.Zero,
                StartTimeText = schedule.TimeSlot is null ? "-" : TimeDisplay.Format(schedule.TimeSlot.StartTime),
                EndTimeText = schedule.TimeSlot is null ? "-" : TimeDisplay.Format(schedule.TimeSlot.EndTime),
                StudyYearName = schedule.StudyYear?.YearName ?? "-",
                BranchName = schedule.Branch?.BranchName ?? "-",
                SectionName = FormatScheduleSection(schedule),
                GroupName = string.IsNullOrWhiteSpace(schedule.GroupName) ? "All" : schedule.GroupName,
                LectureType = schedule.LectureType
            };
        }

        public static ScheduleRow FromDetails(ScheduleDetailsView details)
        {
            return new ScheduleRow
            {
                ScheduleID = details.ScheduleID,
                SubjectID = details.SubjectID,
                FacultyMemberID = details.FacultyMemberID,
                ClassroomID = details.ClassroomID,
                TimeSlotID = details.TimeSlotID,
                SemesterNumber = details.SemesterNumber,
                StudyYearID = details.StudyYearID,
                BranchID = details.BranchID,
                SectionID = details.SectionID,
                DayOfWeek = details.DayOfWeek,
                SubjectName = details.SubjectName,
                FacultyMemberName = details.FacultyMemberName,
                ClassroomName = details.ClassroomNumber,
                TimeSlotName = TimeDisplay.FormatRange(details.StartTime, details.EndTime),
                StartTime = details.StartTime,
                EndTime = details.EndTime,
                StartTimeText = TimeDisplay.Format(details.StartTime),
                EndTimeText = TimeDisplay.Format(details.EndTime),
                StudyYearName = details.YearName,
                BranchName = string.IsNullOrWhiteSpace(details.BranchName) ? "-" : details.BranchName,
                SectionName = details.SectionName,
                GroupName = string.IsNullOrWhiteSpace(details.GroupName) ? "All" : details.GroupName,
                LectureType = details.LectureType
            };
        }

        private static string FormatScheduleSection(Schedule schedule)
        {
            if (schedule.Section is null)
            {
                return "-";
            }

            string year = schedule.StudyYear?.YearName ?? schedule.Section.StudyYear?.YearName ?? "Year";
            string? branch = schedule.Branch?.BranchName ?? schedule.Section.Branch?.BranchName;

            return string.IsNullOrWhiteSpace(branch)
                ? $"{schedule.Section.SectionName} - {year}"
                : $"{schedule.Section.SectionName} - {year} - {branch}";
        }
    }
}
