using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

internal static class Program
{
    static readonly List<object> Results = [];
    static int failures;
    static async Task Check(string name, Func<Task> action)
    {
        try { await action(); Results.Add(new { name, passed = true }); Console.WriteLine("PASS " + name); }
        catch (Exception e) { failures++; Results.Add(new { name, passed = false, error = e.ToString() }); Console.WriteLine("FAIL " + name + ": " + e.Message); }
    }
    static void Ensure(bool value) { if (!value) throw new Exception("Assertion failed"); }
    static string Snapshot(List<Schedule> rows) => JsonSerializer.Serialize(rows.OrderBy(s => s.ScheduleID).Select(s => new {
        s.ScheduleID, s.SubjectID, s.FacultyMemberID, s.ClassroomID, s.TimeSlotID, s.SemesterNumber,
        s.LectureType, s.GroupName, s.DayOfWeek, s.StudyYearID, s.BranchID, s.SectionID }));
    static async Task Main()
    {
        await PracticalExchangeChecks.RunAsync(Check);
        var db = "UniversityTimetable_OriginalExchange_" + Guid.NewGuid().ToString("N")[..8];
        var connection = new SqlConnectionStringBuilder { DataSource = Environment.GetEnvironmentVariable("UNIVERSITY_TIMETABLE_TEST_SERVER") ?? "localhost",
            InitialCatalog = db, IntegratedSecurity = true, Encrypt = false, TrustServerCertificate = true };
        Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, connection.ConnectionString);
        Console.WriteLine("ISOLATED DATABASE: " + db);
        await new DatabaseMaintenanceService().ApplyPendingFixesAsync();
        for (int y = 1; y <= 4; y++) await new StudyYearService().AddAsync(new() { YearName = new[] { "First Year", "Second Year", "Third Year", "Fourth Year" }[y-1] });
        await new SectionService().AddAsync(new() { SectionName = "A", StudyYearID = 1, StudentCount = 30 });
        var teachers = new FacultyMemberService(); var subjects = new SubjectService(); var teaching = new FacultyMemberSubjectService(); var schedules = new ScheduleService();
        var teacher = await teachers.AddAsync(new() { FullName = "Exchange Test Teacher 1" });
        var first = await subjects.AddAsync(new() { SubjectName = "Practical Test 1", StudyYearID = 1, SemesterNumber = 1, PracticalHours = 1, CreditUnits = 1, RequirementType = "Core" });
        await teaching.AddAsync(new() { SubjectID = first.SubjectID, FacultyMemberID = teacher.FacultyMemberID });
        await Check("SQL generation saves both groups of an odd subject", async () => {
            var result = await schedules.GenerateAsync(); var rows = await schedules.GetAllAsync();
            Ensure(result.CreatedCount == 2 && result.SkippedCount == 0 && rows.Count == 2);
        });
        var before = Snapshot(await schedules.GetAllAsync());
        var second = await subjects.AddAsync(new() { SubjectName = "Practical Test 2", StudyYearID = 1, SemesterNumber = 1, PracticalHours = 1, CreditUnits = 1, RequirementType = "Core" });
        await Check("SQL unassigned practical rejection preserves every timetable field", async () => {
            try { await schedules.GenerateAsync(); throw new Exception("Accepted missing assignment"); }
            catch (ArgumentException e) when (e.Message.Contains("Assign teachers to all practical subjects")) { }
            Ensure(Snapshot(await schedules.GetAllAsync()) == before);
        });
        await teaching.AddAsync(new() { SubjectID = second.SubjectID, FacultyMemberID = teacher.FacultyMemberID });
        await Check("SQL flexible practical groups allow one teacher without requiring an exchange", async () => {
            var result = await schedules.GenerateAsync(); var rows = await schedules.GetAllAsync();
            Ensure(result.CreatedCount == 4 && result.SkippedCount == 0 && rows.Count == 4);
            Ensure(rows.GroupBy(s=>s.SubjectID).All(g=>g.Count()==2 && g.Select(s=>s.GroupName).Distinct().Count()==2));
            Ensure(rows.GroupBy(s=>new{s.SemesterNumber,s.DayOfWeek,s.TimeSlotID,s.FacultyMemberID}).All(g=>g.Count()==1));
        });
        // A used assignment is protected; remove its test lessons before replacing it.
        foreach(var lesson in (await schedules.GetAllAsync()).Where(s=>s.SubjectID==second.SubjectID))
            await schedules.DeleteAsync(lesson.ScheduleID);
        await teaching.DeleteAsync(teacher.FacultyMemberID, second.SubjectID);
        var other = await teachers.AddAsync(new() { FullName = "Exchange Test Teacher 2" });
        await teaching.AddAsync(new() { SubjectID = second.SubjectID, FacultyMemberID = other.FacultyMemberID });
        await Check("SQL successful exchange persists four complete nonconflicting bookings", async () => {
            var result = await schedules.GenerateAsync(); var rows = await schedules.GetAllAsync();
            Ensure(result.CreatedCount == 4 && result.SkippedCount == 0 && rows.Count == 4);
            PracticalExchangeChecks.ValidateBlocks(rows, (await new TimeSlotService().GetAllAsync()).Where(p => !p.IsBreak).ToList());
        });
        var output = Path.GetFullPath(".tmp/OriginalPracticalExchange/results"); Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, "checks-" + (System.Diagnostics.Debugger.IsAttached ? "attached" : typeof(Program).Assembly.Location.Contains("Release") ? "release" : "debug") + ".json"), JsonSerializer.Serialize(new { database = db, results = Results }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"SUMMARY: {Results.Count} checks, {failures} failures"); Environment.ExitCode = failures == 0 ? 0 : 1;
    }
}
