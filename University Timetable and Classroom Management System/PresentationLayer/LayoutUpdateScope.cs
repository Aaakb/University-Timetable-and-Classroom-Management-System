namespace University_Timetable_and_Classroom_Management_System;

// Keep related size and text changes in one synchronous layout pass. Never
// retain this scope across an await: visible pages must remain responsive.
internal sealed class LayoutUpdateScope : IDisposable
{
    private readonly List<Control> containers = [];
    private bool disposed;

    public LayoutUpdateScope(Control root)
    {
        Collect(root);
        foreach (var container in containers) container.SuspendLayout();
    }

    private void Collect(Control control)
    {
        containers.Add(control);
        foreach (Control child in control.Controls)
            if (child.HasChildren) Collect(child);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        for (int i = containers.Count - 1; i >= 0; i--)
            if (!containers[i].IsDisposed && !containers[i].Disposing)
                containers[i].ResumeLayout(true);
    }
}
