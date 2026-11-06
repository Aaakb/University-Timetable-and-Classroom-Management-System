using ClosedXML.Excel;
using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

var prior = Environment.GetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName);
var name = "UniversityTimetable_FacultyImport_" + Guid.NewGuid().ToString("N");
var settings = new SqlConnectionStringBuilder(DatabaseConnection.ConnectionString) { InitialCatalog = name };
Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, settings.ConnectionString);
string csv = Path.Combine(Path.GetTempPath(), name + ".csv");
string xlsx = Path.ChangeExtension(csv, ".xlsx");
var service = new FacultyMemberService();

void Check(bool result, string message)
{
    if (!result) throw new InvalidOperationException(message);
    Console.WriteLine("PASS " + message);
}

try
{
    await using (var db = new AppDbContext())
    {
        Check(db.Database.GetDbConnection().Database == name, "Database isolated");
        await db.Database.EnsureCreatedAsync();
    }
    var existing = await service.AddAsync(new FacultyMember { FullName = "تدريسي موجود", AcademicTitle = "أستاذ" });
    await File.WriteAllTextAsync(csv, "اسم التدريسي,اللقب العلمي\r\nليلى حسن,مدرس\r\nعلي سالم\r\nتدريسي موجود,مدرس\r\n");
    var csvResult = await service.ImportListAsync(csv);
    Check(csvResult.AddedIds.Count == 2 && csvResult.Skipped == 1, "CSV accepts rows with and without a title");
    var rows = await service.GetAllAsync();
    Check(rows.Single(x => x.FullName == "ليلى حسن").AcademicTitle == "مدرس" &&
        rows.Single(x => x.FullName == "علي سالم").AcademicTitle is null, "Optional titles are stored correctly");
    Check((await service.GetByIdAsync(existing.FacultyMemberID))!.AcademicTitle == "أستاذ", "Import preserves an existing faculty record");
    using (var workbook = new XLWorkbook())
    {
        var sheet = workbook.AddWorksheet("التدريسيون");
        sheet.Cell(1, 1).Value = "الاسم الكامل";
        sheet.Cell(2, 1).Value = "فاطمة أحمد";
        workbook.SaveAs(xlsx);
    }
    Check((await service.ImportListAsync(xlsx)).AddedIds.Count == 1, "Excel accepts a single name column");
    Check((await service.ImportListAsync(xlsx)).AddedIds.Count == 0, "Importing the same Excel file again skips the saved name");
    await File.WriteAllTextAsync(csv, "FullName,AcademicTitle\r\nValid,Professor\r\n,Professor\r\n");
    try { await service.ImportListAsync(csv); throw new InvalidOperationException("Accepted a row with a title and no name"); }
    catch (ArgumentException ex) { Check(ex.Message.Contains("3"), "Invalid input identifies the Excel or CSV row"); }
    Check((await service.GetAllAsync()).Count == 4, "An invalid row rolls back the whole import");
}
finally
{
    File.Delete(csv);
    File.Delete(xlsx);
    await using var db = new AppDbContext();
    if (db.Database.GetDbConnection().Database != name || !name.StartsWith("UniversityTimetable_FacultyImport_", StringComparison.Ordinal))
        throw new Exception("Unsafe cleanup");
    await db.Database.EnsureDeletedAsync();
    Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, prior);
}
