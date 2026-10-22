using System.Data;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using UniversityTimetable.API;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

// Pure rule regressions can run without creating or connecting to a database.
if (args.Contains("--rules-only"))
{
    int ruleChecks = 0;
    foreach (var test in new (string Assigned, string? Requested, int? AssignedSection, int? RequestedSection, int Subject, bool Expected)[]
    {
        ("Theory", "theory", 10, 10, 7, true),
        ("theory", "THEORY", 10, 10, 7, true),
        ("Practical", "practical", 10, 10, 7, true),
        ("practical", "PRACTICAL", 10, 10, 7, true),
        ("both", "Theory", 10, 10, 7, true),
        ("BOTH", "Practical", 10, 10, 7, true),
        ("Theory", "Practical", 10, 10, 7, false),
        ("Practical", "Theory", 10, 10, 7, false),
        ("both", "Theory", 10, 11, 7, false),
        ("both", "Theory", 10, 10, 8, false),
        ("both", "Theory", null, 10, 7, true),
        ("both", "Practical", null, 11, 7, true),
        ("Theory", null, 10, 10, 7, true),
        ("Theory", "Theory", 10, null, 7, false)
    })
    {
        var assignment = new FacultyMemberSubject { FacultyMemberID = 42, SubjectID = 7,
            SectionID = test.AssignedSection, LectureType = test.Assigned };
        var actual = FacultyAssignmentRules.FacultyIds([assignment], test.Subject, test.RequestedSection, test.Requested);
        if (!actual.SequenceEqual(test.Expected ? new[] { 42 } : Array.Empty<int>()))
            throw new Exception($"Incorrect faculty eligibility for {test}.");
        ruleChecks++;
    }
    var faculty = FacultyAssignmentRules.FacultyIds([
        new() { FacultyMemberID = 9, SubjectID = 7, SectionID = 10, LectureType = "theory" },
        new() { FacultyMemberID = 2, SubjectID = 7, SectionID = null, LectureType = "BOTH" },
        new() { FacultyMemberID = 9, SubjectID = 7, SectionID = 10, LectureType = "Theory" },
        new() { FacultyMemberID = 4, SubjectID = 7, SectionID = 11, LectureType = "Theory" },
        new() { FacultyMemberID = 5, SubjectID = 8, SectionID = 10, LectureType = "Both" },
        new() { FacultyMemberID = 6, SubjectID = 7, SectionID = 10, LectureType = "Practical" }
    ], 7, 10, "THEORY");
    if (!faculty.SequenceEqual(new[] { 2, 9 }))
        throw new Exception("Faculty selection must retain scope/type filtering, sorting and deduplication.");
    ruleChecks++;
    Console.WriteLine($"PASS {ruleChecks} faculty eligibility regressions; no database connection opened.");
    return;
}

