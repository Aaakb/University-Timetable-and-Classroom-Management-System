using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    internal static class ScheduleConflictChecker
    {
        public static async Task<bool> HasClassroomConflictAsync(
            AppDbContext context,
            Schedule schedule,
            bool isUpdate)
        {
            return await context.Schedules.AnyAsync(existing =>
                existing.ClassroomID == schedule.ClassroomID &&
                existing.SemesterNumber == schedule.SemesterNumber &&
                existing.TimeSlotID == schedule.TimeSlotID &&
                existing.DayOfWeek == schedule.DayOfWeek &&
                (!isUpdate || existing.ScheduleID != schedule.ScheduleID));
        }

        public static async Task<bool> HasFacultyConflictAsync(
            AppDbContext context,
            Schedule schedule,
            bool isUpdate)
        {
            return await context.Schedules.AnyAsync(existing =>
                existing.FacultyMemberID == schedule.FacultyMemberID &&
                existing.SemesterNumber == schedule.SemesterNumber &&
                existing.TimeSlotID == schedule.TimeSlotID &&
                existing.DayOfWeek == schedule.DayOfWeek &&
                (!isUpdate || existing.ScheduleID != schedule.ScheduleID));
        }

        public static async Task<bool> HasSectionOrGroupConflictAsync(
            AppDbContext context,
            Schedule schedule,
            bool isUpdate)
        {
            string? groupName = NormalizeGroupName(schedule.GroupName);

            return await context.Schedules.AnyAsync(existing =>
                existing.StudyYearID == schedule.StudyYearID &&
                existing.BranchID == schedule.BranchID &&
                existing.SectionID == schedule.SectionID &&
                existing.SemesterNumber == schedule.SemesterNumber &&
                existing.TimeSlotID == schedule.TimeSlotID &&
                existing.DayOfWeek == schedule.DayOfWeek &&
                (groupName == null ||
                    existing.GroupName == null ||
                    existing.GroupName == string.Empty ||
                    existing.GroupName == groupName) &&
                (!isUpdate || existing.ScheduleID != schedule.ScheduleID));
        }

        public static bool HasAnyPairConflict(IReadOnlyList<Schedule> schedules)
        {
            for (int i = 0; i < schedules.Count; i++)
            {
                for (int j = i + 1; j < schedules.Count; j++)
                {
                    if (HasPairConflict(schedules[i], schedules[j]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool HasPairConflict(Schedule first, Schedule second)
        {
            return first.SemesterNumber == second.SemesterNumber &&
                first.DayOfWeek == second.DayOfWeek &&
                first.TimeSlotID == second.TimeSlotID &&
                (first.ClassroomID == second.ClassroomID ||
                    first.FacultyMemberID == second.FacultyMemberID ||
                    (first.SectionID == second.SectionID &&
                        (string.IsNullOrWhiteSpace(first.GroupName) ||
                            string.IsNullOrWhiteSpace(second.GroupName) ||
                            string.Equals(first.GroupName, second.GroupName, StringComparison.OrdinalIgnoreCase))));
        }

        private static string? NormalizeGroupName(string? groupName)
        {
            return string.IsNullOrWhiteSpace(groupName)
                ? null
                : groupName.Trim().ToUpperInvariant();
        }
    }
}
