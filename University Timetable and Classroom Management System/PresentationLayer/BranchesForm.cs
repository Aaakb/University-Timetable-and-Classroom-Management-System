using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System
{
    public partial class BranchesForm : System.Windows.Forms.UserControl
    {
        private readonly BranchService branchService = new();

        public BranchesForm()
        {
            InitializeComponent();
            if (DesignerEnvironment.IsDesignTime(this)) return;
            ArabicUi.Apply(this);
            ConfigureBranchesGrid();
            ConfigureBranchesEvents();
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (DesignerEnvironment.IsDesignTime(this) || IsDisposed || Disposing) return;
            await LoadBranchesAsync();
            if (IsDisposed || Disposing) return;
            ClearBranchForm();
        }


        private void ConfigureBranchesGrid()
        {
            dgvBranches.AutoGenerateColumns = false;
            GridRowNumbers.Attach(dgvBranches, colRowNumber);
        }

        private void ConfigureBranchesEvents()
        {
            dgvBranches.SelectionChanged += (_, _) => PopulateBranchEditorFromSelection();

            btnAddBranch.Click += async (_, _) => await AddBranchAsync();
            btnUpdateBranch.Click += async (_, _) => await UpdateBranchAsync();
            btnDeleteBranch.Click += async (_, _) => await DeleteBranchAsync();
        }

        private async Task LoadBranchesAsync()
        {
            if (IsDisposed || Disposing) return;

            SetBranchActionsEnabled(false);

            try
            {
                var branches = await branchService.GetAllAsync();
                if (IsDisposed || Disposing) return;
                dgvBranches.DataSource = branches;
                dgvBranches.ClearSelection();
            }
            catch (Exception ex)
            {
                ShowError("تعذر تحميل الفروع.", ex);
            }
            finally
            {
                SetBranchActionsEnabled(true);
            }
        }

        private async Task AddBranchAsync()
        {
            if (IsDisposed || Disposing) return;

            if (!TryBuildBranch(out var branch, requireId: false))
            {
                return;
            }

            await ExecuteBranchActionAsync(
                async () => await branchService.AddAsync(branch),
                UiMessages.RecordAdded);
        }

        private async Task UpdateBranchAsync()
        {
            if (IsDisposed || Disposing) return;

            if (!TryBuildBranch(out var branch))
            {
                return;
            }

            await ExecuteBranchActionAsync(
                async () => await branchService.UpdateAsync(branch),
                UiMessages.RecordUpdated);
        }

        private async Task DeleteBranchAsync()
        {
            if (IsDisposed || Disposing) return;

            if (!TryGetBranchIdFromEditor(out int branchId))
            {
                return;
            }

            var confirmation = UiMessages.ConfirmDeletion(this, "حذف الفرع");

            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            await ExecuteBranchActionAsync(
                async () => await branchService.DeleteAsync(branchId),
                UiMessages.RecordDeleted);
        }

        private async Task ExecuteBranchActionAsync(Func<Task> action, string successMessage)
        {
            if (IsDisposed || Disposing) return;

            SetBranchActionsEnabled(false);

            try
            {
                await action();
                if (IsDisposed || Disposing) return;
                await LoadBranchesAsync();
                if (IsDisposed || Disposing) return;
                ClearBranchForm();
                ShowInformation(successMessage);
            }
            catch (Exception ex)
            {
                ShowError("تعذر إتمام العملية على الفرع.", ex);
            }
            finally
            {
                SetBranchActionsEnabled(true);
            }
        }

        private bool TryBuildBranch(out Branch branch, bool requireId = true)
        {
            branch = new Branch
            {
                BranchName = txtBranchName.Text.Trim()
            };

            var branchId = 0;
            if (requireId && !TryGetBranchIdFromEditor(out branchId))
            {
                return false;
            }

            branch.BranchID = requireId ? branchId : 0;

            if (!string.IsNullOrWhiteSpace(branch.BranchName))
            {
                return true;
            }

            ShowInformation(UiMessages.RequiredFields);
            txtBranchName.Focus();
            return false;
        }

        private bool TryGetBranchIdFromEditor(out int branchId)
        {
            if (int.TryParse(txtBranchId.Text, out branchId) && branchId > 0)
            {
                return true;
            }

            ShowInformation("اختر فرعاً من الجدول أولاً.");
            return false;
        }

        private void PopulateBranchEditorFromSelection()
        {
            if (IsDisposed || Disposing) return;

            if (dgvBranches.CurrentRow?.DataBoundItem is not Branch branch)
            {
                return;
            }

            txtBranchId.Text = branch.BranchID.ToString();
            txtBranchName.Text = branch.BranchName;
        }

        private void ClearBranchForm()
        {
            if (IsDisposed || Disposing) return;

            txtBranchId.Text = "تلقائي";
            txtBranchName.Clear();
            dgvBranches.ClearSelection();
            txtBranchName.Focus();
        }

        private void SetBranchActionsEnabled(bool enabled)
        {
            if (IsDisposed || Disposing) return;

            btnAddBranch.Enabled = enabled;
            btnUpdateBranch.Enabled = enabled;
            btnDeleteBranch.Enabled = enabled;
            dgvBranches.Enabled = enabled;
        }

        private void ShowInformation(string message)
        {
            UiMessages.ShowInformation(this, message, "الفروع");
        }

        private void ShowError(string message, Exception ex)
        {
            UiMessages.ShowError(this, message, "الفروع", ex);
        }

    }
}
