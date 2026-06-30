using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.Models;

namespace Data_Access_Layer.Repositories
{
    public class StudyYearRepository
    {
        private readonly AppDbContext _context;

        public StudyYearRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<StudyYear>> GetAllAsync()
        {
            return await _context.StudyYears
                .AsNoTracking()
                .OrderBy(studyYear => studyYear.StudyYearID)
                .ToListAsync();
        }

        public async Task<StudyYear?> GetByIdAsync(int id)
        {
            return await _context.StudyYears.AsNoTracking().FirstOrDefaultAsync(sy => sy.StudyYearID == id);
        }

        public async Task<int> AddAsync(StudyYear entity)
        {
            await _context.StudyYears.AddAsync(entity);
            return await _context.SaveChangesAsync();
        }

        public async Task<bool> UpdateAsync(StudyYear entity)
        {
            _context.StudyYears.Update(entity);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _context.StudyYears.FindAsync(id);

            if (entity is null)
            {
                return false;
            }

            _context.StudyYears.Remove(entity);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.StudyYears.AnyAsync(sy => sy.StudyYearID == id);
        }
    }
}
