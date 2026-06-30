using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System
{
    public partial class StudyYearsForm : System.Windows.Forms.UserControl
    {
        private readonly StudyYearService studyYearService = new();

        public StudyYearsForm()
        {
            InitializeComponent();
            ConfigureAutoIdField();
            ConfigureNavigation();
            ConfigureStudyYearsGrid();
            ConfigureStudyYearsEvents();
        }

        private void ConfigureAutoIdField()
        {
            txtStudyYearId.ReadOnly = true;
            txtStudyYearId.TabStop = false;
            txtStudyYearId.PlaceholderText = "Auto";
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await LoadStudyYearsAsync();
            ClearStudyYearForm();
        }

        private void ConfigureNavigation()
        {
            FormNavigation.ConfigureSidebar(this, pnlSidebar, NavigationPage.StudyYears);
        }

        private void ConfigureStudyYearsGrid()
        {
            dgvStudyYears.AutoGenerateColumns = false;
            dgvStudyYears.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            GridStyle.Apply(dgvStudyYears);
        }

        private void ConfigureStudyYearsEvents()
        {
            dgvStudyYears.SelectionChanged += (_, _) => PopulateStudyYearEditorFromSelection();
            txtStudyYearId.Leave += async (_, _) => await PopulateStudyYearEditorFromEnteredIdAsync();
            btnAddStudyYear.Click += async (_, _) => await AddStudyYearAsync();
            btnUpdateStudyYear.Click += async (_, _) => await UpdateStudyYearAsync();
            btnDeleteStudyYear.Click += async (_, _) => await DeleteStudyYearAsync();
        }

        private async Task LoadStudyYearsAsync()
        {
            SetStudyYearActionsEnabled(false);

            try
            {
                var studyYears = await studyYearService.GetAllAsync();
                dgvStudyYears.DataSource = studyYears;
                dgvStudyYears.ClearSelection();
            }
            catch (Exception ex)
            {
                ShowError("Unable to load study years.", ex);
            }
            finally
            {
                SetStudyYearActionsEnabled(true);
            }
        }

        private async Task AddStudyYearAsync()
        {
            if (!TryBuildStudyYear(out var studyYear, requireId: false))
            {
                return;
            }

            await ExecuteStudyYearActionAsync(
                async () => await studyYearService.AddAsync(studyYear),
                UiMessages.RecordAdded);
        }

        private async Task UpdateStudyYearAsync()
        {
            if (!TryBuildStudyYear(out var studyYear))
            {
                return;
            }

            await ExecuteStudyYearActionAsync(
                async () => await studyYearService.UpdateAsync(studyYear),
                UiMessages.RecordUpdated);
        }

        private async Task DeleteStudyYearAsync()
        {
            if (!TryGetStudyYearIdFromEditor(out int studyYearId))
            {
                return;
            }

            var confirmation = UiMessages.ConfirmDeletion(this, "Delete Study Year");

            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            await ExecuteStudyYearActionAsync(
                async () => await studyYearService.DeleteAsync(studyYearId),
                UiMessages.RecordDeleted);
        }

        private async Task ExecuteStudyYearActionAsync(Func<Task> action, string successMessage)
        {
            SetStudyYearActionsEnabled(false);

            try
            {
                await action();
                await LoadStudyYearsAsync();
                ClearStudyYearForm();
                ShowInformation(successMessage);
            }
            catch (Exception ex)
            {
                ShowError("Unable to complete the study year operation.", ex);
            }
            finally
            {
                SetStudyYearActionsEnabled(true);
            }
        }

        private bool TryBuildStudyYear(out StudyYear studyYear, bool requireId = true)
        {
            studyYear = new StudyYear
            {
                YearName = txtYearName.Text.Trim()
            };

            var studyYearId = 0;
            if (requireId && !TryGetStudyYearIdFromEditor(out studyYearId))
            {
                return false;
            }

            studyYear.StudyYearID = requireId ? studyYearId : 0;

            if (!string.IsNullOrWhiteSpace(studyYear.YearName))
            {
                return true;
            }

            ShowInformation(UiMessages.RequiredFields);
            txtYearName.Focus();
            return false;
        }

        private bool TryGetStudyYearIdFromEditor(out int studyYearId)
        {
            if (int.TryParse(txtStudyYearId.Text, out studyYearId) && studyYearId > 0)
            {
                return true;
            }

            ShowInformation("Select a study year row first.");
            return false;
        }

        private void PopulateStudyYearEditorFromSelection()
        {
            if (dgvStudyYears.CurrentRow?.DataBoundItem is not StudyYear studyYear)
            {
                return;
            }

            txtStudyYearId.Text = studyYear.StudyYearID.ToString();
            txtYearName.Text = studyYear.YearName;
        }

        private async Task PopulateStudyYearEditorFromEnteredIdAsync()
        {
            if (!int.TryParse(txtStudyYearId.Text, out int studyYearId) || studyYearId <= 0)
            {
                return;
            }

            try
            {
                var studyYear = await studyYearService.GetByIdAsync(studyYearId);

                if (studyYear is null)
                {
                    return;
                }

                txtYearName.Text = studyYear.YearName;
                SelectStudyYearRow(studyYear.StudyYearID);
            }
            catch (Exception ex)
            {
                ShowError("Unable to load study year details.", ex);
            }
        }

        private void SelectStudyYearRow(int studyYearId)
        {
            foreach (DataGridViewRow row in dgvStudyYears.Rows)
            {
                if (row.DataBoundItem is not StudyYear studyYear || studyYear.StudyYearID != studyYearId)
                {
                    continue;
                }

                row.Selected = true;
                dgvStudyYears.CurrentCell = row.Cells[0];
                break;
            }
        }

        private void ClearStudyYearForm()
        {
            txtStudyYearId.Text = "Auto";
            txtYearName.Clear();
            dgvStudyYears.ClearSelection();
            txtYearName.Focus();
        }

        private void SetStudyYearActionsEnabled(bool enabled)
        {
            btnAddStudyYear.Enabled = enabled;
            btnUpdateStudyYear.Enabled = enabled;
            btnDeleteStudyYear.Enabled = enabled;
            dgvStudyYears.Enabled = enabled;
        }

        private void ShowInformation(string message)
        {
            UiMessages.ShowInformation(this, message, "Study Years");
        }

        private void ShowError(string message, Exception ex)
        {
            UiMessages.ShowError(this, message, "Study Years", ex);
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
            pnlStudyYearsTable = new Guna.UI2.WinForms.Guna2Panel();
            dgvStudyYears = new Guna.UI2.WinForms.Guna2DataGridView();
            colStudyYearId = new System.Windows.Forms.DataGridViewTextBoxColumn();
            colYearName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            lblTableSubtitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblTableTitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            pnlStudyYearEditor = new Guna.UI2.WinForms.Guna2Panel();
            btnClearStudyYearForm = new Guna.UI2.WinForms.Guna2Button();
            btnDeleteStudyYear = new Guna.UI2.WinForms.Guna2Button();
            btnUpdateStudyYear = new Guna.UI2.WinForms.Guna2Button();
            btnAddStudyYear = new Guna.UI2.WinForms.Guna2Button();
            txtYearName = new Guna.UI2.WinForms.Guna2TextBox();
            lblYearName = new Guna.UI2.WinForms.Guna2HtmlLabel();
            txtStudyYearId = new Guna.UI2.WinForms.Guna2TextBox();
            lblStudyYearId = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblEditorSubtitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblEditorTitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            pnlHeader = new Guna.UI2.WinForms.Guna2Panel();
            lblPageSubtitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblPageTitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            pnlSidebar.SuspendLayout();
            pnlMain.SuspendLayout();
            pnlWorkspace.SuspendLayout();
            pnlStudyYearsTable.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvStudyYears).BeginInit();
            pnlStudyYearEditor.SuspendLayout();
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
            btnNavigationFaculty.Cursor = System.Windows.Forms.Cursors.Hand;
            btnNavigationFaculty.FillColor = System.Drawing.Color.FromArgb(24, 38, 62);
            btnNavigationFaculty.Font = new System.Drawing.Font("Segoe UI Semibold", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnNavigationFaculty.ForeColor = System.Drawing.Color.FromArgb(226, 232, 240);
            btnNavigationFaculty.HoverState.FillColor = System.Drawing.Color.FromArgb(36, 55, 86);
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
            btnNavigationStudyYears.Checked = true;
            btnNavigationStudyYears.Cursor = System.Windows.Forms.Cursors.Hand;
            btnNavigationStudyYears.FillColor = System.Drawing.Color.FromArgb(37, 99, 235);
            btnNavigationStudyYears.Font = new System.Drawing.Font("Segoe UI Semibold", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnNavigationStudyYears.ForeColor = System.Drawing.Color.White;
            btnNavigationStudyYears.HoverState.FillColor = System.Drawing.Color.FromArgb(29, 78, 216);
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
            pnlWorkspace.Controls.Add(pnlStudyYearsTable);
            pnlWorkspace.Controls.Add(pnlStudyYearEditor);
            pnlWorkspace.Dock = System.Windows.Forms.DockStyle.Fill;
            pnlWorkspace.FillColor = System.Drawing.Color.FromArgb(245, 247, 250);
            pnlWorkspace.Location = new System.Drawing.Point(0, 88);
            pnlWorkspace.Name = "pnlWorkspace";
            pnlWorkspace.Padding = new System.Windows.Forms.Padding(28, 24, 28, 28);
            pnlWorkspace.Size = new System.Drawing.Size(940, 632);
            pnlWorkspace.TabIndex = 1;
            pnlStudyYearsTable.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            pnlStudyYearsTable.BackColor = System.Drawing.Color.Transparent;
            pnlStudyYearsTable.BorderColor = System.Drawing.Color.FromArgb(226, 232, 240);
            pnlStudyYearsTable.BorderRadius = 8;
            pnlStudyYearsTable.BorderThickness = 1;
            pnlStudyYearsTable.Controls.Add(dgvStudyYears);
            pnlStudyYearsTable.Controls.Add(lblTableSubtitle);
            pnlStudyYearsTable.Controls.Add(lblTableTitle);
            pnlStudyYearsTable.FillColor = System.Drawing.Color.White;
            pnlStudyYearsTable.Location = new System.Drawing.Point(28, 236);
            pnlStudyYearsTable.Name = "pnlStudyYearsTable";
            pnlStudyYearsTable.Size = new System.Drawing.Size(884, 368);
            pnlStudyYearsTable.TabIndex = 1;
            dgvStudyYears.AllowUserToAddRows = false;
            dgvStudyYears.AllowUserToDeleteRows = false;
            dgvStudyYears.AllowUserToResizeRows = false;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.FromArgb(248, 250, 252);
            dataGridViewCellStyle1.ForeColor = System.Drawing.Color.FromArgb(30, 41, 59);
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.Color.FromArgb(219, 234, 254);
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.Color.FromArgb(30, 64, 175);
            dgvStudyYears.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            dgvStudyYears.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            dgvStudyYears.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dgvStudyYears.BackgroundColor = System.Drawing.Color.White;
            dgvStudyYears.BorderStyle = System.Windows.Forms.BorderStyle.None;
            dgvStudyYears.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleHorizontal;
            dgvStudyYears.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.Color.FromArgb(15, 23, 42);
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Segoe UI Semibold", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            dataGridViewCellStyle2.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.Color.FromArgb(15, 23, 42);
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            dgvStudyYears.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle2;
            dgvStudyYears.ColumnHeadersHeight = 44;
            dgvStudyYears.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvStudyYears.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { colStudyYearId, colYearName });
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.Color.White;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            dataGridViewCellStyle3.ForeColor = System.Drawing.Color.FromArgb(30, 41, 59);
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.Color.FromArgb(219, 234, 254);
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.Color.FromArgb(30, 64, 175);
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            dgvStudyYears.DefaultCellStyle = dataGridViewCellStyle3;
            dgvStudyYears.EnableHeadersVisualStyles = false;
            dgvStudyYears.GridColor = System.Drawing.Color.FromArgb(226, 232, 240);
            dgvStudyYears.Location = new System.Drawing.Point(24, 78);
            dgvStudyYears.MultiSelect = false;
            dgvStudyYears.Name = "dgvStudyYears";
            dgvStudyYears.ReadOnly = true;
            dgvStudyYears.RowHeadersVisible = false;
            dgvStudyYears.RowTemplate.Height = 42;
            dgvStudyYears.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            dgvStudyYears.Size = new System.Drawing.Size(836, 266);
            dgvStudyYears.TabIndex = 2;
            colStudyYearId.DataPropertyName = "StudyYearID";
            colStudyYearId.FillWeight = 34F;
            colStudyYearId.HeaderText = "Study Year ID";
            colStudyYearId.Name = "colStudyYearId";
            colStudyYearId.ReadOnly = true;
            colYearName.DataPropertyName = "YearName";
            colYearName.FillWeight = 166F;
            colYearName.HeaderText = "Year Name";
            colYearName.Name = "colYearName";
            colYearName.ReadOnly = true;
            lblTableSubtitle.BackColor = System.Drawing.Color.Transparent;
            lblTableSubtitle.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblTableSubtitle.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            lblTableSubtitle.Location = new System.Drawing.Point(24, 43);
            lblTableSubtitle.Name = "lblTableSubtitle";
            lblTableSubtitle.Size = new System.Drawing.Size(229, 17);
            lblTableSubtitle.TabIndex = 1;
            lblTableSubtitle.Text = "Review and select academic year records.";
            lblTableTitle.BackColor = System.Drawing.Color.Transparent;
            lblTableTitle.Font = new System.Drawing.Font("Segoe UI Semibold", 13F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblTableTitle.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            lblTableTitle.Location = new System.Drawing.Point(24, 18);
            lblTableTitle.Name = "lblTableTitle";
            lblTableTitle.Size = new System.Drawing.Size(131, 25);
            lblTableTitle.TabIndex = 0;
            lblTableTitle.Text = "Study Years List";
            pnlStudyYearEditor.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            pnlStudyYearEditor.BackColor = System.Drawing.Color.Transparent;
            pnlStudyYearEditor.BorderColor = System.Drawing.Color.FromArgb(226, 232, 240);
            pnlStudyYearEditor.BorderRadius = 8;
            pnlStudyYearEditor.BorderThickness = 1;
            pnlStudyYearEditor.Controls.Add(btnClearStudyYearForm);
            pnlStudyYearEditor.Controls.Add(btnDeleteStudyYear);
            pnlStudyYearEditor.Controls.Add(btnUpdateStudyYear);
            pnlStudyYearEditor.Controls.Add(btnAddStudyYear);
            pnlStudyYearEditor.Controls.Add(txtYearName);
            pnlStudyYearEditor.Controls.Add(lblYearName);
            pnlStudyYearEditor.Controls.Add(txtStudyYearId);
            pnlStudyYearEditor.Controls.Add(lblStudyYearId);
            pnlStudyYearEditor.Controls.Add(lblEditorSubtitle);
            pnlStudyYearEditor.Controls.Add(lblEditorTitle);
            pnlStudyYearEditor.FillColor = System.Drawing.Color.White;
            pnlStudyYearEditor.Location = new System.Drawing.Point(28, 24);
            pnlStudyYearEditor.Name = "pnlStudyYearEditor";
            pnlStudyYearEditor.Size = new System.Drawing.Size(884, 190);
            pnlStudyYearEditor.TabIndex = 0;
            btnClearStudyYearForm.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnClearStudyYearForm.BorderColor = System.Drawing.Color.FromArgb(203, 213, 225);
            btnClearStudyYearForm.BorderRadius = 8;
            btnClearStudyYearForm.BorderThickness = 1;
            btnClearStudyYearForm.Cursor = System.Windows.Forms.Cursors.Hand;
            btnClearStudyYearForm.FillColor = System.Drawing.Color.White;
            btnClearStudyYearForm.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnClearStudyYearForm.ForeColor = System.Drawing.Color.FromArgb(51, 65, 85);
            btnClearStudyYearForm.HoverState.FillColor = System.Drawing.Color.FromArgb(248, 250, 252);
            btnClearStudyYearForm.Location = new System.Drawing.Point(752, 135);
            btnClearStudyYearForm.Name = "btnClearStudyYearForm";
            btnClearStudyYearForm.Size = new System.Drawing.Size(108, 38);
            btnClearStudyYearForm.TabIndex = 9;
            btnClearStudyYearForm.Text = "Clear";
            btnClearStudyYearForm.Visible = false;
            btnDeleteStudyYear.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnDeleteStudyYear.BorderRadius = 8;
            btnDeleteStudyYear.Cursor = System.Windows.Forms.Cursors.Hand;
            btnDeleteStudyYear.FillColor = System.Drawing.Color.FromArgb(220, 38, 38);
            btnDeleteStudyYear.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnDeleteStudyYear.ForeColor = System.Drawing.Color.White;
            btnDeleteStudyYear.HoverState.FillColor = System.Drawing.Color.FromArgb(185, 28, 28);
            btnDeleteStudyYear.Location = new System.Drawing.Point(632, 135);
            btnDeleteStudyYear.Name = "btnDeleteStudyYear";
            btnDeleteStudyYear.Size = new System.Drawing.Size(108, 38);
            btnDeleteStudyYear.TabIndex = 8;
            btnDeleteStudyYear.Text = "Delete";
            btnUpdateStudyYear.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnUpdateStudyYear.BorderRadius = 8;
            btnUpdateStudyYear.Cursor = System.Windows.Forms.Cursors.Hand;
            btnUpdateStudyYear.FillColor = System.Drawing.Color.FromArgb(14, 116, 144);
            btnUpdateStudyYear.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnUpdateStudyYear.ForeColor = System.Drawing.Color.White;
            btnUpdateStudyYear.HoverState.FillColor = System.Drawing.Color.FromArgb(21, 94, 117);
            btnUpdateStudyYear.Location = new System.Drawing.Point(752, 91);
            btnUpdateStudyYear.Name = "btnUpdateStudyYear";
            btnUpdateStudyYear.Size = new System.Drawing.Size(108, 38);
            btnUpdateStudyYear.TabIndex = 7;
            btnUpdateStudyYear.Text = "Update";
            btnAddStudyYear.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnAddStudyYear.BorderRadius = 8;
            btnAddStudyYear.Cursor = System.Windows.Forms.Cursors.Hand;
            btnAddStudyYear.FillColor = System.Drawing.Color.FromArgb(22, 163, 74);
            btnAddStudyYear.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnAddStudyYear.ForeColor = System.Drawing.Color.White;
            btnAddStudyYear.HoverState.FillColor = System.Drawing.Color.FromArgb(21, 128, 61);
            btnAddStudyYear.Location = new System.Drawing.Point(632, 91);
            btnAddStudyYear.Name = "btnAddStudyYear";
            btnAddStudyYear.Size = new System.Drawing.Size(108, 38);
            btnAddStudyYear.TabIndex = 6;
            btnAddStudyYear.Text = "Add";
            txtYearName.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            txtYearName.BorderColor = System.Drawing.Color.FromArgb(203, 213, 225);
            txtYearName.BorderRadius = 8;
            txtYearName.Cursor = System.Windows.Forms.Cursors.IBeam;
            txtYearName.DefaultText = "";
            txtYearName.DisabledState.BorderColor = System.Drawing.Color.FromArgb(226, 232, 240);
            txtYearName.DisabledState.FillColor = System.Drawing.Color.FromArgb(248, 250, 252);
            txtYearName.DisabledState.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            txtYearName.FocusedState.BorderColor = System.Drawing.Color.FromArgb(37, 99, 235);
            txtYearName.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtYearName.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            txtYearName.HoverState.BorderColor = System.Drawing.Color.FromArgb(59, 130, 246);
            txtYearName.Location = new System.Drawing.Point(240, 112);
            txtYearName.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            txtYearName.Name = "txtYearName";
            txtYearName.PasswordChar = '\0';
            txtYearName.PlaceholderForeColor = System.Drawing.Color.FromArgb(148, 163, 184);
            txtYearName.PlaceholderText = "Enter study year name";
            txtYearName.SelectedText = "";
            txtYearName.Size = new System.Drawing.Size(350, 42);
            txtYearName.TabIndex = 5;
            lblYearName.BackColor = System.Drawing.Color.Transparent;
            lblYearName.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblYearName.ForeColor = System.Drawing.Color.FromArgb(51, 65, 85);
            lblYearName.Location = new System.Drawing.Point(240, 87);
            lblYearName.Name = "lblYearName";
            lblYearName.Size = new System.Drawing.Size(67, 19);
            lblYearName.TabIndex = 4;
            lblYearName.Text = "Year Name";
            txtStudyYearId.BorderColor = System.Drawing.Color.FromArgb(226, 232, 240);
            txtStudyYearId.BorderRadius = 8;
            txtStudyYearId.Cursor = System.Windows.Forms.Cursors.IBeam;
            txtStudyYearId.DefaultText = "";
            txtStudyYearId.DisabledState.BorderColor = System.Drawing.Color.FromArgb(226, 232, 240);
            txtStudyYearId.DisabledState.FillColor = System.Drawing.Color.FromArgb(248, 250, 252);
            txtStudyYearId.DisabledState.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            txtStudyYearId.Enabled = true;
            txtStudyYearId.FillColor = System.Drawing.Color.White;
            txtStudyYearId.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtStudyYearId.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            txtStudyYearId.Location = new System.Drawing.Point(24, 112);
            txtStudyYearId.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            txtStudyYearId.Name = "txtStudyYearId";
            txtStudyYearId.PasswordChar = '\0';
            txtStudyYearId.PlaceholderForeColor = System.Drawing.Color.FromArgb(148, 163, 184);
            txtStudyYearId.PlaceholderText = "Enter ID";
            txtStudyYearId.SelectedText = "";
            txtStudyYearId.Size = new System.Drawing.Size(190, 42);
            txtStudyYearId.TabIndex = 3;
            lblStudyYearId.BackColor = System.Drawing.Color.Transparent;
            lblStudyYearId.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblStudyYearId.ForeColor = System.Drawing.Color.FromArgb(51, 65, 85);
            lblStudyYearId.Location = new System.Drawing.Point(24, 87);
            lblStudyYearId.Name = "lblStudyYearId";
            lblStudyYearId.Size = new System.Drawing.Size(83, 19);
            lblStudyYearId.TabIndex = 2;
            lblStudyYearId.Text = "Study Year ID";
            lblEditorSubtitle.BackColor = System.Drawing.Color.Transparent;
            lblEditorSubtitle.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblEditorSubtitle.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            lblEditorSubtitle.Location = new System.Drawing.Point(24, 44);
            lblEditorSubtitle.Name = "lblEditorSubtitle";
            lblEditorSubtitle.Size = new System.Drawing.Size(291, 17);
            lblEditorSubtitle.TabIndex = 1;
            lblEditorSubtitle.Text = "Prepare study year details before applying an action.";
            lblEditorTitle.BackColor = System.Drawing.Color.Transparent;
            lblEditorTitle.Font = new System.Drawing.Font("Segoe UI Semibold", 13F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblEditorTitle.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            lblEditorTitle.Location = new System.Drawing.Point(24, 18);
            lblEditorTitle.Name = "lblEditorTitle";
            lblEditorTitle.Size = new System.Drawing.Size(142, 25);
            lblEditorTitle.TabIndex = 0;
            lblEditorTitle.Text = "Study Year Details";
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
            lblPageSubtitle.Size = new System.Drawing.Size(333, 19);
            lblPageSubtitle.TabIndex = 1;
            lblPageSubtitle.Text = "Manage academic years used across schedules and sections.";
            lblPageTitle.BackColor = System.Drawing.Color.Transparent;
            lblPageTitle.Font = new System.Drawing.Font("Segoe UI Semibold", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblPageTitle.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            lblPageTitle.Location = new System.Drawing.Point(32, 16);
            lblPageTitle.Name = "lblPageTitle";
            lblPageTitle.Size = new System.Drawing.Size(281, 34);
            lblPageTitle.TabIndex = 0;
            lblPageTitle.Text = "Study Years Management";
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(245, 247, 250);
            ClientSize = new System.Drawing.Size(1180, 720);
            Controls.Add(pnlMain);
            Controls.Add(pnlSidebar);
            Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            MinimumSize = new System.Drawing.Size(980, 600);
            Name = "StudyYearsForm";
            Text = "Study Years Management";
            pnlSidebar.ResumeLayout(false);
            pnlSidebar.PerformLayout();
            pnlMain.ResumeLayout(false);
            pnlWorkspace.ResumeLayout(false);
            pnlStudyYearsTable.ResumeLayout(false);
            pnlStudyYearsTable.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvStudyYears).EndInit();
            pnlStudyYearEditor.ResumeLayout(false);
            pnlStudyYearEditor.PerformLayout();
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
        private Guna.UI2.WinForms.Guna2Panel pnlStudyYearEditor = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblEditorTitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblEditorSubtitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblStudyYearId = null!;
        private Guna.UI2.WinForms.Guna2TextBox txtStudyYearId = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblYearName = null!;
        private Guna.UI2.WinForms.Guna2TextBox txtYearName = null!;
        private Guna.UI2.WinForms.Guna2Button btnAddStudyYear = null!;
        private Guna.UI2.WinForms.Guna2Button btnUpdateStudyYear = null!;
        private Guna.UI2.WinForms.Guna2Button btnDeleteStudyYear = null!;
        private Guna.UI2.WinForms.Guna2Button btnClearStudyYearForm = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlStudyYearsTable = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblTableTitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblTableSubtitle = null!;
        private Guna.UI2.WinForms.Guna2DataGridView dgvStudyYears = null!;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStudyYearId = null!;
        private System.Windows.Forms.DataGridViewTextBoxColumn colYearName = null!;
    }
}

