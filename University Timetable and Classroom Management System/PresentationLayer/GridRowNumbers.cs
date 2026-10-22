using System.Runtime.CompilerServices;

namespace University_Timetable_and_Classroom_Management_System
{
    /// <summary>
    /// Numbers the visible rows independently of the entity's permanent database key.
    /// The unbound number column itself is created by the Windows Forms Designer.
    /// </summary>
    internal static class GridRowNumbers
    {
        private static readonly ConditionalWeakTable<DataGridView, Registration> Registrations = new();

        public static void Attach(DataGridView grid, DataGridViewColumn numberColumn)
        {
            if (numberColumn.DataGridView != grid || !string.IsNullOrEmpty(numberColumn.DataPropertyName))
            {
                throw new ArgumentException("Row numbers require an unbound column belonging to the grid.");
            }

            Registrations.GetValue(grid, _ => new Registration(grid, numberColumn));
        }

        private sealed class Registration
        {
            private readonly DataGridView grid;
            private readonly DataGridViewColumn numberColumn;

            public Registration(DataGridView grid, DataGridViewColumn numberColumn)
            {
                this.grid = grid;
                this.numberColumn = numberColumn;
                grid.DataBindingComplete += OnDataBindingComplete;
                grid.Sorted += OnSorted;
                grid.RowsAdded += OnRowsAdded;
                grid.RowsRemoved += OnRowsRemoved;
                grid.RowStateChanged += OnRowStateChanged;
                grid.Disposed += OnDisposed;
                RefreshNumbers();
            }

            private void RefreshNumbers()
            {
                if (grid.IsDisposed || grid.Disposing || numberColumn.Index < 0) return;

                int number = 1;
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (row.IsNewRow) continue;
                    object? value = row.Visible ? number++ : null;
                    var cell = row.Cells[numberColumn.Index];
                    if (!Equals(cell.Value, value)) cell.Value = value;
                }
            }

            private void OnDataBindingComplete(object? sender, DataGridViewBindingCompleteEventArgs e) => RefreshNumbers();
            private void OnSorted(object? sender, EventArgs e) => RefreshNumbers();
            private void OnRowsAdded(object? sender, DataGridViewRowsAddedEventArgs e)
            {
                // Bound rows are numbered once binding completes, rather than after every insertion.
                if (grid.DataSource is null) RefreshNumbers();
            }
            private void OnRowsRemoved(object? sender, DataGridViewRowsRemovedEventArgs e)
            {
                if (grid.DataSource is null) RefreshNumbers();
            }
            private void OnRowStateChanged(object? sender, DataGridViewRowStateChangedEventArgs e)
            {
                if ((e.StateChanged & DataGridViewElementStates.Visible) != 0) RefreshNumbers();
            }
            private void OnDisposed(object? sender, EventArgs e)
            {
                grid.DataBindingComplete -= OnDataBindingComplete;
                grid.Sorted -= OnSorted;
                grid.RowsAdded -= OnRowsAdded;
                grid.RowsRemoved -= OnRowsRemoved;
                grid.RowStateChanged -= OnRowStateChanged;
                grid.Disposed -= OnDisposed;
                Registrations.Remove(grid);
            }
        }
    }
}
