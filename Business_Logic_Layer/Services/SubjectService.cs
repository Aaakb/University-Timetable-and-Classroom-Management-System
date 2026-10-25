using Microsoft.EntityFrameworkCore;
using Data_Access_Layer;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    public class SubjectService
    {
        public async Task<List<Subject>> GetAllAsync()
        {
            await using var context = new AppDbContext();
            var subjects = await context.Subjects
                .Where(s => !s.IsDeleted)
                .Include(s => s.StudyYear)
                .Include(s => s.Branch)
                .AsNoTracking()
                .ToListAsync();

            return subjects
                .OrderBy(subject => AcademicStructureRules.GetStudyYearOrder(subject.StudyYear.YearName))
                .ThenBy(subject => subject.Branch?.BranchName ?? string.Empty)
                .ThenBy(subject => subject.SemesterNumber)
                .ThenBy(subject => subject.SubjectID)
                .ToList();
        }

        public async Task<Subject?> GetByIdAsync(int id)
        {
            await using var context = new AppDbContext();
            return await context.Subjects
                .Include(s => s.StudyYear)
                .Include(s => s.Branch)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SubjectID == id && !s.IsDeleted);
        }

        public async Task<Subject> AddAsync(Subject subject)
        {
            await using var context = new AppDbContext();
            subject.SubjectID = await AutoKeyGenerator.FirstAvailableAsync(context.Subjects.Select(s => s.SubjectID));
            return await AddInContextAsync(context, subject);
        }

        internal static async Task<Subject> AddInContextAsync(AppDbContext context, Subject subject)
        {
            subject.IsDeleted = false;
            subject.RequirementType = Subject.DefaultRequirementType;
            await ValidateAsync(context, subject, false);
            await context.Subjects.AddAsync(subject);
            await ManualKeySaveHelper.SaveWithManualKeyAsync(context, "[Subjects]");
            return subject;
        }


        internal static async Task<Subject> AddNameOnlyInContextAsync(AppDbContext context, Subject subject)
        {
            if (!subject.IsUnclassified || subject.BranchID.HasValue || subject.TheoreticalLectureCount != 0 || subject.PracticalLectureCount != 0)
                throw new ArgumentException("Name-only subjects must remain unclassified without weekly lectures.");
            if (!await context.StudyYears.AnyAsync(y => y.StudyYearID == Subject.UnclassifiedStudyYearId))
            {
                context.StudyYears.Add(new StudyYear { StudyYearID = Subject.UnclassifiedStudyYearId, YearName = "غير مصنفة" });
                await context.SaveChangesAsync();
            }
            return await AddInContextAsync(context, subject);
        }

        public async Task<Subject> UpdateAsync(Subject subject)
        {
            await using var context = new AppDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var result = await UpdateInContextAsync(context, subject);
            await transaction.CommitAsync();
            return result;
        }

        internal static async Task<Subject> UpdateInContextAsync(AppDbContext context, Subject subject)
        {
            var existing = await context.Subjects.SingleOrDefaultAsync(s => s.SubjectID == subject.SubjectID && !s.IsDeleted)
                ?? throw new KeyNotFoundException("Subject not found.");
            // The retired field is no longer edited; preserve historical metadata.
            subject.RequirementType = existing.RequirementType;
            subject.IsDeleted = false;
            await ValidateAsync(context, subject, true);
            var assignedSections = await context.FacultyMemberSubjects.Where(a => a.SubjectID == subject.SubjectID && a.SectionID != null)
                .Select(a => a.Section!).AsNoTracking().ToListAsync();
            if (assignedSections.Any(section => !FacultyAssignmentRules.Matches(subject, section)))
                throw new ArgumentException("للمادة تكليفات مرتبطة بشعب محددة؛ عدّل التكليفات قبل تغيير مرحلة المادة أو تخصصها.");
            var linkedSchedules = await context.Schedules.Where(s => s.SubjectID == subject.SubjectID).ToListAsync();
            SubjectLectureRules.ValidateSavedLectures(subject, linkedSchedules);
            if (linkedSchedules.Any(s => s.LectureType == "Practical") && subject.EffectivePracticalLectureCount <= 0 ||
                linkedSchedules.Any(s => s.LectureType == "Theory") && subject.EffectiveTheoreticalLectureCount <= 0)
            {
                throw new ArgumentException("Remove the linked lessons before removing this subject's theory or practical component.");
            }

            if (linkedSchedules.Count > 0 &&
                (existing.StudyYearID != subject.StudyYearID || existing.BranchID != subject.BranchID))
            {
                throw new ArgumentException("Remove this subject's scheduled lessons before changing its study year or branch.");
            }

            if (linkedSchedules.Any(s => s.SemesterNumber != subject.SemesterNumber))
            {
                var otherSchedules = await context.Schedules.AsNoTracking()
                    .Where(s => s.SubjectID != subject.SubjectID && s.SemesterNumber == subject.SemesterNumber)
                    .ToListAsync();
                foreach (var schedule in linkedSchedules)
                {
                    schedule.SemesterNumber = subject.SemesterNumber;
                    if (otherSchedules.Any(other => ScheduleConflictChecker.HasPairConflict(schedule, other)))
                    {
                        throw new ArgumentException("The semester change conflicts with an existing room, faculty, or section booking. The subject and its timetable have been kept unchanged.");
                    }
                }
                if (ScheduleConflictChecker.HasAnyPairConflict(linkedSchedules))
                {
                    throw new ArgumentException("This subject's lessons would conflict in the selected semester. The subject and its timetable have been kept unchanged.");
                }
            }

            context.Entry(existing).CurrentValues.SetValues(subject);
            await context.SaveChangesAsync();
            return subject;
        }


        public async Task DeleteAsync(int id)
        {
            await using var context = new AppDbContext();
            var subject = await context.Subjects.FindAsync(id)
                ?? throw new KeyNotFoundException("Subject not found.");

            // Keep the referenced record so assignments and saved lectures retain
            // their original subject and remain valid after removal from the list.
            subject.IsDeleted = true;
            await context.SaveChangesAsync();
        }

        private static async Task ValidateAsync(AppDbContext context, Subject subject, bool isUpdate)
        {
            SubjectLectureRules.Validate(subject);
            await InputValidation.RecordIdAsync(subject.SubjectID, isUpdate,
                () => context.Subjects.AnyAsync(s => s.SubjectID == subject.SubjectID), "Subject");

            if (string.IsNullOrWhiteSpace(subject.SubjectName))
            {
                throw new ArgumentException("Subject name is required.");
            }

            if (subject.SemesterNumber is not (1 or 2))
            {
                throw new ArgumentException("Semester number must be 1 or 2.");
            }

            if (!double.IsFinite(subject.TheoreticalHours) || !double.IsFinite(subject.PracticalHours) ||
                !double.IsFinite(subject.CreditUnits) || subject.TheoreticalHours < 0 || subject.PracticalHours < 0 || subject.CreditUnits < 0)
            {
                throw new ArgumentException("Hours and credit units must be finite numbers that are zero or greater.");
            }

            if (!await context.StudyYears.AnyAsync(sy => sy.StudyYearID == subject.StudyYearID))
            {
                throw new ArgumentException("Study year does not exist.");
            }

            if (AcademicStructureRules.UsesGeneralSections(subject.StudyYearID) && subject.BranchID.HasValue)
            {
                throw new ArgumentException("First and second year subjects must be general and not linked to a branch.");
            }

            if (AcademicStructureRules.UsesBranches(subject.StudyYearID) && !subject.BranchID.HasValue)
            {
                throw new ArgumentException("Third and fourth year subjects must be linked to a branch.");
            }

            if (subject.BranchID.HasValue &&
                !await context.Branches.AnyAsync(b => b.BranchID == subject.BranchID.Value))
            {
                throw new ArgumentException("Branch does not exist.");
            }

            subject.SubjectName = subject.SubjectName.Trim();
            InputValidation.TextLength(subject.SubjectName, 100, "Subject name");

            var exists = await context.Subjects.AnyAsync(s =>
                !s.IsDeleted &&
                s.SubjectName == subject.SubjectName &&
                s.StudyYearID == subject.StudyYearID &&
                s.BranchID == subject.BranchID &&
                s.SemesterNumber == subject.SemesterNumber &&
                (!isUpdate || s.SubjectID != subject.SubjectID));

            if (exists)
            {
                throw new ArgumentException("Subject already exists in the same study year, branch, and semester.");
            }
        }
    }
}
