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
                .Include(fms => fms.Section)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<FacultyMemberSubject?> GetByIdAsync(int facultyMemberId, int subjectId)
        {
            await using var context = new AppDbContext();
            return await context.FacultyMemberSubjects
                .Include(fms => fms.FacultyMember)
                .Include(fms => fms.Subject)
                .Include(fms => fms.Section)
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
            await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var item = await context.FacultyMemberSubjects.FindAsync(currentFacultyMemberId, currentSubjectId)
                ?? throw new KeyNotFoundException("Faculty member subject assignment not found.");

            bool sameKey = item.FacultyMemberID == entity.FacultyMemberID && item.SubjectID == entity.SubjectID;
            entity.LectureType = FacultyAssignmentRules.NormalizeLectureType(entity.LectureType);
            if (sameKey && item.SectionID == entity.SectionID && item.LectureType == entity.LectureType)
            {
                return entity;
            }

            await ValidateAsync(context, entity, currentFacultyMemberId, currentSubjectId);
            if (sameKey)
            {
                if (entity.SectionID.HasValue && await context.Schedules.AnyAsync(s =>
                    s.FacultyMemberID == currentFacultyMemberId && s.SubjectID == currentSubjectId &&
                    s.SectionID != entity.SectionID))
                    throw new ArgumentException("للتدريسي محاضرات محفوظة في شعب أخرى لهذه المادة. انقلها إلى تدريسي آخر قبل تخصيص التكليف لشعبة واحدة.");
                item.SectionID = entity.SectionID;
                if (await context.Schedules.AnyAsync(s => s.FacultyMemberID == currentFacultyMemberId &&
                    s.SubjectID == currentSubjectId && entity.LectureType != "Both" && s.LectureType != entity.LectureType))
                    throw new ArgumentException("نوع التكليف يستبعد محاضرات محفوظة لهذا التدريسي. صحح محاضراته أولًا.");
                item.LectureType = entity.LectureType;
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
                return entity;
            }
            await EnsureAssignmentIsUnusedAsync(context, currentFacultyMemberId, currentSubjectId);

            context.FacultyMemberSubjects.Remove(item);
            await context.FacultyMemberSubjects.AddAsync(entity);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            return entity;
        }

        public async Task DeleteAsync(int facultyMemberId, int subjectId)
        {
            await using var context = new AppDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var item = await context.FacultyMemberSubjects.FindAsync(facultyMemberId, subjectId)
                ?? throw new KeyNotFoundException("Faculty member subject assignment not found.");

            // Archived lectures refer to the subject and teacher directly. Removing
            // this assignment must not remove or invalidate those saved lectures.
            if (!await context.Subjects.AnyAsync(s => s.SubjectID == subjectId && s.IsDeleted))
                await EnsureAssignmentIsUnusedAsync(context, facultyMemberId, subjectId);
            context.FacultyMemberSubjects.Remove(item);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        private static async Task EnsureAssignmentIsUnusedAsync(AppDbContext context, int facultyMemberId, int subjectId)
        {
            int count = await context.Schedules.CountAsync(s => s.FacultyMemberID == facultyMemberId && s.SubjectID == subjectId);
            if (count > 0)
            {
                string subjectName = await context.Subjects.Where(s => s.SubjectID == subjectId).Select(s => s.SubjectName).SingleAsync();
                throw new ArgumentException($"التكليف لمادة «{subjectName}» مرتبط بعدد {count} من المحاضرات المحفوظة. " +
                    "غيّر التدريسي في هذه المحاضرات أو احذفها قبل تغيير التكليف أو حذفه. " +
                    "إذا حذفت المادة من قائمة المواد، يمكنك حذف تكليفها مع إبقاء محاضراتها السابقة.");
            }
        }

        private static async Task ValidateAsync(
            AppDbContext context,
            FacultyMemberSubject entity,
            int? currentFacultyMemberId = null,
            int? currentSubjectId = null)
        {
            entity.LectureType = FacultyAssignmentRules.NormalizeLectureType(entity.LectureType);
            if (!await context.FacultyMembers.AnyAsync(f => f.FacultyMemberID == entity.FacultyMemberID))
            {
                throw new ArgumentException("Faculty member does not exist.");
            }

            var subject = await context.Subjects.AsNoTracking().FirstOrDefaultAsync(s => s.SubjectID == entity.SubjectID && !s.IsDeleted);
            if (subject is null)
            {
                throw new ArgumentException("Subject does not exist.");
            }

            if (entity.SectionID.HasValue)
            {
                var section = await context.Sections.AsNoTracking().FirstOrDefaultAsync(s => s.SectionID == entity.SectionID);
                if (section is null || !FacultyAssignmentRules.Matches(subject, section))
                    throw new ArgumentException("الشعبة المحددة لا تطابق مرحلة المادة وتخصصها.");
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

            // Multiple faculty remain possible within the selected section only.
        }
    }
}
