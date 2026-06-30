using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.Models;

namespace Data_Access_Layer.Repositories
{
    public class TimeSlotRepository
    {
        private readonly AppDbContext _context;

        public TimeSlotRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<TimeSlot>> GetAllAsync()
        {
            return await _context.TimeSlots
                .AsNoTracking()
                .OrderBy(timeSlot => timeSlot.IsBreak)
                .ThenBy(timeSlot => timeSlot.StartTime)
                .ThenBy(timeSlot => timeSlot.EndTime)
                .ToListAsync();
        }

        public async Task<TimeSlot?> GetByIdAsync(int id)
        {
            return await _context.TimeSlots.AsNoTracking().FirstOrDefaultAsync(t => t.TimeSlotID == id);
        }

        public async Task<int> AddAsync(TimeSlot entity)
        {
            await _context.TimeSlots.AddAsync(entity);
            return await _context.SaveChangesAsync();
        }

        public async Task<bool> UpdateAsync(TimeSlot entity)
        {
            _context.TimeSlots.Update(entity);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _context.TimeSlots.FindAsync(id);

            if (entity is null)
            {
                return false;
            }

            _context.TimeSlots.Remove(entity);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.TimeSlots.AnyAsync(t => t.TimeSlotID == id);
        }
    }
}
