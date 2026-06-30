using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.Models;

namespace Data_Access_Layer.Repositories
{
    public class FacultyMemberRepository
    {
        private readonly AppDbContext _context;

        public FacultyMemberRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<FacultyMember>> GetAllAsync()
        {
            return await _context.FacultyMembers
                .AsNoTracking()
                .OrderBy(facultyMember => facultyMember.FullName)
                .ToListAsync();
        }

        public async Task<FacultyMember?> GetByIdAsync(int id)
        {
            return await _context.FacultyMembers.AsNoTracking().FirstOrDefaultAsync(f => f.FacultyMemberID == id);
        }

        public async Task<int> AddAsync(FacultyMember entity)
        {
            await _context.FacultyMembers.AddAsync(entity);
            return await _context.SaveChangesAsync();
        }

        public async Task<bool> UpdateAsync(FacultyMember entity)
        {
            _context.FacultyMembers.Update(entity);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _context.FacultyMembers.FindAsync(id);

            if (entity is null)
            {
                return false;
            }

            _context.FacultyMembers.Remove(entity);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.FacultyMembers.AnyAsync(f => f.FacultyMemberID == id);
        }
    }
}
