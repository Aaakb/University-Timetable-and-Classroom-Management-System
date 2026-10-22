namespace University_Timetable_and_Classroom_Management_System
{
    internal static class WindowSizing
    {
        public static int Pixels(Control control, int logicalPixels) => (int)Math.Round(logicalPixels * control.DeviceDpi / 96.0);
        public static void Attach(Form form)
        {
            string? monitor = null;
            form.Shown += (_, _) =>
            {
                monitor = Screen.FromControl(form).DeviceName;
                FitToWorkingArea(form);
            };
            form.LocationChanged += (_, _) =>
            {
                if (monitor is null || form.IsDisposed || !form.IsHandleCreated) return;
                string nextMonitor = Screen.FromControl(form).DeviceName;
                if (monitor == nextMonitor) return;
                monitor = nextMonitor;
                FitToWorkingArea(form);
            };
            form.DpiChanged += (_, _) =>
            {
                if (!form.IsDisposed && form.IsHandleCreated)
                    form.BeginInvoke((Action)(() => FitToWorkingArea(form)));
            };
        }

        private static void FitToWorkingArea(Form form)
        {
            if (form.IsDisposed || form.Disposing || form.WindowState != FormWindowState.Normal) return;
            var area = Screen.FromControl(form).WorkingArea;
            form.MinimumSize = new Size(Math.Min(form.MinimumSize.Width, area.Width), Math.Min(form.MinimumSize.Height, area.Height));
            var size = new Size(Math.Min(form.Width, area.Width), Math.Min(form.Height, area.Height));
            form.Bounds = new Rectangle(
                Math.Clamp(form.Left, area.Left, area.Right - size.Width),
                Math.Clamp(form.Top, area.Top, area.Bottom - size.Height), size.Width, size.Height);
        }

        public static void CenterScrollableCard(ScrollableControl viewport, Control card)
        {
            int margin = Pixels(viewport, 24);
            viewport.AutoScrollMinSize = new Size(card.Width + margin * 2, card.Height + margin * 2);
            var scroll = viewport.AutoScrollPosition;
            card.Location = new Point(scroll.X + Math.Max(margin, (viewport.ClientSize.Width - card.Width) / 2),
                scroll.Y + Math.Max(margin, (viewport.ClientSize.Height - card.Height) / 2));
        }
    }
}
