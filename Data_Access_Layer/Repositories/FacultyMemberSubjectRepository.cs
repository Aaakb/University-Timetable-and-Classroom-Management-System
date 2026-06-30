using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.Models;

namespace Data_Access_Layer.Repositories
{
    public class FacultyMemberSubjectRepository
    {
        private readonly AppDbContext _context;

        public FacultyMemberSubjectRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<FacultyMemberSubject>> GetAllAsync()
        {
            return await _context.FacultyMemberSubjects
                .Include(fms => fms.FacultyMember)
                .Include(fms => fms.Subject)
                .AsNoTracking()
                .OrderBy(fms => fms.Subject.StudyYearID)
                .ThenBy(fms => fms.Subject.BranchID ?? 0)
                .ThenBy(fms => fms.Subject.SemesterNumber)
                .ThenBy(fms => fms.Subject.SubjectName)
                .ThenBy(fms => fms.FacultyMember.FullName)
                .ToListAsync();
        }

        public async Task<FacultyMemberSubject?> GetByIdAsync(int facultyMemberId, int subjectId)
        {
            return await _context.FacultyMemberSubjects
                .Include(fms => fms.FacultyMember)
                .Include(fms => fms.Subject)
                .AsNoTracking()
                .FirstOrDefaultAsync(fms =>
                    fms.FacultyMemberID == facultyMemberId &&
                    fms.SubjectID == subjectId);
        }

        public async Task<List<FacultyMemberSubject>> GetByFacultyMemberIdAsync(int facultyMemberId)
        {
            return await _context.FacultyMemberSubjects
                .Include(fms => fms.FacultyMember)
                .Include(fms => fms.Subject)
                .AsNoTracking()
                .Where(fms => fms.FacultyMemberID == facultyMemberId)
                .OrderBy(fms => fms.Subject.StudyYearID)
                .ThenBy(fms => fms.Subject.BranchID ?? 0)
                .ThenBy(fms => fms.Subject.SemesterNumber)
                .ThenBy(fms => fms.Subject.SubjectName)
                .ToListAsync();
        }

        public async Task<List<FacultyMemberSubject>> GetBySubjectIdAsync(int subjectId)
        {
            return await _context.FacultyMemberSubjects
                .Include(fms => fms.FacultyMember)
                .Include(fms => fms.Subject)
                .AsNoTracking()
                .Where(fms => fms.SubjectID == subjectId)
                .OrderBy(fms => fms.FacultyMember.FullName)
                .ToListAsync();
        }

        public async Task<int> AddAsync(FacultyMemberSubject entity)
        {
            await _context.FacultyMemberSubjects.AddAsync(entity);
            return await _context.SaveChangesAsync();
        }

        public async Task<bool> UpdateAsync(FacultyMemberSubject entity)
        {
            _context.FacultyMemberSubjects.Update(entity);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int facultyMemberId, int subjectId)
        {
            var entity = await _context.FacultyMemberSubjects.FindAsync(facultyMemberId, subjectId);

            if (entity is null)
            {
                return false;
            }

            _context.FacultyMemberSubjects.Remove(entity);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> ExistsAsync(int facultyMemberId, int subjectId)
        {
            return await _context.FacultyMemberSubjects.AnyAsync(fms =>
                fms.FacultyMemberID == facultyMemberId &&
                fms.SubjectID == subjectId);
        }
    }
}
