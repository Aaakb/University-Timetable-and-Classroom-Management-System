using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    internal static class ManualKeySaveHelper
    {
        private static readonly HashSet<string> AllowedTableNames = new(StringComparer.Ordinal)
        {
            "[Branches]",
            "[StudyYears]",
            "[Sections]",
            "[Subjects]",
            "[FacultyMembers]",
            "[Classrooms]",
            "[TimeSlots]",
            "[ApplicationUsers]"
        };

        public static async Task SaveWithManualKeyAsync(AppDbContext context, string tableName)
        {
            if (!AllowedTableNames.Contains(tableName))
            {
                throw new ArgumentException("Unsupported manual key table.", nameof(tableName));
            }

            if (!context.Database.IsSqlServer())
            {
                await context.SaveChangesAsync();
                return;
            }

            await using var transaction = await context.Database.BeginTransactionAsync();
            var identityInsertEnabled = false;

            try
            {
#pragma warning disable EF1002
                await context.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT {tableName} ON");
#pragma warning restore EF1002
                identityInsertEnabled = true;

                await context.SaveChangesAsync();

#pragma warning disable EF1002
                await context.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT {tableName} OFF");
#pragma warning restore EF1002
                identityInsertEnabled = false;
                await transaction.CommitAsync();
            }
            catch (Exception ex) when (!identityInsertEnabled && IsNoIdentityColumnError(ex))
            {
                await transaction.RollbackAsync();
                await context.SaveChangesAsync();
            }
            catch
            {
                if (identityInsertEnabled)
                {
                    try
                    {
#pragma warning disable EF1002
                        await context.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT {tableName} OFF");
#pragma warning restore EF1002
                    }
                    catch
                    {
                        // Keep the original database error visible to the caller.
                    }
                }

                throw;
            }
        }

        private static bool IsNoIdentityColumnError(Exception exception)
        {
            for (var current = exception; current is not null; current = current.InnerException)
            {
                if (current.Message.Contains("does not have the identity property", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
