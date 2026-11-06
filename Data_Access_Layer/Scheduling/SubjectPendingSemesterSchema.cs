using Microsoft.EntityFrameworkCore;

namespace Data_Access_Layer;

public static class SubjectPendingSemesterSchema
{
    // Called within the import transaction so upgrading the legacy constraint
    // and inserting the pending subjects succeed or roll back together.
    public static Task EnsureAsync(AppDbContext context) => context.Database.ExecuteSqlRawAsync("""
        IF OBJECT_ID(N'dbo.CK_Subjects_Semester', N'C') IS NOT NULL
        BEGIN
            ALTER TABLE dbo.Subjects DROP CONSTRAINT CK_Subjects_Semester;
            ALTER TABLE dbo.Subjects WITH CHECK ADD CONSTRAINT CK_Subjects_SemesterOrPending
                CHECK (SemesterNumber IN (0, 1, 2));
        END
        """);
}
