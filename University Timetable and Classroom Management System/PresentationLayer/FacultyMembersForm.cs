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
            ConfigureAutoIdField();
            ConfigureNavigation();
            ConfigureFacultyMembersGrid();
            ConfigureFacultyMembersEvents();
        }

        private void ConfigureAutoIdField()
        {
            txtFacultyMemberId.ReadOnly = true;
            txtFacultyMemberId.TabStop = false;
            txtFacultyMemberId.PlaceholderText = "Auto";
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await LoadFacultyMembersAsync();
            ClearFacultyMemberForm();
        }

        private void ConfigureNavigation()
        {
            FormNavigation.ConfigureSidebar(this, pnlSidebar, NavigationPage.FacultyMembers);
        }

        private void ConfigureFacultyMembersGrid()
        {
            dgvFacultyMembers.AutoGenerateColumns = false;
            dgvFacultyMembers.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            GridStyle.Apply(dgvFacultyMembers);
        }

        private void ConfigureFacultyMembersEvents()
        {
            dgvFacultyMembers.SelectionChanged += (_, _) => PopulateFacultyMemberEditorFromSelection();
            txtFacultyMemberId.Leave += async (_, _) => await PopulateFacultyMemberEditorFromEnteredIdAsync();
            btnAddFacultyMember.Click += async (_, _) => await AddFacultyMemberAsync();
            btnUpdateFacultyMember.Click += async (_, _) => await UpdateFacultyMemberAsync();
            btnDeleteFacultyMember.Click += async (_, _) => await DeleteFacultyMemberAsync();
        }

        private async Task LoadFacultyMembersAsync()
        {
            SetFacultyMemberActionsEnabled(false);

            try
            {
                var facultyMembers = await facultyMemberService.GetAllAsync();
                dgvFacultyMembers.DataSource = facultyMembers;
                dgvFacultyMembers.ClearSelection();
            }
            catch (Exception ex)
            {
                ShowError("Unable to load faculty members.", ex);
            }
            finally
            {
                SetFacultyMemberActionsEnabled(true);
            }
        }

        private async Task AddFacultyMemberAsync()
        {
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
            if (!TryGetFacultyMemberIdFromEditor(out int facultyMemberId))
            {
                return;
            }

            var confirmation = UiMessages.ConfirmDeletion(this, "Delete Faculty Member");

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
            SetFacultyMemberActionsEnabled(false);

            try
            {
                await action();
                await LoadFacultyMembersAsync();
                ClearFacultyMemberForm();
                ShowInformation(successMessage);
            }
            catch (Exception ex)
            {
                ShowError("Unable to complete the faculty member operation.", ex);
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
                AcademicTitle = cmbAcademicTitle.Text.Trim()
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

            ShowInformation("Select a faculty member row first.");
            return false;
        }

        private void PopulateFacultyMemberEditorFromSelection()
        {
            if (dgvFacultyMembers.CurrentRow?.DataBoundItem is not FacultyMember facultyMember)
            {
                return;
            }

            txtFacultyMemberId.Text = facultyMember.FacultyMemberID.ToString();
            txtFacultyMemberFullName.Text = facultyMember.FullName;
            cmbAcademicTitle.Text = facultyMember.AcademicTitle ?? string.Empty;
        }

        private async Task PopulateFacultyMemberEditorFromEnteredIdAsync()
        {
            if (!int.TryParse(txtFacultyMemberId.Text, out int facultyMemberId) || facultyMemberId <= 0)
            {
                return;
            }

            try
            {
                var facultyMember = await facultyMemberService.GetByIdAsync(facultyMemberId);

                if (facultyMember is null)
                {
                    return;
                }

                txtFacultyMemberFullName.Text = facultyMember.FullName;
                cmbAcademicTitle.Text = facultyMember.AcademicTitle ?? string.Empty;
                SelectFacultyMemberRow(facultyMember.FacultyMemberID);
            }
            catch (Exception ex)
            {
                ShowError("Unable to load faculty member details.", ex);
            }
        }

        private void SelectFacultyMemberRow(int facultyMemberId)
        {
            foreach (DataGridViewRow row in dgvFacultyMembers.Rows)
            {
                if (row.DataBoundItem is not FacultyMember facultyMember || facultyMember.FacultyMemberID != facultyMemberId)
                {
                    continue;
                }

                row.Selected = true;
                dgvFacultyMembers.CurrentCell = row.Cells[0];
                break;
            }
        }

        private void ClearFacultyMemberForm()
        {
            txtFacultyMemberId.Text = "Auto";
            txtFacultyMemberFullName.Clear();
            cmbAcademicTitle.SelectedIndex = -1;
            dgvFacultyMembers.ClearSelection();
            txtFacultyMemberFullName.Focus();
        }

        private void SetFacultyMemberActionsEnabled(bool enabled)
        {
            btnAddFacultyMember.Enabled = enabled;
            btnUpdateFacultyMember.Enabled = enabled;
            btnDeleteFacultyMember.Enabled = enabled;
            dgvFacultyMembers.Enabled = enabled;
        }

        private void ShowInformation(string message)
        {
            UiMessages.ShowInformation(this, message, "Faculty Members");
        }

        private void ShowError(string message, Exception ex)
        {
            UiMessages.ShowError(this, message, "Faculty Members", ex);
        }
        private void InitializeComponent()
        {
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
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
            pnlFacultyMembersTable = new Guna.UI2.WinForms.Guna2Panel();
            dgvFacultyMembers = new Guna.UI2.WinForms.Guna2DataGridView();
            colFacultyMemberId = new System.Windows.Forms.DataGridViewTextBoxColumn();
            colFullName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            colAcademicTitle = new System.Windows.Forms.DataGridViewTextBoxColumn();
            lblTableSubtitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblTableTitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            pnlFacultyMemberEditor = new Guna.UI2.WinForms.Guna2Panel();
            btnClearFacultyMemberForm = new Guna.UI2.WinForms.Guna2Button();
            btnDeleteFacultyMember = new Guna.UI2.WinForms.Guna2Button();
            btnUpdateFacultyMember = new Guna.UI2.WinForms.Guna2Button();
            btnAddFacultyMember = new Guna.UI2.WinForms.Guna2Button();
            cmbAcademicTitle = new Guna.UI2.WinForms.Guna2ComboBox();
            lblAcademicTitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            txtFacultyMemberFullName = new Guna.UI2.WinForms.Guna2TextBox();
            lblFullName = new Guna.UI2.WinForms.Guna2HtmlLabel();
            txtFacultyMemberId = new Guna.UI2.WinForms.Guna2TextBox();
            lblFacultyMemberId = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblEditorSubtitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblEditorTitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            pnlHeader = new Guna.UI2.WinForms.Guna2Panel();
            lblPageSubtitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblPageTitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            pnlSidebar.SuspendLayout();
            pnlMain.SuspendLayout();
            pnlWorkspace.SuspendLayout();
            pnlFacultyMembersTable.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvFacultyMembers).BeginInit();
            pnlFacultyMemberEditor.SuspendLayout();
            pnlHeader.SuspendLayout();
            SuspendLayout();
            pnlSidebar.BackColor = System.Drawing.Color.Transparent;
            pnlSidebar.Controls.Add(lblSidebarFooter);
            pnlSidebar.Controls.Add(btnNavigationSchedules);
            pnlSidebar.Controls.Add(btnNavigationFaculty);
            pnlSidebar.Controls.Add(btnNavigationClassrooms);
            pnlSidebar.Controls.Add(btnNavigationStudyYears);
            pnlSidebar.Controls.Add(btnNavigationBranches);
            pnlSidebar.Controls.Add(separatorSidebar);
            pnlSidebar.Controls.Add(lblSidebarSubtitle);
            pnlSidebar.Controls.Add(lblApplicationName);
            pnlSidebar.Dock = System.Windows.Forms.DockStyle.Left;
            pnlSidebar.FillColor = System.Drawing.Color.FromArgb(24, 38, 62);
            pnlSidebar.Location = new System.Drawing.Point(0, 0);
            pnlSidebar.Name = "pnlSidebar";
            pnlSidebar.Size = new System.Drawing.Size(240, 720);
            pnlSidebar.TabIndex = 0;
            lblSidebarFooter.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            lblSidebarFooter.BackColor = System.Drawing.Color.Transparent;
            lblSidebarFooter.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblSidebarFooter.ForeColor = System.Drawing.Color.FromArgb(148, 163, 184);
            lblSidebarFooter.Location = new System.Drawing.Point(24, 671);
            lblSidebarFooter.Name = "lblSidebarFooter";
            lblSidebarFooter.Size = new System.Drawing.Size(146, 17);
            lblSidebarFooter.TabIndex = 9;
            lblSidebarFooter.Text = "Academic Scheduling Suite";
            btnNavigationSchedules.BorderRadius = 8;
            btnNavigationSchedules.Cursor = System.Windows.Forms.Cursors.Hand;
            btnNavigationSchedules.FillColor = System.Drawing.Color.FromArgb(24, 38, 62);
            btnNavigationSchedules.Font = new System.Drawing.Font("Segoe UI Semibold", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnNavigationSchedules.ForeColor = System.Drawing.Color.FromArgb(226, 232, 240);
            btnNavigationSchedules.HoverState.FillColor = System.Drawing.Color.FromArgb(36, 55, 86);
            btnNavigationSchedules.Location = new System.Drawing.Point(24, 378);
            btnNavigationSchedules.Name = "btnNavigationSchedules";
            btnNavigationSchedules.Size = new System.Drawing.Size(192, 44);
            btnNavigationSchedules.TabIndex = 8;
            btnNavigationSchedules.Text = "Schedules";
            btnNavigationSchedules.TextAlign = System.Windows.Forms.HorizontalAlignment.Left;
            btnNavigationSchedules.TextOffset = new System.Drawing.Point(14, 0);
            btnNavigationFaculty.BorderRadius = 8;
            btnNavigationFaculty.Checked = true;
            btnNavigationFaculty.Cursor = System.Windows.Forms.Cursors.Hand;
            btnNavigationFaculty.FillColor = System.Drawing.Color.FromArgb(37, 99, 235);
            btnNavigationFaculty.Font = new System.Drawing.Font("Segoe UI Semibold", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnNavigationFaculty.ForeColor = System.Drawing.Color.White;
            btnNavigationFaculty.HoverState.FillColor = System.Drawing.Color.FromArgb(29, 78, 216);
            btnNavigationFaculty.Location = new System.Drawing.Point(24, 322);
            btnNavigationFaculty.Name = "btnNavigationFaculty";
            btnNavigationFaculty.Size = new System.Drawing.Size(192, 44);
            btnNavigationFaculty.TabIndex = 7;
            btnNavigationFaculty.Text = "Faculty Members";
            btnNavigationFaculty.TextAlign = System.Windows.Forms.HorizontalAlignment.Left;
            btnNavigationFaculty.TextOffset = new System.Drawing.Point(14, 0);
            btnNavigationClassrooms.BorderRadius = 8;
            btnNavigationClassrooms.Cursor = System.Windows.Forms.Cursors.Hand;
            btnNavigationClassrooms.FillColor = System.Drawing.Color.FromArgb(24, 38, 62);
            btnNavigationClassrooms.Font = new System.Drawing.Font("Segoe UI Semibold", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnNavigationClassrooms.ForeColor = System.Drawing.Color.FromArgb(226, 232, 240);
            btnNavigationClassrooms.HoverState.FillColor = System.Drawing.Color.FromArgb(36, 55, 86);
            btnNavigationClassrooms.Location = new System.Drawing.Point(24, 266);
            btnNavigationClassrooms.Name = "btnNavigationClassrooms";
            btnNavigationClassrooms.Size = new System.Drawing.Size(192, 44);
            btnNavigationClassrooms.TabIndex = 6;
            btnNavigationClassrooms.Text = "Classrooms";
            btnNavigationClassrooms.TextAlign = System.Windows.Forms.HorizontalAlignment.Left;
            btnNavigationClassrooms.TextOffset = new System.Drawing.Point(14, 0);
            btnNavigationStudyYears.BorderRadius = 8;
            btnNavigationStudyYears.Cursor = System.Windows.Forms.Cursors.Hand;
            btnNavigationStudyYears.FillColor = System.Drawing.Color.FromArgb(24, 38, 62);
            btnNavigationStudyYears.Font = new System.Drawing.Font("Segoe UI Semibold", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnNavigationStudyYears.ForeColor = System.Drawing.Color.FromArgb(226, 232, 240);
            btnNavigationStudyYears.HoverState.FillColor = System.Drawing.Color.FromArgb(36, 55, 86);
            btnNavigationStudyYears.Location = new System.Drawing.Point(24, 210);
            btnNavigationStudyYears.Name = "btnNavigationStudyYears";
            btnNavigationStudyYears.Size = new System.Drawing.Size(192, 44);
            btnNavigationStudyYears.TabIndex = 5;
            btnNavigationStudyYears.Text = "Study Years";
            btnNavigationStudyYears.TextAlign = System.Windows.Forms.HorizontalAlignment.Left;
            btnNavigationStudyYears.TextOffset = new System.Drawing.Point(14, 0);
            btnNavigationBranches.BorderRadius = 8;
            btnNavigationBranches.Cursor = System.Windows.Forms.Cursors.Hand;
            btnNavigationBranches.FillColor = System.Drawing.Color.FromArgb(24, 38, 62);
            btnNavigationBranches.Font = new System.Drawing.Font("Segoe UI Semibold", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnNavigationBranches.ForeColor = System.Drawing.Color.FromArgb(226, 232, 240);
            btnNavigationBranches.HoverState.FillColor = System.Drawing.Color.FromArgb(36, 55, 86);
            btnNavigationBranches.Location = new System.Drawing.Point(24, 154);
            btnNavigationBranches.Name = "btnNavigationBranches";
            btnNavigationBranches.Size = new System.Drawing.Size(192, 44);
            btnNavigationBranches.TabIndex = 4;
            btnNavigationBranches.Text = "Branches";
            btnNavigationBranches.TextAlign = System.Windows.Forms.HorizontalAlignment.Left;
            btnNavigationBranches.TextOffset = new System.Drawing.Point(14, 0);
            separatorSidebar.FillColor = System.Drawing.Color.FromArgb(51, 65, 85);
            separatorSidebar.Location = new System.Drawing.Point(24, 78);
            separatorSidebar.Name = "separatorSidebar";
            separatorSidebar.Size = new System.Drawing.Size(192, 10);
            separatorSidebar.TabIndex = 2;
            lblSidebarSubtitle.BackColor = System.Drawing.Color.Transparent;
            lblSidebarSubtitle.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblSidebarSubtitle.ForeColor = System.Drawing.Color.FromArgb(148, 163, 184);
            lblSidebarSubtitle.Location = new System.Drawing.Point(26, 52);
            lblSidebarSubtitle.Name = "lblSidebarSubtitle";
            lblSidebarSubtitle.Size = new System.Drawing.Size(130, 17);
            lblSidebarSubtitle.TabIndex = 1;
            lblSidebarSubtitle.Text = "Classroom Management";
            lblApplicationName.BackColor = System.Drawing.Color.Transparent;
            lblApplicationName.Font = new System.Drawing.Font("Segoe UI Semibold", 17F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblApplicationName.ForeColor = System.Drawing.Color.White;
            lblApplicationName.Location = new System.Drawing.Point(24, 20);
            lblApplicationName.Name = "lblApplicationName";
            lblApplicationName.Size = new System.Drawing.Size(206, 33);
            lblApplicationName.TabIndex = 0;
            lblApplicationName.Text = "University Timetable";
            pnlMain.Controls.Add(pnlWorkspace);
            pnlMain.Controls.Add(pnlHeader);
            pnlMain.Dock = System.Windows.Forms.DockStyle.Fill;
            pnlMain.FillColor = System.Drawing.Color.FromArgb(245, 247, 250);
            pnlMain.Location = new System.Drawing.Point(240, 0);
            pnlMain.Name = "pnlMain";
            pnlMain.Size = new System.Drawing.Size(940, 720);
            pnlMain.TabIndex = 1;
            pnlWorkspace.Controls.Add(pnlFacultyMembersTable);
            pnlWorkspace.Controls.Add(pnlFacultyMemberEditor);
            pnlWorkspace.Dock = System.Windows.Forms.DockStyle.Fill;
            pnlWorkspace.FillColor = System.Drawing.Color.FromArgb(245, 247, 250);
            pnlWorkspace.Location = new System.Drawing.Point(0, 88);
            pnlWorkspace.Name = "pnlWorkspace";
            pnlWorkspace.Padding = new System.Windows.Forms.Padding(28, 24, 28, 28);
            pnlWorkspace.Size = new System.Drawing.Size(940, 632);
            pnlWorkspace.TabIndex = 1;
            pnlFacultyMembersTable.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            pnlFacultyMembersTable.BackColor = System.Drawing.Color.Transparent;
            pnlFacultyMembersTable.BorderColor = System.Drawing.Color.FromArgb(226, 232, 240);
            pnlFacultyMembersTable.BorderRadius = 8;
            pnlFacultyMembersTable.BorderThickness = 1;
            pnlFacultyMembersTable.Controls.Add(dgvFacultyMembers);
            pnlFacultyMembersTable.Controls.Add(lblTableSubtitle);
            pnlFacultyMembersTable.Controls.Add(lblTableTitle);
            pnlFacultyMembersTable.FillColor = System.Drawing.Color.White;
            pnlFacultyMembersTable.Location = new System.Drawing.Point(28, 248);
            pnlFacultyMembersTable.Name = "pnlFacultyMembersTable";
            pnlFacultyMembersTable.Size = new System.Drawing.Size(884, 356);
            pnlFacultyMembersTable.TabIndex = 1;
            dgvFacultyMembers.AllowUserToAddRows = false;
            dgvFacultyMembers.AllowUserToDeleteRows = false;
            dgvFacultyMembers.AllowUserToResizeRows = false;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.FromArgb(248, 250, 252);
            dataGridViewCellStyle1.ForeColor = System.Drawing.Color.FromArgb(30, 41, 59);
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.Color.FromArgb(219, 234, 254);
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.Color.FromArgb(30, 64, 175);
            dgvFacultyMembers.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            dgvFacultyMembers.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            dgvFacultyMembers.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dgvFacultyMembers.BackgroundColor = System.Drawing.Color.White;
            dgvFacultyMembers.BorderStyle = System.Windows.Forms.BorderStyle.None;
            dgvFacultyMembers.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleHorizontal;
            dgvFacultyMembers.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.Color.FromArgb(15, 23, 42);
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Segoe UI Semibold", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            dataGridViewCellStyle2.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.Color.FromArgb(15, 23, 42);
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            dgvFacultyMembers.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle2;
            dgvFacultyMembers.ColumnHeadersHeight = 44;
            dgvFacultyMembers.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvFacultyMembers.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { colFacultyMemberId, colFullName, colAcademicTitle });
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.Color.White;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            dataGridViewCellStyle3.ForeColor = System.Drawing.Color.FromArgb(30, 41, 59);
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.Color.FromArgb(219, 234, 254);
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.Color.FromArgb(30, 64, 175);
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            dgvFacultyMembers.DefaultCellStyle = dataGridViewCellStyle3;
            dgvFacultyMembers.EnableHeadersVisualStyles = false;
            dgvFacultyMembers.GridColor = System.Drawing.Color.FromArgb(226, 232, 240);
            dgvFacultyMembers.Location = new System.Drawing.Point(24, 78);
            dgvFacultyMembers.MultiSelect = false;
            dgvFacultyMembers.Name = "dgvFacultyMembers";
            dgvFacultyMembers.ReadOnly = true;
            dgvFacultyMembers.RowHeadersVisible = false;
            dgvFacultyMembers.RowTemplate.Height = 42;
            dgvFacultyMembers.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            dgvFacultyMembers.Size = new System.Drawing.Size(836, 254);
            dgvFacultyMembers.TabIndex = 2;
            colFacultyMemberId.DataPropertyName = "FacultyMemberID";
            colFacultyMemberId.FillWeight = 34F;
            colFacultyMemberId.HeaderText = "Faculty ID";
            colFacultyMemberId.Name = "colFacultyMemberId";
            colFacultyMemberId.ReadOnly = true;
            colFullName.DataPropertyName = "FullName";
            colFullName.FillWeight = 116F;
            colFullName.HeaderText = "Full Name";
            colFullName.Name = "colFullName";
            colFullName.ReadOnly = true;
            colAcademicTitle.DataPropertyName = "AcademicTitle";
            colAcademicTitle.FillWeight = 80F;
            colAcademicTitle.HeaderText = "Academic Title";
            colAcademicTitle.Name = "colAcademicTitle";
            colAcademicTitle.ReadOnly = true;
            lblTableSubtitle.BackColor = System.Drawing.Color.Transparent;
            lblTableSubtitle.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblTableSubtitle.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            lblTableSubtitle.Location = new System.Drawing.Point(24, 43);
            lblTableSubtitle.Name = "lblTableSubtitle";
            lblTableSubtitle.Size = new System.Drawing.Size(275, 17);
            lblTableSubtitle.TabIndex = 1;
            lblTableSubtitle.Text = "Review and select faculty member records.";
            lblTableTitle.BackColor = System.Drawing.Color.Transparent;
            lblTableTitle.Font = new System.Drawing.Font("Segoe UI Semibold", 13F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblTableTitle.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            lblTableTitle.Location = new System.Drawing.Point(24, 18);
            lblTableTitle.Name = "lblTableTitle";
            lblTableTitle.Size = new System.Drawing.Size(166, 25);
            lblTableTitle.TabIndex = 0;
            lblTableTitle.Text = "Faculty Members List";
            pnlFacultyMemberEditor.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            pnlFacultyMemberEditor.BackColor = System.Drawing.Color.Transparent;
            pnlFacultyMemberEditor.BorderColor = System.Drawing.Color.FromArgb(226, 232, 240);
            pnlFacultyMemberEditor.BorderRadius = 8;
            pnlFacultyMemberEditor.BorderThickness = 1;
            pnlFacultyMemberEditor.Controls.Add(btnClearFacultyMemberForm);
            pnlFacultyMemberEditor.Controls.Add(btnDeleteFacultyMember);
            pnlFacultyMemberEditor.Controls.Add(btnUpdateFacultyMember);
            pnlFacultyMemberEditor.Controls.Add(btnAddFacultyMember);
            pnlFacultyMemberEditor.Controls.Add(cmbAcademicTitle);
            pnlFacultyMemberEditor.Controls.Add(lblAcademicTitle);
            pnlFacultyMemberEditor.Controls.Add(txtFacultyMemberFullName);
            pnlFacultyMemberEditor.Controls.Add(lblFullName);
            pnlFacultyMemberEditor.Controls.Add(txtFacultyMemberId);
            pnlFacultyMemberEditor.Controls.Add(lblFacultyMemberId);
            pnlFacultyMemberEditor.Controls.Add(lblEditorSubtitle);
            pnlFacultyMemberEditor.Controls.Add(lblEditorTitle);
            pnlFacultyMemberEditor.FillColor = System.Drawing.Color.White;
            pnlFacultyMemberEditor.Location = new System.Drawing.Point(28, 24);
            pnlFacultyMemberEditor.Name = "pnlFacultyMemberEditor";
            pnlFacultyMemberEditor.Size = new System.Drawing.Size(884, 202);
            pnlFacultyMemberEditor.TabIndex = 0;
            btnClearFacultyMemberForm.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnClearFacultyMemberForm.BorderColor = System.Drawing.Color.FromArgb(203, 213, 225);
            btnClearFacultyMemberForm.BorderRadius = 8;
            btnClearFacultyMemberForm.BorderThickness = 1;
            btnClearFacultyMemberForm.Cursor = System.Windows.Forms.Cursors.Hand;
            btnClearFacultyMemberForm.FillColor = System.Drawing.Color.White;
            btnClearFacultyMemberForm.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnClearFacultyMemberForm.ForeColor = System.Drawing.Color.FromArgb(51, 65, 85);
            btnClearFacultyMemberForm.HoverState.FillColor = System.Drawing.Color.FromArgb(248, 250, 252);
            btnClearFacultyMemberForm.Location = new System.Drawing.Point(752, 142);
            btnClearFacultyMemberForm.Name = "btnClearFacultyMemberForm";
            btnClearFacultyMemberForm.Size = new System.Drawing.Size(108, 38);
            btnClearFacultyMemberForm.TabIndex = 11;
            btnClearFacultyMemberForm.Text = "Clear";
            btnClearFacultyMemberForm.Visible = false;
            btnDeleteFacultyMember.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnDeleteFacultyMember.BorderRadius = 8;
            btnDeleteFacultyMember.Cursor = System.Windows.Forms.Cursors.Hand;
            btnDeleteFacultyMember.FillColor = System.Drawing.Color.FromArgb(220, 38, 38);
            btnDeleteFacultyMember.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnDeleteFacultyMember.ForeColor = System.Drawing.Color.White;
            btnDeleteFacultyMember.HoverState.FillColor = System.Drawing.Color.FromArgb(185, 28, 28);
            btnDeleteFacultyMember.Location = new System.Drawing.Point(632, 142);
            btnDeleteFacultyMember.Name = "btnDeleteFacultyMember";
            btnDeleteFacultyMember.Size = new System.Drawing.Size(108, 38);
            btnDeleteFacultyMember.TabIndex = 10;
            btnDeleteFacultyMember.Text = "Delete";
            btnUpdateFacultyMember.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnUpdateFacultyMember.BorderRadius = 8;
            btnUpdateFacultyMember.Cursor = System.Windows.Forms.Cursors.Hand;
            btnUpdateFacultyMember.FillColor = System.Drawing.Color.FromArgb(14, 116, 144);
            btnUpdateFacultyMember.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnUpdateFacultyMember.ForeColor = System.Drawing.Color.White;
            btnUpdateFacultyMember.HoverState.FillColor = System.Drawing.Color.FromArgb(21, 94, 117);
            btnUpdateFacultyMember.Location = new System.Drawing.Point(752, 98);
            btnUpdateFacultyMember.Name = "btnUpdateFacultyMember";
            btnUpdateFacultyMember.Size = new System.Drawing.Size(108, 38);
            btnUpdateFacultyMember.TabIndex = 9;
            btnUpdateFacultyMember.Text = "Update";
            btnAddFacultyMember.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnAddFacultyMember.BorderRadius = 8;
            btnAddFacultyMember.Cursor = System.Windows.Forms.Cursors.Hand;
            btnAddFacultyMember.FillColor = System.Drawing.Color.FromArgb(22, 163, 74);
            btnAddFacultyMember.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnAddFacultyMember.ForeColor = System.Drawing.Color.White;
            btnAddFacultyMember.HoverState.FillColor = System.Drawing.Color.FromArgb(21, 128, 61);
            btnAddFacultyMember.Location = new System.Drawing.Point(632, 98);
            btnAddFacultyMember.Name = "btnAddFacultyMember";
            btnAddFacultyMember.Size = new System.Drawing.Size(108, 38);
            btnAddFacultyMember.TabIndex = 8;
            btnAddFacultyMember.Text = "Add";
            cmbAcademicTitle.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            cmbAcademicTitle.BackColor = System.Drawing.Color.Transparent;
            cmbAcademicTitle.BorderColor = System.Drawing.Color.FromArgb(203, 213, 225);
            cmbAcademicTitle.BorderRadius = 8;
            cmbAcademicTitle.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            cmbAcademicTitle.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbAcademicTitle.FocusedColor = System.Drawing.Color.FromArgb(37, 99, 235);
            cmbAcademicTitle.FocusedState.BorderColor = System.Drawing.Color.FromArgb(37, 99, 235);
            cmbAcademicTitle.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            cmbAcademicTitle.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            cmbAcademicTitle.HoverState.BorderColor = System.Drawing.Color.FromArgb(59, 130, 246);
            cmbAcademicTitle.ItemHeight = 36;
            cmbAcademicTitle.Items.AddRange(new object[] { "Professor", "Associate Professor", "Assistant Professor", "Lecturer", "Assistant Lecturer" });
            cmbAcademicTitle.Location = new System.Drawing.Point(402, 112);
            cmbAcademicTitle.Name = "cmbAcademicTitle";
            cmbAcademicTitle.Size = new System.Drawing.Size(190, 42);
            cmbAcademicTitle.TabIndex = 7;
            lblAcademicTitle.BackColor = System.Drawing.Color.Transparent;
            lblAcademicTitle.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblAcademicTitle.ForeColor = System.Drawing.Color.FromArgb(51, 65, 85);
            lblAcademicTitle.Location = new System.Drawing.Point(402, 87);
            lblAcademicTitle.Name = "lblAcademicTitle";
            lblAcademicTitle.Size = new System.Drawing.Size(89, 19);
            lblAcademicTitle.TabIndex = 6;
            lblAcademicTitle.Text = "Academic Title";
            txtFacultyMemberFullName.BorderColor = System.Drawing.Color.FromArgb(203, 213, 225);
            txtFacultyMemberFullName.BorderRadius = 8;
            txtFacultyMemberFullName.Cursor = System.Windows.Forms.Cursors.IBeam;
            txtFacultyMemberFullName.DefaultText = "";
            txtFacultyMemberFullName.DisabledState.BorderColor = System.Drawing.Color.FromArgb(226, 232, 240);
            txtFacultyMemberFullName.DisabledState.FillColor = System.Drawing.Color.FromArgb(248, 250, 252);
            txtFacultyMemberFullName.DisabledState.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            txtFacultyMemberFullName.FocusedState.BorderColor = System.Drawing.Color.FromArgb(37, 99, 235);
            txtFacultyMemberFullName.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtFacultyMemberFullName.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            txtFacultyMemberFullName.HoverState.BorderColor = System.Drawing.Color.FromArgb(59, 130, 246);
            txtFacultyMemberFullName.Location = new System.Drawing.Point(184, 112);
            txtFacultyMemberFullName.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            txtFacultyMemberFullName.Name = "txtFacultyMemberFullName";
            txtFacultyMemberFullName.PasswordChar = '\0';
            txtFacultyMemberFullName.PlaceholderForeColor = System.Drawing.Color.FromArgb(148, 163, 184);
            txtFacultyMemberFullName.PlaceholderText = "Enter full name";
            txtFacultyMemberFullName.SelectedText = "";
            txtFacultyMemberFullName.Size = new System.Drawing.Size(190, 42);
            txtFacultyMemberFullName.TabIndex = 5;
            lblFullName.BackColor = System.Drawing.Color.Transparent;
            lblFullName.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblFullName.ForeColor = System.Drawing.Color.FromArgb(51, 65, 85);
            lblFullName.Location = new System.Drawing.Point(184, 87);
            lblFullName.Name = "lblFullName";
            lblFullName.Size = new System.Drawing.Size(62, 19);
            lblFullName.TabIndex = 4;
            lblFullName.Text = "Full Name";
            txtFacultyMemberId.BorderColor = System.Drawing.Color.FromArgb(226, 232, 240);
            txtFacultyMemberId.BorderRadius = 8;
            txtFacultyMemberId.Cursor = System.Windows.Forms.Cursors.IBeam;
            txtFacultyMemberId.DefaultText = "";
            txtFacultyMemberId.DisabledState.BorderColor = System.Drawing.Color.FromArgb(226, 232, 240);
            txtFacultyMemberId.DisabledState.FillColor = System.Drawing.Color.FromArgb(248, 250, 252);
            txtFacultyMemberId.DisabledState.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            txtFacultyMemberId.Enabled = true;
            txtFacultyMemberId.FillColor = System.Drawing.Color.White;
            txtFacultyMemberId.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtFacultyMemberId.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            txtFacultyMemberId.Location = new System.Drawing.Point(24, 112);
            txtFacultyMemberId.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            txtFacultyMemberId.Name = "txtFacultyMemberId";
            txtFacultyMemberId.PasswordChar = '\0';
            txtFacultyMemberId.PlaceholderForeColor = System.Drawing.Color.FromArgb(148, 163, 184);
            txtFacultyMemberId.PlaceholderText = "Enter ID";
            txtFacultyMemberId.SelectedText = "";
            txtFacultyMemberId.Size = new System.Drawing.Size(132, 42);
            txtFacultyMemberId.TabIndex = 3;
            lblFacultyMemberId.BackColor = System.Drawing.Color.Transparent;
            lblFacultyMemberId.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblFacultyMemberId.ForeColor = System.Drawing.Color.FromArgb(51, 65, 85);
            lblFacultyMemberId.Location = new System.Drawing.Point(24, 87);
            lblFacultyMemberId.Name = "lblFacultyMemberId";
            lblFacultyMemberId.Size = new System.Drawing.Size(63, 19);
            lblFacultyMemberId.TabIndex = 2;
            lblFacultyMemberId.Text = "Faculty ID";
            lblEditorSubtitle.BackColor = System.Drawing.Color.Transparent;
            lblEditorSubtitle.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblEditorSubtitle.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            lblEditorSubtitle.Location = new System.Drawing.Point(24, 44);
            lblEditorSubtitle.Name = "lblEditorSubtitle";
            lblEditorSubtitle.Size = new System.Drawing.Size(322, 17);
            lblEditorSubtitle.TabIndex = 1;
            lblEditorSubtitle.Text = "Prepare faculty member details before applying an action.";
            lblEditorTitle.BackColor = System.Drawing.Color.Transparent;
            lblEditorTitle.Font = new System.Drawing.Font("Segoe UI Semibold", 13F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblEditorTitle.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            lblEditorTitle.Location = new System.Drawing.Point(24, 18);
            lblEditorTitle.Name = "lblEditorTitle";
            lblEditorTitle.Size = new System.Drawing.Size(185, 25);
            lblEditorTitle.TabIndex = 0;
            lblEditorTitle.Text = "Faculty Member Details";
            pnlHeader.Controls.Add(lblPageSubtitle);
            pnlHeader.Controls.Add(lblPageTitle);
            pnlHeader.Dock = System.Windows.Forms.DockStyle.Top;
            pnlHeader.FillColor = System.Drawing.Color.White;
            pnlHeader.Location = new System.Drawing.Point(0, 0);
            pnlHeader.Name = "pnlHeader";
            pnlHeader.Size = new System.Drawing.Size(940, 88);
            pnlHeader.TabIndex = 0;
            lblPageSubtitle.BackColor = System.Drawing.Color.Transparent;
            lblPageSubtitle.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblPageSubtitle.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            lblPageSubtitle.Location = new System.Drawing.Point(32, 50);
            lblPageSubtitle.Name = "lblPageSubtitle";
            lblPageSubtitle.Size = new System.Drawing.Size(349, 19);
            lblPageSubtitle.TabIndex = 1;
            lblPageSubtitle.Text = "Manage teaching staff records in a structured academic layout.";
            lblPageTitle.BackColor = System.Drawing.Color.Transparent;
            lblPageTitle.Font = new System.Drawing.Font("Segoe UI Semibold", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblPageTitle.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            lblPageTitle.Location = new System.Drawing.Point(32, 16);
            lblPageTitle.Name = "lblPageTitle";
            lblPageTitle.Size = new System.Drawing.Size(343, 34);
            lblPageTitle.TabIndex = 0;
            lblPageTitle.Text = "Faculty Members Management";
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(245, 247, 250);
            ClientSize = new System.Drawing.Size(1180, 720);
            Controls.Add(pnlMain);
            Controls.Add(pnlSidebar);
            Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            MinimumSize = new System.Drawing.Size(980, 600);
            Name = "FacultyMembersForm";
            Text = "Faculty Members Management";
            pnlSidebar.ResumeLayout(false);
            pnlSidebar.PerformLayout();
            pnlMain.ResumeLayout(false);
            pnlWorkspace.ResumeLayout(false);
            pnlFacultyMembersTable.ResumeLayout(false);
            pnlFacultyMembersTable.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvFacultyMembers).EndInit();
            pnlFacultyMemberEditor.ResumeLayout(false);
            pnlFacultyMemberEditor.PerformLayout();
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
        private Guna.UI2.WinForms.Guna2Panel pnlFacultyMemberEditor = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblEditorTitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblEditorSubtitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblFacultyMemberId = null!;
        private Guna.UI2.WinForms.Guna2TextBox txtFacultyMemberId = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblFullName = null!;
        private Guna.UI2.WinForms.Guna2TextBox txtFacultyMemberFullName = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblAcademicTitle = null!;
        private Guna.UI2.WinForms.Guna2ComboBox cmbAcademicTitle = null!;
        private Guna.UI2.WinForms.Guna2Button btnAddFacultyMember = null!;
        private Guna.UI2.WinForms.Guna2Button btnUpdateFacultyMember = null!;
        private Guna.UI2.WinForms.Guna2Button btnDeleteFacultyMember = null!;
        private Guna.UI2.WinForms.Guna2Button btnClearFacultyMemberForm = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlFacultyMembersTable = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblTableTitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblTableSubtitle = null!;
        private Guna.UI2.WinForms.Guna2DataGridView dgvFacultyMembers = null!;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFacultyMemberId = null!;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFullName = null!;
        private System.Windows.Forms.DataGridViewTextBoxColumn colAcademicTitle = null!;
    }
}

