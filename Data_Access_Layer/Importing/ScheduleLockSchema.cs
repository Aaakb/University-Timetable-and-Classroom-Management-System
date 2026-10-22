using Microsoft.EntityFrameworkCore;

namespace Data_Access_Layer;

public static class ScheduleLockSchema
{
    public static Task EnsureAsync(AppDbContext context) => context.Database.ExecuteSqlRawAsync("""
        IF OBJECT_ID(N'[dbo].[Schedules]', N'U') IS NOT NULL
           AND COL_LENGTH(N'dbo.Schedules', N'IsLocked') IS NULL
            ALTER TABLE [dbo].[Schedules] ADD [IsLocked] bit NOT NULL
                CONSTRAINT [DF_Schedules_IsLocked] DEFAULT (0) WITH VALUES;
        """);
}
