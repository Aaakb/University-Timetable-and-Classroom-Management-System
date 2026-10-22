using System.Data;
using Microsoft.EntityFrameworkCore;

namespace Data_Access_Layer;

/// <summary>Owns the single connection and transaction used by a whole workbook.</summary>
public static class ImportTransaction
{
    public static async Task<T> ExecuteAsync<T>(Func<AppDbContext> createContext,
        Func<AppDbContext, CancellationToken, Task<T>> operation, CancellationToken token = default)
    {
        await using var context = createContext();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var result = await operation(context, token);
        token.ThrowIfCancellationRequested();
        await transaction.CommitAsync(token);
        return result;
        // Disposal rolls back every write, including prior sheets, on any failure.
    }
}
