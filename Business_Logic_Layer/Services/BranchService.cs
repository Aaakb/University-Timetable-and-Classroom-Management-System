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
            await ValidateAsync(context, branch, false);
            await context.Branches.AddAsync(branch);
            await ManualKeySaveHelper.SaveWithManualKeyAsync(context, "[Branches]");
            return branch;
        }

        public async Task<Branch> UpdateAsync(Branch branch)
        {
            await using var context = new AppDbContext();
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
            if (branch.BranchID <= 0)
            {
                throw new ArgumentException("Branch ID is required.");
            }

            var idExists = await context.Branches.AnyAsync(b => b.BranchID == branch.BranchID);

            if (!isUpdate && idExists)
            {
                throw new ArgumentException("Branch ID already exists.");
            }

            if (isUpdate && !idExists)
            {
                throw new KeyNotFoundException("Branch not found.");
            }

            if (string.IsNullOrWhiteSpace(branch.BranchName))
            {
                throw new ArgumentException("Branch name is required.");
            }

            branch.BranchName = branch.BranchName.Trim();
            var exists = await context.Branches.AnyAsync(b =>
                b.BranchName == branch.BranchName && (!isUpdate || b.BranchID != branch.BranchID));

            if (exists)
            {
                throw new ArgumentException("Branch name already exists.");
            }
        }
    }
}
