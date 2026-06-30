using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    public class DatabaseMaintenanceService
    {
        public async Task ApplyPendingFixesAsync()
        {
            await using var context = new AppDbContext();
            await context.Database.EnsureCreatedAsync();

            if (!context.Database.IsSqlServer())
            {
                return;
            }

            await EnsureApplicationUsersTableAsync(context);
            await EnsureSubjectsSchemaAsync(context);
            await EnsureSchedulesSchemaAsync(context);
            await NormalizeScheduleDataAsync(context);
            await RebuildScheduleIndexesAsync(context);
            await RecreateScheduleDetailsViewAsync(context);
            await SchedulingResourceMaintenance.EnsureOfficialResourcesAsync(context);
        }

        private static async Task EnsureApplicationUsersTableAsync(AppDbContext context)
        {
            await context.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'[ApplicationUsers]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [ApplicationUsers]
                    (
                        [ApplicationUserID] int NOT NULL,
                        [FullName] nvarchar(150) NOT NULL,
                        [UserName] nvarchar(50) NOT NULL,
                        [NormalizedUserName] nvarchar(50) NOT NULL,
                        [PasswordHash] nvarchar(128) NOT NULL,
                        [PasswordSalt] nvarchar(128) NOT NULL,
                        [CreatedAtUtc] datetime2 NOT NULL CONSTRAINT [DF_ApplicationUsers_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
                        CONSTRAINT [PK_ApplicationUsers] PRIMARY KEY ([ApplicationUserID])
                    );
                END

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ApplicationUsers_NormalizedUserName' AND object_id = OBJECT_ID(N'[ApplicationUsers]'))
                    CREATE UNIQUE INDEX [IX_ApplicationUsers_NormalizedUserName]
                    ON [ApplicationUsers] ([NormalizedUserName]);
                """);
        }

        private static async Task EnsureSubjectsSchemaAsync(AppDbContext context)
        {
            await context.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'[Subjects]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.Subjects', N'SemesterNumber') IS NULL
                    ALTER TABLE [Subjects]
                    ADD [SemesterNumber] int NOT NULL
                    CONSTRAINT [DF_Subjects_SemesterNumber] DEFAULT 1;
                """);

            await context.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'[Subjects]', N'U') IS NOT NULL
                BEGIN
                    UPDATE [Subjects]
                    SET [SemesterNumber] = 1
                    WHERE [SemesterNumber] IS NULL OR [SemesterNumber] NOT IN (1, 2);

                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Subjects_SubjectName_StudyYearID_SemesterNumber' AND object_id = OBJECT_ID(N'[Subjects]'))
                        DROP INDEX [IX_Subjects_SubjectName_StudyYearID_SemesterNumber] ON [Subjects];

                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Subjects_SubjectName_StudyYearID_BranchID_SemesterNumber' AND object_id = OBJECT_ID(N'[Subjects]'))
                        AND NOT EXISTS (
                            SELECT 1
                            FROM [Subjects]
                            GROUP BY [SubjectName], [StudyYearID], [BranchID], [SemesterNumber]
                            HAVING COUNT(*) > 1)
                        CREATE UNIQUE INDEX [IX_Subjects_SubjectName_StudyYearID_BranchID_SemesterNumber]
                        ON [Subjects] ([SubjectName], [StudyYearID], [BranchID], [SemesterNumber]);
                END
                """);
        }

        private static async Task EnsureSchedulesSchemaAsync(AppDbContext context)
        {
            await context.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'[Schedules]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.Schedules', N'SemesterNumber') IS NULL
                    ALTER TABLE [Schedules]
                    ADD [SemesterNumber] int NOT NULL
                    CONSTRAINT [DF_Schedules_SemesterNumber] DEFAULT 1;
                """);

            await context.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'[Schedules]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.Schedules', N'LectureType') IS NULL
                    ALTER TABLE [Schedules] ADD [LectureType] nvarchar(20) NULL;
                """);

            await context.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'[Schedules]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.Schedules', N'GroupName') IS NULL
                    ALTER TABLE [Schedules] ADD [GroupName] nvarchar(20) NULL;
                """);

            await context.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'[Schedules]', N'U') IS NOT NULL
                BEGIN
                    UPDATE schedule
                    SET [SemesterNumber] = subject.[SemesterNumber]
                    FROM [Schedules] schedule
                    INNER JOIN [Subjects] subject ON subject.[SubjectID] = schedule.[SubjectID];

                    UPDATE [Schedules]
                    SET [SemesterNumber] = 1
                    WHERE [SemesterNumber] IS NULL OR [SemesterNumber] NOT IN (1, 2);
                END
                """);
        }

        private static async Task NormalizeScheduleDataAsync(AppDbContext context)
        {
            await context.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'[Schedules]', N'U') IS NOT NULL
                BEGIN
                    UPDATE [Schedules]
                    SET [LectureType] = N'Theory'
                    WHERE [LectureType] IS NULL OR LTRIM(RTRIM([LectureType])) = N'';

                    UPDATE [Schedules]
                    SET [DayOfWeek] =
                        CASE LOWER(LTRIM(RTRIM([DayOfWeek])))
                            WHEN N'sunday' THEN N'Sunday'
                            WHEN N'monday' THEN N'Monday'
                            WHEN N'tuesday' THEN N'Tuesday'
                            WHEN N'wednesday' THEN N'Wednesday'
                            WHEN N'thursday' THEN N'Thursday'
                            ELSE [DayOfWeek]
                        END
                    WHERE [DayOfWeek] IS NOT NULL;

                    UPDATE [Schedules]
                    SET [GroupName] = NULL
                    WHERE [LectureType] = N'Theory';

                    ;WITH SectionMappings AS
                    (
                        SELECT
                            schedule.[ScheduleID],
                            baseSection.[SectionID] AS [BaseSectionID],
                            currentSection.[SectionName] AS [CurrentSectionName]
                        FROM [Schedules] schedule
                        INNER JOIN [Sections] currentSection ON currentSection.[SectionID] = schedule.[SectionID]
                        INNER JOIN [Sections] baseSection ON
                            baseSection.[StudyYearID] = schedule.[StudyYearID] AND
                            baseSection.[BranchID] IS NULL AND
                            baseSection.[SectionName] =
                                CASE
                                    WHEN currentSection.[SectionName] IN (N'A1', N'A2') THEN N'A'
                                    WHEN currentSection.[SectionName] IN (N'B1', N'B2') THEN N'B'
                                END
                        WHERE schedule.[StudyYearID] IN (1, 2)
                          AND currentSection.[BranchID] IS NULL
                          AND currentSection.[SectionName] IN (N'A1', N'A2', N'B1', N'B2')
                    )
                    UPDATE schedule
                    SET
                        [SectionID] = mapping.[BaseSectionID],
                        [LectureType] = N'Practical',
                        [GroupName] = COALESCE(NULLIF(LTRIM(RTRIM(schedule.[GroupName])), N''), mapping.[CurrentSectionName])
                    FROM [Schedules] schedule
                    INNER JOIN SectionMappings mapping ON mapping.[ScheduleID] = schedule.[ScheduleID];

                    UPDATE [Schedules]
                    SET [GroupName] = NULL
                    WHERE [LectureType] = N'Theory';

                    DELETE practicalSection
                    FROM [Sections] practicalSection
                    WHERE practicalSection.[StudyYearID] IN (1, 2)
                      AND practicalSection.[BranchID] IS NULL
                      AND practicalSection.[SectionName] IN (N'A1', N'A2', N'B1', N'B2')
                      AND NOT EXISTS (
                          SELECT 1
                          FROM [Schedules] schedule
                          WHERE schedule.[SectionID] = practicalSection.[SectionID]);
                END
                """);
        }

        private static async Task RebuildScheduleIndexesAsync(AppDbContext context)
        {
            await context.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'[Schedules]', N'U') IS NOT NULL
                BEGIN
                    DECLARE @dropConstraintSql nvarchar(max) = N'';

                    SELECT @dropConstraintSql +=
                        N'ALTER TABLE [Schedules] DROP CONSTRAINT [' + constraintItem.[name] + N'];'
                    FROM sys.key_constraints constraintItem
                    WHERE constraintItem.[parent_object_id] = OBJECT_ID(N'[Schedules]')
                      AND constraintItem.[name] IN
                      (
                          N'UQ_Classroom_Time',
                          N'UQ_Faculty_Time',
                          N'UQ_Section_Time',
                          N'UQ_Classroom_Semester_Time',
                          N'UQ_Faculty_Semester_Time',
                          N'UQ_Section_Semester_Time'
                      );

                    IF @dropConstraintSql <> N''
                        EXEC sp_executesql @dropConstraintSql;

                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_Classroom_Time' AND object_id = OBJECT_ID(N'[Schedules]'))
                        DROP INDEX [UQ_Classroom_Time] ON [Schedules];

                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_Faculty_Time' AND object_id = OBJECT_ID(N'[Schedules]'))
                        DROP INDEX [UQ_Faculty_Time] ON [Schedules];

                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_Section_Time' AND object_id = OBJECT_ID(N'[Schedules]'))
                        DROP INDEX [UQ_Section_Time] ON [Schedules];

                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Schedules_ClassroomID_TimeSlotID' AND object_id = OBJECT_ID(N'[Schedules]'))
                        DROP INDEX [IX_Schedules_ClassroomID_TimeSlotID] ON [Schedules];

                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Schedules_FacultyMemberID_TimeSlotID' AND object_id = OBJECT_ID(N'[Schedules]'))
                        DROP INDEX [IX_Schedules_FacultyMemberID_TimeSlotID] ON [Schedules];

                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Schedules_StudyYearID_BranchID_TimeSlotID' AND object_id = OBJECT_ID(N'[Schedules]'))
                        DROP INDEX [IX_Schedules_StudyYearID_BranchID_TimeSlotID] ON [Schedules];

                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Schedules_ClassroomID_TimeSlotID_DayOfWeek' AND object_id = OBJECT_ID(N'[Schedules]'))
                        DROP INDEX [IX_Schedules_ClassroomID_TimeSlotID_DayOfWeek] ON [Schedules];

                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Schedules_FacultyMemberID_TimeSlotID_DayOfWeek' AND object_id = OBJECT_ID(N'[Schedules]'))
                        DROP INDEX [IX_Schedules_FacultyMemberID_TimeSlotID_DayOfWeek] ON [Schedules];

                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Schedules_StudyYearID_BranchID_SectionID_TimeSlotID_DayOfWeek' AND object_id = OBJECT_ID(N'[Schedules]'))
                        DROP INDEX [IX_Schedules_StudyYearID_BranchID_SectionID_TimeSlotID_DayOfWeek] ON [Schedules];

                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_Section_Semester_Time' AND object_id = OBJECT_ID(N'[Schedules]'))
                        DROP INDEX [UQ_Section_Semester_Time] ON [Schedules];

                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Schedules_Year_Branch_Section_Semester_Time' AND object_id = OBJECT_ID(N'[Schedules]'))
                        DROP INDEX [IX_Schedules_Year_Branch_Section_Semester_Time] ON [Schedules];

                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_Classroom_Semester_Time' AND object_id = OBJECT_ID(N'[Schedules]'))
                        AND NOT EXISTS (
                            SELECT 1
                            FROM [Schedules]
                            GROUP BY [ClassroomID], [SemesterNumber], [DayOfWeek], [TimeSlotID]
                            HAVING COUNT(*) > 1)
                        CREATE UNIQUE INDEX [UQ_Classroom_Semester_Time]
                        ON [Schedules] ([ClassroomID], [SemesterNumber], [DayOfWeek], [TimeSlotID]);

                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_Faculty_Semester_Time' AND object_id = OBJECT_ID(N'[Schedules]'))
                        AND NOT EXISTS (
                            SELECT 1
                            FROM [Schedules]
                            GROUP BY [FacultyMemberID], [SemesterNumber], [DayOfWeek], [TimeSlotID]
                            HAVING COUNT(*) > 1)
                        CREATE UNIQUE INDEX [UQ_Faculty_Semester_Time]
                        ON [Schedules] ([FacultyMemberID], [SemesterNumber], [DayOfWeek], [TimeSlotID]);

                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_Section_Semester_Time' AND object_id = OBJECT_ID(N'[Schedules]'))
                        AND NOT EXISTS (
                            SELECT 1
                            FROM [Schedules]
                            GROUP BY [SectionID], [SemesterNumber], [DayOfWeek], [TimeSlotID], [GroupName]
                            HAVING COUNT(*) > 1)
                        CREATE UNIQUE INDEX [UQ_Section_Semester_Time]
                        ON [Schedules] ([SectionID], [SemesterNumber], [DayOfWeek], [TimeSlotID], [GroupName]);

                END
                """);
        }

        private static async Task RecreateScheduleDetailsViewAsync(AppDbContext context)
        {
            await context.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'[Schedules]', N'U') IS NOT NULL
                BEGIN
                    EXEC(N'
                        CREATE OR ALTER VIEW [dbo].[vw_ScheduleDetails]
                        AS
                        SELECT
                            schedule.[ScheduleID],
                            schedule.[SubjectID],
                            schedule.[FacultyMemberID],
                            schedule.[ClassroomID],
                            schedule.[TimeSlotID],
                            schedule.[SemesterNumber],
                            schedule.[StudyYearID],
                            schedule.[BranchID],
                            schedule.[SectionID],
                            studyYear.[YearName],
                            branch.[BranchName],
                            CASE
                                WHEN section.[SectionName] IN (N''A1'', N''A2'') THEN N''A''
                                WHEN section.[SectionName] IN (N''B1'', N''B2'') THEN N''B''
                                ELSE section.[SectionName]
                            END AS [SectionName],
                            CASE
                                WHEN schedule.[LectureType] = N''Practical''
                                    THEN COALESCE(
                                        schedule.[GroupName],
                                        CASE
                                            WHEN section.[SectionName] IN (N''A1'', N''A2'', N''B1'', N''B2'')
                                                THEN section.[SectionName]
                                        END)
                                ELSE NULL
                            END AS [GroupName],
                            schedule.[LectureType],
                            subject.[SubjectName],
                            faculty.[FullName] AS [FacultyMemberName],
                            classroom.[ClassroomNumber],
                            classroom.[Capacity],
                            timeSlot.[StartTime],
                            timeSlot.[EndTime],
                            schedule.[DayOfWeek]
                        FROM [dbo].[Schedules] schedule
                        INNER JOIN [dbo].[Subjects] subject ON subject.[SubjectID] = schedule.[SubjectID]
                        INNER JOIN [dbo].[FacultyMembers] faculty ON faculty.[FacultyMemberID] = schedule.[FacultyMemberID]
                        INNER JOIN [dbo].[Classrooms] classroom ON classroom.[ClassroomID] = schedule.[ClassroomID]
                        INNER JOIN [dbo].[TimeSlots] timeSlot ON timeSlot.[TimeSlotID] = schedule.[TimeSlotID]
                        LEFT JOIN [dbo].[StudyYears] studyYear ON studyYear.[StudyYearID] = schedule.[StudyYearID]
                        LEFT JOIN [dbo].[Branches] branch ON branch.[BranchID] = schedule.[BranchID]
                        LEFT JOIN [dbo].[Sections] section ON section.[SectionID] = schedule.[SectionID];
                    ');
                END
                """);
        }
    }
}
