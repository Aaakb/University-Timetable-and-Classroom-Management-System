namespace University_Timetable_and_Classroom_Management_System
{
    // TableLayoutPanel can measure an AutoSize row with an unconstrained width.
    // Measure wrapping against the allocated column, including during design.
    public class ResponsiveFlowPanel : FlowLayoutPanel
    {
        public override Size GetPreferredSize(Size proposedSize)
        {
            // During a resize the parent can still have its previous, narrower
            // bounds. A finite proposed width is the new allocation: clamping
            // it to those stale bounds leaves the AutoSize row one pass behind.
            if (Parent is not null && (proposedSize.Width <= 1 || proposedSize.Width >= 65536))
            {
                int available = Math.Max(1, Parent.ClientSize.Width - Parent.Padding.Horizontal - Margin.Horizontal);
                proposedSize.Width = available;
            }
            return base.GetPreferredSize(proposedSize);
        }

    }
}
