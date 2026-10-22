using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System
{
    internal sealed class ScheduleRow
    {
        public int ScheduleID { get; init; }
        public bool IsLocked { get; set; }
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
        public string StudyYearDisplay => StudyYearID switch
        {
            1 => "الأولى", 2 => "الثانية", 3 => "الثالثة", 4 => "الرابعة",
            _ => ArabicUi.Display(StudyYearName).Replace("المرحلة ", "")
        };
        public string SectionDisplay
        {
            get
            {
                string branch = BranchName.Trim().ToLowerInvariant() switch
                {
                    "computer science" or "علوم الحاسوب" or "علوم الحاسبات" or "cs" => "CS",
                    "cyber security" or "cybersecurity" or "الأمن السيبراني" or "الامن السيبراني" or "cys" => "CYS",
                    "information technology" or "تكنولوجيا المعلومات" or "تقنية المعلومات" or "it" => "IT",
                    _ => BranchName == "-" ? "" : BranchName.Trim()
                };
                // Fourth year is taught as a whole specialization, without subgroup labels.
                if (StudyYearID == 4) return branch;
                string section = LectureType == "Practical" && !string.IsNullOrWhiteSpace(GroupName) &&
                    !string.Equals(GroupName, "All", StringComparison.OrdinalIgnoreCase)
                    ? GroupName.Trim().ToUpperInvariant() : SectionName.Trim().ToUpperInvariant();
                return StudyYearID == 3 && branch.Length > 0 ? $"{section} / {branch}" : section;
            }
        }

        public static ScheduleRow FromDetails(ScheduleDetailsView details)
        {
            return new ScheduleRow
            {
                ScheduleID = details.ScheduleID,
                IsLocked = details.IsLocked,
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
    }
}
