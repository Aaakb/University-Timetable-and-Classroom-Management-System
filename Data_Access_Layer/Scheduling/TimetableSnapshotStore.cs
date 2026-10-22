using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.Models;

namespace Data_Access_Layer;

public static class TimetableSnapshotStore
{
    public static async Task ReplaceAsync(AppDbContext context, IReadOnlyList<Schedule> rows, CancellationToken token)
    {
        if (context.Database.CurrentTransaction is null) throw new InvalidOperationException("A restore transaction is required.");
        await context.Schedules.ExecuteDeleteAsync(token);
        if (rows.Count == 0) return;
        bool identity = context.Database.IsSqlServer() && await context.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS [Value] FROM sys.identity_columns WHERE object_id=OBJECT_ID(N'Schedules')").SingleAsync(token) > 0;
        bool enabled = false;
        try
        {
            if (identity) { await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Schedules] ON",token); enabled = true; }
            await context.Schedules.AddRangeAsync(rows,token);
            await context.SaveChangesAsync(token);
            if (enabled) { await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Schedules] OFF",CancellationToken.None); enabled = false; }
        }
        catch
        {
            if (enabled) { try { await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Schedules] OFF",CancellationToken.None); } catch { /* Preserve original failure; caller rolls back. */ } }
            throw;
        }
    }
}
