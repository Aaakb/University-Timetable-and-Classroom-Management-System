using Microsoft.EntityFrameworkCore;

namespace Data_Access_Layer;

public static class SubjectLectureCountSchema
{
    public static Task EnsureAsync(AppDbContext context) => context.Database.ExecuteSqlRawAsync("""
        IF OBJECT_ID(N'[dbo].[Subjects]', N'U') IS NOT NULL
        BEGIN
            IF COL_LENGTH(N'dbo.Subjects', N'TheoreticalLectureCount') IS NULL
                ALTER TABLE [dbo].[Subjects] ADD [TheoreticalLectureCount] int NULL;
            IF COL_LENGTH(N'dbo.Subjects', N'PracticalLectureCount') IS NULL
                ALTER TABLE [dbo].[Subjects] ADD [PracticalLectureCount] int NULL;
        END;
        """);
}
