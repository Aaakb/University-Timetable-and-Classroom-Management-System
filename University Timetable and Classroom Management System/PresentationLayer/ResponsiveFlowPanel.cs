namespace University_Timetable_and_Classroom_Management_System
{
    // TableLayoutPanel can measure an AutoSize row with an unconstrained width.
    // Measure wrapping against the allocated column, including during design.
    public class ResponsiveFlowPanel : FlowLayoutPanel
    {
        public override Size GetPreferredSize(Size proposedSize)
        {
            if (Parent is not null)
            {
                int available = Math.Max(1, Parent.ClientSize.Width - Parent.Padding.Horizontal - Margin.Horizontal);
                proposedSize.Width = proposedSize.Width <= 1 ? available : Math.Min(proposedSize.Width, available);
            }
            return base.GetPreferredSize(proposedSize);
        }

    }
}
