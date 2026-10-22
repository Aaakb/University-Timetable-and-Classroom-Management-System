using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System
{
    public partial class FacultyMembersForm : System.Windows.Forms.UserControl
    {
        private readonly FacultyMemberService facultyMemberService = new();

        public FacultyMembersForm()
        {
            InitializeComponent();
            if (DesignerEnvironment.IsDesignTime(this)) return;
            ArabicUi.Apply(this);
            ConfigureFacultyMembersGrid();
            ConfigureFacultyMembersEvents();
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (DesignerEnvironment.IsDesignTime(this) || IsDisposed || Disposing) return;
            await LoadFacultyMembersAsync();
            if (IsDisposed || Disposing) return;
            ClearFacultyMemberForm();
        }


        private void ConfigureFacultyMembersGrid()
        {
            dgvFacultyMembers.AutoGenerateColumns = false;
            GridRowNumbers.Attach(dgvFacultyMembers, colRowNumber);
        }

        private void ConfigureFacultyMembersEvents()
        {
            dgvFacultyMembers.SelectionChanged += (_, _) => PopulateFacultyMemberEditorFromSelection();

            btnAddFacultyMember.Click += async (_, _) => await AddFacultyMemberAsync();
            btnUpdateFacultyMember.Click += async (_, _) => await UpdateFacultyMemberAsync();
            btnDeleteFacultyMember.Click += async (_, _) => await DeleteFacultyMemberAsync();
        }

        private async Task LoadFacultyMembersAsync()
        {
            if (IsDisposed || Disposing) return;

            SetFacultyMemberActionsEnabled(false);

            try
            {
                var facultyMembers = await facultyMemberService.GetAllAsync();
                if (IsDisposed || Disposing) return;
                dgvFacultyMembers.DataSource = facultyMembers;
                dgvFacultyMembers.ClearSelection();
            }
            catch (Exception ex)
            {
                ShowError("تعذر تحميل التدريسيين.", ex);
            }
            finally
            {
                SetFacultyMemberActionsEnabled(true);
            }
        }

        private async Task AddFacultyMemberAsync()
        {
            if (IsDisposed || Disposing) return;

            if (!TryBuildFacultyMember(out var facultyMember, requireId: false))
            {
                return;
            }

            await ExecuteFacultyMemberActionAsync(
                async () => await facultyMemberService.AddAsync(facultyMember),
                UiMessages.RecordAdded);
        }

        private async Task UpdateFacultyMemberAsync()
        {
            if (IsDisposed || Disposing) return;

            if (!TryBuildFacultyMember(out var facultyMember))
            {
                return;
            }

            await ExecuteFacultyMemberActionAsync(
                async () => await facultyMemberService.UpdateAsync(facultyMember),
                UiMessages.RecordUpdated);
        }

        private async Task DeleteFacultyMemberAsync()
        {
            if (IsDisposed || Disposing) return;

            if (!TryGetFacultyMemberIdFromEditor(out int facultyMemberId))
            {
                return;
            }

            var confirmation = UiMessages.ConfirmDeletion(this, "حذف التدريسي");

            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            await ExecuteFacultyMemberActionAsync(
                async () => await facultyMemberService.DeleteAsync(facultyMemberId),
                UiMessages.RecordDeleted);
        }

        private async Task ExecuteFacultyMemberActionAsync(Func<Task> action, string successMessage)
        {
            if (IsDisposed || Disposing) return;

            SetFacultyMemberActionsEnabled(false);

            try
            {
                await action();
                if (IsDisposed || Disposing) return;
                await LoadFacultyMembersAsync();
                if (IsDisposed || Disposing) return;
                ClearFacultyMemberForm();
                ShowInformation(successMessage);
            }
            catch (Exception ex)
            {
                ShowError("تعذر إتمام العملية على التدريسي.", ex);
            }
            finally
            {
                SetFacultyMemberActionsEnabled(true);
            }
        }

        private bool TryBuildFacultyMember(out FacultyMember facultyMember, bool requireId = true)
        {
            facultyMember = new FacultyMember
            {
                FullName = txtFacultyMemberFullName.Text.Trim(),
                AcademicTitle = ComboBoxHelper.GetSelectedPlainText(cmbAcademicTitle)?.Trim()
            };

            var facultyMemberId = 0;
            if (requireId && !TryGetFacultyMemberIdFromEditor(out facultyMemberId))
            {
                return false;
            }

            facultyMember.FacultyMemberID = requireId ? facultyMemberId : 0;

            if (string.IsNullOrWhiteSpace(facultyMember.FullName))
            {
                ShowInformation(UiMessages.RequiredFields);
                txtFacultyMemberFullName.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(facultyMember.AcademicTitle))
            {
                facultyMember.AcademicTitle = null;
            }

            return true;
        }

        private bool TryGetFacultyMemberIdFromEditor(out int facultyMemberId)
        {
            if (int.TryParse(txtFacultyMemberId.Text, out facultyMemberId) && facultyMemberId > 0)
            {
                return true;
            }

            ShowInformation("اختر تدريسياً من الجدول أولاً.");
            return false;
        }

        private void PopulateFacultyMemberEditorFromSelection()
        {
            if (IsDisposed || Disposing) return;

            if (dgvFacultyMembers.CurrentRow?.DataBoundItem is not FacultyMember facultyMember)
            {
                return;
            }

            txtFacultyMemberId.Text = facultyMember.FacultyMemberID.ToString();
            txtFacultyMemberFullName.Text = facultyMember.FullName;
            ComboBoxHelper.SelectText(cmbAcademicTitle, facultyMember.AcademicTitle, addMissing: true);
        }

        private void ClearFacultyMemberForm()
        {
            if (IsDisposed || Disposing) return;

            txtFacultyMemberId.Text = "تلقائي";
            txtFacultyMemberFullName.Clear();
            cmbAcademicTitle.SelectedIndex = -1;
            dgvFacultyMembers.ClearSelection();
            txtFacultyMemberFullName.Focus();
        }

        private void SetFacultyMemberActionsEnabled(bool enabled)
        {
            if (IsDisposed || Disposing) return;

            btnAddFacultyMember.Enabled = enabled;
            btnUpdateFacultyMember.Enabled = enabled;
            btnDeleteFacultyMember.Enabled = enabled;
            dgvFacultyMembers.Enabled = enabled;
        }

        private void ShowInformation(string message)
        {
            UiMessages.ShowInformation(this, message, "التدريسيون");
        }

        private void ShowError(string message, Exception ex)
        {
            UiMessages.ShowError(this, message, "التدريسيون", ex);
        }

        private void btnClearFacultyMemberForm_Click(object sender, EventArgs e)
        {

        }
    }
}
