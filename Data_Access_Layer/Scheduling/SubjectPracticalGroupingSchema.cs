using Microsoft.EntityFrameworkCore;

namespace Data_Access_Layer;

public static class SubjectPracticalGroupingSchema
{
    public static Task EnsureAsync(AppDbContext context) => context.Database.ExecuteSqlRawAsync("""
        IF OBJECT_ID(N'[dbo].[Subjects]', N'U') IS NOT NULL
           AND COL_LENGTH(N'dbo.Subjects', N'MergePracticalGroups') IS NULL
            ALTER TABLE [dbo].[Subjects] ADD [MergePracticalGroups] bit NOT NULL
                CONSTRAINT [DF_Subjects_MergePracticalGroups] DEFAULT(0);
        """);
}
