namespace University_Timetable_and_Classroom_Management_System
{
    // A top-docked, AutoSize table grows for wrapped editors, while its percent
    // results row fills spare height. The enclosing panel owns scrolling.
    public class ResponsiveWorkspacePanel : TableLayoutPanel
    {
        private Control? viewport;
        protected override void OnParentChanged(EventArgs e)
        {
            if (viewport is not null) viewport.ClientSizeChanged -= ParentSizeChanged;
            base.OnParentChanged(e);
            viewport = Parent;
            if (viewport is not null) viewport.ClientSizeChanged += ParentSizeChanged;
        }

        private void ParentSizeChanged(object? sender, EventArgs e)
        {
            if (IsDisposed || Disposing || Parent is null || DesignerEnvironment.IsDesignTime(this)) return;
            MinimumSize = new Size(MinimumSize.Width, Math.Max(0, Parent.ClientSize.Height - Parent.Padding.Vertical));
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            ParentSizeChanged(null, EventArgs.Empty);
            base.OnLayout(levent);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (!IsDisposed && !Disposing && !DesignerEnvironment.IsDesignTime(this) && Parent is ScrollableControl { AutoScroll: true } scroll)
                scroll.AutoScrollMinSize = new Size(MinimumSize.Width + scroll.Padding.Horizontal, Height + scroll.Padding.Vertical);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && viewport is not null) viewport.ClientSizeChanged -= ParentSizeChanged;
            base.Dispose(disposing);
        }
    }
}
