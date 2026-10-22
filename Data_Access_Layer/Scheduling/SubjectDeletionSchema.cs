using Microsoft.EntityFrameworkCore;

namespace Data_Access_Layer;

public static class SubjectDeletionSchema
{
    public static async Task EnsureAsync(AppDbContext context)
    {
        await context.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[Subjects]', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.Subjects', N'IsDeleted') IS NULL
                ALTER TABLE [dbo].[Subjects] ADD [IsDeleted] bit NOT NULL
                    CONSTRAINT [DF_Subjects_IsDeleted] DEFAULT (0) WITH VALUES;
            """);
        await context.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[Subjects]', N'U') IS NOT NULL
            BEGIN
                IF EXISTS (SELECT 1 FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'[dbo].[Subjects]')
                      AND name = N'IX_Subjects_SubjectName_StudyYearID_BranchID_SemesterNumber'
                      AND has_filter = 0)
                    DROP INDEX [IX_Subjects_SubjectName_StudyYearID_BranchID_SemesterNumber] ON [dbo].[Subjects];
                IF NOT EXISTS (SELECT 1 FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'[dbo].[Subjects]')
                      AND name = N'IX_Subjects_SubjectName_StudyYearID_BranchID_SemesterNumber')
                    CREATE UNIQUE INDEX [IX_Subjects_SubjectName_StudyYearID_BranchID_SemesterNumber]
                    ON [dbo].[Subjects] ([SubjectName], [StudyYearID], [BranchID], [SemesterNumber])
                    WHERE [IsDeleted] = 0;
            END;
            """);
    }
}
