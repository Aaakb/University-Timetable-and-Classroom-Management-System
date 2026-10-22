using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

var connection = new SqlConnectionStringBuilder(DatabaseConnection.ConnectionString);
if (!connection.InitialCatalog.StartsWith("UniversityTimetable_Final") || !connection.InitialCatalog.EndsWith("_Review"))
    throw new InvalidOperationException("Run only against an isolated final-timetable review database.");
var service = new ScheduleService();
await using var db = new AppDbContext();
var rows = await db.Schedules.AsNoTracking().Where(s => s.SemesterNumber == 1).OrderBy(s => s.ScheduleID).ToListAsync();
if (rows.Count != 132) throw new Exception("Expected all 132 source lessons.");
var original = JsonSerializer.Serialize(rows);
foreach (var row in rows) await service.UpdateAsync(row);
var after = await db.Schedules.AsNoTracking().Where(s => s.SemesterNumber == 1).OrderBy(s => s.ScheduleID).ToListAsync();
if (original != JsonSerializer.Serialize(after)) throw new Exception("Service round trip changed the timetable.");
Console.WriteLine("PASS: all 132 lessons round-trip through ScheduleService validation without changes.");

async Task Reject(Schedule lesson, string message)
{
    try { await service.UpdateAsync(lesson); }
    catch (ArgumentException ex) when (ex.Message.Contains(message, StringComparison.OrdinalIgnoreCase)) { return; }
    throw new Exception("Expected rejection: " + message);
}
var firstYear = rows.First(r => r.StudyYearID == 1 && r.LectureType == "Practical");
var oldGroup = firstYear.GroupName;
firstYear.GroupName = null;
await Reject(firstYear, "Practical sessions must use");
firstYear.GroupName = oldGroup;
Console.WriteLine("PASS: first-year practical sessions still require an explicit group.");

var full = rows.First(r => r.StudyYearID == 4 && r.LectureType == "Practical" && r.GroupName == null);
var room = await db.Classrooms.SingleAsync(r => r.ClassroomID == full.ClassroomID);
var oldCapacity = room.Capacity;
try
{
    room.Capacity = 29;
    await db.SaveChangesAsync();
    await Reject(full, "capacity is not enough");
}
finally { room.Capacity = oldCapacity; await db.SaveChangesAsync(); }
Console.WriteLine("PASS: whole-section practical uses all 30 seats, not half the section.");

var existing = rows.First(r => r.StudyYearID == 4 && r.BranchID == 1 && r.DayOfWeek == "Monday" && r.LectureType == "Practical");
var conflicting = rows.First(r => r.StudyYearID == 4 && r.BranchID == 1 && r.SubjectID == 93 && r.LectureType == "Practical");
var copy = JsonSerializer.Deserialize<Schedule>(JsonSerializer.Serialize(conflicting))!;
copy.DayOfWeek = existing.DayOfWeek; copy.TimeSlotID = existing.TimeSlotID; copy.ClassroomID = 19; copy.GroupName = "A1";
await Reject(copy, "section or group already has");
Console.WriteLine("PASS: whole-section practical blocks overlapping subgroup lessons.");

var details = await service.GetScheduleDetailsByFiltersAsync(1, null, null, null, null);
if (details.Count != 132 || details.Any(r => string.IsNullOrWhiteSpace(r.FacultyMemberName))) throw new Exception("Incomplete display view.");
Console.WriteLine("PASS: all 132 lessons appear in the display view with faculty names.");

var second = await db.Schedules.AsNoTracking().Where(s => s.SemesterNumber == 2).OrderBy(s => s.ScheduleID).ToListAsync();
if (second.Count != 145) throw new Exception("Existing semester-2 lesson count changed.");
foreach (var lesson in second) await service.UpdateAsync(lesson);
Console.WriteLine("PASS: all 145 retained semester-2 lessons satisfy the updated room and capacity rules.");
var beforeStartup = JsonSerializer.Serialize(await db.Schedules.AsNoTracking().OrderBy(s => s.ScheduleID).ToListAsync());
await new DatabaseMaintenanceService().ApplyPendingFixesAsync();
var afterStartup = JsonSerializer.Serialize(await db.Schedules.AsNoTracking().OrderBy(s => s.ScheduleID).ToListAsync());
if (beforeStartup != afterStartup) throw new Exception("Startup changed the imported timetable.");
Console.WriteLine("PASS: application startup preserves all 277 saved lessons.");
