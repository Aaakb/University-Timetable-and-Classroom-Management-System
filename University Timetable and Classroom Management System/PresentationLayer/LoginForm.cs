using University_Timetable_and_Classroom_Management_System.BusinessLayer;

namespace University_Timetable_and_Classroom_Management_System
{
    public class LoginForm : Form
    {
        private readonly AuthService authService = new();

        private Guna.UI2.WinForms.Guna2Panel pnlBrand = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlContent = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlCard = null!;
        private PictureBox picBrandLogo = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblBrandTitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblBrandSubtitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblTitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblSubtitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblUserName = null!;
        private Guna.UI2.WinForms.Guna2TextBox txtUserName = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblPassword = null!;
        private Guna.UI2.WinForms.Guna2TextBox txtPassword = null!;
        private Guna.UI2.WinForms.Guna2Button btnTogglePassword = null!;
        private Guna.UI2.WinForms.Guna2Button btnSignIn = null!;
        private Guna.UI2.WinForms.Guna2Button btnCreateAccount = null!;
        private bool passwordVisible;

        public LoginForm()
        {
            InitializeComponent();
            PasswordToggleIcon.Apply(btnTogglePassword, passwordVisible);
            WireEvents();
            AcceptButton = btnSignIn;
            Shown += (_, _) => txtUserName.Focus();
        }

        private void WireEvents()
        {
            btnSignIn.Click += async (_, _) => await SignInAsync();
            btnCreateAccount.Click += (_, _) => OpenRegisterForm();
            btnTogglePassword.Click += (_, _) => TogglePasswordVisibility();
            txtPassword.KeyDown += async (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    await SignInAsync();
                }
            };
        }

