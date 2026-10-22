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

            int identityColumns = await context.Database.SqlQuery<int>(
                $"SELECT COUNT(*) AS [Value] FROM sys.identity_columns WHERE object_id = OBJECT_ID({tableName})")
                .SingleAsync();
            if (identityColumns == 0)
            {
                await context.SaveChangesAsync();
                return;
            }

            // Reuse the caller's transaction so validation and saving remain atomic.
            await using var transaction = context.Database.CurrentTransaction is null
                ? await context.Database.BeginTransactionAsync() : null;
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
                if (transaction is not null) await transaction.CommitAsync();
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

    }
}
