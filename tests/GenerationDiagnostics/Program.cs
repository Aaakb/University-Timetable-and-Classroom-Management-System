using System.Text.Json;
using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;

// Copy source tables into a new local diagnostic database. Generation is never
// invoked against the source, and this utility never updates source data.
System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(Console.Out));
var source = new SqlConnectionStringBuilder(DatabaseConnection.ConnectionString);
bool reuse = args.Length == 2 && args[0] == "--reuse";
string database = reuse ? args[1] : "UniversityTimetable_Diagnosis_" + Guid.NewGuid().ToString("N");
if (!System.Text.RegularExpressions.Regex.IsMatch(database, "^UniversityTimetable_Diagnosis_[a-f0-9]{32}$")) throw new ArgumentException("Only isolated diagnostic databases are allowed.");
if (!reuse)
{
await using (var sql = new SqlConnection(source.ConnectionString))
{
    await sql.OpenAsync();
    await using var create = sql.CreateCommand();
    create.CommandText = $"CREATE DATABASE [{database}]";
    await create.ExecuteNonQueryAsync();
    await using var copyTx = (SqlTransaction)await sql.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
    foreach (string table in new[] { "StudyYears", "Branches", "FacultyMembers", "Classrooms", "TimeSlots", "Sections", "Subjects", "FacultyMemberSubjects", "Schedules", "FacultySchedulingPreferences", "FacultyPeriodPreferences" })
    {
        await using var copy = sql.CreateCommand();
        copy.Transaction = copyTx;
        copy.CommandText = $"SELECT * INTO [{database}].[dbo].[{table}] FROM [dbo].[{table}]";
        await copy.ExecuteNonQueryAsync();
    }
    await copyTx.CommitAsync();
}
}
source.InitialCatalog = database;
Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, source.ConnectionString);
Console.WriteLine("ISOLATED COPY " + database);
await using (var check = new AppDbContext())
{
    if (check.Database.GetDbConnection().Database != database) throw new InvalidOperationException("Diagnostic isolation failed.");
    // SELECT INTO copies rows and identity, but not SQL default constraints.
    await check.Database.ExecuteSqlRawAsync("""
        IF NOT EXISTS (SELECT 1 FROM sys.default_constraints dc JOIN sys.columns c
            ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id
            WHERE dc.parent_object_id=OBJECT_ID(N'dbo.Schedules') AND c.name=N'IsLocked')
            ALTER TABLE dbo.Schedules ADD DEFAULT(0) FOR IsLocked;
        """);
}
try
{
    string folder = Path.GetFullPath("output/generation-diagnostics");
    Directory.CreateDirectory(folder);
    var result = await new ScheduleService().RegenerateAsync(Path.Combine(folder, database + "-" + Guid.NewGuid().ToString("N") + ".uttbackup.json"));
    Console.WriteLine(JsonSerializer.Serialize(result));
    var rows = await new ScheduleService().GetAllAsync();
    var assembly = typeof(ScheduleService).Assembly;
    bool conflicts = (bool)assembly.GetType("University_Timetable_and_Classroom_Management_System.BusinessLayer.ScheduleConflictChecker")!
        .GetMethod("HasAnyPairConflict")!.Invoke(null, [rows])!;
    var summary = new { Database = database, Result = result, Conflicts = conflicts,
        MaxSectionDays = rows.GroupBy(s => (s.SemesterNumber, s.SectionID)).Max(g => g.Select(s => s.DayOfWeek).Distinct().Count()),
        Semesters = rows.GroupBy(s => s.SemesterNumber).Select(g => new { Semester = g.Key, Lectures = g.Count() }).ToArray() };
    await File.WriteAllTextAsync(Path.Combine(folder, database + ".results.json"), JsonSerializer.Serialize(summary));
    Console.WriteLine(JsonSerializer.Serialize(summary));
    if (conflicts || summary.MaxSectionDays > 4) throw new Exception("Generated diagnostic plan failed validation.");
}
catch (Exception error) { Console.WriteLine(error.ToString()); Environment.ExitCode = 1; }
