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
            return await context.Classrooms.AsNoTracking()
                .OrderBy(c => c.RoomType == "Lecture" ? 0 : 1)
                .ThenBy(c => c.ClassroomNumber).ToListAsync();
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
            return await AddInContextAsync(context, classroom);
        }

        internal static async Task<Classroom> AddInContextAsync(AppDbContext context, Classroom classroom)
        {
            await ValidateAsync(context, classroom, false);
            await context.Classrooms.AddAsync(classroom);
            await ManualKeySaveHelper.SaveWithManualKeyAsync(context, "[Classrooms]");
            return classroom;
        }


        public async Task<Classroom> UpdateAsync(Classroom classroom)
        {
            await using var context = new AppDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var result = await UpdateInContextAsync(context, classroom);
            await transaction.CommitAsync();
            return result;
        }

        internal static async Task<Classroom> UpdateInContextAsync(AppDbContext context, Classroom classroom)
        {
            await ValidateAsync(context, classroom, true);
            var existing = await context.Classrooms.SingleAsync(c => c.ClassroomID == classroom.ClassroomID);
            var lessons = await context.Schedules.Include(s => s.Section)
                .Where(s => s.ClassroomID == classroom.ClassroomID).ToListAsync();
            if (lessons.Any(s => !ScheduleRoomSelector.MatchesLectureType(classroom, s.LectureType) ||
                !ScheduleRoomSelector.HasCapacity(classroom, s.Section!, s.GroupName)))
                throw new ArgumentException("This change would make the classroom unsuitable for a saved lesson. Move those lessons before changing its type or reducing its capacity.");
            context.Entry(existing).CurrentValues.SetValues(classroom);
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
            await InputValidation.RecordIdAsync(classroom.ClassroomID, isUpdate,
                () => context.Classrooms.AnyAsync(c => c.ClassroomID == classroom.ClassroomID), "Classroom");

            if (string.IsNullOrWhiteSpace(classroom.ClassroomNumber))
            {
                throw new ArgumentException("Classroom number is required.");
            }

            if (classroom.Capacity <= 0)
            {
                throw new ArgumentException("Classroom capacity must be greater than zero.");
            }

            classroom.ClassroomNumber = classroom.ClassroomNumber.Trim();
            InputValidation.TextLength(classroom.ClassroomNumber, 20, "Classroom number");
            classroom.RoomType = ClassroomRules.NormalizeRoomType(classroom.RoomType, classroom.ClassroomNumber);

            var exists = await context.Classrooms.AnyAsync(c =>
                c.ClassroomNumber == classroom.ClassroomNumber &&
                (!isUpdate || c.ClassroomID != classroom.ClassroomID));

            if (exists)
            {
                throw new ArgumentException("Classroom number already exists.");
            }
        }

    }
}
