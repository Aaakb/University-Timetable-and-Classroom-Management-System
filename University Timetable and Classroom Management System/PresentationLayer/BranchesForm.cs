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
            ConfigureAutoIdField();
            ConfigureNavigation();
            ConfigureBranchesGrid();
            ConfigureBranchesEvents();
        }

        private void ConfigureAutoIdField()
        {
            txtBranchId.ReadOnly = true;
            txtBranchId.TabStop = false;
            txtBranchId.PlaceholderText = "Auto";
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await LoadBranchesAsync();
            ClearBranchForm();
        }

        private void ConfigureNavigation()
        {
            FormNavigation.ConfigureSidebar(this, pnlSidebar, NavigationPage.Branches);
        }

        private void ConfigureBranchesGrid()
        {
            dgvBranches.AutoGenerateColumns = false;
            dgvBranches.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            GridStyle.Apply(dgvBranches);
        }

        private void ConfigureBranchesEvents()
        {
            dgvBranches.SelectionChanged += (_, _) => PopulateBranchEditorFromSelection();
            txtBranchId.Leave += async (_, _) => await PopulateBranchEditorFromEnteredIdAsync();
            btnAddBranch.Click += async (_, _) => await AddBranchAsync();
            btnUpdateBranch.Click += async (_, _) => await UpdateBranchAsync();
            btnDeleteBranch.Click += async (_, _) => await DeleteBranchAsync();
        }

        private async Task LoadBranchesAsync()
        {
            SetBranchActionsEnabled(false);

            try
            {
                var branches = await branchService.GetAllAsync();
                dgvBranches.DataSource = branches;
                dgvBranches.ClearSelection();
            }
            catch (Exception ex)
            {
                ShowError("Unable to load branches.", ex);
            }
            finally
            {
                SetBranchActionsEnabled(true);
            }
        }

        private async Task AddBranchAsync()
        {
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
            if (!TryGetBranchIdFromEditor(out int branchId))
            {
                return;
            }

            var confirmation = UiMessages.ConfirmDeletion(this, "Delete Branch");

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
            SetBranchActionsEnabled(false);

            try
            {
                await action();
                await LoadBranchesAsync();
                ClearBranchForm();
                ShowInformation(successMessage);
            }
            catch (Exception ex)
            {
                ShowError("Unable to complete the branch operation.", ex);
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

            ShowInformation("Select a branch row first.");
            return false;
        }

        private void PopulateBranchEditorFromSelection()
        {
            if (dgvBranches.CurrentRow?.DataBoundItem is not Branch branch)
            {
                return;
            }

            txtBranchId.Text = branch.BranchID.ToString();
            txtBranchName.Text = branch.BranchName;
        }

        private async Task PopulateBranchEditorFromEnteredIdAsync()
        {
            if (!int.TryParse(txtBranchId.Text, out int branchId) || branchId <= 0)
            {
                return;
            }

            try
            {
                var branch = await branchService.GetByIdAsync(branchId);

                if (branch is null)
                {
                    return;
                }

                txtBranchName.Text = branch.BranchName;
                SelectBranchRow(branch.BranchID);
            }
            catch (Exception ex)
            {
                ShowError("Unable to load branch details.", ex);
            }
        }

        private void SelectBranchRow(int branchId)
        {
            foreach (DataGridViewRow row in dgvBranches.Rows)
            {
                if (row.DataBoundItem is not Branch branch || branch.BranchID != branchId)
                {
                    continue;
                }

                row.Selected = true;
                dgvBranches.CurrentCell = row.Cells[0];
                break;
            }
        }

        private void ClearBranchForm()
        {
            txtBranchId.Text = "Auto";
            txtBranchName.Clear();
            dgvBranches.ClearSelection();
            txtBranchName.Focus();
        }

        private void SetBranchActionsEnabled(bool enabled)
        {
            btnAddBranch.Enabled = enabled;
            btnUpdateBranch.Enabled = enabled;
            btnDeleteBranch.Enabled = enabled;
            dgvBranches.Enabled = enabled;
        }

        private void ShowInformation(string message)
        {
            UiMessages.ShowInformation(this, message, "Branches");
        }

        private void ShowError(string message, Exception ex)
        {
            UiMessages.ShowError(this, message, "Branches", ex);
        }
        private void InitializeComponent()
        {
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle3 = new DataGridViewCellStyle();
            pnlSidebar = new Guna.UI2.WinForms.Guna2Panel();
            lblSidebarFooter = new Guna.UI2.WinForms.Guna2HtmlLabel();
            btnNavigationSchedules = new Guna.UI2.WinForms.Guna2Button();
            btnNavigationFaculty = new Guna.UI2.WinForms.Guna2Button();
            btnNavigationClassrooms = new Guna.UI2.WinForms.Guna2Button();
            btnNavigationStudyYears = new Guna.UI2.WinForms.Guna2Button();
            btnNavigationBranches = new Guna.UI2.WinForms.Guna2Button();
            separatorSidebar = new Guna.UI2.WinForms.Guna2Separator();
            lblSidebarSubtitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblApplicationName = new Guna.UI2.WinForms.Guna2HtmlLabel();
            pnlMain = new Guna.UI2.WinForms.Guna2Panel();
            pnlWorkspace = new Guna.UI2.WinForms.Guna2Panel();
            pnlBranchesTable = new Guna.UI2.WinForms.Guna2Panel();
            dgvBranches = new Guna.UI2.WinForms.Guna2DataGridView();
            colBranchId = new DataGridViewTextBoxColumn();
            colBranchName = new DataGridViewTextBoxColumn();
            lblTableSubtitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblTableTitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            pnlBranchEditor = new Guna.UI2.WinForms.Guna2Panel();
            btnClearBranchForm = new Guna.UI2.WinForms.Guna2Button();
            btnDeleteBranch = new Guna.UI2.WinForms.Guna2Button();
            btnUpdateBranch = new Guna.UI2.WinForms.Guna2Button();
            btnAddBranch = new Guna.UI2.WinForms.Guna2Button();
            txtBranchName = new Guna.UI2.WinForms.Guna2TextBox();
            lblBranchName = new Guna.UI2.WinForms.Guna2HtmlLabel();
            txtBranchId = new Guna.UI2.WinForms.Guna2TextBox();
            lblBranchId = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblEditorSubtitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblEditorTitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            pnlHeader = new Guna.UI2.WinForms.Guna2Panel();
            lblPageSubtitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblPageTitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            pnlSidebar.SuspendLayout();
            pnlMain.SuspendLayout();
            pnlWorkspace.SuspendLayout();
            pnlBranchesTable.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvBranches).BeginInit();
            pnlBranchEditor.SuspendLayout();
            pnlHeader.SuspendLayout();
            SuspendLayout();
            pnlSidebar.BackColor = Color.Transparent;
            pnlSidebar.Controls.Add(lblSidebarFooter);
            pnlSidebar.Controls.Add(btnNavigationSchedules);
            pnlSidebar.Controls.Add(btnNavigationFaculty);
            pnlSidebar.Controls.Add(btnNavigationClassrooms);
            pnlSidebar.Controls.Add(btnNavigationStudyYears);
            pnlSidebar.Controls.Add(btnNavigationBranches);
            pnlSidebar.Controls.Add(separatorSidebar);
            pnlSidebar.Controls.Add(lblSidebarSubtitle);
            pnlSidebar.Controls.Add(lblApplicationName);
            pnlSidebar.Dock = DockStyle.Left;
            pnlSidebar.FillColor = Color.FromArgb(24, 38, 62);
            pnlSidebar.Location = new Point(0, 0);
            pnlSidebar.Name = "pnlSidebar";
            pnlSidebar.Size = new Size(240, 720);
            pnlSidebar.TabIndex = 0;
            lblSidebarFooter.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblSidebarFooter.BackColor = Color.Transparent;
            lblSidebarFooter.Font = new Font("Segoe UI", 9F);
            lblSidebarFooter.ForeColor = Color.FromArgb(148, 163, 184);
            lblSidebarFooter.Location = new Point(24, 671);
            lblSidebarFooter.Name = "lblSidebarFooter";
            lblSidebarFooter.Size = new Size(147, 17);
            lblSidebarFooter.TabIndex = 9;
            lblSidebarFooter.Text = "Academic Scheduling Suite";
            btnNavigationSchedules.BorderRadius = 8;
            btnNavigationSchedules.Cursor = Cursors.Hand;
            btnNavigationSchedules.FillColor = Color.FromArgb(24, 38, 62);
            btnNavigationSchedules.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            btnNavigationSchedules.ForeColor = Color.FromArgb(226, 232, 240);
            btnNavigationSchedules.HoverState.FillColor = Color.FromArgb(36, 55, 86);
            btnNavigationSchedules.Location = new Point(24, 378);
            btnNavigationSchedules.Name = "btnNavigationSchedules";
            btnNavigationSchedules.Size = new Size(192, 44);
            btnNavigationSchedules.TabIndex = 8;
            btnNavigationSchedules.Text = "Schedules";
            btnNavigationSchedules.TextAlign = HorizontalAlignment.Left;
            btnNavigationSchedules.TextOffset = new Point(14, 0);
            btnNavigationFaculty.BorderRadius = 8;
            btnNavigationFaculty.Cursor = Cursors.Hand;
            btnNavigationFaculty.FillColor = Color.FromArgb(24, 38, 62);
            btnNavigationFaculty.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            btnNavigationFaculty.ForeColor = Color.FromArgb(226, 232, 240);
            btnNavigationFaculty.HoverState.FillColor = Color.FromArgb(36, 55, 86);
            btnNavigationFaculty.Location = new Point(24, 322);
            btnNavigationFaculty.Name = "btnNavigationFaculty";
            btnNavigationFaculty.Size = new Size(192, 44);
            btnNavigationFaculty.TabIndex = 7;
            btnNavigationFaculty.Text = "Faculty Members";
            btnNavigationFaculty.TextAlign = HorizontalAlignment.Left;
            btnNavigationFaculty.TextOffset = new Point(14, 0);
            btnNavigationClassrooms.BorderRadius = 8;
            btnNavigationClassrooms.Cursor = Cursors.Hand;
            btnNavigationClassrooms.FillColor = Color.FromArgb(24, 38, 62);
            btnNavigationClassrooms.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            btnNavigationClassrooms.ForeColor = Color.FromArgb(226, 232, 240);
            btnNavigationClassrooms.HoverState.FillColor = Color.FromArgb(36, 55, 86);
            btnNavigationClassrooms.Location = new Point(24, 266);
            btnNavigationClassrooms.Name = "btnNavigationClassrooms";
            btnNavigationClassrooms.Size = new Size(192, 44);
            btnNavigationClassrooms.TabIndex = 6;
            btnNavigationClassrooms.Text = "Classrooms";
            btnNavigationClassrooms.TextAlign = HorizontalAlignment.Left;
            btnNavigationClassrooms.TextOffset = new Point(14, 0);
            btnNavigationStudyYears.BorderRadius = 8;
            btnNavigationStudyYears.Cursor = Cursors.Hand;
            btnNavigationStudyYears.FillColor = Color.FromArgb(24, 38, 62);
            btnNavigationStudyYears.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            btnNavigationStudyYears.ForeColor = Color.FromArgb(226, 232, 240);
            btnNavigationStudyYears.HoverState.FillColor = Color.FromArgb(36, 55, 86);
            btnNavigationStudyYears.Location = new Point(24, 210);
            btnNavigationStudyYears.Name = "btnNavigationStudyYears";
            btnNavigationStudyYears.Size = new Size(192, 44);
            btnNavigationStudyYears.TabIndex = 5;
            btnNavigationStudyYears.Text = "Study Years";
            btnNavigationStudyYears.TextAlign = HorizontalAlignment.Left;
            btnNavigationStudyYears.TextOffset = new Point(14, 0);
            btnNavigationBranches.BorderRadius = 8;
            btnNavigationBranches.Checked = true;
            btnNavigationBranches.Cursor = Cursors.Hand;
            btnNavigationBranches.FillColor = Color.FromArgb(37, 99, 235);
            btnNavigationBranches.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            btnNavigationBranches.ForeColor = Color.White;
            btnNavigationBranches.HoverState.FillColor = Color.FromArgb(29, 78, 216);
            btnNavigationBranches.Location = new Point(24, 154);
            btnNavigationBranches.Name = "btnNavigationBranches";
            btnNavigationBranches.Size = new Size(192, 44);
            btnNavigationBranches.TabIndex = 4;
            btnNavigationBranches.Text = "Branches";
            btnNavigationBranches.TextAlign = HorizontalAlignment.Left;
            btnNavigationBranches.TextOffset = new Point(14, 0);
            separatorSidebar.FillColor = Color.FromArgb(51, 65, 85);
            separatorSidebar.Location = new Point(24, 78);
            separatorSidebar.Name = "separatorSidebar";
            separatorSidebar.Size = new Size(192, 10);
            separatorSidebar.TabIndex = 2;
            lblSidebarSubtitle.BackColor = Color.Transparent;
            lblSidebarSubtitle.Font = new Font("Segoe UI", 9F);
            lblSidebarSubtitle.ForeColor = Color.FromArgb(148, 163, 184);
            lblSidebarSubtitle.Location = new Point(26, 52);
            lblSidebarSubtitle.Name = "lblSidebarSubtitle";
            lblSidebarSubtitle.Size = new Size(133, 17);
            lblSidebarSubtitle.TabIndex = 1;
            lblSidebarSubtitle.Text = "Classroom Management";
            lblApplicationName.BackColor = Color.Transparent;
            lblApplicationName.Font = new Font("Segoe UI Semibold", 17F, FontStyle.Bold);
            lblApplicationName.ForeColor = Color.White;
            lblApplicationName.Location = new Point(24, 20);
            lblApplicationName.Name = "lblApplicationName";
            lblApplicationName.Size = new Size(216, 33);
            lblApplicationName.TabIndex = 0;
            lblApplicationName.Text = "University Timetable";
            pnlMain.Controls.Add(pnlWorkspace);
            pnlMain.Controls.Add(pnlHeader);
            pnlMain.Dock = DockStyle.Fill;
            pnlMain.FillColor = Color.FromArgb(245, 247, 250);
            pnlMain.Location = new Point(240, 0);
            pnlMain.Name = "pnlMain";
            pnlMain.Size = new Size(940, 720);
            pnlMain.TabIndex = 1;
            pnlWorkspace.Controls.Add(pnlBranchesTable);
            pnlWorkspace.Controls.Add(pnlBranchEditor);
            pnlWorkspace.Dock = DockStyle.Fill;
            pnlWorkspace.FillColor = Color.FromArgb(245, 247, 250);
            pnlWorkspace.Location = new Point(0, 88);
            pnlWorkspace.Name = "pnlWorkspace";
            pnlWorkspace.Padding = new Padding(28, 24, 28, 28);
            pnlWorkspace.Size = new Size(940, 632);
            pnlWorkspace.TabIndex = 1;
            pnlBranchesTable.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pnlBranchesTable.BackColor = Color.Transparent;
            pnlBranchesTable.BorderColor = Color.FromArgb(226, 232, 240);
            pnlBranchesTable.BorderRadius = 8;
            pnlBranchesTable.BorderThickness = 1;
            pnlBranchesTable.Controls.Add(dgvBranches);
            pnlBranchesTable.Controls.Add(lblTableSubtitle);
            pnlBranchesTable.Controls.Add(lblTableTitle);
            pnlBranchesTable.FillColor = Color.White;
            pnlBranchesTable.Location = new Point(28, 236);
            pnlBranchesTable.Name = "pnlBranchesTable";
            pnlBranchesTable.Size = new Size(884, 368);
            pnlBranchesTable.TabIndex = 1;
            dgvBranches.AllowUserToAddRows = false;
            dgvBranches.AllowUserToDeleteRows = false;
            dgvBranches.AllowUserToResizeRows = false;
            dataGridViewCellStyle1.BackColor = Color.FromArgb(248, 250, 252);
            dataGridViewCellStyle1.ForeColor = Color.FromArgb(30, 41, 59);
            dataGridViewCellStyle1.SelectionBackColor = Color.FromArgb(219, 234, 254);
            dataGridViewCellStyle1.SelectionForeColor = Color.FromArgb(30, 64, 175);
            dgvBranches.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            dgvBranches.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = Color.FromArgb(15, 23, 42);
            dataGridViewCellStyle2.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            dataGridViewCellStyle2.ForeColor = Color.White;
            dataGridViewCellStyle2.SelectionBackColor = Color.FromArgb(15, 23, 42);
            dataGridViewCellStyle2.SelectionForeColor = Color.White;
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.True;
            dgvBranches.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle2;
            dgvBranches.ColumnHeadersHeight = 44;
            dgvBranches.Columns.AddRange(new DataGridViewColumn[] { colBranchId, colBranchName });
            dataGridViewCellStyle3.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = Color.White;
            dataGridViewCellStyle3.Font = new Font("Segoe UI", 10F);
            dataGridViewCellStyle3.ForeColor = Color.FromArgb(30, 41, 59);
            dataGridViewCellStyle3.SelectionBackColor = Color.FromArgb(219, 234, 254);
            dataGridViewCellStyle3.SelectionForeColor = Color.FromArgb(30, 64, 175);
            dataGridViewCellStyle3.WrapMode = DataGridViewTriState.False;
            dgvBranches.DefaultCellStyle = dataGridViewCellStyle3;
            dgvBranches.GridColor = Color.FromArgb(226, 232, 240);
            dgvBranches.Location = new Point(24, 78);
            dgvBranches.MultiSelect = false;
            dgvBranches.Name = "dgvBranches";
            dgvBranches.ReadOnly = true;
            dgvBranches.RowHeadersVisible = false;
            dgvBranches.RowTemplate.Height = 42;
            dgvBranches.Size = new Size(836, 266);
            dgvBranches.TabIndex = 2;
            dgvBranches.ThemeStyle.AlternatingRowsStyle.BackColor = Color.FromArgb(248, 250, 252);
            dgvBranches.ThemeStyle.AlternatingRowsStyle.ForeColor = Color.FromArgb(30, 41, 59);
            dgvBranches.ThemeStyle.AlternatingRowsStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
            dgvBranches.ThemeStyle.AlternatingRowsStyle.SelectionForeColor = Color.FromArgb(30, 64, 175);
            dgvBranches.ThemeStyle.GridColor = Color.FromArgb(226, 232, 240);
            dgvBranches.ThemeStyle.HeaderStyle.BackColor = Color.FromArgb(15, 23, 42);
            dgvBranches.ThemeStyle.HeaderStyle.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            dgvBranches.ThemeStyle.HeaderStyle.HeaightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvBranches.ThemeStyle.HeaderStyle.Height = 44;
            dgvBranches.ThemeStyle.ReadOnly = true;
            dgvBranches.ThemeStyle.RowsStyle.Font = new Font("Segoe UI", 10F);
            dgvBranches.ThemeStyle.RowsStyle.ForeColor = Color.FromArgb(30, 41, 59);
            dgvBranches.ThemeStyle.RowsStyle.Height = 42;
            dgvBranches.ThemeStyle.RowsStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
            dgvBranches.ThemeStyle.RowsStyle.SelectionForeColor = Color.FromArgb(30, 64, 175);
            colBranchId.DataPropertyName = "BranchID";
            colBranchId.FillWeight = 34F;
            colBranchId.HeaderText = "Branch ID";
            colBranchId.Name = "colBranchId";
            colBranchId.ReadOnly = true;
            colBranchName.DataPropertyName = "BranchName";
            colBranchName.FillWeight = 166F;
            colBranchName.HeaderText = "Branch Name";
            colBranchName.Name = "colBranchName";
            colBranchName.ReadOnly = true;
            lblTableSubtitle.BackColor = Color.Transparent;
            lblTableSubtitle.Font = new Font("Segoe UI", 9F);
            lblTableSubtitle.ForeColor = Color.FromArgb(100, 116, 139);
            lblTableSubtitle.Location = new Point(24, 43);
            lblTableSubtitle.Name = "lblTableSubtitle";
            lblTableSubtitle.Size = new Size(235, 17);
            lblTableSubtitle.TabIndex = 1;
            lblTableSubtitle.Text = "Review and select academic branch records.";
            lblTableTitle.BackColor = Color.Transparent;
            lblTableTitle.Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold);
            lblTableTitle.ForeColor = Color.FromArgb(15, 23, 42);
            lblTableTitle.Location = new Point(24, 18);
            lblTableTitle.Name = "lblTableTitle";
            lblTableTitle.Size = new Size(102, 25);
            lblTableTitle.TabIndex = 0;
            lblTableTitle.Text = "Branches List";
            pnlBranchEditor.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnlBranchEditor.BackColor = Color.Transparent;
            pnlBranchEditor.BorderColor = Color.FromArgb(226, 232, 240);
            pnlBranchEditor.BorderRadius = 8;
            pnlBranchEditor.BorderThickness = 1;
            pnlBranchEditor.Controls.Add(btnClearBranchForm);
            pnlBranchEditor.Controls.Add(btnDeleteBranch);
            pnlBranchEditor.Controls.Add(btnUpdateBranch);
            pnlBranchEditor.Controls.Add(btnAddBranch);
            pnlBranchEditor.Controls.Add(txtBranchName);
            pnlBranchEditor.Controls.Add(lblBranchName);
            pnlBranchEditor.Controls.Add(txtBranchId);
            pnlBranchEditor.Controls.Add(lblBranchId);
            pnlBranchEditor.Controls.Add(lblEditorSubtitle);
            pnlBranchEditor.Controls.Add(lblEditorTitle);
            pnlBranchEditor.FillColor = Color.White;
            pnlBranchEditor.Location = new Point(28, 24);
            pnlBranchEditor.Name = "pnlBranchEditor";
            pnlBranchEditor.Size = new Size(884, 190);
            pnlBranchEditor.TabIndex = 0;
            btnClearBranchForm.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClearBranchForm.BorderColor = Color.FromArgb(203, 213, 225);
            btnClearBranchForm.BorderRadius = 8;
            btnClearBranchForm.BorderThickness = 1;
            btnClearBranchForm.Cursor = Cursors.Hand;
            btnClearBranchForm.FillColor = Color.White;
            btnClearBranchForm.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnClearBranchForm.ForeColor = Color.FromArgb(51, 65, 85);
            btnClearBranchForm.HoverState.FillColor = Color.FromArgb(248, 250, 252);
            btnClearBranchForm.Location = new Point(752, 135);
            btnClearBranchForm.Name = "btnClearBranchForm";
            btnClearBranchForm.Size = new Size(108, 38);
            btnClearBranchForm.TabIndex = 9;
            btnClearBranchForm.Text = "Clear";
            btnClearBranchForm.Visible = false;
            btnDeleteBranch.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnDeleteBranch.BorderRadius = 8;
            btnDeleteBranch.Cursor = Cursors.Hand;
            btnDeleteBranch.FillColor = Color.FromArgb(220, 38, 38);
            btnDeleteBranch.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnDeleteBranch.ForeColor = Color.White;
            btnDeleteBranch.HoverState.FillColor = Color.FromArgb(185, 28, 28);
            btnDeleteBranch.Location = new Point(632, 135);
            btnDeleteBranch.Name = "btnDeleteBranch";
            btnDeleteBranch.Size = new Size(108, 38);
            btnDeleteBranch.TabIndex = 8;
            btnDeleteBranch.Text = "Delete";
            btnUpdateBranch.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnUpdateBranch.BorderRadius = 8;
            btnUpdateBranch.Cursor = Cursors.Hand;
            btnUpdateBranch.FillColor = Color.FromArgb(14, 116, 144);
            btnUpdateBranch.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnUpdateBranch.ForeColor = Color.White;
            btnUpdateBranch.HoverState.FillColor = Color.FromArgb(21, 94, 117);
            btnUpdateBranch.Location = new Point(752, 91);
            btnUpdateBranch.Name = "btnUpdateBranch";
            btnUpdateBranch.Size = new Size(108, 38);
            btnUpdateBranch.TabIndex = 7;
            btnUpdateBranch.Text = "Update";
            btnAddBranch.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnAddBranch.BorderRadius = 8;
            btnAddBranch.Cursor = Cursors.Hand;
            btnAddBranch.FillColor = Color.FromArgb(22, 163, 74);
            btnAddBranch.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnAddBranch.ForeColor = Color.White;
            btnAddBranch.HoverState.FillColor = Color.FromArgb(21, 128, 61);
            btnAddBranch.Location = new Point(632, 91);
            btnAddBranch.Name = "btnAddBranch";
            btnAddBranch.Size = new Size(108, 38);
            btnAddBranch.TabIndex = 6;
            btnAddBranch.Text = "Add";
            txtBranchName.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtBranchName.BorderColor = Color.FromArgb(203, 213, 225);
            txtBranchName.BorderRadius = 8;
            txtBranchName.Cursor = Cursors.IBeam;
            txtBranchName.DefaultText = "";
            txtBranchName.DisabledState.BorderColor = Color.FromArgb(226, 232, 240);
            txtBranchName.DisabledState.FillColor = Color.FromArgb(248, 250, 252);
            txtBranchName.DisabledState.ForeColor = Color.FromArgb(100, 116, 139);
            txtBranchName.FocusedState.BorderColor = Color.FromArgb(37, 99, 235);
            txtBranchName.Font = new Font("Segoe UI", 10F);
            txtBranchName.ForeColor = Color.FromArgb(15, 23, 42);
            txtBranchName.HoverState.BorderColor = Color.FromArgb(59, 130, 246);
            txtBranchName.Location = new Point(240, 112);
            txtBranchName.Margin = new Padding(3, 4, 3, 4);
            txtBranchName.Name = "txtBranchName";
            txtBranchName.PlaceholderForeColor = Color.FromArgb(148, 163, 184);
            txtBranchName.PlaceholderText = "Enter branch name";
            txtBranchName.SelectedText = "";
            txtBranchName.Size = new Size(350, 42);
            txtBranchName.TabIndex = 5;
            lblBranchName.BackColor = Color.Transparent;
            lblBranchName.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            lblBranchName.ForeColor = Color.FromArgb(51, 65, 85);
            lblBranchName.Location = new Point(240, 87);
            lblBranchName.Name = "lblBranchName";
            lblBranchName.Size = new Size(85, 19);
            lblBranchName.TabIndex = 4;
            lblBranchName.Text = "Branch Name";
            txtBranchId.BorderColor = Color.FromArgb(226, 232, 240);
            txtBranchId.BorderRadius = 8;
            txtBranchId.Cursor = Cursors.IBeam;
            txtBranchId.DefaultText = "";
            txtBranchId.DisabledState.BorderColor = Color.FromArgb(226, 232, 240);
            txtBranchId.DisabledState.FillColor = Color.FromArgb(248, 250, 252);
            txtBranchId.DisabledState.ForeColor = Color.FromArgb(100, 116, 139);
            txtBranchId.Enabled = true;
            txtBranchId.FillColor = Color.White;
            txtBranchId.Font = new Font("Segoe UI", 10F);
            txtBranchId.ForeColor = Color.FromArgb(15, 23, 42);
            txtBranchId.Location = new Point(24, 112);
            txtBranchId.Margin = new Padding(3, 4, 3, 4);
            txtBranchId.Name = "txtBranchId";
            txtBranchId.PlaceholderForeColor = Color.FromArgb(148, 163, 184);
            txtBranchId.PlaceholderText = "Enter ID";
            txtBranchId.SelectedText = "";
            txtBranchId.Size = new Size(190, 42);
            txtBranchId.TabIndex = 3;
            lblBranchId.BackColor = Color.Transparent;
            lblBranchId.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            lblBranchId.ForeColor = Color.FromArgb(51, 65, 85);
            lblBranchId.Location = new Point(24, 87);
            lblBranchId.Name = "lblBranchId";
            lblBranchId.Size = new Size(62, 19);
            lblBranchId.TabIndex = 2;
            lblBranchId.Text = "Branch ID";
            lblEditorSubtitle.BackColor = Color.Transparent;
            lblEditorSubtitle.Font = new Font("Segoe UI", 9F);
            lblEditorSubtitle.ForeColor = Color.FromArgb(100, 116, 139);
            lblEditorSubtitle.Location = new Point(24, 44);
            lblEditorSubtitle.Name = "lblEditorSubtitle";
            lblEditorSubtitle.Size = new Size(261, 17);
            lblEditorSubtitle.TabIndex = 1;
            lblEditorSubtitle.Text = "Prepare branch details before applying an action.";
            lblEditorTitle.BackColor = Color.Transparent;
            lblEditorTitle.Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold);
            lblEditorTitle.ForeColor = Color.FromArgb(15, 23, 42);
            lblEditorTitle.Location = new Point(24, 18);
            lblEditorTitle.Name = "lblEditorTitle";
            lblEditorTitle.Size = new Size(112, 25);
            lblEditorTitle.TabIndex = 0;
            lblEditorTitle.Text = "Branch Details";
            pnlHeader.Controls.Add(lblPageSubtitle);
            pnlHeader.Controls.Add(lblPageTitle);
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.FillColor = Color.White;
            pnlHeader.Location = new Point(0, 0);
            pnlHeader.Name = "pnlHeader";
            pnlHeader.Size = new Size(940, 88);
            pnlHeader.TabIndex = 0;
            lblPageSubtitle.BackColor = Color.Transparent;
            lblPageSubtitle.Font = new Font("Segoe UI", 10F);
            lblPageSubtitle.ForeColor = Color.FromArgb(100, 116, 139);
            lblPageSubtitle.Location = new Point(32, 50);
            lblPageSubtitle.Name = "lblPageSubtitle";
            lblPageSubtitle.Size = new Size(297, 19);
            lblPageSubtitle.TabIndex = 1;
            lblPageSubtitle.Text = "Manage academic branches in a structured layout.";
            lblPageTitle.BackColor = Color.Transparent;
            lblPageTitle.Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold);
            lblPageTitle.ForeColor = Color.FromArgb(15, 23, 42);
            lblPageTitle.Location = new Point(32, 16);
            lblPageTitle.Name = "lblPageTitle";
            lblPageTitle.Size = new Size(255, 34);
            lblPageTitle.TabIndex = 0;
            lblPageTitle.Text = "Branches Management";
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(245, 247, 250);
            ClientSize = new Size(1180, 720);
            Controls.Add(pnlMain);
            Controls.Add(pnlSidebar);
            Font = new Font("Segoe UI", 9F);
            MinimumSize = new Size(980, 600);
            Name = "BranchesForm";
            Text = "Branches Management";
            pnlSidebar.ResumeLayout(false);
            pnlSidebar.PerformLayout();
            pnlMain.ResumeLayout(false);
            pnlWorkspace.ResumeLayout(false);
            pnlBranchesTable.ResumeLayout(false);
            pnlBranchesTable.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvBranches).EndInit();
            pnlBranchEditor.ResumeLayout(false);
            pnlBranchEditor.PerformLayout();
            pnlHeader.ResumeLayout(false);
            pnlHeader.PerformLayout();
            ResumeLayout(false);
        }
        private Guna.UI2.WinForms.Guna2Panel pnlSidebar = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblApplicationName = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblSidebarSubtitle = null!;
        private Guna.UI2.WinForms.Guna2Separator separatorSidebar = null!;
        private Guna.UI2.WinForms.Guna2Button btnNavigationBranches = null!;
        private Guna.UI2.WinForms.Guna2Button btnNavigationStudyYears = null!;
        private Guna.UI2.WinForms.Guna2Button btnNavigationClassrooms = null!;
        private Guna.UI2.WinForms.Guna2Button btnNavigationFaculty = null!;
        private Guna.UI2.WinForms.Guna2Button btnNavigationSchedules = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblSidebarFooter = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlMain = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlHeader = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblPageTitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblPageSubtitle = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlWorkspace = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlBranchEditor = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblEditorTitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblEditorSubtitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblBranchId = null!;
        private Guna.UI2.WinForms.Guna2TextBox txtBranchId = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblBranchName = null!;
        private Guna.UI2.WinForms.Guna2TextBox txtBranchName = null!;
        private Guna.UI2.WinForms.Guna2Button btnAddBranch = null!;
        private Guna.UI2.WinForms.Guna2Button btnUpdateBranch = null!;
        private Guna.UI2.WinForms.Guna2Button btnDeleteBranch = null!;
        private Guna.UI2.WinForms.Guna2Button btnClearBranchForm = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlBranchesTable = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblTableTitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblTableSubtitle = null!;
        private Guna.UI2.WinForms.Guna2DataGridView dgvBranches = null!;
        private System.Windows.Forms.DataGridViewTextBoxColumn colBranchId = null!;
        private System.Windows.Forms.DataGridViewTextBoxColumn colBranchName = null!;
    }
}

