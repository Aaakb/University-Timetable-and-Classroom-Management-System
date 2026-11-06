using System.Text.Json;
using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

string database = "UniversityTimetable_SubjectDeletion_" + Guid.NewGuid().ToString("N")[..8];
string? originalEnvironment = Environment.GetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName);
var connection = new SqlConnectionStringBuilder
{
    DataSource = Environment.GetEnvironmentVariable("UNIVERSITY_TIMETABLE_TEST_SERVER") ?? "localhost",
    InitialCatalog = database, IntegratedSecurity = true, Encrypt = false, TrustServerCertificate = true
};
Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, connection.ConnectionString);
int checks = 0, failures = 0;
var service = new SubjectService();
Console.WriteLine("ISOLATED DATABASE " + database);

void Check(string name, bool success)
{
    checks++;
    if (!success) failures++;
    Console.WriteLine((success ? "PASS " : "FAIL ") + name);
}

async Task<string> Snapshot(int? excludedSubject = null)
{
    await using var db = new AppDbContext();
    return JsonSerializer.Serialize(new
    {
        Subjects = await db.Subjects.Where(x => x.SubjectID != excludedSubject).OrderBy(x => x.SubjectID)
            .Select(x => new { x.SubjectID, x.SubjectName, x.StudyYearID, x.SemesterNumber, x.IsDeleted }).ToListAsync(),
        Assignments = await db.FacultyMemberSubjects.Where(x => x.SubjectID != excludedSubject)
            .OrderBy(x => x.SubjectID).ThenBy(x => x.FacultyMemberID)
            .Select(x => new { x.SubjectID, x.FacultyMemberID, x.SectionID, x.LectureType }).ToListAsync(),
        Lectures = await db.Schedules.Where(x => x.SubjectID != excludedSubject).OrderBy(x => x.ScheduleID)
            .Select(x => new { x.ScheduleID, x.SubjectID, x.FacultyMemberID, x.SectionID, x.ClassroomID,
                x.TimeSlotID, x.DayOfWeek, x.SemesterNumber, x.LectureType, x.GroupName, x.IsLocked }).ToListAsync(),
        Teachers = await db.FacultyMembers.OrderBy(x => x.FacultyMemberID)
            .Select(x => new { x.FacultyMemberID, x.FullName }).ToListAsync()
    });
}

