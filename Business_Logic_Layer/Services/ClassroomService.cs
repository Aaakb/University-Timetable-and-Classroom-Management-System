using Microsoft.EntityFrameworkCore;
using Data_Access_Layer;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    public class ClassroomService
    {
        public async Task<List<Classroom>> GetAllAsync()
        {
            await using var context = new AppDbContext();
            return await context.Classrooms.AsNoTracking().ToListAsync();
        }

        public async Task<Classroom?> GetByIdAsync(int id)
        {
            await using var context = new AppDbContext();
            return await context.Classrooms.AsNoTracking().FirstOrDefaultAsync(c => c.ClassroomID == id);
        }

        public async Task<Classroom> AddAsync(Classroom classroom)
        {
            await using var context = new AppDbContext();
            classroom.ClassroomID = await AutoKeyGenerator.NextAsync(context.Classrooms.Select(c => c.ClassroomID));
            await ValidateAsync(context, classroom, false);
            await context.Classrooms.AddAsync(classroom);
            await ManualKeySaveHelper.SaveWithManualKeyAsync(context, "[Classrooms]");
            return classroom;
        }

        public async Task<Classroom> UpdateAsync(Classroom classroom)
        {
            await using var context = new AppDbContext();
            await ValidateAsync(context, classroom, true);
            context.Classrooms.Update(classroom);
            await context.SaveChangesAsync();
            return classroom;
        }

        public async Task DeleteAsync(int id)
        {
            await using var context = new AppDbContext();
            var classroom = await context.Classrooms.FindAsync(id)
                ?? throw new KeyNotFoundException("Classroom not found.");

            context.Classrooms.Remove(classroom);
            await context.SaveChangesAsync();
        }

        private static async Task ValidateAsync(AppDbContext context, Classroom classroom, bool isUpdate)
        {
            if (classroom.ClassroomID <= 0)
            {
                throw new ArgumentException("Classroom ID is required.");
            }

            var idExists = await context.Classrooms.AnyAsync(c => c.ClassroomID == classroom.ClassroomID);

            if (!isUpdate && idExists)
            {
                throw new ArgumentException("Classroom ID already exists.");
            }

            if (isUpdate && !idExists)
            {
                throw new KeyNotFoundException("Classroom not found.");
            }

            if (string.IsNullOrWhiteSpace(classroom.ClassroomNumber))
            {
                throw new ArgumentException("Classroom number is required.");
            }

            if (classroom.Capacity <= 0)
            {
                throw new ArgumentException("Classroom capacity must be greater than zero.");
            }

            classroom.ClassroomNumber = classroom.ClassroomNumber.Trim();
            classroom.RoomType = NormalizeRoomType(classroom.RoomType, classroom.ClassroomNumber);

            var exists = await context.Classrooms.AnyAsync(c =>
                c.ClassroomNumber == classroom.ClassroomNumber &&
                (!isUpdate || c.ClassroomID != classroom.ClassroomID));

            if (exists)
            {
                throw new ArgumentException("Classroom number already exists.");
            }
        }

        private static string NormalizeRoomType(string? roomType, string classroomNumber)
        {
            string value = roomType?.Trim() ?? string.Empty;

            if (value.Contains("Lab", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("Laboratory", StringComparison.OrdinalIgnoreCase) ||
                classroomNumber.Contains("Lab", StringComparison.OrdinalIgnoreCase))
            {
                return "Lab";
            }

            return "Lecture";
        }
    }
}
