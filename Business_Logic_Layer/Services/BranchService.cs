using Microsoft.EntityFrameworkCore;
using Data_Access_Layer;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    public class BranchService
    {
        public async Task<List<Branch>> GetAllAsync()
        {
            await using var context = new AppDbContext();
            return await context.Branches.AsNoTracking().ToListAsync();
        }

        public async Task<Branch?> GetByIdAsync(int id)
        {
            await using var context = new AppDbContext();
            return await context.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.BranchID == id);
        }

        public async Task<Branch> AddAsync(Branch branch)
        {
            await using var context = new AppDbContext();
            branch.BranchID = await AutoKeyGenerator.NextAsync(context.Branches.Select(b => b.BranchID));
            return await AddInContextAsync(context, branch);
        }

        internal static async Task<Branch> AddInContextAsync(AppDbContext context, Branch branch)
        {
            await ValidateAsync(context, branch, false);
            await context.Branches.AddAsync(branch);
            await ManualKeySaveHelper.SaveWithManualKeyAsync(context, "[Branches]");
            return branch;
        }


        public async Task<Branch> UpdateAsync(Branch branch)
        {
            await using var context = new AppDbContext();
            return await UpdateInContextAsync(context, branch);
        }

        internal static async Task<Branch> UpdateInContextAsync(AppDbContext context, Branch branch)
        {
            await ValidateAsync(context, branch, true);
            context.Branches.Update(branch);
            await context.SaveChangesAsync();
            return branch;
        }


        public async Task DeleteAsync(int id)
        {
            await using var context = new AppDbContext();
            var branch = await context.Branches.FindAsync(id)
                ?? throw new KeyNotFoundException("Branch not found.");

            context.Branches.Remove(branch);
            await context.SaveChangesAsync();
        }

        private static async Task ValidateAsync(AppDbContext context, Branch branch, bool isUpdate)
        {
            await InputValidation.RecordIdAsync(branch.BranchID, isUpdate,
                () => context.Branches.AnyAsync(b => b.BranchID == branch.BranchID), "Branch");

            if (string.IsNullOrWhiteSpace(branch.BranchName))
            {
                throw new ArgumentException("Branch name is required.");
            }

            branch.BranchName = branch.BranchName.Trim();
            InputValidation.TextLength(branch.BranchName, 100, "Branch name");
            var exists = await context.Branches.AnyAsync(b =>
                b.BranchName == branch.BranchName && (!isUpdate || b.BranchID != branch.BranchID));

            if (exists)
            {
                throw new ArgumentException("Branch name already exists.");
            }
        }
    }
}
