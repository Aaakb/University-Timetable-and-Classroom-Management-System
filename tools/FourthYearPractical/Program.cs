using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

await using var db = new AppDbContext();
Console.WriteLine($"Database: {db.Database.GetDbConnection().DataSource}/{db.Database.GetDbConnection().Database}");
var summary = await db.Schedules.AsNoTracking().Where(s => s.StudyYearID == 4 && s.LectureType == "Practical")
    .GroupBy(s => new { s.SemesterNumber, s.BranchID, s.GroupName })
    .Select(g => new { g.Key.SemesterNumber, g.Key.BranchID, g.Key.GroupName, Count = g.Count() }).ToListAsync();
foreach (var group in summary) Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(group));
int verifyIndex = Array.IndexOf(args, "--verify-backup");
if (verifyIndex >= 0)
{
    using var document = JsonDocument.Parse(await File.ReadAllTextAsync(args[verifyIndex + 1]));
    var before = document.RootElement.GetProperty("Payload").GetProperty("Lectures").Deserialize<List<Schedule>>()!;
    var expected = before.Where(s => !(s.StudyYearID == 4 && s.LectureType == "Practical" && s.GroupName == "A2")).ToList();
    foreach (var row in expected.Where(s => s.StudyYearID == 4 && s.LectureType == "Practical")) row.GroupName = null;
    var after = await db.Schedules.AsNoTracking().OrderBy(s => s.ScheduleID).ToListAsync();
    if (JsonSerializer.Serialize(expected.OrderBy(s => s.ScheduleID)) != JsonSerializer.Serialize(after))
        throw new InvalidOperationException("Saved timetable differs from the expected A1-to-whole-section conversion.");
    Console.WriteLine($"VERIFIED: {before.Count} -> {after.Count} lectures; all retained fields match the safety snapshot except fourth-year practical GroupName.");
    foreach (int semester in new[] { 1, 2 })
        Console.WriteLine($"Semester {semester}: {JsonSerializer.Serialize((await new TimetableAlternativesService().GetCurrentAsync(semester)).Metrics)}");
    return;
}
if (!args.Contains("--apply"))
{
    Console.WriteLine("Preview only. Use --apply to consolidate saved groups, with an automatic safety snapshot.");
    return;
}
string path = Path.Combine(TimetableBackupService.AutomaticBackupDirectory,
    $"BeforeFourthYearWholeSection-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.uttbackup.json");
Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(await new FourthYearPracticalMaintenance().ApplyAsync(path)));
