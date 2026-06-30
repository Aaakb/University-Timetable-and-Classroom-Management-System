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
                .FirstOrDefaultAsync(s => s.SubjectID == id);
        }

        public async Task<Subject> AddAsync(Subject subject)
        {
            await using var context = new AppDbContext();
            subject.SubjectID = await AutoKeyGenerator.FirstAvailableAsync(context.Subjects.Select(s => s.SubjectID));
            await ValidateAsync(context, subject, false);
            await context.Subjects.AddAsync(subject);
            await ManualKeySaveHelper.SaveWithManualKeyAsync(context, "[Subjects]");
            return subject;
        }

        public async Task<Subject> UpdateAsync(Subject subject)
        {
            await using var context = new AppDbContext();
            await ValidateAsync(context, subject, true);
            context.Subjects.Update(subject);
            await context.SaveChangesAsync();
            return subject;
        }

        public async Task DeleteAsync(int id)
        {
            await using var context = new AppDbContext();
            var subject = await context.Subjects.FindAsync(id)
                ?? throw new KeyNotFoundException("Subject not found.");

            context.Subjects.Remove(subject);
            await context.SaveChangesAsync();
        }

        private static async Task ValidateAsync(AppDbContext context, Subject subject, bool isUpdate)
        {
            if (subject.SubjectID <= 0)
            {
                throw new ArgumentException("Subject ID is required.");
            }

            var idExists = await context.Subjects.AnyAsync(s => s.SubjectID == subject.SubjectID);

            if (!isUpdate && idExists)
            {
                throw new ArgumentException("Subject ID already exists.");
            }

            if (isUpdate && !idExists)
            {
                throw new KeyNotFoundException("Subject not found.");
            }

            if (string.IsNullOrWhiteSpace(subject.SubjectName))
            {
                throw new ArgumentException("Subject name is required.");
            }

            if (subject.SemesterNumber <= 0)
            {
                throw new ArgumentException("Semester number must be greater than zero.");
            }

            if (subject.TheoreticalHours < 0 || subject.PracticalHours < 0 || subject.CreditUnits < 0)
            {
                throw new ArgumentException("Hours and credit units cannot be negative.");
            }

            if (string.IsNullOrWhiteSpace(subject.RequirementType))
            {
                throw new ArgumentException("Requirement type is required.");
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
            subject.RequirementType = subject.RequirementType.Trim();

            var exists = await context.Subjects.AnyAsync(s =>
                s.SubjectName == subject.SubjectName &&
                s.StudyYearID == subject.StudyYearID &&
                s.SemesterNumber == subject.SemesterNumber &&
                (!isUpdate || s.SubjectID != subject.SubjectID));

            if (exists)
            {
                throw new ArgumentException("Subject already exists in the same study year and semester.");
            }
        }
    }
}
