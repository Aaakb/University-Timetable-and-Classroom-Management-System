using University_Timetable_and_Classroom_Management_System.BusinessLayer;

namespace University_Timetable_and_Classroom_Management_System
{
    public class RegisterForm : Form
    {
        private readonly AuthService authService = new();

        private Guna.UI2.WinForms.Guna2Panel pnlCard = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblTitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblSubtitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblFullName = null!;
        private Guna.UI2.WinForms.Guna2TextBox txtFullName = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblUserName = null!;
        private Guna.UI2.WinForms.Guna2TextBox txtUserName = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblPassword = null!;
        private Guna.UI2.WinForms.Guna2TextBox txtPassword = null!;
        private Guna.UI2.WinForms.Guna2Button btnTogglePassword = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblConfirmPassword = null!;
        private Guna.UI2.WinForms.Guna2TextBox txtConfirmPassword = null!;
        private Guna.UI2.WinForms.Guna2Button btnToggleConfirmPassword = null!;
        private Guna.UI2.WinForms.Guna2Button btnCreateAccount = null!;
        private Guna.UI2.WinForms.Guna2Button btnCancel = null!;
        private bool passwordVisible;
        private bool confirmPasswordVisible;

        public string CreatedUserName { get; private set; } = string.Empty;

        public RegisterForm()
        {
            InitializeComponent();
            PasswordToggleIcon.Apply(btnTogglePassword, passwordVisible);
            PasswordToggleIcon.Apply(btnToggleConfirmPassword, confirmPasswordVisible);
            WireEvents();
            AcceptButton = btnCreateAccount;
            CancelButton = btnCancel;
            Shown += (_, _) => txtFullName.Focus();
        }

        private void WireEvents()
        {
            btnCreateAccount.Click += async (_, _) => await CreateAccountAsync();
            btnCancel.Click += (_, _) => Close();
            btnTogglePassword.Click += (_, _) => TogglePasswordVisibility();
            btnToggleConfirmPassword.Click += (_, _) => ToggleConfirmPasswordVisibility();
            txtConfirmPassword.KeyDown += async (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    await CreateAccountAsync();
                }
            };
        }

