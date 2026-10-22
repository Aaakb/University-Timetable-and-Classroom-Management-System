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
            await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            await ValidateAsync(context, schedule, false);
            await context.Schedules.AddAsync(schedule);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return schedule;
        }

        public async Task<Schedule> UpdateAsync(Schedule schedule)
        {
            await using var context = new AppDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            await ValidateAsync(context, schedule, true);
            var existing = await context.Schedules.FindAsync(schedule.ScheduleID)
                ?? throw new KeyNotFoundException("Schedule not found.");
            // The ordinary editor has no lock field; retain the persisted lock.
            schedule.IsLocked = existing.IsLocked;
            context.Entry(existing).CurrentValues.SetValues(schedule);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return schedule;
        }

        public async Task DeleteAsync(int id)
        {
            await using var context = new AppDbContext();
            var schedule = await context.Schedules.FindAsync(id)
                ?? throw new KeyNotFoundException("Schedule not found.");

            context.Schedules.Remove(schedule);
            await context.SaveChangesAsync();
        }

        public async Task SetLockedAsync(int id, bool locked)
        {
            await using var context = new AppDbContext();
            var schedule = await context.Schedules.FindAsync(id)
                ?? throw new KeyNotFoundException("Schedule not found.");
            schedule.IsLocked = locked;
            await context.SaveChangesAsync();
        }

        public Task<ScheduleGenerationResult> GenerateAsync() => BuildTimetableAsync(regenerate: false, null);

        public Task<ScheduleGenerationResult> RegenerateAsync(string? safetyBackupPath = null) =>
            BuildTimetableAsync(regenerate: true, safetyBackupPath);

        private async Task<ScheduleGenerationResult> BuildTimetableAsync(bool regenerate, string? safetyBackupPath)
        {
            await using var context = new AppDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var preferences = FacultyPreferenceRules.FromRows(await context.FacultySchedulingPreferences.AsNoTracking().ToListAsync(),
                await context.FacultyPeriodPreferences.AsNoTracking().ToListAsync());
            var allSubjects = await context.Subjects
                .AsNoTracking()
                .ToListAsync();
            var subjects = allSubjects.Where(subject => subject.HasWeeklyLectures).ToList();

            var assignments = await context.FacultyMemberSubjects
                .Include(fms => fms.Subject)
                .OrderBy(fms => fms.Subject.StudyYearID)
                .ThenBy(fms => fms.Subject.SubjectName)
                .AsNoTracking()
                .ToListAsync();
            assignments.RemoveAll(assignment => !assignment.Subject.HasWeeklyLectures);

            var sections = (await context.Sections
                .Include(section => section.StudyYear)
                .Include(section => section.Branch)
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

            if (classrooms.Count == 0)
                throw new ArgumentException("Add classrooms before generating. The existing timetable has been kept.");

            var timeSlots = await context.TimeSlots
                .AsNoTracking()
                .Where(slot => !slot.IsBreak)
                .OrderBy(slot => slot.StartTime)
                .ToListAsync();
            ScheduleTimingRules.EnsureCompatibleLectureSlots(timeSlots);

            var existingSchedules = await context.Schedules.ToListAsync();
            var generationInput = regenerate ? existingSchedules.Where(s => s.IsLocked).ToList() : existingSchedules;
            int variationSeed = regenerate ? Random.Shared.Next(1, int.MaxValue) : 0;
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

            var unassignedPractical = subjects.Where(subject => subject.EffectivePracticalLectureCount > 0 &&
                !assignedSubjectIds.Contains(subject.SubjectID)).ToList();
            if (unassignedPractical.Count > 0)
                throw new ArgumentException("Assign teachers to all practical subjects before generating: " +
                    string.Join(", ", unassignedPractical.Select(subject => subject.SubjectName)) +
                    ". The existing timetable has been kept.");
            if (unassignedSubjectsCount > 0)
                throw new ArgumentException("Assign teachers to all subjects before generating. The existing timetable has been kept.");

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
                        if (subject.EffectivePracticalLectureCount > 0)
                            throw new ArgumentException($"Add matching sections for practical subject {subject.SubjectName} before generating. The existing timetable has been kept.");
                        skippedCount++;
                        missingSectionCount++;
                    }

                    return matchingSections.SelectMany(section =>
                    {
                        var facultyIds = FacultyAssignmentRules.FacultyIds(assignments, subject.SubjectID, section.SectionID);
                        if (facultyIds.Count == 0)
                            throw new ArgumentException($"لا يوجد تكليف تدريسي لمادة «{subject.SubjectName}» للشعبة {section.SectionName} في مرحلتها وتخصصها. أكمل التكليفات قبل التوليد؛ بقي الجدول الحالي محفوظًا.");
                        return ScheduleGenerator.CreateScheduleRequests(subject, section, assignments);
                    });
                })
                .OrderByDescending(request => request.Subject.StudyYearID)
                .ThenBy(request => request.Subject.BranchID ?? request.Section.BranchID ?? 0)
                .ThenBy(request => request.Subject.SemesterNumber)
                .ThenBy(request => request.Subject.SubjectName)
                .ThenBy(request => request.LessonNumber)
                .ThenBy(request => request.Section.SectionName)
                .ToList();

            if (missingSectionCount > 0)
                throw new ArgumentException("Add matching sections for all subjects before generating. The existing timetable has been kept.");

            // Retain valid placements for this run without setting permanent locks.
            // Explicit locks win conflicts; otherwise use stable IDs for repeatable repairs.
            var retainedSchedules = new List<Schedule>();
            var displacedSchedules = new List<Schedule>();
            foreach (var existing in generationInput.OrderByDescending(s => s.IsLocked).ThenBy(s => s.ScheduleID))
            {
                string original = ImportCatalog.ScalarJson(existing);
                await ValidateAsync(context, existing, true, checkConflicts: false);
                if (original != ImportCatalog.ScalarJson(existing))
                    throw new ArgumentException("An existing lecture needs manual correction. The existing timetable has been kept.");

                if (retainedSchedules.Any(saved => ScheduleConflictChecker.HasPairConflict(saved, existing)))
                {
                    if (existing.IsLocked)
                        throw new ArgumentException("Locked lectures conflict. The existing timetable has been kept.");
                    displacedSchedules.Add(existing);
                }
                else retainedSchedules.Add(existing);
            }

            // Repair every displaced lecture, including extra lessons and whole-section
            // practicals in an imported timetable. Never silently drop them.
            var repairRequests = displacedSchedules.Select((lecture, index) => new ScheduleGenerationRequest(
                allSubjects.Single(subject => subject.SubjectID == lecture.SubjectID),
                new[] { lecture.FacultyMemberID },
                sections.Single(section => section.SectionID == lecture.SectionID),
                scheduleRequests.Count + index + 1, 1, lecture.LectureType, lecture.GroupName)).ToList();
            var remainingRequests = ScheduleRequirementMatcher.Remaining(scheduleRequests, generationInput);
            remainingRequests.AddRange(repairRequests);
            var planningResult = ScheduleGenerator.BuildPlan(
                remainingRequests,
                new ScheduleBuildContext(classrooms, timeSlots)
                { RequirePracticalExchange = false, EnforceSectionDayLimit = regenerate,
                    FixedSchedules = retainedSchedules, Preferences = preferences, VariationSeed = variationSeed });

            bool Complete(SchedulePlanningResult plan) => plan.PracticalExchangeIssues.Count == 0 &&
                plan.NoClassroomCount == 0 && plan.ConflictCount == 0 &&
                ScheduleRequirementMatcher.Remaining(remainingRequests, plan.Schedules.Except(retainedSchedules).ToList()).Count == 0;
            if (regenerate && !Complete(planningResult))
            {
                // Variation must not make an otherwise feasible baseline fail.
                var baseline = ScheduleGenerator.BuildPlan(remainingRequests,
                    new ScheduleBuildContext(classrooms, timeSlots)
                    { RequirePracticalExchange = false, EnforceSectionDayLimit = false,
                        FixedSchedules = retainedSchedules, Preferences = preferences });
                if (Complete(baseline)) planningResult = baseline;
            }
            if (retainedSchedules.Count > 0 && remainingRequests.Count > 0 && !Complete(planningResult))
            {
                var expanded = ScheduleGenerator.BuildPlan(remainingRequests,
                    new ScheduleBuildContext(classrooms, timeSlots)
                    { RequirePracticalExchange = false, EnforceSectionDayLimit = regenerate, FixedSchedules = retainedSchedules,
                        UseAllTeachingDays = true, Preferences = preferences, VariationSeed = variationSeed });
                if (Complete(expanded)) planningResult = expanded;
            }

            if (regenerate && !Complete(planningResult))
            {
                var catalog = await ImportCatalog.LoadAsync(context, default);
                var solved = await FreshTimetablePlanner.CompleteAsync(remainingRequests, retainedSchedules,
                    planningResult.Schedules, catalog);
                if (solved.Schedules is not null)
                    planningResult = new(solved.Schedules, 0, 0);
                else
                {
                    string reason = solved.Status == "Infeasible"
                        ? "لم يجد محلل القيود توزيعًا يحقق الموارد والتكليفات والأيام الإلزامية."
                        : "لم يكتمل البحث عن جدول كامل خلال المهلة؛ هذا لا يثبت استحالة الحل.";
                    throw new ArgumentException(reason + Environment.NewLine +
                        string.Join(Environment.NewLine, solved.Diagnostics.Take(6)) +
                        Environment.NewLine + "بقي الجدول الحالي والتفضيلات محفوظين دون تغيير.");
                }
            }

            if (planningResult.PracticalExchangeIssues.Count > 0)
            {
                string details = string.Join(Environment.NewLine, planningResult.PracticalExchangeIssues.Take(6));
                if (planningResult.PracticalExchangeIssues.Count > 6)
                    details += $"{Environment.NewLine}And {planningResult.PracticalExchangeIssues.Count - 6} more section(s).";
                throw new ArgumentException("Unable to place all practical groups. " +
                    "The existing timetable has been kept." + Environment.NewLine + details);
            }

            generatedSchedules.AddRange(planningResult.Schedules);
            skippedCount += planningResult.NoClassroomCount + planningResult.ConflictCount;
            noClassroomCount += planningResult.NoClassroomCount;
            conflictCount += planningResult.ConflictCount;

            if (generatedSchedules.Count == 0)
            {
                throw new ArgumentException("No lessons could be generated. Check subject hours, teaching assignments, sections, rooms, and time slots. The existing timetable has been kept.");
            }

            if (skippedCount > 0 || ScheduleRequirementMatcher.Remaining(scheduleRequests, generatedSchedules).Count > 0)
            {
                var unresolved = ScheduleRequirementMatcher.Remaining(remainingRequests,
                    generatedSchedules.Except(retainedSchedules).ToList());
                string details = string.Join(Environment.NewLine, unresolved.Take(8).Select(r =>
                    regenerate
                        ? $"{r.Subject.SubjectName}: المرحلة {r.Subject.StudyYearID}، الشعبة {r.Section.SectionName}، الفصل {r.Subject.SemesterNumber}، " +
                            (r.LectureType == "Practical" ? "عملي" : "نظري") + (string.IsNullOrWhiteSpace(r.GroupName) ? "" : $"، المجموعة {r.GroupName}")
                        : $"{r.Subject.SubjectName}: year {r.Subject.StudyYearID}, section {r.Section.SectionName}, semester {r.Subject.SemesterNumber}, {r.LectureType}" +
                            (string.IsNullOrWhiteSpace(r.GroupName) ? "" : $", group {r.GroupName}")));
                string failure = regenerate
                    ? $"تعذر توزيع {unresolved.Count} من أصل {remainingRequests.Count} محاضرة في الجدول الجديد. راجع أعداد المحاضرات والتكليفات والقاعات والفترات والتفضيلات. بقي الجدول الحالي محفوظًا."
                    : $"Unable to place {unresolved.Count} of {remainingRequests.Count} additional lessons without moving existing lectures. " +
                    "No complete solution was found within the current resources and search limits. The existing timetable has been kept." +
                    Environment.NewLine;
                throw new ArgumentException(failure + Environment.NewLine + details);
            }

            var newPlacements = generatedSchedules.Except(retainedSchedules).ToList();
            if ((regenerate || preferences.Values.Any(p => p.HasScoredPreferences)) && newPlacements.Count > 0)
            {
                var catalog = await ImportCatalog.LoadAsync(context, default);
                foreach (var batch in newPlacements.GroupBy(s => s.SemesterNumber).ToList())
                {
                    var batchRows = batch.ToList();
                    var optimizationRows = batchRows;
                    if (regenerate)
                    {
                        var semesterRows = generatedSchedules.Where(s => s.SemesterNumber == batch.Key).ToList();
                        var affected = semesterRows.GroupBy(s => s.SectionID).Where(g =>
                            g.Select(s => s.DayOfWeek).Distinct().Count() > ScheduleDayRules.MaximumSectionTeachingDays).Select(g => g.Key).ToHashSet();
                        foreach (var preference in preferences.Where(p => p.Key.Semester == batch.Key))
                        {
                            var own = semesterRows.Where(s => s.FacultyMemberID == preference.Key.Faculty).ToList();
                            if (FacultyPreferenceRules.Violation(preference.Value, own, timeSlots.ToDictionary(p => p.TimeSlotID), requireAttendance: true) is not null ||
                                own.Any(s => FacultyPreferenceRules.Level(preference.Value, s.DayOfWeek, s.TimeSlotID) == FacultyPreferenceLevel.Preferred))
                                foreach (var lecture in own) affected.Add(lecture.SectionID);
                        }
                        optimizationRows = batchRows.Where(s => affected.Contains(s.SectionID)).ToList();
                        if (optimizationRows.Count == 0) continue;
                    }
                    System.Diagnostics.Trace.WriteLine($"Generation optimization: semester={batch.Key}, movable={optimizationRows.Count}, total={batchRows.Count}");
                    var optimized = await Task.Run(() => ImportedTimetableRepairPlanner.Solve(catalog,
                        optimizationRows.Select(s => new TimetableImportRow("Generated", 0, s, s.IsLocked)).ToList(),
                        generatedSchedules.Except(optimizationRows).ToList(), 20, default, optimizePreferences: true,
                        allowFacultyReassignment: regenerate, requirePracticalExchange: !regenerate));
                    if (regenerate && optimized.Plan is null && optimizationRows.Count < batchRows.Count)
                    {
                        optimizationRows = batchRows;
                        optimized = await Task.Run(() => ImportedTimetableRepairPlanner.Solve(catalog,
                            batchRows.Select(s => new TimetableImportRow("Generated", 0, s, s.IsLocked)).ToList(),
                            generatedSchedules.Except(batchRows).ToList(), 40, default, optimizePreferences: true,
                            allowFacultyReassignment: true, requirePracticalExchange: false));
                    }
                    if (optimized.Plan is null)
                    {
                        string reason = optimized.Status == "Infeasible"
                            ? "لا يوجد توزيع يجمع القيود الحالية للفصل الدراسي " + batch.Key + "."
                            : "لم يكتمل البحث عن توزيع للفصل الدراسي " + batch.Key + " خلال المهلة. هذا لا يثبت استحالة الحل.";
                        string constraints = optimized.Diagnostics.Count == 0 ? "" : "\nالقيود التي يتعذر جمعها مع الموارد والتكليفات الحالية:\n" + string.Join("\n", optimized.Diagnostics.Take(6));
                        string teachers = string.Join("\n", preferences.Where(p => p.Key.Semester == batch.Key).Take(4).Select(p =>
                            "التدريسي «" + catalog.Rows<FacultyMember>().Single(f => f.FacultyMemberID == p.Key.Faculty).FullName + "»: الأيام المسموحة " +
                            string.Join("، ", p.Value.SelectedDays.Select(day => day switch { "Sunday" => "الأحد", "Monday" => "الاثنين", "Tuesday" => "الثلاثاء", "Wednesday" => "الأربعاء", "Thursday" => "الخميس", _ => day }))));
                        throw new ArgumentException(reason + constraints + (teachers.Length == 0 ? "" : "\n" + teachers) +
                            "\nراجع الأيام المتاحة والفترات والقاعات والتكليفات للشعب المذكورة، ثم أعد المحاولة. بقي الجدول الحالي محفوظًا دون تغيير.");
                    }
                    for (int i = 0; i < optimizationRows.Count; i++)
                    {
                        optimizationRows[i].DayOfWeek = optimized.Plan[i].DayOfWeek;
                        optimizationRows[i].TimeSlotID = optimized.Plan[i].TimeSlotID;
                        optimizationRows[i].ClassroomID = optimized.Plan[i].ClassroomID;
                        optimizationRows[i].FacultyMemberID = optimized.Plan[i].FacultyMemberID;
                    }
                }
            }
            FacultyPreferenceRules.Validate(generatedSchedules, timeSlots.ToDictionary(p => p.TimeSlotID), preferences);
            if (regenerate || preferences.Values.Any(p => p.HasScoredPreferences))
                ScheduleDayRules.EnsureSectionDayLimit(generatedSchedules, sections);

            string? backupPath = null;
            if (regenerate && existingSchedules.Count > 0)
            {
                // Only explicitly requested snapshots are written; normal generation has no automatic backup.
                if (safetyBackupPath is not null)
                    backupPath = (await TimetableBackupService.SaveSnapshotAsync(context, safetyBackupPath)).Path;
                context.Schedules.RemoveRange(existingSchedules.Where(s => !s.IsLocked));
                await context.SaveChangesAsync();
            }
            foreach (var displaced in displacedSchedules)
            {
                var replacement = newPlacements.FirstOrDefault(lecture =>
                    lecture.SubjectID == displaced.SubjectID && lecture.SectionID == displaced.SectionID &&
                    lecture.SemesterNumber == displaced.SemesterNumber && lecture.LectureType == displaced.LectureType &&
                    lecture.GroupName == displaced.GroupName && lecture.FacultyMemberID == displaced.FacultyMemberID)
                    ?? throw new ArgumentException("A conflicting lecture could not be repaired. The existing timetable has been kept.");
                replacement.ScheduleID = displaced.ScheduleID;
                context.Entry(displaced).CurrentValues.SetValues(replacement);
                newPlacements.Remove(replacement);
            }
            // Retained rows (explicit locks in fresh mode) keep their IDs and placements.
            await context.Schedules.AddRangeAsync(newPlacements);
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
                // Generation consumes configured resources; it never creates them.
                0,
                0,
                0,
                0)
            {
                PreservedCount = retainedSchedules.Count,
                AddedCount = newPlacements.Count,
                RepairedCount = displacedSchedules.Count,
                IsRegenerated = regenerate,
                ReplacedCount = regenerate ? existingSchedules.Count - retainedSchedules.Count : 0,
                SafetyBackupPath = backupPath
            };
        }

        internal static Task ValidateAsync(AppDbContext context, Schedule schedule, bool isUpdate, bool checkConflicts = true) =>
            ValidateCoreAsync(context, schedule, isUpdate, checkConflicts, restoringBackup: false);

        // Called only after the backup checksum and all academic reference values
        // have been verified. An archived lecture may no longer exist in the new table.
        internal static Task ValidateBackupLectureAsync(AppDbContext context, Schedule schedule) =>
            ValidateCoreAsync(context, schedule, isUpdate: true, checkConflicts: false, restoringBackup: true);

        private static async Task ValidateCoreAsync(AppDbContext context, Schedule schedule, bool isUpdate, bool checkConflicts, bool restoringBackup)
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

            // Only an existing lecture may retain a deleted subject. Its identity
            // stays fixed even when its placement is changed by the timetable editor.
            var archivedOriginal = subject.IsDeleted && restoringBackup ? schedule : subject.IsDeleted && isUpdate
                ? await context.Schedules.AsNoTracking().FirstOrDefaultAsync(s => s.ScheduleID == schedule.ScheduleID)
                : null;
            if (subject.IsDeleted && (archivedOriginal is null ||
                archivedOriginal.SubjectID != schedule.SubjectID || archivedOriginal.FacultyMemberID != schedule.FacultyMemberID ||
                archivedOriginal.SectionID != schedule.SectionID || archivedOriginal.LectureType != schedule.LectureType ||
                archivedOriginal.GroupName != schedule.GroupName))
                throw new ArgumentException("هذه المادة محذوفة من القائمة. يمكن الاحتفاظ بمحاضراتها المحفوظة ونقل مواعيدها فقط؛ لا يمكن إضافة محاضرات جديدة لها أو تغيير هوية محاضراتها.");

            if (!await context.FacultyMembers.AnyAsync(f => f.FacultyMemberID == schedule.FacultyMemberID))
            {
                throw new ArgumentException("Faculty member does not exist.");
            }

            if (!subject.IsDeleted && !await CanFacultyTeachSubjectAsync(context, schedule.FacultyMemberID, schedule.SubjectID, schedule.SectionID, NormalizeLectureType(schedule.LectureType)))
            {
                throw new ArgumentException("التدريسي غير مكلف بنوع هذه المحاضرة للمادة والشعبة المحددتين.");
            }

            var classroom = await context.Classrooms
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ClassroomID == schedule.ClassroomID);

            if (classroom is null)
            {
                throw new ArgumentException("Classroom does not exist.");
            }

            var lecturePeriods = await context.TimeSlots.AsNoTracking().Where(slot => !slot.IsBreak).ToListAsync();
            if (!lecturePeriods.Any(slot => slot.TimeSlotID == schedule.TimeSlotID))
            {
                throw new ArgumentException("Time slot does not exist.");
            }

            ScheduleTimingRules.EnsureCompatibleLectureSlots(lecturePeriods);

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
            if (schedule.LectureType == "Practical" && subject.EffectivePracticalLectureCount <= 0 ||
                schedule.LectureType == "Theory" && subject.EffectiveTheoreticalLectureCount <= 0)
                throw new ArgumentException("The selected subject does not have this lecture type.");
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

            if (checkConflicts)
            {
                await EnsureNoConflictsAsync(context, schedule, isUpdate);
                var profileRows = await context.FacultySchedulingPreferences.AsNoTracking().Where(p =>
                    p.FacultyMemberID == schedule.FacultyMemberID && p.SemesterNumber == schedule.SemesterNumber).ToListAsync();
                if (profileRows.Count > 0)
                {
                    var otherLectures = await context.Schedules.AsNoTracking().Where(s =>
                        s.FacultyMemberID == schedule.FacultyMemberID && s.SemesterNumber == schedule.SemesterNumber &&
                        (!isUpdate || s.ScheduleID != schedule.ScheduleID)).ToListAsync();
                    var cells = await context.FacultyPeriodPreferences.AsNoTracking().Where(p => p.FacultyMemberID == schedule.FacultyMemberID && p.SemesterNumber == schedule.SemesterNumber).ToListAsync();
                    foreach (var profile in FacultyPreferenceRules.FromRows(profileRows, cells).Values)
                        if (FacultyPreferenceRules.Violation(profile, otherLectures.Append(schedule), lecturePeriods.ToDictionary(p => p.TimeSlotID)) is { } violation)
                            throw new ArgumentException(violation);
                }
            }
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
            int subjectId,
            int? sectionId, string lectureType)
        {
            return await context.FacultyMemberSubjects.AnyAsync(assignment =>
                assignment.FacultyMemberID == facultyMemberId &&
                assignment.SubjectID == subjectId &&
                (assignment.SectionID == null || assignment.SectionID == sectionId) &&
                (assignment.LectureType == "Both" || assignment.LectureType == lectureType));
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
            return AcademicStructureRules.IsGeneralSectionValid(section);
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

            if (AcademicStructureRules.AllowsWholeSectionPractical(section.StudyYearID))
            {
                if (normalizedGroupName is not null &&
                    !string.Equals(normalizedGroupName, section.SectionName.Trim(), StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("عملي المرحلة الرابعة للشعبة كاملة في جميع التخصصات؛ استخدم A بدون A1 أو A2.");
                schedule.GroupName = null;
                return;
            }

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
                return AcademicStructureRules.AllowsWholeSectionPractical(section.StudyYearID);
            }

            string normalizedGroupName = groupName.Trim().ToUpperInvariant();

            if (!AcademicStructureRules.GetAllowedPracticalGroupNames(section.StudyYearID, section.SectionName)
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
            return AcademicStructureRules.GetStudyYearOrder(yearName);
        }

    }

}
