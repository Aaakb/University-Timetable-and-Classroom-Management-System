using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

// One-off audited adoption: prepare on an isolated copy, then apply the reviewed
// plan only if every academic/placement/preference value is still unchanged.
if (args.Length != 5 || args[0] is not ("prepare" or "apply"))
    throw new ArgumentException("prepare|apply DATABASE SOURCE_TRANSCRIPTION PDF PLAN_JSON");
bool prepare = args[0] == "prepare";
if (prepare && args[1] == "UniversityTimetable") throw new ArgumentException("Prepare requires an isolated copy.");
var connection = new SqlConnectionStringBuilder(DatabaseConnection.ConnectionString) { InitialCatalog = args[1] };
Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, connection.ConnectionString);
var json = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };
string pdfHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(args[3])));
var assembly = typeof(ScheduleService).Assembly;
var catalogType = assembly.GetType(typeof(ScheduleService).Namespace + ".ImportCatalog")!;
async Task<object> Catalog(AppDbContext db)
{
    var task = (Task)catalogType.GetMethod("LoadAsync")!.Invoke(null, [db, CancellationToken.None])!;
    await task; return task.GetType().GetProperty("Result")!.GetValue(task)!;
}
string Version(object catalog) => (string)catalogType.GetMethod("Version")!.Invoke(catalog, null)!;
string RowsJson(IEnumerable<Schedule> rows) => JsonSerializer.Serialize(rows.OrderBy(s => s.ScheduleID));
void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); Console.WriteLine("PASS " + message); }
await using var db = new AppDbContext();
await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
await FacultyAssignmentScopeSchema.EnsureAsync(db);
await FacultyAssignmentScopeSchema.EnsureAsync(db);
var catalog = await Catalog(db);
string beforeVersion = Version(catalog);
var allRows = await db.Schedules.OrderBy(s => s.ScheduleID).ToListAsync();
string semester2 = RowsJson(allRows.Where(s => s.SemesterNumber == 2));
var before = JsonSerializer.Deserialize<List<Schedule>>(RowsJson(allRows))!;
var assignments = await db.FacultyMemberSubjects.OrderBy(a => a.SubjectID).ThenBy(a => a.FacultyMemberID).ToListAsync();
Plan plan;
if (prepare)
{
    var source = JsonSerializer.Deserialize<Source>(await File.ReadAllTextAsync(args[2]), json)!;
    var subjects = await db.Subjects.AsNoTracking().Where(s => s.SemesterNumber == 1 && !s.IsDeleted).ToListAsync();
    var faculty = await db.FacultyMembers.AsNoTracking().ToListAsync();
    var expected = new List<FacultyMemberSubject>();
    foreach (var group in source.Lessons.GroupBy(r => (r.Year, r.Branch, r.Subject, r.Faculty)))
    {
        var key = group.Key;
        var subject = subjects.Single(s => s.StudyYearID == key.Year && s.BranchID == key.Branch && s.SubjectName == key.Subject);
        int id = faculty.Single(f => f.FullName == key.Faculty).FacultyMemberID;
        string type = group.Select(r => r.Type).Distinct().Count() == 2 ? "Both" : group.First().Type;
        var assignment = assignments.Single(a => a.SubjectID == subject.SubjectID && a.FacultyMemberID == id);
        assignment.LectureType = type;
        expected.Add(new() { SubjectID = subject.SubjectID, FacultyMemberID = id, SectionID = assignment.SectionID, LectureType = type });
    }
    Check(expected.Count == 73 && expected.Select(a => a.SubjectID).Distinct().Count() == 50,
        "All 73 PDF assignments cover exactly 50 semester-one subjects");
    Check(assignments.Count(a => subjects.Any(s => s.SubjectID == a.SubjectID)) == expected.Count,
        "No extra semester-one assignment is silently retained");
    int changed = 0;
    foreach (var row in allRows.Where(s => s.SemesterNumber == 1))
    {
        int teacher = expected.Where(a => FacultyAssignmentRules.Covers(a, row.SubjectID, row.SectionID, row.LectureType))
            .Select(a => a.FacultyMemberID).Distinct().Single();
        if (teacher != row.FacultyMemberID) { row.FacultyMemberID = teacher; changed++; }
    }
    Console.WriteLine($"Corrected {changed} saved lecture teachers to the source assignments.");
    var tables = (Dictionary<Type, List<object>>)catalogType.GetProperty("Tables")!.GetValue(catalog)!;
    tables[typeof(FacultyMemberSubject)] = assignments.Cast<object>().ToList();
    tables[typeof(Schedule)] = allRows.Cast<object>().ToList();
    var target = allRows.Where(s => s.SemesterNumber == 1).Select((s, i) => new TimetableImportRow("PDF assignments", i + 1, s, s.IsLocked)).ToList();
    var planner = assembly.GetType(typeof(ScheduleService).Namespace + ".ImportedTimetableRepairPlanner")!;
    Console.WriteLine("Finding a complete conflict-free plan with the PDF teachers fixed...");
    var result = planner.GetMethod("Solve")!.Invoke(null,
        [catalog, target, allRows.Where(s => s.SemesterNumber != 1).ToList(), 55.0, CancellationToken.None, false, null, false, false])!;
    Console.WriteLine("SEARCH " + result.GetType().GetProperty("Status")!.GetValue(result));
    var solved = (List<Schedule>?)result.GetType().GetProperty("Plan")!.GetValue(result);
    if (solved is null) throw new InvalidOperationException(JsonSerializer.Serialize(result, result.GetType(), json));
    plan = new(beforeVersion, pdfHash, expected, solved);
    await File.WriteAllTextAsync(args[4], JsonSerializer.Serialize(plan, json));
}
else
{
    plan = JsonSerializer.Deserialize<Plan>(await File.ReadAllTextAsync(args[4]), json)!;
    Check(plan.PdfSha256 == pdfHash, "Source PDF still has the reviewed SHA-256");
    Check(plan.BeforeVersion == beforeVersion, "Production data exactly matches the reviewed isolated snapshot");
    foreach (var a in plan.Assignments)
    {
        var current = assignments.Single(x => x.FacultyMemberID == a.FacultyMemberID && x.SubjectID == a.SubjectID);
        current.LectureType = a.LectureType;
        Check(current.SectionID == a.SectionID, "Assignment section preserved " + a.SubjectID + "/" + a.FacultyMemberID);
    }
}
foreach (var row in plan.Schedules)
{
    var current = allRows.Single(s => s.ScheduleID == row.ScheduleID);
    Check(current.SemesterNumber == 1 && current.SubjectID == row.SubjectID && current.SectionID == row.SectionID &&
        current.LectureType == row.LectureType && current.GroupName == row.GroupName && current.IsLocked == row.IsLocked,
        "Lecture identity preserved " + row.ScheduleID);
    db.Entry(current).CurrentValues.SetValues(row);
}
// Save cyclic swaps atomically using the application's existing writer. SQL
// Server checks unique teacher/room slots immediately during individual UPDATEs.
foreach (var row in allRows) db.Entry(row).State = EntityState.Detached;
await db.SaveChangesAsync();
var changedRows = allRows.Where(a => JsonSerializer.Serialize(a) !=
    JsonSerializer.Serialize(before.Single(b => b.ScheduleID == a.ScheduleID))).ToList();