        private async Task CreateAccountAsync()
        {
            SetBusy(true);

            try
            {
                var result = await authService.RegisterAsync(
                    txtFullName.Text,
                    txtUserName.Text,
                    txtPassword.Text,
                    txtConfirmPassword.Text);

                if (!result.Succeeded)
                {
                    UiMessages.ShowWarning(this, result.Message, "Create Account");
                    return;
                }

                CreatedUserName = result.User?.UserName ?? txtUserName.Text.Trim();
                UiMessages.ShowInformation(this, result.Message, "Create Account");
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                UiMessages.ShowError(this, "Unable to create account.", "Create Account", ex);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            txtFullName.Enabled = !busy;
            txtUserName.Enabled = !busy;
            txtPassword.Enabled = !busy;
            txtConfirmPassword.Enabled = !busy;
            btnCancel.Enabled = !busy;
            btnTogglePassword.Enabled = !busy;
            btnToggleConfirmPassword.Enabled = !busy;
            btnCreateAccount.Enabled = !busy;
            btnCreateAccount.Text = busy ? "Creating..." : "Create Account";
        }

        private void TogglePasswordVisibility()
        {
            passwordVisible = !passwordVisible;
            txtPassword.PasswordChar = passwordVisible ? '\0' : '*';
            PasswordToggleIcon.Apply(btnTogglePassword, passwordVisible);
        }

        private void ToggleConfirmPasswordVisibility()
        {
            confirmPasswordVisible = !confirmPasswordVisible;
            txtConfirmPassword.PasswordChar = confirmPasswordVisible ? '\0' : '*';
            PasswordToggleIcon.Apply(btnToggleConfirmPassword, confirmPasswordVisible);
        }

        private void InitializeComponent()
        {
            pnlCard = new Guna.UI2.WinForms.Guna2Panel();
            lblTitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblSubtitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblFullName = new Guna.UI2.WinForms.Guna2HtmlLabel();
            txtFullName = new Guna.UI2.WinForms.Guna2TextBox();
            lblUserName = new Guna.UI2.WinForms.Guna2HtmlLabel();
            txtUserName = new Guna.UI2.WinForms.Guna2TextBox();
            lblPassword = new Guna.UI2.WinForms.Guna2HtmlLabel();
            txtPassword = new Guna.UI2.WinForms.Guna2TextBox();
            btnTogglePassword = new Guna.UI2.WinForms.Guna2Button();
            lblConfirmPassword = new Guna.UI2.WinForms.Guna2HtmlLabel();
            txtConfirmPassword = new Guna.UI2.WinForms.Guna2TextBox();
            btnToggleConfirmPassword = new Guna.UI2.WinForms.Guna2Button();
            btnCreateAccount = new Guna.UI2.WinForms.Guna2Button();
            btnCancel = new Guna.UI2.WinForms.Guna2Button();
            pnlCard.SuspendLayout();
            SuspendLayout();

            pnlCard.Anchor = AnchorStyles.None;
            pnlCard.BorderColor = Color.FromArgb(226, 232, 240);
            pnlCard.BorderRadius = 8;
            pnlCard.BorderThickness = 1;
            pnlCard.FillColor = Color.White;
            pnlCard.Location = new Point(42, 32);
            pnlCard.Size = new Size(456, 524);
            pnlCard.Controls.Add(lblTitle);
            pnlCard.Controls.Add(lblSubtitle);
            pnlCard.Controls.Add(lblFullName);
            pnlCard.Controls.Add(txtFullName);
            pnlCard.Controls.Add(lblUserName);
            pnlCard.Controls.Add(txtUserName);
            pnlCard.Controls.Add(lblPassword);
            pnlCard.Controls.Add(txtPassword);
            pnlCard.Controls.Add(btnTogglePassword);
            pnlCard.Controls.Add(lblConfirmPassword);
            pnlCard.Controls.Add(txtConfirmPassword);
            pnlCard.Controls.Add(btnToggleConfirmPassword);
            pnlCard.Controls.Add(btnCreateAccount);
            pnlCard.Controls.Add(btnCancel);

            lblTitle.BackColor = Color.Transparent;
            lblTitle.Font = new Font("Segoe UI Semibold", 20F, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(15, 23, 42);
            lblTitle.Location = new Point(32, 28);
            lblTitle.Text = "Create Account";

            lblSubtitle.BackColor = Color.Transparent;
            lblSubtitle.Font = new Font("Segoe UI", 9.5F);
            lblSubtitle.ForeColor = Color.FromArgb(100, 116, 139);
            lblSubtitle.Location = new Point(34, 70);
            lblSubtitle.Text = "Add a user who can access the timetable system.";

            lblFullName.BackColor = Color.Transparent;
            lblFullName.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            lblFullName.ForeColor = Color.FromArgb(51, 65, 85);
            lblFullName.Location = new Point(34, 116);
            lblFullName.Text = "Full Name";

            txtFullName.BorderColor = Color.FromArgb(203, 213, 225);
            txtFullName.BorderRadius = 8;
            txtFullName.Font = new Font("Segoe UI", 10F);
            txtFullName.ForeColor = Color.FromArgb(15, 23, 42);
            txtFullName.Location = new Point(32, 142);
            txtFullName.PlaceholderText = "Enter full name";
            txtFullName.Size = new Size(392, 42);

            lblUserName.BackColor = Color.Transparent;
            lblUserName.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            lblUserName.ForeColor = Color.FromArgb(51, 65, 85);
            lblUserName.Location = new Point(34, 198);
            lblUserName.Text = "User Name";

            txtUserName.BorderColor = Color.FromArgb(203, 213, 225);
            txtUserName.BorderRadius = 8;
            txtUserName.Font = new Font("Segoe UI", 10F);
            txtUserName.ForeColor = Color.FromArgb(15, 23, 42);
            txtUserName.Location = new Point(32, 224);
            txtUserName.PlaceholderText = "Enter user name";
            txtUserName.Size = new Size(392, 42);

            lblPassword.BackColor = Color.Transparent;
            lblPassword.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            lblPassword.ForeColor = Color.FromArgb(51, 65, 85);
            lblPassword.Location = new Point(34, 280);
            lblPassword.Text = "Password";

            txtPassword.BorderColor = Color.FromArgb(203, 213, 225);
            txtPassword.BorderRadius = 8;
            txtPassword.Font = new Font("Segoe UI", 10F);
            txtPassword.ForeColor = Color.FromArgb(15, 23, 42);
            txtPassword.Location = new Point(32, 306);
            txtPassword.PasswordChar = '*';
            txtPassword.PlaceholderText = "At least 4 characters";
            txtPassword.Size = new Size(306, 42);

            btnTogglePassword.BorderColor = Color.FromArgb(203, 213, 225);
            btnTogglePassword.BorderRadius = 8;
            btnTogglePassword.BorderThickness = 1;
            btnTogglePassword.Cursor = Cursors.Hand;
            btnTogglePassword.FillColor = Color.White;
            btnTogglePassword.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            btnTogglePassword.ForeColor = Color.FromArgb(37, 99, 235);
            btnTogglePassword.HoverState.FillColor = Color.FromArgb(239, 246, 255);
            btnTogglePassword.Location = new Point(348, 306);
            btnTogglePassword.Size = new Size(76, 42);
            btnTogglePassword.Text = "";

            lblConfirmPassword.BackColor = Color.Transparent;
            lblConfirmPassword.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            lblConfirmPassword.ForeColor = Color.FromArgb(51, 65, 85);
            lblConfirmPassword.Location = new Point(34, 362);
            lblConfirmPassword.Text = "Confirm Password";

            txtConfirmPassword.BorderColor = Color.FromArgb(203, 213, 225);
            txtConfirmPassword.BorderRadius = 8;
            txtConfirmPassword.Font = new Font("Segoe UI", 10F);
            txtConfirmPassword.ForeColor = Color.FromArgb(15, 23, 42);
            txtConfirmPassword.Location = new Point(32, 388);
            txtConfirmPassword.PasswordChar = '*';
            txtConfirmPassword.PlaceholderText = "Repeat password";
            txtConfirmPassword.Size = new Size(306, 42);

            btnToggleConfirmPassword.BorderColor = Color.FromArgb(203, 213, 225);
            btnToggleConfirmPassword.BorderRadius = 8;
            btnToggleConfirmPassword.BorderThickness = 1;
            btnToggleConfirmPassword.Cursor = Cursors.Hand;
            btnToggleConfirmPassword.FillColor = Color.White;
            btnToggleConfirmPassword.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            btnToggleConfirmPassword.ForeColor = Color.FromArgb(37, 99, 235);
            btnToggleConfirmPassword.HoverState.FillColor = Color.FromArgb(239, 246, 255);
            btnToggleConfirmPassword.Location = new Point(348, 388);
            btnToggleConfirmPassword.Size = new Size(76, 42);
            btnToggleConfirmPassword.Text = "";

            btnCreateAccount.BorderRadius = 8;
            btnCreateAccount.Cursor = Cursors.Hand;
            btnCreateAccount.FillColor = Color.FromArgb(37, 99, 235);
            btnCreateAccount.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            btnCreateAccount.ForeColor = Color.White;
            btnCreateAccount.HoverState.FillColor = Color.FromArgb(29, 78, 216);
            btnCreateAccount.Location = new Point(32, 458);
            btnCreateAccount.Size = new Size(246, 42);
            btnCreateAccount.Text = "Create Account";

            btnCancel.BorderRadius = 8;
            btnCancel.Cursor = Cursors.Hand;
            btnCancel.FillColor = Color.FromArgb(100, 116, 139);
            btnCancel.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            btnCancel.ForeColor = Color.White;
            btnCancel.HoverState.FillColor = Color.FromArgb(71, 85, 105);
            btnCancel.Location = new Point(292, 458);
            btnCancel.Size = new Size(132, 42);
            btnCancel.Text = "Cancel";

            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(245, 247, 250);
            ClientSize = new Size(540, 588);
            Controls.Add(pnlCard);
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "RegisterForm";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Create Account";
            Icon = BrandAssets.LoadIcon();
            pnlCard.ResumeLayout(false);
            pnlCard.PerformLayout();
            ResumeLayout(false);
        }
    }
}
