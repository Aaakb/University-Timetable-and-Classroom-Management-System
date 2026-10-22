using System.ComponentModel;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;

namespace University_Timetable_and_Classroom_Management_System
{
    public partial class RegisterForm : Form
    {
        private readonly AuthService authService = new();

        private bool passwordVisible;
        private bool confirmPasswordVisible;
        private bool arrangingCard;
        private bool isCreatingAccount;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string CreatedUserName { get; private set; } = string.Empty;

        public RegisterForm()
        {
            InitializeComponent();
            if (DesignerEnvironment.IsDesignTime(this)) return;
            ArabicUi.Apply(this);
            WireEvents();
            WindowSizing.Attach(this);
            ClientSizeChanged += (_, _) => ArrangeRegisterCard();
            pnlCard.SizeChanged += (_, _) => ArrangeRegisterCard();
            ArrangeRegisterCard();
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

        private void ArrangeRegisterCard()
        {
            if (arrangingCard || IsDisposed || Disposing) return;
            arrangingCard = true;
            try { WindowSizing.CenterScrollableCard(this, pnlCard); }
            finally { arrangingCard = false; }
        }

        private async Task CreateAccountAsync()
        {
            if (IsDisposed || Disposing || isCreatingAccount) return;

            isCreatingAccount = true;
            SetBusy(true);

            try
            {
                var result = await authService.RegisterAsync(
                    txtFullName.Text,
                    txtUserName.Text,
                    txtPassword.Text,
                    txtConfirmPassword.Text);
                if (IsDisposed || Disposing) return;

                if (!result.Succeeded)
                {
                    UiMessages.ShowWarning(this, result.Message, "إنشاء حساب");
                    return;
                }

                CreatedUserName = result.User?.UserName ?? txtUserName.Text.Trim();
                UiMessages.ShowInformation(this, result.Message, "إنشاء حساب");
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                UiMessages.ShowError(this, "تعذر إنشاء الحساب.", "إنشاء حساب", ex);
            }
            finally
            {
                isCreatingAccount = false;
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            if (IsDisposed || Disposing) return;

            txtFullName.Enabled = !busy;
            txtUserName.Enabled = !busy;
            txtPassword.Enabled = !busy;
            txtConfirmPassword.Enabled = !busy;
            btnCancel.Enabled = !busy;
            btnTogglePassword.Enabled = !busy;
            btnToggleConfirmPassword.Enabled = !busy;
            btnCreateAccount.Enabled = !busy;
            btnCreateAccount.Text = busy ? "جارٍ الإنشاء..." : "إنشاء حساب";
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

    }
}
