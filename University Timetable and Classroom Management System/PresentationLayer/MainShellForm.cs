namespace University_Timetable_and_Classroom_Management_System
{
    public partial class MainShellForm : System.Windows.Forms.Form
    {
        private System.Windows.Forms.UserControl? activePage;
        private bool resizingPage;
        private NavigationPage currentPage;

        internal static MainShellForm? Current { get; private set; }

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                // Buffer the child HWNDs together: Guna controls otherwise expose
                // intermediate parent/child paints while a page is being shown.
                if (!DesignerEnvironment.IsDesignTime(this))
                    parameters.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
                return parameters;
            }
        }

        public MainShellForm()
        {
            InitializeComponent();
            if (DesignerEnvironment.IsDesignTime(this)) return;
            ArabicUi.Apply(this);
            Current = this;
            WindowSizing.Attach(this);
            AddExcelImportButton();
            AddFacultyScheduleEditorButton();
            AddTimetableAlternativesButton();
            pnlPageHost.Resize += (_, _) => ResizeActivePage();
            ShowPage(NavigationPage.Branches);
        }

        private void AddExcelImportButton()
        {
            var button = new Guna.UI2.WinForms.Guna2Button
            {
                Name = "btnExcelImport", Text = "استيراد Excel / PDF", Dock = System.Windows.Forms.DockStyle.Fill,
                Height = btnNavigationSchedules.Height, Margin = btnNavigationSchedules.Margin,
                BorderRadius = 6, FillColor = System.Drawing.Color.Transparent,
                ForeColor = btnNavigationSchedules.ForeColor, Font = btnNavigationSchedules.Font,
                TextAlign = System.Windows.Forms.HorizontalAlignment.Left, TextOffset = new(-14, 0),
                Cursor = System.Windows.Forms.Cursors.Hand
            };
            button.Click += (_, _) =>
            {
                using var import = new ExcelImportForm();
                import.ShowDialog(this);
                // Reload the page lookups after an academic or timetable import.
                if (import.HasSavedChanges) ShowPage(currentPage);
            };
            layoutNavigation.SuspendLayout();
            layoutNavigation.RowCount++;
            layoutNavigation.RowStyles.Add(new(System.Windows.Forms.SizeType.AutoSize));
            layoutNavigation.SetRow(lblSidebarFooter, 12);
            layoutNavigation.Controls.Add(button, 0, 11);
            layoutNavigation.ResumeLayout(true);
        }

        private void AddFacultyScheduleEditorButton()
        {
            var button = new Guna.UI2.WinForms.Guna2Button
            {
                Name = "btnFacultyScheduleEditor", Text = "تفضيلات التدريسيين", Dock = System.Windows.Forms.DockStyle.Fill,
                Height = btnNavigationSchedules.Height, Margin = btnNavigationSchedules.Margin,
                BorderRadius = 6, FillColor = System.Drawing.Color.Transparent,
                ForeColor = btnNavigationSchedules.ForeColor, Font = btnNavigationSchedules.Font,
                TextAlign = System.Windows.Forms.HorizontalAlignment.Left, TextOffset = new(-14, 0),
                Cursor = System.Windows.Forms.Cursors.Hand
            };
            button.Click += (_, _) =>
            {
                using var editor = new FacultyPreferencesForm();
                editor.ShowDialog(this);
                if (editor.HasSavedChanges) ShowPage(currentPage);
            };
            layoutNavigation.SuspendLayout();
            layoutNavigation.RowCount++;
            layoutNavigation.RowStyles.Add(new(System.Windows.Forms.SizeType.AutoSize));
            layoutNavigation.SetRow(lblSidebarFooter, 13);
            layoutNavigation.Controls.Add(button, 0, 12);
            layoutNavigation.ResumeLayout(true);
        }

        private void AddTimetableAlternativesButton()
        {
            var button = new Guna.UI2.WinForms.Guna2Button
            {
                Name = "btnTimetableAlternatives", Text = "جداول بديلة", Dock = System.Windows.Forms.DockStyle.Fill,
                Height = btnNavigationSchedules.Height, Margin = btnNavigationSchedules.Margin, BorderRadius = 6,
                FillColor = System.Drawing.Color.Transparent, ForeColor = btnNavigationSchedules.ForeColor, Font = btnNavigationSchedules.Font,
                TextAlign = System.Windows.Forms.HorizontalAlignment.Left, TextOffset = new(-14, 0), Cursor = System.Windows.Forms.Cursors.Hand
            };
            button.Click += (_, _) => { using var dialog = new TimetableAlternativesForm(); dialog.ShowDialog(this); if (dialog.HasSavedChanges) ShowPage(currentPage); };
            layoutNavigation.RowCount++; layoutNavigation.RowStyles.Add(new(System.Windows.Forms.SizeType.AutoSize));
            layoutNavigation.SetRow(lblSidebarFooter, 14); layoutNavigation.Controls.Add(button, 0, 13);
        }

        internal void ShowPage(NavigationPage page)
        {
            currentPage = page;
            var nextPage = FormNavigation.CreatePage(page);
            nextPage.Dock = System.Windows.Forms.DockStyle.None;
            nextPage.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            nextPage.Margin = System.Windows.Forms.Padding.Empty;
            // Complete the container layout before exposing its child windows.
            nextPage.Visible = false;

            var previousPage = activePage;
            pnlPageHost.SuspendLayout();
            try
            {
                pnlPageHost.AutoScrollPosition = System.Drawing.Point.Empty;
                activePage = nextPage;
                ResizeActivePage();
                pnlPageHost.Controls.Add(nextPage);
                nextPage.BringToFront();
            }
            finally { pnlPageHost.ResumeLayout(true); }

            nextPage.PerformLayout();
            nextPage.Visible = true;
            FormNavigation.ConfigureShellSidebar(this, pnlSidebar, page);
            previousPage?.Dispose();
        }

        private void ResizeActivePage()
        {
            if (resizingPage || IsDisposed || Disposing || activePage is null || activePage.IsDisposed) return;
            resizingPage = true;
            try
            {
                // Keep the designer layout readable; smaller windows scroll the page canvas.
                // Use the full panel bounds here: existing scrollbars must not
                // make a page that fits the viewport appear too large.
                bool needsCanvasScroll = activePage.MinimumSize.Width > pnlPageHost.Width ||
                    activePage.MinimumSize.Height > pnlPageHost.Height;
                pnlPageHost.AutoScrollMinSize = needsCanvasScroll ? activePage.MinimumSize : System.Drawing.Size.Empty;
                pnlPageHost.AutoScroll = needsCanvasScroll;
                var scroll = pnlPageHost.AutoScrollPosition;
                activePage.Bounds = new System.Drawing.Rectangle(scroll.X, scroll.Y,
                    Math.Max(activePage.MinimumSize.Width, pnlPageHost.ClientSize.Width),
                    Math.Max(activePage.MinimumSize.Height, pnlPageHost.ClientSize.Height));
            }
            finally { resizingPage = false; }
        }

        protected override void OnFormClosed(System.Windows.Forms.FormClosedEventArgs e)
        {
            if (ReferenceEquals(Current, this))
            {
                Current = null;
            }

            base.OnFormClosed(e);
        }

    }
}
