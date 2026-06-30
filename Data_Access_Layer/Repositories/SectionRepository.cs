using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.Models;

namespace Data_Access_Layer.Repositories
{
    public class SectionRepository
    {
        private readonly AppDbContext _context;

        public SectionRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Section>> GetAllAsync()
        {
            return await _context.Sections
                .Include(s => s.StudyYear)
                .Include(s => s.Branch)
                .AsNoTracking()
                .OrderBy(section => section.StudyYearID)
                .ThenBy(section => section.BranchID ?? 0)
                .ThenBy(section => section.SectionName)
                .ToListAsync();
        }

        public async Task<Section?> GetByIdAsync(int id)
        {
            return await _context.Sections
                .Include(s => s.StudyYear)
                .Include(s => s.Branch)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SectionID == id);
        }

        public async Task<int> AddAsync(Section entity)
        {
            await _context.Sections.AddAsync(entity);
            return await _context.SaveChangesAsync();
        }

        public async Task<bool> UpdateAsync(Section entity)
        {
            _context.Sections.Update(entity);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _context.Sections.FindAsync(id);

            if (entity is null)
            {
                return false;
            }

            _context.Sections.Remove(entity);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Sections.AnyAsync(s => s.SectionID == id);
        }
    }
}