        private async Task SignInAsync()
        {
            SetBusy(true);

            try
            {
                var result = await authService.SignInAsync(txtUserName.Text, txtPassword.Text);

                if (!result.Succeeded)
                {
                    UiMessages.ShowWarning(this, result.Message, "Sign In");
                    return;
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                UiMessages.ShowError(this, "Unable to sign in.", "Sign In", ex);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void OpenRegisterForm()
        {
            using var registerForm = new RegisterForm();

            if (registerForm.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            txtUserName.Text = registerForm.CreatedUserName;
            txtPassword.Clear();
            txtPassword.Focus();
        }

        private void SetBusy(bool busy)
        {
            txtUserName.Enabled = !busy;
            txtPassword.Enabled = !busy;
            btnTogglePassword.Enabled = !busy;
            btnCreateAccount.Enabled = !busy;
            btnSignIn.Enabled = !busy;
            btnSignIn.Text = busy ? "Signing in..." : "Sign In";
        }

        private void TogglePasswordVisibility()
        {
            passwordVisible = !passwordVisible;
            txtPassword.PasswordChar = passwordVisible ? '\0' : '*';
            PasswordToggleIcon.Apply(btnTogglePassword, passwordVisible);
        }

        private void InitializeComponent()
        {
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges1 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges2 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges15 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges16 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges13 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges14 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges3 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges4 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges5 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges6 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges7 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges8 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges9 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges10 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges11 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            Guna.UI2.WinForms.Suite.CustomizableEdges customizableEdges12 = new Guna.UI2.WinForms.Suite.CustomizableEdges();
            pnlBrand = new Guna.UI2.WinForms.Guna2Panel();
            picBrandLogo = new PictureBox();
            lblBrandTitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblBrandSubtitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            pnlContent = new Guna.UI2.WinForms.Guna2Panel();
            pnlCard = new Guna.UI2.WinForms.Guna2Panel();
            lblTitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblSubtitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblUserName = new Guna.UI2.WinForms.Guna2HtmlLabel();
            txtUserName = new Guna.UI2.WinForms.Guna2TextBox();
            lblPassword = new Guna.UI2.WinForms.Guna2HtmlLabel();
            txtPassword = new Guna.UI2.WinForms.Guna2TextBox();
            btnTogglePassword = new Guna.UI2.WinForms.Guna2Button();
            btnSignIn = new Guna.UI2.WinForms.Guna2Button();
            btnCreateAccount = new Guna.UI2.WinForms.Guna2Button();
            pnlBrand.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)picBrandLogo).BeginInit();
            pnlContent.SuspendLayout();
            pnlCard.SuspendLayout();
            SuspendLayout();
            // 
            // pnlBrand
            // 
            pnlBrand.Controls.Add(picBrandLogo);
            pnlBrand.Controls.Add(lblBrandTitle);
            pnlBrand.Controls.Add(lblBrandSubtitle);
            pnlBrand.CustomizableEdges = customizableEdges1;
            pnlBrand.Dock = DockStyle.Left;
            pnlBrand.FillColor = Color.FromArgb(24, 38, 62);
            pnlBrand.Location = new Point(0, 0);
            pnlBrand.Name = "pnlBrand";
            pnlBrand.ShadowDecoration.CustomizableEdges = customizableEdges2;
            pnlBrand.Size = new Size(360, 620);
            pnlBrand.TabIndex = 1;
            // 
            // picBrandLogo
            // 
            picBrandLogo.BackColor = Color.Transparent;
            picBrandLogo.Image = BrandAssets.LoadLogoImage();
            picBrandLogo.Location = new Point(56, 42);
            picBrandLogo.Name = "picBrandLogo";
            picBrandLogo.Size = new Size(248, 248);
            picBrandLogo.SizeMode = PictureBoxSizeMode.Zoom;
            picBrandLogo.TabIndex = 2;
            picBrandLogo.TabStop = false;
            // 
            // lblBrandTitle
            // 
            lblBrandTitle.BackColor = Color.Transparent;
            lblBrandTitle.Font = new Font("Segoe UI Semibold", 22F, FontStyle.Bold);
            lblBrandTitle.ForeColor = Color.White;
            lblBrandTitle.Location = new Point(36, 318);
            lblBrandTitle.Name = "lblBrandTitle";
            lblBrandTitle.Size = new Size(226, 42);
            lblBrandTitle.TabIndex = 0;
            lblBrandTitle.Text = "Timetable Studio";
            // 
            // lblBrandSubtitle
            // 
            lblBrandSubtitle.BackColor = Color.Transparent;
            lblBrandSubtitle.Font = new Font("Segoe UI", 10F);
            lblBrandSubtitle.ForeColor = Color.FromArgb(203, 213, 225);
            lblBrandSubtitle.Location = new Point(38, 368);
            lblBrandSubtitle.Name = "lblBrandSubtitle";
            lblBrandSubtitle.Size = new Size(265, 42);
            lblBrandSubtitle.TabIndex = 1;
            lblBrandSubtitle.Text = "Secure access for classroom, subject,<br>faculty, and schedule management.";
            // 
            // pnlContent
            // 
            pnlContent.Controls.Add(pnlCard);
            pnlContent.CustomizableEdges = customizableEdges15;
            pnlContent.Dock = DockStyle.Fill;
            pnlContent.FillColor = Color.FromArgb(245, 247, 250);
            pnlContent.Location = new Point(360, 0);
            pnlContent.Name = "pnlContent";
            pnlContent.ShadowDecoration.CustomizableEdges = customizableEdges16;
            pnlContent.Size = new Size(620, 620);
            pnlContent.TabIndex = 0;
            // 
            // pnlCard
            // 
            pnlCard.Anchor = AnchorStyles.None;
            pnlCard.BorderColor = Color.FromArgb(226, 232, 240);
            pnlCard.BorderRadius = 8;
            pnlCard.BorderThickness = 1;
            pnlCard.Controls.Add(lblTitle);
            pnlCard.Controls.Add(lblSubtitle);
            pnlCard.Controls.Add(lblUserName);
            pnlCard.Controls.Add(txtUserName);
            pnlCard.Controls.Add(lblPassword);
            pnlCard.Controls.Add(txtPassword);
            pnlCard.Controls.Add(btnTogglePassword);
            pnlCard.Controls.Add(btnSignIn);
            pnlCard.Controls.Add(btnCreateAccount);
            pnlCard.CustomizableEdges = customizableEdges13;
            pnlCard.FillColor = Color.White;
            pnlCard.Location = new Point(93, 94);
            pnlCard.Name = "pnlCard";
            pnlCard.ShadowDecoration.CustomizableEdges = customizableEdges14;
            pnlCard.Size = new Size(408, 464);
            pnlCard.TabIndex = 0;
            // 
            // lblTitle
            // 
            lblTitle.BackColor = Color.Transparent;
            lblTitle.Font = new Font("Segoe UI Semibold", 20F, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(15, 23, 42);
            lblTitle.Location = new Point(32, 30);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(88, 39);
            lblTitle.TabIndex = 0;
            lblTitle.Text = "Sign In";
            // 
            // lblSubtitle
            // 
            lblSubtitle.BackColor = Color.Transparent;
            lblSubtitle.Font = new Font("Segoe UI", 9.5F);
            lblSubtitle.ForeColor = Color.FromArgb(100, 116, 139);
            lblSubtitle.Location = new Point(34, 72);
            lblSubtitle.Name = "lblSubtitle";
            lblSubtitle.Size = new Size(226, 19);
            lblSubtitle.TabIndex = 1;
            lblSubtitle.Text = "Enter your account details to continue.";
            // 
            // lblUserName
            // 
            lblUserName.BackColor = Color.Transparent;
            lblUserName.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            lblUserName.ForeColor = Color.FromArgb(51, 65, 85);
            lblUserName.Location = new Point(34, 122);
            lblUserName.Name = "lblUserName";
            lblUserName.Size = new Size(70, 19);
            lblUserName.TabIndex = 2;
            lblUserName.Text = "User Name";
            // 
            // txtUserName
            // 
            txtUserName.BorderColor = Color.FromArgb(203, 213, 225);
            txtUserName.BorderRadius = 8;
            txtUserName.CustomizableEdges = customizableEdges3;
            txtUserName.DefaultText = "";
            txtUserName.Font = new Font("Segoe UI", 10F);
            txtUserName.ForeColor = Color.FromArgb(15, 23, 42);
            txtUserName.Location = new Point(32, 150);
            txtUserName.Name = "txtUserName";
            txtUserName.PlaceholderText = "Enter user name";
            txtUserName.SelectedText = "";
            txtUserName.ShadowDecoration.CustomizableEdges = customizableEdges4;
            txtUserName.Size = new Size(344, 44);
            txtUserName.TabIndex = 3;
            // 
            // lblPassword
            // 
            lblPassword.BackColor = Color.Transparent;
            lblPassword.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            lblPassword.ForeColor = Color.FromArgb(51, 65, 85);
            lblPassword.Location = new Point(34, 214);
            lblPassword.Name = "lblPassword";
            lblPassword.Size = new Size(61, 19);
            lblPassword.TabIndex = 4;
            lblPassword.Text = "Password";
            // 
            // txtPassword
            // 
            txtPassword.BorderColor = Color.FromArgb(203, 213, 225);
            txtPassword.BorderRadius = 8;
            txtPassword.CustomizableEdges = customizableEdges5;
            txtPassword.DefaultText = "";
            txtPassword.Font = new Font("Segoe UI", 10F);
            txtPassword.ForeColor = Color.FromArgb(15, 23, 42);
            txtPassword.Location = new Point(32, 242);
            txtPassword.Name = "txtPassword";
            txtPassword.PasswordChar = '*';
            txtPassword.PlaceholderText = "Enter password";
            txtPassword.SelectedText = "";
            txtPassword.ShadowDecoration.CustomizableEdges = customizableEdges6;
            txtPassword.Size = new Size(264, 44);
            txtPassword.TabIndex = 5;
            // 
            // btnTogglePassword
            // 
            btnTogglePassword.BorderColor = Color.FromArgb(203, 213, 225);
            btnTogglePassword.BorderRadius = 8;
            btnTogglePassword.BorderThickness = 1;
            btnTogglePassword.Cursor = Cursors.Hand;
            btnTogglePassword.CustomizableEdges = customizableEdges7;
            btnTogglePassword.FillColor = Color.White;
            btnTogglePassword.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            btnTogglePassword.ForeColor = Color.FromArgb(37, 99, 235);
            btnTogglePassword.HoverState.FillColor = Color.FromArgb(239, 246, 255);
            btnTogglePassword.Location = new Point(308, 242);
            btnTogglePassword.Name = "btnTogglePassword";
            btnTogglePassword.ShadowDecoration.CustomizableEdges = customizableEdges8;
            btnTogglePassword.Size = new Size(68, 44);
            btnTogglePassword.TabIndex = 6;
            btnTogglePassword.Text = "";
            // 
            // btnSignIn
            // 
            btnSignIn.BorderRadius = 8;
            btnSignIn.Cursor = Cursors.Hand;
            btnSignIn.CustomizableEdges = customizableEdges9;
            btnSignIn.FillColor = Color.FromArgb(37, 99, 235);
            btnSignIn.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            btnSignIn.ForeColor = Color.White;
            btnSignIn.HoverState.FillColor = Color.FromArgb(29, 78, 216);
            btnSignIn.Location = new Point(32, 316);
            btnSignIn.Name = "btnSignIn";
            btnSignIn.ShadowDecoration.CustomizableEdges = customizableEdges10;
            btnSignIn.Size = new Size(344, 44);
            btnSignIn.TabIndex = 7;
            btnSignIn.Text = "Sign In";
            // 
            // btnCreateAccount
            // 
            btnCreateAccount.BorderRadius = 8;
            btnCreateAccount.Cursor = Cursors.Hand;
            btnCreateAccount.CustomizableEdges = customizableEdges11;
            btnCreateAccount.FillColor = Color.White;
            btnCreateAccount.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnCreateAccount.ForeColor = Color.FromArgb(37, 99, 235);
            btnCreateAccount.HoverState.FillColor = Color.FromArgb(239, 246, 255);
            btnCreateAccount.Location = new Point(32, 368);
            btnCreateAccount.Name = "btnCreateAccount";
            btnCreateAccount.ShadowDecoration.CustomizableEdges = customizableEdges12;
            btnCreateAccount.Size = new Size(344, 38);
            btnCreateAccount.TabIndex = 8;
            btnCreateAccount.Text = "Create New Account";
            // 
            // LoginForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(245, 247, 250);
            ClientSize = new Size(980, 620);
            Controls.Add(pnlContent);
            Controls.Add(pnlBrand);
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimumSize = new Size(980, 620);
            Name = "LoginForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Sign In";
            Icon = BrandAssets.LoadIcon();
            pnlBrand.ResumeLayout(false);
            pnlBrand.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)picBrandLogo).EndInit();
            pnlContent.ResumeLayout(false);
            pnlCard.ResumeLayout(false);
            pnlCard.PerformLayout();
            ResumeLayout(false);
        }
    }
}
