using ClosedXML.Excel;
using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

var original = Environment.GetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName);
var name = "UniversityTimetable_SubjectListTest_" + Guid.NewGuid().ToString("N");
var connection = new SqlConnectionStringBuilder(DatabaseConnection.ConnectionString) { InitialCatalog = name };
var workbook = Path.Combine(Path.GetTempPath(), name + ".xlsx");
Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, connection.ConnectionString);
var service = new SubjectService();

void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine("PASS " + message);
}
void WriteWorkbook(params (string Subject, string Year)[] rows)
{
    using var document = new XLWorkbook();
    var sheet = document.AddWorksheet("المواد");
    sheet.Cell(1, 1).Value = "المادة";
    sheet.Cell(1, 2).Value = "المرحلة";
    for (int i = 0; i < rows.Length; i++)
    {
        sheet.Cell(i + 2, 1).Value = rows[i].Subject;
        sheet.Cell(i + 2, 2).Value = rows[i].Year;
    }
    document.SaveAs(workbook);
}
Task Import() => service.ImportListAsync(workbook);

try
{
    await using (var db = new AppDbContext())
    {
        if (db.Database.GetDbConnection().Database != name) throw new Exception("Test database isolation failed.");
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.Subjects ADD CONSTRAINT CK_Subjects_Semester CHECK (SemesterNumber IN (1, 2));");
        db.StudyYears.AddRange(new StudyYear { StudyYearID = 2, YearName = "Second Year" }, new StudyYear { StudyYearID = 3, YearName = "Third Year" });
        db.Branches.Add(new Branch { BranchID = 1, BranchName = "CS" });
        db.FacultyMembers.Add(new FacultyMember { FacultyMemberID = 1, FullName = "Test Faculty" });
        await db.SaveChangesAsync();
    }
    var existing = await service.AddAsync(new Subject { SubjectName = "Existing", StudyYearID = 2, SemesterNumber = 1 });
    WriteWorkbook(("Databases", "المرحلة الثالثة"), ("Programming", "٢"), ("Databases", "3"), ("Existing", "Second Year"));
    await Import();
    await Import();
    var subjects = await new SubjectService().GetAllAsync();
    Check(subjects.Count == 3, "Existing subjects and repeated workbook rows do not create duplicates");
    var draft = subjects.Single(s => s.SubjectName == "Databases");
    Check(draft.StudyYearID == 3 && draft.SemesterNumber == 0 && draft.BranchID is null && !draft.HasWeeklyLectures,
        "Imported row retains its stage and persists without a semester, branch or scheduled lectures");
    try
    {
        await new FacultyMemberSubjectService().AddAsync(new FacultyMemberSubject { FacultyMemberID = 1, SubjectID = draft.SubjectID });
        throw new Exception("Pending subject accepted an assignment.");
    }
    catch (ArgumentException ex) when (ex.Message.Contains("استكمل")) { Console.WriteLine("PASS Pending subjects cannot receive assignments"); }
    draft.StudyYear = null!;
    draft.BranchID = 1;
    draft.SemesterNumber = 2;
    draft.TheoreticalLectureCount = 1;
    await service.UpdateAsync(draft);
    var completed = (await service.GetByIdAsync(draft.SubjectID))!;
    Check(completed.SemesterNumber == 2 && completed.BranchID == 1 && completed.HasWeeklyLectures,
        "Completing the subject retains its identity and makes it schedulable");
    WriteWorkbook(("Would be valid", "2"), ("Bad row", "Unknown"));
    try { await Import(); throw new Exception("Invalid year was accepted."); }
    catch (ArgumentException ex) when (ex.Message.Contains("3")) { Console.WriteLine("PASS Import reports the invalid Excel row"); }
    Check((await service.GetAllAsync()).Count == 3 && (await service.GetByIdAsync(existing.SubjectID))!.SemesterNumber == 1,
        "Invalid workbook inserts nothing and leaves existing subjects intact");
}
finally
{
    if (File.Exists(workbook)) File.Delete(workbook);
    await using var db = new AppDbContext();
    if (db.Database.GetDbConnection().Database != name || !name.StartsWith("UniversityTimetable_SubjectListTest_", StringComparison.Ordinal))
        throw new Exception("Unsafe database cleanup.");
    await db.Database.EnsureDeletedAsync();
    Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, original);
}
