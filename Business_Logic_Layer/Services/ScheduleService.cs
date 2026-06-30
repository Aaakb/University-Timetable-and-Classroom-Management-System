using Microsoft.EntityFrameworkCore;
using Data_Access_Layer;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    public class ScheduleService
    {
        public async Task<List<Schedule>> GetAllAsync()
        {
            await using var context = new AppDbContext();
            return await context.Schedules
                .Include(s => s.Subject)
                .Include(s => s.FacultyMember)
                .Include(s => s.Classroom)
                .Include(s => s.TimeSlot)
                .Include(s => s.StudyYear)
                .Include(s => s.Branch)
                .Include(s => s.Section)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<ScheduleDetailsView>> GetScheduleDetailsAsync()
        {
            return await GetScheduleDetailsByFiltersAsync(null, null, null, null, null);
        }

        public async Task<List<ScheduleDetailsView>> GetScheduleDetailsBySemesterAsync(int semesterNumber)
        {
            return await GetScheduleDetailsByFiltersAsync(semesterNumber, null, null, null, null);
        }

        public async Task<List<ScheduleDetailsView>> GetScheduleDetailsByFiltersAsync(
            int? semesterNumber,
            int? studyYearId,
            int? branchId,
            int? sectionId,
            string? lectureType = null)
        {
            await using var context = new AppDbContext();
            var query = context.ScheduleDetails.AsNoTracking().AsQueryable();

            if (semesterNumber.HasValue)
            {
                query = query.Where(schedule => schedule.SemesterNumber == semesterNumber.Value);
            }

            if (!string.IsNullOrWhiteSpace(lectureType) &&
                !string.Equals(lectureType, "All", StringComparison.OrdinalIgnoreCase))
            {
                string normalizedLectureType = NormalizeLectureType(lectureType);
                query = query.Where(schedule => schedule.LectureType == normalizedLectureType);
            }

            if (studyYearId.HasValue || branchId.HasValue || sectionId.HasValue)
            {
                var scheduleIds = context.Schedules.AsNoTracking().AsQueryable();

                if (studyYearId.HasValue)
                {
                    scheduleIds = scheduleIds.Where(schedule => schedule.StudyYearID == studyYearId.Value);
                }

                if (branchId.HasValue)
                {
                    scheduleIds = scheduleIds.Where(schedule => schedule.BranchID == branchId.Value);
                }

                if (sectionId.HasValue)
                {
                    scheduleIds = scheduleIds.Where(schedule => schedule.SectionID == sectionId.Value);
                }

                query = query.Where(schedule => scheduleIds
                    .Select(item => item.ScheduleID)
                    .Contains(schedule.ScheduleID));
            }

            var rows = await query.ToListAsync();

            return rows
                .OrderBy(row => StudyYearOrder(row.YearName))
                .ThenBy(row => row.BranchName)
                .ThenBy(row => row.SectionName)
                .ThenBy(row => row.SemesterNumber)
                .ThenBy(row => ScheduleDayRules.GetDayOrder(row.DayOfWeek))
                .ThenBy(row => row.StartTime)
                .ToList();
        }

        public async Task<Schedule?> GetByIdAsync(int id)
        {
            await using var context = new AppDbContext();
            return await context.Schedules
                .Include(s => s.Subject)
                .Include(s => s.FacultyMember)
                .Include(s => s.Classroom)
                .Include(s => s.TimeSlot)
                .Include(s => s.StudyYear)
                .Include(s => s.Branch)
                .Include(s => s.Section)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ScheduleID == id);
        }

        public async Task<Schedule> AddAsync(Schedule schedule)
        {
            await using var context = new AppDbContext();
            await ValidateAsync(context, schedule, false);
            await context.Schedules.AddAsync(schedule);
            await context.SaveChangesAsync();
            return schedule;
        }

        public async Task<Schedule> UpdateAsync(Schedule schedule)
        {
            await using var context = new AppDbContext();
            await ValidateAsync(context, schedule, true);
            context.Schedules.Update(schedule);
            await context.SaveChangesAsync();
            return schedule;
        }

        public bool IsClassroomCapacityEnough(int classroomId, int sectionId)
        {
            using var context = new AppDbContext();

            var classroom = context.Classrooms
                .AsNoTracking()
                .FirstOrDefault(item => item.ClassroomID == classroomId);
            var section = context.Sections
                .AsNoTracking()
                .FirstOrDefault(item => item.SectionID == sectionId);

            return classroom is not null &&
                section is not null &&
                ScheduleRoomSelector.HasCapacity(classroom, section);
        }

        public async Task DeleteAsync(int id)
        {
            await using var context = new AppDbContext();
            var schedule = await context.Schedules.FindAsync(id)
                ?? throw new KeyNotFoundException("Schedule not found.");

            context.Schedules.Remove(schedule);
            await context.SaveChangesAsync();
        }

        public async Task<ScheduleGenerationResult> GenerateAsync()
        {
            await using var context = new AppDbContext();
            var resourceResult = await SchedulingResourceMaintenance.EnsureOfficialResourcesAsync(context);

            var subjects = await context.Subjects
                .AsNoTracking()
                .ToListAsync();

            var assignments = await context.FacultyMemberSubjects
                .Include(fms => fms.Subject)
                .OrderBy(fms => fms.Subject.StudyYearID)
                .ThenBy(fms => fms.Subject.SubjectName)
                .AsNoTracking()
                .ToListAsync();

            var sections = (await context.Sections
                .AsNoTracking()
                .OrderBy(section => section.StudyYearID)
                .ThenBy(section => section.BranchID)
                .ThenBy(section => section.SectionName)
                .ToListAsync())
                .Where(IsSchedulableSection)
                .ToList();

            var classrooms = await context.Classrooms
                .AsNoTracking()
                .OrderBy(classroom => classroom.Capacity)
                .ThenBy(classroom => classroom.ClassroomNumber)
                .ToListAsync();

            var timeSlots = (await context.TimeSlots
                .AsNoTracking()
                .Where(slot => !slot.IsBreak)
                .OrderBy(slot => slot.StartTime)
                .ToListAsync())
                .Where(ScheduleTimingRules.IsOfficialLectureSlot)
                .ToList();

            var existingSchedules = await context.Schedules.ToListAsync();
            var generatedSchedules = new List<Schedule>();
            int skippedCount = 0;
            int missingSectionCount = 0;
            int noClassroomCount = 0;
            int conflictCount = 0;
            int duplicateAssignmentCount = assignments
                .GroupBy(assignment => new { assignment.FacultyMemberID, assignment.SubjectID })
                .Sum(group => Math.Max(0, group.Count() - 1));

            var assignedSubjectIds = assignments
                .Select(assignment => assignment.SubjectID)
                .ToHashSet();
            int unassignedSubjectsCount = subjects.Count(subject => !assignedSubjectIds.Contains(subject.SubjectID));

            var subjectTeachingAssignments = assignments
                .GroupBy(assignment => assignment.SubjectID)
                .Select(group => new SubjectTeachingAssignment(
                    group.First().Subject,
                    group.Select(assignment => assignment.FacultyMemberID)
                        .Distinct()
                        .OrderBy(facultyMemberId => facultyMemberId)
                        .ToList()))
                .ToList();

            var scheduleRequests = subjectTeachingAssignments
                .SelectMany(teachingAssignment =>
                {
                    var subject = teachingAssignment.Subject;
                    var matchingSections = sections
                        .Where(section =>
                            section.StudyYearID == subject.StudyYearID &&
                            AcademicStructureRules.SectionMatchesSubject(
                                subject.StudyYearID,
                                subject.BranchID,
                                section.BranchID,
                                section.SectionName))
                        .ToList();

                    if (matchingSections.Count == 0)
                    {
                        skippedCount++;
                        missingSectionCount++;
                    }

                    return matchingSections.SelectMany(section => ScheduleGenerator.CreateScheduleRequests(teachingAssignment, section));
                })
                .OrderByDescending(request => request.Subject.StudyYearID)
                .ThenBy(request => request.Subject.BranchID ?? request.Section.BranchID ?? 0)
                .ThenBy(request => request.Subject.SemesterNumber)
                .ThenBy(request => request.Subject.SubjectName)
                .ThenBy(request => request.LessonNumber)
                .ThenBy(request => request.Section.SectionName)
                .ToList();

            var planningResult = ScheduleGenerator.BuildPlan(
                scheduleRequests,
                new ScheduleBuildContext(classrooms, timeSlots));

            generatedSchedules.AddRange(planningResult.Schedules);
            skippedCount += planningResult.NoClassroomCount + planningResult.ConflictCount;
            noClassroomCount += planningResult.NoClassroomCount;
            conflictCount += planningResult.ConflictCount;

            await using var transaction = await context.Database.BeginTransactionAsync();

            context.Schedules.RemoveRange(existingSchedules);
            await context.SaveChangesAsync();

            await context.Schedules.AddRangeAsync(generatedSchedules);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new ScheduleGenerationResult(
                generatedSchedules.Count,
                skippedCount,
                scheduleRequests.Count,
                unassignedSubjectsCount,
                missingSectionCount,
                noClassroomCount,
                conflictCount,
                duplicateAssignmentCount,
                timeSlots.Count,
                classrooms.Count,
                sections.Count,
                resourceResult.AddedTimeSlots,
                resourceResult.AddedClassrooms,
                resourceResult.AddedFacultyMembers,
                resourceResult.AddedFacultySubjectAssignments);
        }

        private static async Task ValidateAsync(AppDbContext context, Schedule schedule, bool isUpdate)
        {
            if (string.IsNullOrWhiteSpace(schedule.DayOfWeek))
            {
                throw new ArgumentException("Day of week is required.");
            }

            schedule.DayOfWeek = ScheduleDayRules.NormalizeDayName(schedule.DayOfWeek);

            var subject = await context.Subjects
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SubjectID == schedule.SubjectID);

            if (subject is null)
            {
                throw new ArgumentException("Subject does not exist.");
            }

            if (!await context.FacultyMembers.AnyAsync(f => f.FacultyMemberID == schedule.FacultyMemberID))
            {
                throw new ArgumentException("Faculty member does not exist.");
            }

            if (!await CanFacultyTeachSubjectAsync(context, schedule.FacultyMemberID, schedule.SubjectID))
            {
                throw new ArgumentException("The selected faculty member is not assigned to teach this subject.");
            }

            var classroom = await context.Classrooms
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ClassroomID == schedule.ClassroomID);

            if (classroom is null)
            {
                throw new ArgumentException("Classroom does not exist.");
            }

            if (!await context.TimeSlots.AnyAsync(t => t.TimeSlotID == schedule.TimeSlotID))
            {
                throw new ArgumentException("Time slot does not exist.");
            }

            if (!schedule.SectionID.HasValue)
            {
                throw new ArgumentException("Section is required.");
            }

            var section = await context.Sections
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SectionID == schedule.SectionID.Value);

            if (section is null)
            {
                throw new ArgumentException("Section does not exist.");
            }

            EnsureBaseSectionIsUsed(section);
            NormalizeLectureTypeAndGroup(schedule, section);
            ScheduleRoomSelector.EnsureMatchesLectureType(classroom, schedule.LectureType);

            if (!ScheduleRoomSelector.HasCapacity(classroom, section, schedule.GroupName))
            {
                throw new ArgumentException("The selected classroom capacity is not enough for this section.");
            }

            if (section.StudyYearID != subject.StudyYearID)
            {
                throw new ArgumentException("The selected section does not belong to the subject study year.");
            }

            if (!AcademicStructureRules.SectionMatchesSubject(
                subject.StudyYearID,
                subject.BranchID,
                section.BranchID,
                section.SectionName))
            {
                throw new ArgumentException("The selected section is not valid for this subject.");
            }

            schedule.StudyYearID = subject.StudyYearID;
            schedule.BranchID = subject.BranchID ?? section.BranchID;
            schedule.SemesterNumber = subject.SemesterNumber;

            await EnsureNoConflictsAsync(context, schedule, isUpdate);
        }

        private static async Task EnsureNoConflictsAsync(AppDbContext context, Schedule schedule, bool isUpdate)
        {
            if (await ScheduleConflictChecker.HasClassroomConflictAsync(context, schedule, isUpdate))
            {
                throw new ArgumentException("This classroom is already booked in the selected semester, day, and time slot.");
            }

            if (await ScheduleConflictChecker.HasFacultyConflictAsync(context, schedule, isUpdate))
            {
                throw new ArgumentException("This faculty member is already booked in the selected semester, day, and time slot.");
            }

            if (await ScheduleConflictChecker.HasSectionOrGroupConflictAsync(context, schedule, isUpdate))
            {
                throw new ArgumentException("This section or group already has a schedule in the selected semester, day, and time slot.");
            }
        }

        private static async Task<bool> CanFacultyTeachSubjectAsync(
            AppDbContext context,
            int facultyMemberId,
            int subjectId)
        {
            return await context.FacultyMemberSubjects.AnyAsync(assignment =>
                assignment.FacultyMemberID == facultyMemberId &&
                assignment.SubjectID == subjectId);
        }

        private static void EnsureBaseSectionIsUsed(Section section)
        {
            if (!AcademicStructureRules.UsesGeneralSections(section.StudyYearID))
            {
                return;
            }

            var allowedSections = AcademicStructureRules.GetAllowedSectionNames(section.StudyYearID);

            if (!allowedSections.Contains(section.SectionName.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException("A1, A2, B1, and B2 must be stored as practical groups, not as independent sections.");
            }
        }

        private static bool IsSchedulableSection(Section section)
        {
            if (AcademicStructureRules.UsesGeneralSections(section.StudyYearID))
            {
                return !section.BranchID.HasValue &&
                    AcademicStructureRules.GetAllowedSectionNames(section.StudyYearID)
                        .Contains(section.SectionName.Trim(), StringComparer.OrdinalIgnoreCase);
            }

            return true;
        }

        private static void NormalizeLectureTypeAndGroup(Schedule schedule, Section section)
        {
            schedule.LectureType = NormalizeLectureType(schedule.LectureType);

            if (schedule.LectureType == "Theory")
            {
                schedule.GroupName = null;
                return;
            }

            string? normalizedGroupName = string.IsNullOrWhiteSpace(schedule.GroupName)
                ? null
                : schedule.GroupName.Trim().ToUpperInvariant();

            if (!IsValidGroupForSection(section, normalizedGroupName))
            {
                throw new ArgumentException("Practical sessions must use A1/A2 for section A or B1/B2 for section B.");
            }

            schedule.GroupName = normalizedGroupName;
        }

        private static bool IsValidGroupForSection(Section section, string? groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                return false;
            }

            string normalizedGroupName = groupName.Trim().ToUpperInvariant();

            if (!AcademicStructureRules.GetAllowedPracticalGroupNames(section.SectionName)
                .Contains(normalizedGroupName, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            string baseSectionName = AcademicStructureRules.GetBaseSectionName(normalizedGroupName);
            return string.Equals(baseSectionName, section.SectionName.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeLectureType(string? lectureType)
        {
            if (string.Equals(lectureType, "Practical", StringComparison.OrdinalIgnoreCase))
            {
                return "Practical";
            }

            if (string.IsNullOrWhiteSpace(lectureType) ||
                string.Equals(lectureType, "Theory", StringComparison.OrdinalIgnoreCase))
            {
                return "Theory";
            }

            throw new ArgumentException("Lecture type must be Theory or Practical.");
        }

        private static int StudyYearOrder(string yearName)
        {
            return yearName.Trim().ToLowerInvariant() switch
            {
                "first year" => 1,
                "second year" => 2,
                "third year" => 3,
                "fourth year" => 4,
                _ => 99
            };
        }

    }

}
