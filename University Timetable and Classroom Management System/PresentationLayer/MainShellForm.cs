namespace University_Timetable_and_Classroom_Management_System
{
    public partial class MainShellForm : System.Windows.Forms.Form
    {
        private System.Windows.Forms.UserControl? activePage;

        internal static MainShellForm? Current { get; private set; }

        public MainShellForm()
        {
            InitializeComponent();
            Current = this;
            ShowPage(NavigationPage.Branches);
        }

        internal void ShowPage(NavigationPage page)
        {
            var nextPage = FormNavigation.CreatePage(page);
            nextPage.Dock = System.Windows.Forms.DockStyle.Fill;

            pnlPageHost.SuspendLayout();
            pnlPageHost.Controls.Clear();

            activePage?.Dispose();
            activePage = nextPage;

            pnlPageHost.Controls.Add(activePage);
            pnlPageHost.ResumeLayout();

            FormNavigation.ConfigureShellSidebar(this, pnlSidebar, page);
        }

        protected override void OnFormClosed(System.Windows.Forms.FormClosedEventArgs e)
        {
            if (ReferenceEquals(Current, this))
            {
                Current = null;
            }

            base.OnFormClosed(e);
        }
        private Guna.UI2.WinForms.Guna2Panel pnlSidebar = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlPageHost = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblSidebarFooter = null!;
        private Guna.UI2.WinForms.Guna2Separator separatorSidebar = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblSidebarSubtitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblApplicationName = null!;
        private PictureBox picSidebarLogo = null!;
        private void InitializeComponent()
        {
            pnlSidebar = new Guna.UI2.WinForms.Guna2Panel();
            pnlPageHost = new Guna.UI2.WinForms.Guna2Panel();
            lblSidebarFooter = new Guna.UI2.WinForms.Guna2HtmlLabel();
            separatorSidebar = new Guna.UI2.WinForms.Guna2Separator();
            lblSidebarSubtitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblApplicationName = new Guna.UI2.WinForms.Guna2HtmlLabel();
            picSidebarLogo = new PictureBox();
            pnlSidebar.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)picSidebarLogo).BeginInit();
            SuspendLayout();
            pnlSidebar.BackColor = System.Drawing.Color.Transparent;
            pnlSidebar.Controls.Add(lblSidebarFooter);
            pnlSidebar.Controls.Add(separatorSidebar);
            pnlSidebar.Controls.Add(lblSidebarSubtitle);
            pnlSidebar.Controls.Add(lblApplicationName);
            pnlSidebar.Controls.Add(picSidebarLogo);
            pnlSidebar.Dock = System.Windows.Forms.DockStyle.Left;
            pnlSidebar.FillColor = System.Drawing.Color.FromArgb(24, 38, 62);
            pnlSidebar.Location = new System.Drawing.Point(0, 0);
            pnlSidebar.Name = "pnlSidebar";
            pnlSidebar.Size = new System.Drawing.Size(240, 800);
            pnlSidebar.TabIndex = 0;
            lblSidebarFooter.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            lblSidebarFooter.BackColor = System.Drawing.Color.Transparent;
            lblSidebarFooter.Font = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblSidebarFooter.ForeColor = System.Drawing.Color.FromArgb(148, 163, 184);
            lblSidebarFooter.Location = new System.Drawing.Point(24, 751);
            lblSidebarFooter.Name = "lblSidebarFooter";
            lblSidebarFooter.Size = new System.Drawing.Size(104, 17);
            lblSidebarFooter.TabIndex = 3;
            lblSidebarFooter.Text = "Academic system";
            lblSidebarFooter.Visible = false;
            separatorSidebar.FillColor = System.Drawing.Color.FromArgb(51, 65, 85);
            separatorSidebar.Location = new System.Drawing.Point(24, 86);
            separatorSidebar.Name = "separatorSidebar";
            separatorSidebar.Size = new System.Drawing.Size(192, 10);
            separatorSidebar.TabIndex = 2;
            lblSidebarSubtitle.BackColor = System.Drawing.Color.Transparent;
            lblSidebarSubtitle.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblSidebarSubtitle.ForeColor = System.Drawing.Color.FromArgb(148, 163, 184);
            lblSidebarSubtitle.Location = new System.Drawing.Point(26, 52);
            lblSidebarSubtitle.Name = "lblSidebarSubtitle";
            lblSidebarSubtitle.Size = new System.Drawing.Size(145, 17);
            lblSidebarSubtitle.TabIndex = 1;
            lblSidebarSubtitle.Text = "Classroom management";
            lblSidebarSubtitle.Visible = false;
            lblApplicationName.BackColor = System.Drawing.Color.Transparent;
            lblApplicationName.Font = new System.Drawing.Font("Segoe UI Semibold", 13F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblApplicationName.ForeColor = System.Drawing.Color.White;
            lblApplicationName.Location = new System.Drawing.Point(78, 24);
            lblApplicationName.Name = "lblApplicationName";
            lblApplicationName.Size = new System.Drawing.Size(145, 25);
            lblApplicationName.TabIndex = 0;
            lblApplicationName.Text = "Timetable Studio";
            lblApplicationName.Visible = true;
            picSidebarLogo.BackColor = System.Drawing.Color.Transparent;
            picSidebarLogo.Image = BrandAssets.LoadLogoImage();
            picSidebarLogo.Location = new System.Drawing.Point(20, 18);
            picSidebarLogo.Name = "picSidebarLogo";
            picSidebarLogo.Size = new System.Drawing.Size(48, 48);
            picSidebarLogo.SizeMode = PictureBoxSizeMode.Zoom;
            picSidebarLogo.TabIndex = 4;
            picSidebarLogo.TabStop = false;
            pnlPageHost.BackColor = System.Drawing.Color.FromArgb(245, 247, 251);
            pnlPageHost.Dock = System.Windows.Forms.DockStyle.Fill;
            pnlPageHost.Location = new System.Drawing.Point(240, 0);
            pnlPageHost.Name = "pnlPageHost";
            pnlPageHost.Size = new System.Drawing.Size(1040, 800);
            pnlPageHost.TabIndex = 1;
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(245, 247, 251);
            ClientSize = new System.Drawing.Size(1280, 800);
            Controls.Add(pnlPageHost);
            Controls.Add(pnlSidebar);
            MinimumSize = new System.Drawing.Size(1100, 700);
            Name = "MainShellForm";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Timetable Studio";
            Icon = BrandAssets.LoadIcon();
            pnlSidebar.ResumeLayout(false);
            pnlSidebar.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)picSidebarLogo).EndInit();
            ResumeLayout(false);
        }
    }
}
