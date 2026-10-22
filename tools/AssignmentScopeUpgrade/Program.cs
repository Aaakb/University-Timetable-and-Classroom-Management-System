using System.Text.Json;
using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

await using var db = new AppDbContext();
Console.WriteLine($"Database: {db.Database.GetDbConnection().DataSource}/{db.Database.GetDbConnection().Database}");
await db.Database.OpenConnectionAsync();
async Task<int> Scalar(string sql)
{
    await using var command = db.Database.GetDbConnection().CreateCommand();
    command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction(); command.CommandText = sql;
    return Convert.ToInt32(await command.ExecuteScalarAsync());
}
int exists = await Scalar("SELECT CASE WHEN COL_LENGTH(N'dbo.FacultyMemberSubjects',N'SectionID') IS NULL THEN 0 ELSE 1 END");
Console.WriteLine($"Assignment scope column exists: {exists == 1}");
if (!args.Contains("--apply")) return;
await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
int count = await Scalar("SELECT COUNT(*) FROM FacultyMemberSubjects");
int keys = await Scalar("SELECT COALESCE(CHECKSUM_AGG(BINARY_CHECKSUM(FacultyMemberID,SubjectID)),0) FROM FacultyMemberSubjects");
string before = JsonSerializer.Serialize(await db.Schedules.AsNoTracking().OrderBy(s => s.ScheduleID).ToListAsync());
await FacultyAssignmentScopeSchema.EnsureAsync(db);
if (count != await db.FacultyMemberSubjects.CountAsync() ||
    keys != await Scalar("SELECT COALESCE(CHECKSUM_AGG(BINARY_CHECKSUM(FacultyMemberID,SubjectID)),0) FROM FacultyMemberSubjects") ||
    (exists == 0 && await db.FacultyMemberSubjects.AnyAsync(a => a.SectionID != null)) ||
    before != JsonSerializer.Serialize(await db.Schedules.AsNoTracking().OrderBy(s => s.ScheduleID).ToListAsync()))
    throw new InvalidOperationException("Upgrade changed existing assignment identities or timetable rows; rolling back.");
await transaction.CommitAsync();
Console.WriteLine($"Verified upgrade: {count} existing assignments preserved; all timetable fields unchanged.");