string database = "UniversityTimetable_AssignmentTypes_" + Guid.NewGuid().ToString("N")[..8];
var connection = new SqlConnectionStringBuilder(DatabaseConnection.ConnectionString) { InitialCatalog = database };
Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, connection.ConnectionString);
Console.WriteLine("ISOLATED DATABASE " + database);
int checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
async Task Reject(Func<Task> action, string name) { try { await action(); } catch (ArgumentException) { Check(true, name); return; } throw new Exception("Expected rejection: " + name); }
async Task<List<Schedule>> Rows() { await using var db = new AppDbContext(); return await db.Schedules.AsNoTracking().OrderBy(s => s.ScheduleID).ToListAsync(); }
string Json(object value) => JsonSerializer.Serialize(value);
await using (var db = new AppDbContext())
{
    await db.Database.EnsureCreatedAsync();
    db.StudyYears.Add(new() { StudyYearID = 4, YearName = "Fourth Year" });
    db.Branches.Add(new() { BranchID = 1, BranchName = "Computer Science" });
    db.Sections.Add(new() { SectionID = 1, SectionName = "A", StudyYearID = 4, BranchID = 1, StudentCount = 30 });
    db.Subjects.Add(new() { SubjectID = 1, SubjectName = "Separate theory and practical", StudyYearID = 4, BranchID = 1, SemesterNumber = 1, TheoreticalHours = 2, PracticalHours = 2 });
    for (int i = 1; i <= 2; i++)
    {
        db.FacultyMembers.Add(new() { FacultyMemberID = i, FullName = "Teacher " + i });
        db.TimeSlots.Add(new() { TimeSlotID = i, StartTime = TimeSpan.FromHours(8 + 2 * i), EndTime = TimeSpan.FromHours(9 + 2 * i) });
    }
    db.Classrooms.AddRange(new Classroom { ClassroomID = 1, ClassroomNumber = "Lecture", RoomType = "Lecture", Capacity = 30 },
        new Classroom { ClassroomID = 2, ClassroomNumber = "Lab", RoomType = "Lab", Capacity = 30 });
    db.FacultyMemberSubjects.Add(new() { FacultyMemberID = 1, SubjectID = 1 });
    await db.SaveChangesAsync();
    await db.Database.ExecuteSqlRawAsync("""
        DECLARE @constraint sysname=(SELECT name FROM sys.default_constraints WHERE parent_object_id=OBJECT_ID('FacultyMemberSubjects') AND parent_column_id=COLUMNPROPERTY(OBJECT_ID('FacultyMemberSubjects'),'LectureType','ColumnId'));
        IF @constraint IS NOT NULL EXEC('ALTER TABLE FacultyMemberSubjects DROP CONSTRAINT '+@constraint);
        ALTER TABLE FacultyMemberSubjects DROP COLUMN LectureType;
        """);
    await FacultyAssignmentScopeSchema.EnsureAsync(db);
    await FacultyAssignmentScopeSchema.EnsureAsync(db);
    db.ChangeTracker.Clear();
    Check((await db.FacultyMemberSubjects.SingleAsync()).LectureType == "Both", "Legacy schema upgrades idempotently with Both and preserves assignment identities");
}
var assignmentService = new FacultyMemberSubjectService();
await assignmentService.UpdateAsync(1, 1, new() { FacultyMemberID = 1, SubjectID = 1, LectureType = "Theory" });
await assignmentService.AddAsync(new() { FacultyMemberID = 2, SubjectID = 1, LectureType = "Practical" });
await Reject(() => assignmentService.UpdateAsync(1,1,new() { FacultyMemberID=1,SubjectID=1,LectureType="Anything" }), "Unknown assignment component rejected");
var dto = AssignmentDto.From((await assignmentService.GetAllAsync()).First(a => a.FacultyMemberID == 1));
Check(dto.LectureType == "Theory" && dto.ToEntity().LectureType == "Theory", "API round-trips assignment lecture type");
var schedules = new ScheduleService();
await schedules.GenerateAsync();
var generated = await Rows();
Check(generated.Count == 2 && generated.All(s => s.FacultyMemberID == (s.LectureType == "Theory" ? 1 : 2)), "Generator uses the PDF-style theory/practical assignment");
var bad = JsonSerializer.Deserialize<Schedule>(Json(generated.First(s => s.LectureType == "Theory")))!;
bad.FacultyMemberID = 2;
await Reject(() => schedules.UpdateAsync(bad), "Manual/API update rejects a practical teacher for theory");
await Reject(() => assignmentService.UpdateAsync(1,1,new() { FacultyMemberID=1,SubjectID=1,LectureType="Practical" }), "Changing component cannot orphan saved lectures");
Check(Json(generated) == Json(await Rows()), "Rejected changes preserve the timetable");

ExcelSheetData AssignmentSheet(params string[] columns)
{
    var table = new DataTable("FacultyMemberSubjects"); foreach(var column in columns) table.Columns.Add(column);
    table.Rows.Add(columns.Select(c => c switch { "FacultyMemberID" => "1", "SubjectID" => "1", "LectureType" => "Practical", _ => "" }).ToArray());
    return new("FacultyMemberSubjects",table,[2]);
}
var import = new ExcelImportService();
var omitted = await import.ValidateAsync(ExcelImportMode.AcademicData,[AssignmentSheet("FacultyMemberID","SubjectID")],true);
Check(omitted.IsValid && omitted.Changes.All(c => c.Action == "Unchanged"), "Old Excel imports preserve the existing lecture type when the column is omitted");
var invalid = await import.ValidateAsync(ExcelImportMode.AcademicData,[AssignmentSheet("FacultyMemberID","SubjectID","LectureType")],true);
Check(!invalid.IsValid, "Excel preview rejects component changes that exclude saved lectures");
var alternatives = await new TimetableAlternativesService().GenerateAsync(new(1,1,5));
Check(alternatives.Alternatives.Count > 0 && alternatives.Alternatives.All(a => a.Lectures.All(l => l.Faculty == (l.LectureType=="Theory" ? "Teacher 1" : "Teacher 2"))), "Alternative plans retain theory/practical teacher assignments");

