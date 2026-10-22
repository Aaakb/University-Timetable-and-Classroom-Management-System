using Microsoft.EntityFrameworkCore;
using Data_Access_Layer;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    public class TimeSlotService
    {
        public static (TimeSpan Start, TimeSpan End) DefaultLecturePeriod => ScheduleTimingRules.DefaultLectureSlots[0];

        public static void ValidateLectureRange(TimeSpan startTime, TimeSpan endTime)
        {
            if (endTime <= startTime)
                throw new ArgumentException("End time must be after start time.");

            if (!ScheduleTimingRules.IsValidLectureRange(startTime, endTime))
            {
                throw new ArgumentException("Choose start and end times within the same day, using whole minutes. Lecture duration is flexible.");
            }
        }

        public async Task<List<TimeSlot>> GetAllAsync()
        {
            await using var context = new AppDbContext();
            var timeSlots = await context.TimeSlots
                .AsNoTracking()
                .Where(t => !t.IsBreak)
                .OrderBy(t => t.StartTime)
                .ToListAsync();

            return timeSlots;
        }

        public async Task<TimeSlot?> GetByIdAsync(int id)
        {
            await using var context = new AppDbContext();
            var timeSlot = await context.TimeSlots
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TimeSlotID == id && !t.IsBreak);

            return timeSlot;
        }

        public async Task<TimeSlot> AddAsync(TimeSlot timeSlot)
        {
            await using var context = new AppDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            timeSlot.TimeSlotID = await AutoKeyGenerator.NextAsync(context.TimeSlots.Select(t => t.TimeSlotID));
            var result = await AddInContextAsync(context, timeSlot);
            await transaction.CommitAsync();
            return result;
        }

        internal static async Task<TimeSlot> AddInContextAsync(AppDbContext context, TimeSlot timeSlot)
        {
            timeSlot.IsBreak = false;
            await ValidateAsync(context, timeSlot, false);
            await context.TimeSlots.AddAsync(timeSlot);
            await ManualKeySaveHelper.SaveWithManualKeyAsync(context, "[TimeSlots]");
            return timeSlot;
        }


        public async Task<TimeSlot> UpdateAsync(TimeSlot timeSlot)
        {
            await using var context = new AppDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var result = await UpdateInContextAsync(context, timeSlot);
            await transaction.CommitAsync();
            return result;
        }

        internal static async Task<TimeSlot> UpdateInContextAsync(AppDbContext context, TimeSlot timeSlot)
        {
            timeSlot.IsBreak = false;
            await ValidateAsync(context, timeSlot, true);
            var existing = await context.TimeSlots.SingleAsync(slot => slot.TimeSlotID == timeSlot.TimeSlotID);
            context.Entry(existing).CurrentValues.SetValues(timeSlot);
            await context.SaveChangesAsync();
            return timeSlot;
        }


        public async Task DeleteAsync(int id)
        {
            await using var context = new AppDbContext();
            var timeSlot = await context.TimeSlots.FindAsync(id)
                ?? throw new KeyNotFoundException("Time slot not found.");

            context.TimeSlots.Remove(timeSlot);
            await context.SaveChangesAsync();
        }

        private static async Task ValidateAsync(AppDbContext context, TimeSlot timeSlot, bool isUpdate)
        {
            await InputValidation.RecordIdAsync(timeSlot.TimeSlotID, isUpdate,
                () => context.TimeSlots.AnyAsync(t => t.TimeSlotID == timeSlot.TimeSlotID), "Time slot");

            ValidateLectureRange(timeSlot.StartTime, timeSlot.EndTime);

            var exists = await context.TimeSlots.AnyAsync(t =>
                t.StartTime == timeSlot.StartTime &&
                t.EndTime == timeSlot.EndTime &&
                t.IsBreak == timeSlot.IsBreak &&
                (!isUpdate || t.TimeSlotID != timeSlot.TimeSlotID));

            if (exists)
            {
                throw new ArgumentException("Time slot already exists.");
            }

            var lectureSlots = await context.TimeSlots
                .AsNoTracking()
                .Where(t => !t.IsBreak && (!isUpdate || t.TimeSlotID != timeSlot.TimeSlotID))
                .ToListAsync();

            bool tooCloseToAnotherLecture = lectureSlots
                .Any(t =>
                    timeSlot.StartTime < t.EndTime + ScheduleTimingRules.LectureGap &&
                    timeSlot.EndTime + ScheduleTimingRules.LectureGap > t.StartTime);

            if (tooCloseToAnotherLecture)
            {
                throw new ArgumentException("Lecture periods must not overlap and must leave at least a 10-minute gap.");
            }
        }
    }
}
