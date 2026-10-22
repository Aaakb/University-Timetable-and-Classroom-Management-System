using Microsoft.EntityFrameworkCore;

namespace Data_Access_Layer;

public static class FacultyPreferenceSchema
{
    public static Task EnsureAsync(AppDbContext context) => context.Database.ExecuteSqlRawAsync("""
        IF OBJECT_ID(N'[dbo].[FacultySchedulingPreferences]', N'U') IS NULL
        BEGIN
            CREATE TABLE [dbo].[FacultySchedulingPreferences] (
                [FacultyMemberID] int NOT NULL,
                [SemesterNumber] int NOT NULL,
                [SettingsJson] nvarchar(max) NOT NULL,
                [Revision] bigint NOT NULL,
                [UpdatedAtUtc] datetime2 NOT NULL,
                CONSTRAINT [PK_FacultySchedulingPreferences] PRIMARY KEY ([FacultyMemberID], [SemesterNumber]),
                CONSTRAINT [FK_FacultySchedulingPreferences_FacultyMembers] FOREIGN KEY ([FacultyMemberID])
                    REFERENCES [dbo].[FacultyMembers] ([FacultyMemberID]) ON DELETE CASCADE,
                CONSTRAINT [CK_FacultySchedulingPreferences_Semester] CHECK ([SemesterNumber] IN (1, 2))
            );
        END
        IF OBJECT_ID(N'[dbo].[FacultyPeriodPreferences]', N'U') IS NULL
        BEGIN
            CREATE TABLE [dbo].[FacultyPeriodPreferences] (
                [FacultyMemberID] int NOT NULL, [SemesterNumber] int NOT NULL,
                [DayOfWeek] nvarchar(20) NOT NULL, [TimeSlotID] int NOT NULL, [Level] int NOT NULL,
                CONSTRAINT [PK_FacultyPeriodPreferences] PRIMARY KEY ([FacultyMemberID], [SemesterNumber], [DayOfWeek], [TimeSlotID]),
                CONSTRAINT [FK_FacultyPeriodPreferences_Profile] FOREIGN KEY ([FacultyMemberID], [SemesterNumber])
                    REFERENCES [dbo].[FacultySchedulingPreferences] ([FacultyMemberID], [SemesterNumber]) ON DELETE CASCADE,
                CONSTRAINT [FK_FacultyPeriodPreferences_TimeSlots] FOREIGN KEY ([TimeSlotID]) REFERENCES [dbo].[TimeSlots] ([TimeSlotID]),
                CONSTRAINT [CK_FacultyPeriodPreferences_Level] CHECK ([Level] IN (0,1,2)),
                CONSTRAINT [CK_FacultyPeriodPreferences_Day] CHECK ([DayOfWeek] IN ('Sunday','Monday','Tuesday','Wednesday','Thursday'))
            );
        END
        """);
}
