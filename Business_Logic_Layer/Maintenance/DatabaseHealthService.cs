using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    public sealed class DatabaseHealthService
    {
        public async Task<DatabaseHealthResult> CheckConnectionAsync()
        {
            try
            {
                await using var context = new AppDbContext();
                await context.Database.OpenConnectionAsync();

                string dataSource = context.Database.GetDbConnection().DataSource;
                string database = context.Database.GetDbConnection().Database;

                return new DatabaseHealthResult(
                    true,
                    $"Connected to {dataSource} / {database}");
            }
            catch (Exception ex)
            {
                return new DatabaseHealthResult(false, ex.Message);
            }
        }

        public async Task<IReadOnlyDictionary<string, int>> GetEntityCountsAsync()
        {
            await using var context = new AppDbContext();

            return new Dictionary<string, int>
            {
                ["Subjects"] = await context.Subjects.CountAsync(),
                ["Faculty"] = await context.FacultyMembers.CountAsync(),
                ["Classrooms"] = await context.Classrooms.CountAsync(),
                ["Schedules"] = await context.Schedules.CountAsync(),
                ["Sections"] = await context.Sections.CountAsync(),
                ["Teaching"] = await context.FacultyMemberSubjects.CountAsync(),
                ["TimeSlots"] = await context.TimeSlots.CountAsync(slot => !slot.IsBreak)
            };
        }

        public async Task<ScheduleReadinessResult> GetScheduleReadinessAsync()
        {
            await using var context = new AppDbContext();

            var subjects = await context.Subjects.AsNoTracking().ToListAsync();
            var assignedSubjectIds = await context.FacultyMemberSubjects
                .AsNoTracking()
                .Select(assignment => assignment.SubjectID)
                .Distinct()
                .ToListAsync();
            var sections = await context.Sections.AsNoTracking().ToListAsync();
            var classrooms = await context.Classrooms.AsNoTracking().ToListAsync();
            int nonBreakTimeSlots = await context.TimeSlots.CountAsync(slot => !slot.IsBreak);

            var assignedSubjectIdSet = assignedSubjectIds.ToHashSet();
            int unassignedSubjects = subjects.Count(subject => !assignedSubjectIdSet.Contains(subject.SubjectID));
            int subjectsWithoutSections = subjects.Count(subject =>
                !sections.Any(section =>
                    section.StudyYearID == subject.StudyYearID &&
                    AcademicStructureRules.SectionMatchesSubject(
                        subject.StudyYearID,
                        subject.BranchID,
                        section.BranchID,
                        section.SectionName)));
            int oversizedSections = sections.Count(section =>
                classrooms.Count == 0 || !classrooms.Any(classroom => classroom.Capacity >= section.StudentCount));

            return new ScheduleReadinessResult(
                unassignedSubjects,
                subjectsWithoutSections,
                oversizedSections,
                nonBreakTimeSlots,
                classrooms.Count,
                sections.Count);
        }
    }

    public sealed record DatabaseHealthResult(bool CanConnect, string Message);

    public sealed record ScheduleReadinessResult(
        int UnassignedSubjects,
        int SubjectsWithoutSections,
        int OversizedSections,
        int NonBreakTimeSlots,
        int ClassroomCount,
        int SectionCount);
}
