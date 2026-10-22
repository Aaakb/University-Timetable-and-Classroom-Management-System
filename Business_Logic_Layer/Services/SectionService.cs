using Microsoft.EntityFrameworkCore;
using Data_Access_Layer;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    public class SectionService
    {
        public async Task<List<Section>> GetAllAsync()
        {
            await using var context = new AppDbContext();
            return await context.Sections
                .Include(s => s.StudyYear)
                .Include(s => s.Branch)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Section?> GetByIdAsync(int id)
        {
            await using var context = new AppDbContext();
            return await context.Sections
                .Include(s => s.StudyYear)
                .Include(s => s.Branch)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SectionID == id);
        }

        public async Task<Section> AddAsync(Section section)
        {
            await using var context = new AppDbContext();
            section.SectionID = await AutoKeyGenerator.NextAsync(context.Sections.Select(s => s.SectionID));
            var result = await AddInContextAsync(context, section);
            return result;
        }

        internal static async Task<Section> AddInContextAsync(AppDbContext context, Section section)
        {
            await ValidateAsync(context, section, false);
            await context.Sections.AddAsync(section);
            await ManualKeySaveHelper.SaveWithManualKeyAsync(context, "[Sections]");
            return section;
        }


        public async Task<Section> UpdateAsync(Section section)
        {
            await using var context = new AppDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var result = await UpdateInContextAsync(context, section);
            await transaction.CommitAsync();
            return result;
        }

        internal static async Task<Section> UpdateInContextAsync(AppDbContext context, Section section)
        {
            await ValidateAsync(context, section, true);
            var existing = await context.Sections.SingleAsync(s => s.SectionID == section.SectionID);
            var lessons = await context.Schedules.Include(s => s.Classroom)
                .Where(s => s.SectionID == section.SectionID).ToListAsync();
            if (lessons.Count > 0 && (existing.StudyYearID != section.StudyYearID ||
                existing.BranchID != section.BranchID || !string.Equals(existing.SectionName, section.SectionName, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Remove this section's saved lessons before changing its study year, branch, or name.");
            if (lessons.Any(s => !ScheduleRoomSelector.HasCapacity(s.Classroom, section, s.GroupName)))
                throw new ArgumentException("The new student count exceeds the capacity of a room used by this section. Move those lessons before increasing the count.");
            context.Entry(existing).CurrentValues.SetValues(section);
            await context.SaveChangesAsync();
            return section;
        }


        public async Task DeleteAsync(int id)
        {
            await using var context = new AppDbContext();
            var section = await context.Sections.FindAsync(id)
                ?? throw new KeyNotFoundException("Section not found.");

            context.Sections.Remove(section);
            await context.SaveChangesAsync();
        }

        private static async Task ValidateAsync(AppDbContext context, Section section, bool isUpdate)
        {
            if (section.SectionID <= 0)
            {
                throw new ArgumentException("Section ID is required.");
            }

            var idExists = await context.Sections.AnyAsync(s => s.SectionID == section.SectionID);

            if (!isUpdate && idExists)
            {
                throw new ArgumentException("Section ID already exists.");
            }

            if (isUpdate && !idExists)
            {
                throw new KeyNotFoundException("Section not found.");
            }

            if (string.IsNullOrWhiteSpace(section.SectionName))
            {
                throw new ArgumentException("Section name is required.");
            }

            if (section.StudentCount < 0)
            {
                throw new ArgumentException("Student count cannot be negative.");
            }

            if (!await context.StudyYears.AnyAsync(sy => sy.StudyYearID == section.StudyYearID))
            {
                throw new ArgumentException("Study year does not exist.");
            }

            if (AcademicStructureRules.UsesGeneralSections(section.StudyYearID) && section.BranchID.HasValue)
            {
                throw new ArgumentException("First and second year sections must not be linked to a branch.");
            }

            if (AcademicStructureRules.UsesBranches(section.StudyYearID) && !section.BranchID.HasValue)
            {
                throw new ArgumentException("Third and fourth year sections must be linked to a branch.");
            }

            var allowedSectionNames = AcademicStructureRules.GetAllowedSectionNames(section.StudyYearID);

            if (allowedSectionNames.Count > 0 &&
                !allowedSectionNames.Contains(section.SectionName.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"Allowed section names for this study year are: {AcademicStructureRules.FormatAllowedSectionNames(section.StudyYearID)}.");
            }

            if (section.BranchID.HasValue &&
                !await context.Branches.AnyAsync(b => b.BranchID == section.BranchID.Value))
            {
                throw new ArgumentException("Branch does not exist.");
            }

            section.SectionName = section.SectionName.Trim();
            InputValidation.TextLength(section.SectionName, 50, "Section name");
            var exists = await context.Sections.AnyAsync(s =>
                s.StudyYearID == section.StudyYearID &&
                s.BranchID == section.BranchID &&
                s.SectionName == section.SectionName &&
                (!isUpdate || s.SectionID != section.SectionID));

            if (exists)
            {
                throw new ArgumentException("Section already exists for the same study year and branch.");
            }
        }
    }
}