var backups = new TimetableBackupService();
string backup = Path.Combine(Path.GetTempPath(),database+".uttbackup.json");
await backups.SaveAsync(backup);
Check((await backups.PreviewRestoreAsync(backup)).CanRestore, "Backup round-trips restricted assignment types");
await using (var db = new AppDbContext()) await db.FacultyMemberSubjects.ExecuteUpdateAsync(a => a.SetProperty(x => x.LectureType,"Both"));
await backups.SaveAsync(backup);
var node = JsonNode.Parse(await File.ReadAllTextAsync(backup))!;
foreach(var reference in node["Payload"]!["References"]!.AsArray().Where(r => r!["Table"]!.GetValue<string>()=="FacultyMemberSubjects"))
{
    var values = JsonNode.Parse(reference!["Values"]!.GetValue<string>())!; values.AsObject().Remove("LectureType");
    reference["Values"] = values.ToJsonString();
}
var payloadType = typeof(TimetableBackupService).Assembly.GetType(typeof(TimetableBackupService).Namespace+".TimetableBackupPayload")!;
var payload = JsonSerializer.Deserialize(node["Payload"]!.ToJsonString(),payloadType)!;
node["Checksum"]=(string)typeof(TimetableBackupService).GetMethod("Checksum",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[payload])!;
await File.WriteAllTextAsync(backup,node.ToJsonString());
Check((await backups.PreviewRestoreAsync(backup)).CanRestore, "Legacy backups without component restore only for unrestricted assignments");
await assignmentService.UpdateAsync(1,1,new() { FacultyMemberID=1,SubjectID=1,LectureType="Theory" });
await assignmentService.UpdateAsync(2,1,new() { FacultyMemberID=2,SubjectID=1,LectureType="Practical" });
Check(!(await backups.PreviewRestoreAsync(backup)).CanRestore, "Legacy backup cannot bypass newly adopted component restrictions");

await using(var db=new AppDbContext()) await db.Schedules.ExecuteDeleteAsync();
await assignmentService.DeleteAsync(2,1);
await Reject(() => schedules.GenerateAsync(), "Missing practical assignment blocks generation instead of using the theory teacher");
Check((await Rows()).Count==0,"Missing coverage does not save a partial timetable");
await assignmentService.AddAsync(new() { FacultyMemberID=2,SubjectID=1,LectureType="Practical" });
var timetable=new DataTable("Timetable");
foreach(string column in ExcelImportSchema.Timetable.Required) timetable.Columns.Add(column);
timetable.Rows.Add(ExcelImportSchema.Timetable.Required.Select(c=>c switch {
    "Day"=>"Sunday","TimeSlot"=>"#1","Subject"=>"#1","Faculty"=>"#1","Classroom"=>"#1","Section"=>"#1","LectureType"=>"Theory","SemesterNumber"=>"1",_=>""
}).ToArray());
var hybrid=await import.ValidateAsync(ExcelImportMode.Hybrid,[new ExcelSheetData("Timetable",timetable,[2])],semester:1);
var completed=await import.GenerateRemainingAsync(hybrid);
Check(completed.IsValid && completed.GenerationCompleted && completed.Lectures.Count==2 && completed.Lectures.All(l=>l.Schedule.FacultyMemberID==(l.Schedule.LectureType=="Theory"?1:2)),"Hybrid import generation honors assignment components");
await import.SaveAsync(completed,true);
await schedules.RegenerateAsync(Path.Combine(Path.GetTempPath(),database+"-regenerate.uttbackup.json"));
Check((await Rows()).All(s=>s.FacultyMemberID==(s.LectureType=="Theory"?1:2)),"Full regeneration preserves assignment components");
Console.WriteLine($"PASS {checks} checks; isolated database retained for inspection: {database}");