await FacultyScheduleWriter.ReplacePlacementsAsync(db, changedRows);
var validate = typeof(ScheduleService).GetMethod("ValidateAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
foreach (var row in allRows)
    await (Task)validate.Invoke(null, [db, row, true, true])!;
Check(semester2 == RowsJson(allRows.Where(s => s.SemesterNumber == 2)), "All semester-two lecture fields are unchanged");
Check(before.Count == allRows.Count && plan.Schedules.Count == before.Count(s => s.SemesterNumber == 1), $"All {allRows.Count} saved lecture identities retained; no lecture added or deleted");
var afterCatalog = await Catalog(db);
var audit = new {
    Database=args[1], SemesterOneLectures=plan.Schedules.Count, AdoptedAssignments=plan.Assignments.Count,
    ChangedTeachers=before.Count(b => allRows.Single(s => s.ScheduleID == b.ScheduleID).FacultyMemberID != b.FacultyMemberID),
    MovedLectures=before.Count(b => { var a=allRows.Single(s => s.ScheduleID == b.ScheduleID); return a.DayOfWeek!=b.DayOfWeek || a.TimeSlotID!=b.TimeSlotID || a.ClassroomID!=b.ClassroomID; }),
    BeforeVersion=beforeVersion, AfterVersion=Version(afterCatalog), PdfSha256=pdfHash,
    Changes=before.Where(b => JsonSerializer.Serialize(b)!=JsonSerializer.Serialize(allRows.Single(s=>s.ScheduleID==b.ScheduleID)))
        .Select(b=>new { Before=b, After=allRows.Single(s=>s.ScheduleID==b.ScheduleID) }).ToList()
};
await transaction.CommitAsync();
await File.WriteAllTextAsync(args[4] + (prepare ? ".trial-audit.json" : ".applied-audit.json"),JsonSerializer.Serialize(audit,json));
Console.WriteLine(JsonSerializer.Serialize(new { audit.Database,audit.AdoptedAssignments,audit.ChangedTeachers,audit.MovedLectures,audit.AfterVersion },json));

record Source(List<SourceLesson> Lessons);
record SourceLesson(int Year, int? Branch, string Subject, string Faculty, string Type);
record Plan(string BeforeVersion, string PdfSha256, List<FacultyMemberSubject> Assignments, List<Schedule> Schedules);
