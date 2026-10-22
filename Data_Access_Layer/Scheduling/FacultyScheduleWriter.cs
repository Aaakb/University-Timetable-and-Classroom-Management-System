using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.Models;

namespace Data_Access_Layer;

public static class FacultyScheduleWriter
{
    // The caller owns a Serializable transaction and has validated the complete
    // final timetable. Reinsert ONLY changed IDs to permit cyclic slot swaps
    // under SQL Server's unique indexes, preserving all lecture identifiers.
    public static async Task ReplacePlacementsAsync(AppDbContext context, IReadOnlyList<Schedule> changed,
        CancellationToken token = default)
    {
        if (context.Database.CurrentTransaction is null) throw new InvalidOperationException("A transaction is required.");
        if (changed.Count == 0) return;
        var ids = changed.Select(s => s.ScheduleID).ToArray();
        int removed = await context.Schedules.Where(s => ids.Contains(s.ScheduleID)).ExecuteDeleteAsync(token);
        if (removed != ids.Length) throw new InvalidOperationException("Timetable changed during saving.");
        bool identity = context.Database.IsSqlServer() && await context.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS [Value] FROM sys.identity_columns WHERE object_id=OBJECT_ID(N'Schedules')").SingleAsync(token) > 0;
        bool enabled = false;
        try
        {
            if (identity)
            {
                await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Schedules] ON", token);
                enabled = true;
            }
            await context.Schedules.AddRangeAsync(changed, token);
            await context.SaveChangesAsync(token);
            if (enabled)
            {
                await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Schedules] OFF", CancellationToken.None);
                enabled = false;
            }
        }
        catch
        {
            if (enabled)
            {
                try { await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Schedules] OFF", CancellationToken.None); }
                catch { /* Preserve the original error; caller rolls back and disposes the connection. */ }
            }
            throw;
        }
    }
}
