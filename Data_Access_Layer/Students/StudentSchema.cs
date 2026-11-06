using Microsoft.EntityFrameworkCore;

namespace Data_Access_Layer;

public static class StudentSchema
{
    public static Task EnsureAsync(AppDbContext db) => db.Database.ExecuteSqlRawAsync("""
        IF OBJECT_ID(N'dbo.Students', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.Students (
                StudentID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Students PRIMARY KEY,
                FullName nvarchar(150) NOT NULL,
                StudyYearID int NOT NULL,
                CONSTRAINT CK_Students_StudyYear CHECK (StudyYearID BETWEEN 1 AND 4),
                CONSTRAINT FK_Students_StudyYears_StudyYearID FOREIGN KEY (StudyYearID)
                    REFERENCES dbo.StudyYears (StudyYearID)
            );
            CREATE INDEX IX_Students_StudyYearID ON dbo.Students (StudyYearID);
        END
        IF COL_LENGTH(N'dbo.Students', N'SectionID') IS NULL
            ALTER TABLE dbo.Students ADD SectionID int NULL
                CONSTRAINT FK_Students_Sections_SectionID REFERENCES dbo.Sections (SectionID);
        IF COL_LENGTH(N'dbo.Students', N'PracticalGroupNumber') IS NULL
            EXEC(N'ALTER TABLE dbo.Students ADD PracticalGroupNumber int NULL,
                CONSTRAINT CK_Students_PracticalGroup CHECK
                (PracticalGroupNumber IS NULL OR
                 (PracticalGroupNumber IN (1, 2) AND StudyYearID IN (1, 2) AND SectionID IS NOT NULL));');
        """);
}
