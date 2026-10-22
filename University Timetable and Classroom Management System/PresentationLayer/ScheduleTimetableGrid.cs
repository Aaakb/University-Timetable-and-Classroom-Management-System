namespace University_Timetable_and_Classroom_Management_System
{
    // A normal designer-editable DataGridView; only data-cell scrolling differs.
    public class ScheduleTimetableGrid : DataGridView
    {
        internal Func<MouseEventArgs, bool>? ScrollEntryCell { get; set; }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (ScrollEntryCell?.Invoke(e) == true) return;
            base.OnMouseWheel(e);
        }
    }
}
