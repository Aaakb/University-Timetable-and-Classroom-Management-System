namespace University_Timetable_and_Classroom_Management_System;

internal static class EditorAction
{
    public static async Task RunAsync(
        Control owner,
        Func<Task> action,
        Func<Task> reload,
        Action<bool> setActionsEnabled,
        Action showSuccess,
        Action<Exception> showError)
    {
        if (owner.IsDisposed || owner.Disposing) return;
        setActionsEnabled(false);

        try
        {
            await action();
            // A pending database operation can finish after navigation disposed its page.
            if (owner.IsDisposed || owner.Disposing) return;
            await reload();
            if (owner.IsDisposed || owner.Disposing) return;
            showSuccess();
        }
        catch (Exception ex)
        {
            showError(ex);
        }
        finally
        {
            setActionsEnabled(true);
        }
    }
}
