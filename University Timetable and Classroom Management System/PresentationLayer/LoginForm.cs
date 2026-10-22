using University_Timetable_and_Classroom_Management_System.BusinessLayer;

namespace University_Timetable_and_Classroom_Management_System
{
    public partial class LoginForm : Form
    {
        private readonly AuthService authService = new();

        private bool passwordVisible;
        private bool arrangingCard;
        private bool isSigningIn;

        public LoginForm()
        {
            InitializeComponent();
            if (DesignerEnvironment.IsDesignTime(this)) return;
            ArabicUi.Apply(this);
            WireEvents();
            WindowSizing.Attach(this);
            ClientSizeChanged += (_, _) => ArrangeLoginCard();
            pnlContent.SizeChanged += (_, _) => ArrangeLoginCard();
            pnlCard.SizeChanged += (_, _) => ArrangeLoginCard();
            ArrangeLoginCard();
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

        private void ArrangeLoginCard()
        {
            if (arrangingCard || IsDisposed || Disposing) return;
            arrangingCard = true;
            try
            {
                int margin = (int)Math.Round(24 * DeviceDpi / 96.0);
                int brandHeight = pnlBrand.Controls.Cast<Control>().Max(c => c.Bottom) + margin;
                pnlBrand.Visible = ClientSize.Width >= pnlBrand.Width + pnlCard.Width + margin * 2 && ClientSize.Height >= brandHeight;
                PerformLayout();
                WindowSizing.CenterScrollableCard(pnlContent, pnlCard);
            }
            finally { arrangingCard = false; }
        }

        private async Task SignInAsync()
        {
            if (IsDisposed || Disposing || isSigningIn) return;

            isSigningIn = true;
            SetBusy(true);

            try
            {
                var result = await authService.SignInAsync(txtUserName.Text, txtPassword.Text);
                if (IsDisposed || Disposing) return;

                if (!result.Succeeded)
                {
                    UiMessages.ShowWarning(this, result.Message, "تسجيل الدخول");
                    return;
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                UiMessages.ShowError(this, "تعذر تسجيل الدخول.", "تسجيل الدخول", ex);
            }
            finally
            {
                isSigningIn = false;
                SetBusy(false);
            }
        }

        private void OpenRegisterForm()
        {
            using var registerForm = new RegisterForm();

            if (registerForm.ShowDialog(this) != DialogResult.OK || IsDisposed || Disposing)
            {
                return;
            }

            txtUserName.Text = registerForm.CreatedUserName;
            txtPassword.Clear();
            txtPassword.Focus();
        }

        private void SetBusy(bool busy)
        {
            if (IsDisposed || Disposing) return;

            txtUserName.Enabled = !busy;
            txtPassword.Enabled = !busy;
            btnTogglePassword.Enabled = !busy;
            btnCreateAccount.Enabled = !busy;
            btnSignIn.Enabled = !busy;
            btnSignIn.Text = busy ? "جارٍ تسجيل الدخول..." : "تسجيل الدخول";
        }

        private void TogglePasswordVisibility()
        {
            passwordVisible = !passwordVisible;
            txtPassword.PasswordChar = passwordVisible ? '\0' : '*';
            PasswordToggleIcon.Apply(btnTogglePassword, passwordVisible);
        }

    }
}
