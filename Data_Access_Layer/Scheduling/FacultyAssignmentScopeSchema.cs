using Microsoft.EntityFrameworkCore;

namespace Data_Access_Layer;

public static class FacultyAssignmentScopeSchema
{
    public static async Task EnsureAsync(AppDbContext db)
    {
        if (!db.Database.IsSqlServer()) return;
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH(N'dbo.FacultyMemberSubjects', N'SectionID') IS NULL
                ALTER TABLE [dbo].[FacultyMemberSubjects] ADD [SectionID] int NULL;
            IF COL_LENGTH(N'dbo.FacultyMemberSubjects', N'LectureType') IS NULL
                ALTER TABLE [dbo].[FacultyMemberSubjects] ADD [LectureType] nvarchar(20) NOT NULL
                    CONSTRAINT [DF_FacultyMemberSubjects_LectureType] DEFAULT N'Both' WITH VALUES;
            IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_FacultyMemberSubjects_LectureType')
                EXEC(N'ALTER TABLE [dbo].[FacultyMemberSubjects] ADD CONSTRAINT [CK_FacultyMemberSubjects_LectureType]
                    CHECK ([LectureType] IN (N''Both'', N''Theory'', N''Practical''))');
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_FacultyMemberSubjects_Sections_SectionID')
                ALTER TABLE [dbo].[FacultyMemberSubjects] ADD CONSTRAINT [FK_FacultyMemberSubjects_Sections_SectionID]
                    FOREIGN KEY ([SectionID]) REFERENCES [dbo].[Sections] ([SectionID]);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FacultyMemberSubjects_SectionID' AND object_id = OBJECT_ID(N'dbo.FacultyMemberSubjects'))
                CREATE INDEX [IX_FacultyMemberSubjects_SectionID] ON [dbo].[FacultyMemberSubjects] ([SectionID]);
            """);
    }
}