try
{
    await using (var db = new AppDbContext())
    {
        await db.Database.EnsureCreatedAsync();
        db.StudyYears.Add(new() { StudyYearID = 1, YearName = "First Year" });
        db.Sections.AddRange(new Section { SectionID = 11, SectionName = "A", StudyYearID = 1, StudentCount = 20 },
            new Section { SectionID = 12, SectionName = "B", StudyYearID = 1, StudentCount = 20 });
        db.Subjects.AddRange(
            new Subject { SubjectID = 101, SubjectName = "Remove this subject", StudyYearID = 1, SemesterNumber = 1 },
            new Subject { SubjectID = 205, SubjectName = "Other semester", StudyYearID = 1, SemesterNumber = 2 },
            new Subject { SubjectID = 309, SubjectName = "Legacy archived subject", StudyYearID = 1, SemesterNumber = 1, IsDeleted = true },
            new Subject { SubjectID = 410, SubjectName = "Unassigned subject", StudyYearID = 1, SemesterNumber = 1 });
        db.FacultyMembers.AddRange(Enumerable.Range(1, 3).Select(id => new FacultyMember { FacultyMemberID = id, FullName = "Teacher " + id }));
        db.Classrooms.Add(new() { ClassroomID = 1, ClassroomNumber = "Lab", RoomType = "Lab", Capacity = 30 });
        db.TimeSlots.Add(new() { TimeSlotID = 1, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(9) });
        db.TimeSlots.Add(new() { TimeSlotID = 2, StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(10) });
        db.FacultyMemberSubjects.AddRange(
            new FacultyMemberSubject { FacultyMemberID = 1, SubjectID = 101, LectureType = "Theory" },
            new FacultyMemberSubject { FacultyMemberID = 2, SubjectID = 101, SectionID = 11, LectureType = "Practical" },
            new FacultyMemberSubject { FacultyMemberID = 3, SubjectID = 101, SectionID = 12, LectureType = "Both" },
            new FacultyMemberSubject { FacultyMemberID = 1, SubjectID = 205, LectureType = "Both" },
            new FacultyMemberSubject { FacultyMemberID = 2, SubjectID = 309, LectureType = "Both" });
        Schedule Row(int teacher, int subject, int section, string day, string type, bool locked = false) => new()
        {
            FacultyMemberID = teacher, SubjectID = subject, SectionID = section, StudyYearID = 1,
            ClassroomID = 1, TimeSlotID = subject == 309 && teacher == 3 ? 2 : 1, SemesterNumber = subject == 205 ? 2 : 1,
            DayOfWeek = day, LectureType = type, GroupName = type == "Practical" ? "A1" : "All", IsLocked = locked
        };
        db.Schedules.AddRange(Row(1, 101, 11, "Sunday", "Theory", true), Row(2, 101, 11, "Monday", "Practical"),
            Row(3, 101, 12, "Tuesday", "Theory"), Row(1, 205, 11, "Wednesday", "Theory", true),
            Row(2, 309, 11, "Thursday", "Theory"),
            Row(3, 309, 12, "Sunday", "Practical", true)); // Legacy lecture without a current assignment.
        await db.SaveChangesAsync();
    }

    string allBefore = await Snapshot();
    await using (var db = new AppDbContext())
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.SubjectAssignmentBlocker (FacultyMemberID int NOT NULL, SubjectID int NOT NULL, CONSTRAINT FK_TestSubjectAssignment FOREIGN KEY (FacultyMemberID,SubjectID) REFERENCES dbo.FacultyMemberSubjects(FacultyMemberID,SubjectID)); INSERT INTO dbo.SubjectAssignmentBlocker VALUES (2,101)");
    bool assignmentRejected = false;
    try { await service.DeleteAsync(101); }
    catch (SqlException e) when (e.Number == 547) { assignmentRejected = true; }
    catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 547 }) { assignmentRejected = true; }
    finally { await using var db = new AppDbContext(); await db.Database.ExecuteSqlRawAsync("DROP TABLE dbo.SubjectAssignmentBlocker"); }
    Check("Assignment-delete failure is reported", assignmentRejected);
    Check("Assignment failure rolls back the subject and every deleted lecture", await Snapshot() == allBefore);

    // Reset the flag only in the isolated fixture so the old implementation's
    // missing cascade can fail both atomicity probes independently.
    await using (var db = new AppDbContext())
    {
        await db.Subjects.Where(x => x.SubjectID == 101).ExecuteUpdateAsync(u => u.SetProperty(x => x.IsDeleted, false));
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.Subjects ADD CONSTRAINT CK_TestSubjectArchive CHECK (SubjectID <> 101 OR IsDeleted = 0)");
    }
    bool archiveRejected = false;
    try { await service.DeleteAsync(101); }
    catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 547 }) { archiveRejected = true; }
    finally { await using var db = new AppDbContext(); await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.Subjects DROP CONSTRAINT CK_TestSubjectArchive"); }
    Check("Subject archive failure is reported", archiveRejected);
    Check("Archive failure rolls back assignments and lectures together", await Snapshot() == allBefore);

    string unrelated = await Snapshot(101);
    await service.DeleteAsync(101);
    Check("Deleted subject disappears from the active list", !(await service.GetAllAsync()).Any(x => x.SubjectID == 101));
    Check("Deleted subject disappears from the lookup", await service.GetByIdAsync(101) is null);
    await using (var db = new AppDbContext())
    {
        Check("All teachers' general and section-scoped assignments are removed", !await db.FacultyMemberSubjects.AnyAsync(x => x.SubjectID == 101));
        Check("All theory, practical and locked lectures are removed", !await db.Schedules.AnyAsync(x => x.SubjectID == 101));
        Check("Subject identity stays archived for historical backup references", await db.Subjects.AnyAsync(x => x.SubjectID == 101 && x.IsDeleted));
    }
    Check("Other subjects, teachers, assignments and timetable rows are unchanged", await Snapshot(101) == unrelated);
    Check("Assignment list no longer returns the deleted subject", !(await new FacultyMemberSubjectService().GetAllAsync()).Any(x => x.SubjectID == 101));
    string after = await Snapshot();
    await service.DeleteAsync(101);
    Check("Repeated deletion is safe and changes no other records", await Snapshot() == after);
    bool notFound = false;
    try { await service.DeleteAsync(999); } catch (KeyNotFoundException) { notFound = true; }
    Check("Missing subject reports not found", notFound);
    Check("Missing subject cannot change any records", await Snapshot() == after);

    unrelated = await Snapshot(309);
    await service.DeleteAsync(309);
    await using (var db = new AppDbContext())
    {
        Check("Deleting an archived subject cleans its legacy assignments", !await db.FacultyMemberSubjects.AnyAsync(x => x.SubjectID == 309));
        Check("Archived subject cleanup includes orphan and locked lectures", !await db.Schedules.AnyAsync(x => x.SubjectID == 309));
    }
    Check("Archived cleanup affects only that subject", await Snapshot(309) == unrelated);
    unrelated = await Snapshot(410);
    await service.DeleteAsync(410);
    Check("A subject without assignments or lectures can still be deleted", await service.GetByIdAsync(410) is null);
    Check("Deleting an unassigned subject changes no other records", await Snapshot(410) == unrelated);
    Console.WriteLine($"SUMMARY {checks} checks; {failures} failures");
    Environment.ExitCode = failures == 0 ? 0 : 1;
}
finally
{
    if (connection.InitialCatalog != database || !database.StartsWith("UniversityTimetable_SubjectDeletion_", StringComparison.Ordinal))
        throw new InvalidOperationException("Unsafe test cleanup target.");
    await using (var db = new AppDbContext()) await db.Database.EnsureDeletedAsync();
    Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, originalEnvironment);
}
