using Microsoft.EntityFrameworkCore;
using Data_Access_Layer;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    public class FacultyMemberSubjectService
    {
        public async Task<List<FacultyMemberSubject>> GetAllAsync()
        {
            await using var context = new AppDbContext();
            return await context.FacultyMemberSubjects
                .Include(fms => fms.FacultyMember)
                .Include(fms => fms.Subject)
                    .ThenInclude(subject => subject.StudyYear)
                .Include(fms => fms.Subject)
                    .ThenInclude(subject => subject.Branch)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<FacultyMemberSubject?> GetByIdAsync(int facultyMemberId, int subjectId)
        {
            await using var context = new AppDbContext();
            return await context.FacultyMemberSubjects
                .Include(fms => fms.FacultyMember)
                .Include(fms => fms.Subject)
                .AsNoTracking()
                .FirstOrDefaultAsync(fms =>
                    fms.FacultyMemberID == facultyMemberId && fms.SubjectID == subjectId);
        }

        public async Task<FacultyMemberSubject> AddAsync(FacultyMemberSubject entity)
        {
            await using var context = new AppDbContext();
            await ValidateAsync(context, entity);
            await context.FacultyMemberSubjects.AddAsync(entity);
            await context.SaveChangesAsync();
            return entity;
        }

        public async Task<FacultyMemberSubject> UpdateAsync(
            int currentFacultyMemberId,
            int currentSubjectId,
            FacultyMemberSubject entity)
        {
            await using var context = new AppDbContext();
            var item = await context.FacultyMemberSubjects.FindAsync(currentFacultyMemberId, currentSubjectId)
                ?? throw new KeyNotFoundException("Faculty member subject assignment not found.");

            if (item.FacultyMemberID == entity.FacultyMemberID && item.SubjectID == entity.SubjectID)
            {
                return entity;
            }

            await ValidateAsync(context, entity, currentFacultyMemberId, currentSubjectId);

            await using var transaction = await context.Database.BeginTransactionAsync();
            context.FacultyMemberSubjects.Remove(item);
            await context.FacultyMemberSubjects.AddAsync(entity);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            return entity;
        }

        public async Task DeleteAsync(int facultyMemberId, int subjectId)
        {
            await using var context = new AppDbContext();
            var item = await context.FacultyMemberSubjects.FindAsync(facultyMemberId, subjectId)
                ?? throw new KeyNotFoundException("Faculty member subject assignment not found.");

            context.FacultyMemberSubjects.Remove(item);
            await context.SaveChangesAsync();
        }

        private static async Task ValidateAsync(
            AppDbContext context,
            FacultyMemberSubject entity,
            int? currentFacultyMemberId = null,
            int? currentSubjectId = null)
        {
            if (!await context.FacultyMembers.AnyAsync(f => f.FacultyMemberID == entity.FacultyMemberID))
            {
                throw new ArgumentException("Faculty member does not exist.");
            }

            if (!await context.Subjects.AnyAsync(s => s.SubjectID == entity.SubjectID))
            {
                throw new ArgumentException("Subject does not exist.");
            }

            var exists = await context.FacultyMemberSubjects.AnyAsync(fms =>
                fms.FacultyMemberID == entity.FacultyMemberID &&
                fms.SubjectID == entity.SubjectID &&
                (!currentFacultyMemberId.HasValue ||
                    fms.FacultyMemberID != currentFacultyMemberId.Value ||
                    fms.SubjectID != currentSubjectId!.Value));

            if (exists)
            {
                throw new ArgumentException("This faculty member is already assigned to the subject.");
            }

            // A subject can have backup faculty members so the timetable generator has alternatives.
        }
    }
}
