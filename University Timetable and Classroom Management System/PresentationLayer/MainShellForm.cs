namespace University_Timetable_and_Classroom_Management_System
{
    public partial class MainShellForm : System.Windows.Forms.Form
    {
        private System.Windows.Forms.UserControl? activePage;
        private bool resizingPage;
        private bool closingEmbeddedPage;
        private System.Windows.Forms.Form? embeddedPage;
        private NavigationPage currentPage;
        private readonly AcademicSemesterSelection semesterSelection = new();

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
            foreach (var button in layoutNavigation.Controls.OfType<Guna.UI2.WinForms.Guna2Button>())
            {
                button.BorderThickness = 1;
                button.BorderColor = Color.Transparent;
                button.HoverState.FillColor = DesktopPalette.Primary;
                button.HoverState.ForeColor = Color.White;
                button.CheckedState.FillColor = DesktopPalette.Primary;
                button.CheckedState.ForeColor = Color.White;
                button.CheckedState.BorderColor = DesktopPalette.OnDarkMuted;
                button.DisabledState.FillColor = DesktopPalette.Primary;
                button.DisabledState.BorderColor = DesktopPalette.OnDarkMuted;
            }
            Current = this;
            WindowSizing.Attach(this);
            WireFacultyScheduleEditorButton();
            WireTimetableAlternativesButton();
            pnlPageHost.Resize += (_, _) => ResizeActivePage();
            ShowPage(NavigationPage.Branches);
        }

        private void WireFacultyScheduleEditorButton()
        {
            btnFacultyScheduleEditor.Click += (_, _) => ShowEmbeddedPage(
                () => new FacultyPreferencesForm(), btnFacultyScheduleEditor);
        }

        private void WireTimetableAlternativesButton()
        {
            btnTimetableAlternatives.Click += (_, _) => ShowEmbeddedPage(
                () => new TimetableAlternativesForm(), btnTimetableAlternatives);
        }

        internal void ShowPage(NavigationPage page)
        {
            if (!TryCloseEmbeddedPage()) return;
            currentPage = page;
            var nextPage = FormNavigation.CreatePage(page, semesterSelection);
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
            ResetEmbeddedButtons();
            FormNavigation.ConfigureShellSidebar(this, pnlSidebar, page);
            previousPage?.Dispose();
        }

        private bool TryCloseEmbeddedPage()
        {
            var page = embeddedPage;
            if (page is null || page.IsDisposed)
            {
                embeddedPage = null;
                return true;
            }

            // Close runs each form's own busy/cancellation and unsaved-edit
            // confirmation. A canceled close must leave the page in place.
            closingEmbeddedPage = true;
            try { page.Close(); }
            finally { closingEmbeddedPage = false; }
            if (!page.IsDisposed) return false;
            embeddedPage = null;
            return true;
        }

        private void ShowEmbeddedPage(Func<System.Windows.Forms.Form> create, Guna.UI2.WinForms.Guna2Button button)
        {
            if (embeddedPage is not null && button.Checked) return;
            if (!TryCloseEmbeddedPage()) return;

            var dialog = create();
            var host = new System.Windows.Forms.UserControl
            {
                MinimumSize = dialog.MinimumSize,
                Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left,
                Margin = System.Windows.Forms.Padding.Empty,
                Visible = false
            };
            dialog.TopLevel = false;
            dialog.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            dialog.ShowInTaskbar = false;
            dialog.Dock = System.Windows.Forms.DockStyle.Fill;
            host.Controls.Add(dialog);
            dialog.FormClosed += (_, _) =>
            {
                if (closingEmbeddedPage || IsDisposed || Disposing || !ReferenceEquals(activePage, host)) return;
                embeddedPage = null;
                // The close button is raised from within the hosted form. Switch
                // pages after it finishes closing to avoid disposing its host mid-event.
                if (IsHandleCreated)
                    BeginInvoke((Action)(() => { if (!IsDisposed && !Disposing && ReferenceEquals(activePage, host)) ShowPage(currentPage); }));
            };

            var previousPage = activePage;
            pnlPageHost.SuspendLayout();
            try
            {
                pnlPageHost.AutoScrollPosition = System.Drawing.Point.Empty;
                activePage = host;
                embeddedPage = dialog;
                ResizeActivePage();
                pnlPageHost.Controls.Add(host);
                host.BringToFront();
            }
            finally { pnlPageHost.ResumeLayout(true); }
            host.PerformLayout();
            host.Visible = true;
            // These forms load their data from Shown; adding a control alone
            // does not reliably raise that event.
            dialog.Show();
            ResetEmbeddedButtons();
            FormNavigation.ConfigureShellSidebar(this, pnlSidebar, (NavigationPage)(-1));
            button.Checked = true;
            previousPage?.Dispose();
        }

        private void ResetEmbeddedButtons()
        {
            btnFacultyScheduleEditor.Checked = false;
            btnTimetableAlternatives.Checked = false;
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

        protected override void OnFormClosing(System.Windows.Forms.FormClosingEventArgs e)
        {
            if (!TryCloseEmbeddedPage()) e.Cancel = true;
            base.OnFormClosing(e);
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
