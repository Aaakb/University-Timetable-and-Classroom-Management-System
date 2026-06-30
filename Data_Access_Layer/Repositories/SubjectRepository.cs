using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.Models;

namespace Data_Access_Layer.Repositories
{
    public class SubjectRepository
    {
        private readonly AppDbContext _context;

        public SubjectRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Subject>> GetAllAsync()
        {
            return await _context.Subjects
                .Include(s => s.StudyYear)
                .Include(s => s.Branch)
                .AsNoTracking()
                .OrderBy(subject => subject.StudyYearID)
                .ThenBy(subject => subject.BranchID ?? 0)
                .ThenBy(subject => subject.SemesterNumber)
                .ThenBy(subject => subject.SubjectName)
                .ToListAsync();
        }

        public async Task<Subject?> GetByIdAsync(int id)
        {
            return await _context.Subjects
                .Include(s => s.StudyYear)
                .Include(s => s.Branch)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SubjectID == id);
        }

        public async Task<int> AddAsync(Subject entity)
        {
            await _context.Subjects.AddAsync(entity);
            return await _context.SaveChangesAsync();
        }

        public async Task<bool> UpdateAsync(Subject entity)
        {
            _context.Subjects.Update(entity);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _context.Subjects.FindAsync(id);

            if (entity is null)
            {
                return false;
            }

            _context.Subjects.Remove(entity);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Subjects.AnyAsync(s => s.SubjectID == id);
        }
    }
}
