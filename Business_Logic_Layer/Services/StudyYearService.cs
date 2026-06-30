using Microsoft.EntityFrameworkCore;
using Data_Access_Layer;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    public class StudyYearService
    {
        public async Task<List<StudyYear>> GetAllAsync()
        {
            await using var context = new AppDbContext();
            var studyYears = await context.StudyYears.AsNoTracking().ToListAsync();

            return studyYears
                .OrderBy(studyYear => AcademicStructureRules.GetStudyYearOrder(studyYear.YearName))
                .ThenBy(studyYear => studyYear.StudyYearID)
                .ToList();
        }

        public async Task<StudyYear?> GetByIdAsync(int id)
        {
            await using var context = new AppDbContext();
            return await context.StudyYears.AsNoTracking().FirstOrDefaultAsync(sy => sy.StudyYearID == id);
        }

        public async Task<StudyYear> AddAsync(StudyYear studyYear)
        {
            await using var context = new AppDbContext();
            studyYear.StudyYearID = await AutoKeyGenerator.NextAsync(context.StudyYears.Select(sy => sy.StudyYearID));
            await ValidateAsync(context, studyYear, false);
            await context.StudyYears.AddAsync(studyYear);
            await ManualKeySaveHelper.SaveWithManualKeyAsync(context, "[StudyYears]");
            return studyYear;
        }

        public async Task<StudyYear> UpdateAsync(StudyYear studyYear)
        {
            await using var context = new AppDbContext();
            await ValidateAsync(context, studyYear, true);
            context.StudyYears.Update(studyYear);
            await context.SaveChangesAsync();
            return studyYear;
        }

        public async Task DeleteAsync(int id)
        {
            await using var context = new AppDbContext();
            var studyYear = await context.StudyYears.FindAsync(id)
                ?? throw new KeyNotFoundException("Study year not found.");

            context.StudyYears.Remove(studyYear);
            await context.SaveChangesAsync();
        }

        private static async Task ValidateAsync(AppDbContext context, StudyYear studyYear, bool isUpdate)
        {
            if (studyYear.StudyYearID <= 0)
            {
                throw new ArgumentException("Study year ID is required.");
            }

            var idExists = await context.StudyYears.AnyAsync(sy => sy.StudyYearID == studyYear.StudyYearID);

            if (!isUpdate && idExists)
            {
                throw new ArgumentException("Study year ID already exists.");
            }

            if (isUpdate && !idExists)
            {
                throw new KeyNotFoundException("Study year not found.");
            }

            if (string.IsNullOrWhiteSpace(studyYear.YearName))
            {
                throw new ArgumentException("Study year name is required.");
            }

            studyYear.YearName = studyYear.YearName.Trim();
            var exists = await context.StudyYears.AnyAsync(sy =>
                sy.YearName == studyYear.YearName && (!isUpdate || sy.StudyYearID != studyYear.StudyYearID));

            if (exists)
            {
                throw new ArgumentException("Study year name already exists.");
            }
        }
    }
}
