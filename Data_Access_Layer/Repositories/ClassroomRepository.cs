using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.Models;

namespace Data_Access_Layer.Repositories
{
    public class ClassroomRepository
    {
        private readonly AppDbContext _context;

        public ClassroomRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Classroom>> GetAllAsync()
        {
            return await _context.Classrooms
                .AsNoTracking()
                .OrderBy(classroom => classroom.RoomType)
                .ThenBy(classroom => classroom.ClassroomNumber)
                .ToListAsync();
        }

        public async Task<Classroom?> GetByIdAsync(int id)
        {
            return await _context.Classrooms.AsNoTracking().FirstOrDefaultAsync(c => c.ClassroomID == id);
        }

        public async Task<int> AddAsync(Classroom entity)
        {
            await _context.Classrooms.AddAsync(entity);
            return await _context.SaveChangesAsync();
        }

        public async Task<bool> UpdateAsync(Classroom entity)
        {
            _context.Classrooms.Update(entity);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _context.Classrooms.FindAsync(id);

            if (entity is null)
            {
                return false;
            }

            _context.Classrooms.Remove(entity);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Classrooms.AnyAsync(c => c.ClassroomID == id);
        }
    }
}
