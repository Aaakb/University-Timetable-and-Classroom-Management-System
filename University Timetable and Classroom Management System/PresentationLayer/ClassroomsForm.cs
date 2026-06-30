using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System
{
    public partial class ClassroomsForm : System.Windows.Forms.UserControl
    {
        private readonly ClassroomService classroomService = new();

        public ClassroomsForm()
        {
            InitializeComponent();
            ConfigureAutoIdField();
            ConfigureNavigation();
            ConfigureClassroomsGrid();
            ConfigureClassroomsEvents();
        }

        private void ConfigureAutoIdField()
        {
            txtClassroomId.ReadOnly = true;
            txtClassroomId.TabStop = false;
            txtClassroomId.PlaceholderText = "Auto";
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await LoadClassroomsAsync();
            ClearClassroomForm();
        }

        private void ConfigureNavigation()
        {
            FormNavigation.ConfigureSidebar(this, pnlSidebar, NavigationPage.Classrooms);
        }

        private void ConfigureClassroomsGrid()
        {
            dgvClassrooms.AutoGenerateColumns = false;
            dgvClassrooms.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            GridStyle.Apply(dgvClassrooms);
        }

        private void ConfigureClassroomsEvents()
        {
            dgvClassrooms.SelectionChanged += (_, _) => PopulateClassroomEditorFromSelection();
            txtClassroomId.Leave += async (_, _) => await PopulateClassroomEditorFromEnteredIdAsync();
            btnAddClassroom.Click += async (_, _) => await AddClassroomAsync();
            btnUpdateClassroom.Click += async (_, _) => await UpdateClassroomAsync();
            btnDeleteClassroom.Click += async (_, _) => await DeleteClassroomAsync();
        }

        private async Task LoadClassroomsAsync()
        {
            SetClassroomActionsEnabled(false);

            try
            {
                var classrooms = await classroomService.GetAllAsync();
                dgvClassrooms.DataSource = classrooms;
                dgvClassrooms.ClearSelection();
            }
            catch (Exception ex)
            {
                ShowError("Unable to load classrooms.", ex);
            }
            finally
            {
                SetClassroomActionsEnabled(true);
            }
        }

        private async Task AddClassroomAsync()
        {
            if (!TryBuildClassroom(out var classroom, requireId: false))
            {
                return;
            }

            await ExecuteClassroomActionAsync(
                async () => await classroomService.AddAsync(classroom),
                UiMessages.RecordAdded);
        }

        private async Task UpdateClassroomAsync()
        {
            if (!TryBuildClassroom(out var classroom))
            {
                return;
            }

            await ExecuteClassroomActionAsync(
                async () => await classroomService.UpdateAsync(classroom),
                UiMessages.RecordUpdated);
        }

        private async Task DeleteClassroomAsync()
        {
            if (!TryGetClassroomIdFromEditor(out int classroomId))
            {
                return;
            }

            var confirmation = UiMessages.ConfirmDeletion(this, "Delete Classroom");

            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            await ExecuteClassroomActionAsync(
                async () => await classroomService.DeleteAsync(classroomId),
                UiMessages.RecordDeleted);
        }

        private async Task ExecuteClassroomActionAsync(Func<Task> action, string successMessage)
        {
            SetClassroomActionsEnabled(false);

            try
            {
                await action();
                await LoadClassroomsAsync();
                ClearClassroomForm();
                ShowInformation(successMessage);
            }
            catch (Exception ex)
            {
                ShowError("Unable to complete the classroom operation.", ex);
            }
            finally
            {
                SetClassroomActionsEnabled(true);
            }
        }

        private bool TryBuildClassroom(out Classroom classroom, bool requireId = true)
        {
            classroom = new Classroom
            {
                ClassroomNumber = txtClassroomNumber.Text.Trim(),
                RoomType = cmbRoomType.Text.Trim()
            };

            var classroomId = 0;
            if (requireId && !TryGetClassroomIdFromEditor(out classroomId))
            {
                return false;
            }

            classroom.ClassroomID = requireId ? classroomId : 0;

            if (string.IsNullOrWhiteSpace(classroom.ClassroomNumber))
            {
                ShowInformation(UiMessages.RequiredFields);
                txtClassroomNumber.Focus();
                return false;
            }

            if (!int.TryParse(txtCapacity.Text, out int capacity) || capacity <= 0)
            {
                ShowInformation("Capacity must be a positive number.");
                txtCapacity.Focus();
                return false;
            }

            classroom.Capacity = capacity;

            if (string.IsNullOrWhiteSpace(classroom.RoomType))
            {
                classroom.RoomType = null;
            }

            return true;
        }

        private bool TryGetClassroomIdFromEditor(out int classroomId)
        {
            if (int.TryParse(txtClassroomId.Text, out classroomId) && classroomId > 0)
            {
                return true;
            }

            ShowInformation("Select a classroom row first.");
            return false;
        }

        private void PopulateClassroomEditorFromSelection()
        {
            if (dgvClassrooms.CurrentRow?.DataBoundItem is not Classroom classroom)
            {
                return;
            }

            txtClassroomId.Text = classroom.ClassroomID.ToString();
            txtClassroomNumber.Text = classroom.ClassroomNumber;
            txtCapacity.Text = classroom.Capacity.ToString();
            cmbRoomType.Text = classroom.RoomType ?? string.Empty;
        }

        private async Task PopulateClassroomEditorFromEnteredIdAsync()
        {
            if (!int.TryParse(txtClassroomId.Text, out int classroomId) || classroomId <= 0)
            {
                return;
            }

            try
            {
                var classroom = await classroomService.GetByIdAsync(classroomId);

                if (classroom is null)
                {
                    return;
                }

                txtClassroomNumber.Text = classroom.ClassroomNumber;
                txtCapacity.Text = classroom.Capacity.ToString();
                cmbRoomType.Text = classroom.RoomType ?? string.Empty;
                SelectClassroomRow(classroom.ClassroomID);
            }
            catch (Exception ex)
            {
                ShowError("Unable to load classroom details.", ex);
            }
        }

        private void SelectClassroomRow(int classroomId)
        {
            foreach (DataGridViewRow row in dgvClassrooms.Rows)
            {
                if (row.DataBoundItem is not Classroom classroom || classroom.ClassroomID != classroomId)
                {
                    continue;
                }

                row.Selected = true;
                dgvClassrooms.CurrentCell = row.Cells[0];
                break;
            }
        }

        private void ClearClassroomForm()
        {
            txtClassroomId.Text = "Auto";
            txtClassroomNumber.Clear();
            txtCapacity.Clear();
            cmbRoomType.SelectedIndex = -1;
            dgvClassrooms.ClearSelection();
            txtClassroomNumber.Focus();
        }

        private void SetClassroomActionsEnabled(bool enabled)
        {
            btnAddClassroom.Enabled = enabled;
            btnUpdateClassroom.Enabled = enabled;
            btnDeleteClassroom.Enabled = enabled;
            dgvClassrooms.Enabled = enabled;
        }

        private void ShowInformation(string message)
        {
            UiMessages.ShowInformation(this, message, "Classrooms");
        }

        private void ShowError(string message, Exception ex)
        {
            UiMessages.ShowError(this, message, "Classrooms", ex);
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
            btnNavigationTimeSlots = new Guna.UI2.WinForms.Guna2Button();
            btnNavigationClassrooms = new Guna.UI2.WinForms.Guna2Button();
            btnNavigationSubjects = new Guna.UI2.WinForms.Guna2Button();
            btnNavigationStudyYears = new Guna.UI2.WinForms.Guna2Button();
            btnNavigationBranches = new Guna.UI2.WinForms.Guna2Button();
            separatorSidebar = new Guna.UI2.WinForms.Guna2Separator();
            lblSidebarSubtitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblApplicationName = new Guna.UI2.WinForms.Guna2HtmlLabel();
            pnlMain = new Guna.UI2.WinForms.Guna2Panel();
            pnlWorkspace = new Guna.UI2.WinForms.Guna2Panel();
            pnlClassroomsTable = new Guna.UI2.WinForms.Guna2Panel();
            dgvClassrooms = new Guna.UI2.WinForms.Guna2DataGridView();
            colClassroomId = new DataGridViewTextBoxColumn();
            colClassroomNumber = new DataGridViewTextBoxColumn();
            colCapacity = new DataGridViewTextBoxColumn();
            colRoomType = new DataGridViewTextBoxColumn();
            lblTableSubtitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblTableTitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            pnlClassroomEditor = new Guna.UI2.WinForms.Guna2Panel();
            btnClearClassroomForm = new Guna.UI2.WinForms.Guna2Button();
            btnDeleteClassroom = new Guna.UI2.WinForms.Guna2Button();
            btnUpdateClassroom = new Guna.UI2.WinForms.Guna2Button();
            btnAddClassroom = new Guna.UI2.WinForms.Guna2Button();
            cmbRoomType = new Guna.UI2.WinForms.Guna2ComboBox();
            lblRoomType = new Guna.UI2.WinForms.Guna2HtmlLabel();
            txtCapacity = new Guna.UI2.WinForms.Guna2TextBox();
            lblCapacity = new Guna.UI2.WinForms.Guna2HtmlLabel();
            txtClassroomNumber = new Guna.UI2.WinForms.Guna2TextBox();
            lblClassroomNumber = new Guna.UI2.WinForms.Guna2HtmlLabel();
            txtClassroomId = new Guna.UI2.WinForms.Guna2TextBox();
            lblClassroomId = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblEditorSubtitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblEditorTitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            pnlHeader = new Guna.UI2.WinForms.Guna2Panel();
            lblPageSubtitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblPageTitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            pnlSidebar.SuspendLayout();
            pnlMain.SuspendLayout();
            pnlWorkspace.SuspendLayout();
            pnlClassroomsTable.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvClassrooms).BeginInit();
            pnlClassroomEditor.SuspendLayout();
            pnlHeader.SuspendLayout();
            SuspendLayout();
            pnlSidebar.BackColor = Color.Transparent;
            pnlSidebar.Controls.Add(lblSidebarFooter);
            pnlSidebar.Controls.Add(btnNavigationSchedules);
            pnlSidebar.Controls.Add(btnNavigationFaculty);
            pnlSidebar.Controls.Add(btnNavigationTimeSlots);
            pnlSidebar.Controls.Add(btnNavigationClassrooms);
            pnlSidebar.Controls.Add(btnNavigationSubjects);
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
            lblSidebarFooter.TabIndex = 11;
            lblSidebarFooter.Text = "Academic Scheduling Suite";
            btnNavigationSchedules.BorderRadius = 8;
            btnNavigationSchedules.Cursor = Cursors.Hand;
            btnNavigationSchedules.FillColor = Color.FromArgb(24, 38, 62);
            btnNavigationSchedules.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            btnNavigationSchedules.ForeColor = Color.FromArgb(226, 232, 240);
            btnNavigationSchedules.HoverState.FillColor = Color.FromArgb(36, 55, 86);
            btnNavigationSchedules.Location = new Point(24, 490);
            btnNavigationSchedules.Name = "btnNavigationSchedules";
            btnNavigationSchedules.Size = new Size(192, 44);
            btnNavigationSchedules.TabIndex = 10;
            btnNavigationSchedules.Text = "Schedules";
            btnNavigationSchedules.TextAlign = HorizontalAlignment.Left;
            btnNavigationSchedules.TextOffset = new Point(14, 0);
            btnNavigationFaculty.BorderRadius = 8;
            btnNavigationFaculty.Cursor = Cursors.Hand;
            btnNavigationFaculty.FillColor = Color.FromArgb(24, 38, 62);
            btnNavigationFaculty.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            btnNavigationFaculty.ForeColor = Color.FromArgb(226, 232, 240);
            btnNavigationFaculty.HoverState.FillColor = Color.FromArgb(36, 55, 86);
            btnNavigationFaculty.Location = new Point(24, 434);
            btnNavigationFaculty.Name = "btnNavigationFaculty";
            btnNavigationFaculty.Size = new Size(192, 44);
            btnNavigationFaculty.TabIndex = 9;
            btnNavigationFaculty.Text = "Faculty Members";
            btnNavigationFaculty.TextAlign = HorizontalAlignment.Left;
            btnNavigationFaculty.TextOffset = new Point(14, 0);
            btnNavigationTimeSlots.BorderRadius = 8;
            btnNavigationTimeSlots.Cursor = Cursors.Hand;
            btnNavigationTimeSlots.FillColor = Color.FromArgb(24, 38, 62);
            btnNavigationTimeSlots.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            btnNavigationTimeSlots.ForeColor = Color.FromArgb(226, 232, 240);
            btnNavigationTimeSlots.HoverState.FillColor = Color.FromArgb(36, 55, 86);
            btnNavigationTimeSlots.Location = new Point(24, 378);
            btnNavigationTimeSlots.Name = "btnNavigationTimeSlots";
            btnNavigationTimeSlots.Size = new Size(192, 44);
            btnNavigationTimeSlots.TabIndex = 8;
            btnNavigationTimeSlots.Text = "Time Slots";
            btnNavigationTimeSlots.TextAlign = HorizontalAlignment.Left;
            btnNavigationTimeSlots.TextOffset = new Point(14, 0);
            btnNavigationClassrooms.BorderRadius = 8;
            btnNavigationClassrooms.Checked = true;
            btnNavigationClassrooms.Cursor = Cursors.Hand;
            btnNavigationClassrooms.Enabled = false;
            btnNavigationClassrooms.FillColor = Color.FromArgb(37, 99, 235);
            btnNavigationClassrooms.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            btnNavigationClassrooms.ForeColor = Color.White;
            btnNavigationClassrooms.HoverState.FillColor = Color.FromArgb(29, 78, 216);
            btnNavigationClassrooms.Location = new Point(24, 322);
            btnNavigationClassrooms.Name = "btnNavigationClassrooms";
            btnNavigationClassrooms.Size = new Size(192, 44);
            btnNavigationClassrooms.TabIndex = 7;
            btnNavigationClassrooms.Text = "Classrooms";
            btnNavigationClassrooms.TextAlign = HorizontalAlignment.Left;
            btnNavigationClassrooms.TextOffset = new Point(14, 0);
            btnNavigationSubjects.BorderRadius = 8;
            btnNavigationSubjects.Cursor = Cursors.Hand;
            btnNavigationSubjects.FillColor = Color.FromArgb(24, 38, 62);
            btnNavigationSubjects.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            btnNavigationSubjects.ForeColor = Color.FromArgb(226, 232, 240);
            btnNavigationSubjects.HoverState.FillColor = Color.FromArgb(36, 55, 86);
            btnNavigationSubjects.Location = new Point(24, 266);
            btnNavigationSubjects.Name = "btnNavigationSubjects";
            btnNavigationSubjects.Size = new Size(192, 44);
            btnNavigationSubjects.TabIndex = 6;
            btnNavigationSubjects.Text = "Subjects";
            btnNavigationSubjects.TextAlign = HorizontalAlignment.Left;
            btnNavigationSubjects.TextOffset = new Point(14, 0);
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
            btnNavigationBranches.Cursor = Cursors.Hand;
            btnNavigationBranches.FillColor = Color.FromArgb(24, 38, 62);
            btnNavigationBranches.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            btnNavigationBranches.ForeColor = Color.FromArgb(226, 232, 240);
            btnNavigationBranches.HoverState.FillColor = Color.FromArgb(36, 55, 86);
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
            pnlMain.Size = new Size(1004, 720);
            pnlMain.TabIndex = 1;
            pnlWorkspace.Controls.Add(pnlClassroomsTable);
            pnlWorkspace.Controls.Add(pnlClassroomEditor);
            pnlWorkspace.Dock = DockStyle.Fill;
            pnlWorkspace.FillColor = Color.FromArgb(245, 247, 250);
            pnlWorkspace.Location = new Point(0, 88);
            pnlWorkspace.Name = "pnlWorkspace";
            pnlWorkspace.Padding = new Padding(28, 24, 28, 28);
            pnlWorkspace.Size = new Size(1004, 632);
            pnlWorkspace.TabIndex = 1;
            pnlClassroomsTable.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pnlClassroomsTable.BackColor = Color.Transparent;
            pnlClassroomsTable.BorderColor = Color.FromArgb(226, 232, 240);
            pnlClassroomsTable.BorderRadius = 8;
            pnlClassroomsTable.BorderThickness = 1;
            pnlClassroomsTable.Controls.Add(dgvClassrooms);
            pnlClassroomsTable.Controls.Add(lblTableSubtitle);
            pnlClassroomsTable.Controls.Add(lblTableTitle);
            pnlClassroomsTable.FillColor = Color.White;
            pnlClassroomsTable.Location = new Point(28, 248);
            pnlClassroomsTable.Name = "pnlClassroomsTable";
            pnlClassroomsTable.Size = new Size(948, 356);
            pnlClassroomsTable.TabIndex = 1;
            dgvClassrooms.AllowUserToAddRows = false;
            dgvClassrooms.AllowUserToDeleteRows = false;
            dgvClassrooms.AllowUserToResizeRows = false;
            dataGridViewCellStyle1.BackColor = Color.FromArgb(248, 250, 252);
            dataGridViewCellStyle1.ForeColor = Color.FromArgb(30, 41, 59);
            dataGridViewCellStyle1.SelectionBackColor = Color.FromArgb(219, 234, 254);
            dataGridViewCellStyle1.SelectionForeColor = Color.FromArgb(30, 64, 175);
            dgvClassrooms.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            dgvClassrooms.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = Color.FromArgb(15, 23, 42);
            dataGridViewCellStyle2.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            dataGridViewCellStyle2.ForeColor = Color.White;
            dataGridViewCellStyle2.SelectionBackColor = Color.FromArgb(15, 23, 42);
            dataGridViewCellStyle2.SelectionForeColor = Color.White;
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.True;
            dgvClassrooms.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle2;
            dgvClassrooms.ColumnHeadersHeight = 44;
            dgvClassrooms.Columns.AddRange(new DataGridViewColumn[] { colClassroomId, colClassroomNumber, colCapacity, colRoomType });
            dataGridViewCellStyle3.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = Color.White;
            dataGridViewCellStyle3.Font = new Font("Segoe UI", 10F);
            dataGridViewCellStyle3.ForeColor = Color.FromArgb(30, 41, 59);
            dataGridViewCellStyle3.SelectionBackColor = Color.FromArgb(219, 234, 254);
            dataGridViewCellStyle3.SelectionForeColor = Color.FromArgb(30, 64, 175);
            dataGridViewCellStyle3.WrapMode = DataGridViewTriState.False;
            dgvClassrooms.DefaultCellStyle = dataGridViewCellStyle3;
            dgvClassrooms.GridColor = Color.FromArgb(226, 232, 240);
            dgvClassrooms.Location = new Point(24, 78);
            dgvClassrooms.MultiSelect = false;
            dgvClassrooms.Name = "dgvClassrooms";
            dgvClassrooms.ReadOnly = true;
            dgvClassrooms.RowHeadersVisible = false;
            dgvClassrooms.RowTemplate.Height = 42;
            dgvClassrooms.Size = new Size(900, 254);
            dgvClassrooms.TabIndex = 2;
            dgvClassrooms.ThemeStyle.AlternatingRowsStyle.BackColor = Color.FromArgb(248, 250, 252);
            dgvClassrooms.ThemeStyle.AlternatingRowsStyle.ForeColor = Color.FromArgb(30, 41, 59);
            dgvClassrooms.ThemeStyle.AlternatingRowsStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
            dgvClassrooms.ThemeStyle.AlternatingRowsStyle.SelectionForeColor = Color.FromArgb(30, 64, 175);
            dgvClassrooms.ThemeStyle.GridColor = Color.FromArgb(226, 232, 240);
            dgvClassrooms.ThemeStyle.HeaderStyle.BackColor = Color.FromArgb(15, 23, 42);
            dgvClassrooms.ThemeStyle.HeaderStyle.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            dgvClassrooms.ThemeStyle.HeaderStyle.HeaightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvClassrooms.ThemeStyle.HeaderStyle.Height = 44;
            dgvClassrooms.ThemeStyle.ReadOnly = true;
            dgvClassrooms.ThemeStyle.RowsStyle.Font = new Font("Segoe UI", 10F);
            dgvClassrooms.ThemeStyle.RowsStyle.ForeColor = Color.FromArgb(30, 41, 59);
            dgvClassrooms.ThemeStyle.RowsStyle.Height = 42;
            dgvClassrooms.ThemeStyle.RowsStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
            dgvClassrooms.ThemeStyle.RowsStyle.SelectionForeColor = Color.FromArgb(30, 64, 175);
            colClassroomId.DataPropertyName = "ClassroomID";
            colClassroomId.FillWeight = 45F;
            colClassroomId.HeaderText = "Classroom ID";
            colClassroomId.Name = "colClassroomId";
            colClassroomId.ReadOnly = true;
            colClassroomNumber.DataPropertyName = "ClassroomNumber";
            colClassroomNumber.FillWeight = 120F;
            colClassroomNumber.HeaderText = "Classroom Number";
            colClassroomNumber.Name = "colClassroomNumber";
            colClassroomNumber.ReadOnly = true;
            colCapacity.DataPropertyName = "Capacity";
            colCapacity.FillWeight = 65F;
            colCapacity.HeaderText = "Capacity";
            colCapacity.Name = "colCapacity";
            colCapacity.ReadOnly = true;
            colRoomType.DataPropertyName = "RoomType";
            colRoomType.FillWeight = 95F;
            colRoomType.HeaderText = "Room Type";
            colRoomType.Name = "colRoomType";
            colRoomType.ReadOnly = true;
            lblTableSubtitle.BackColor = Color.Transparent;
            lblTableSubtitle.Font = new Font("Segoe UI", 9F);
            lblTableSubtitle.ForeColor = Color.FromArgb(100, 116, 139);
            lblTableSubtitle.Location = new Point(24, 43);
            lblTableSubtitle.Name = "lblTableSubtitle";
            lblTableSubtitle.Size = new Size(245, 17);
            lblTableSubtitle.TabIndex = 1;
            lblTableSubtitle.Text = "Review and select classroom capacity records.";
            lblTableTitle.BackColor = Color.Transparent;
            lblTableTitle.Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold);
            lblTableTitle.ForeColor = Color.FromArgb(15, 23, 42);
            lblTableTitle.Location = new Point(24, 18);
            lblTableTitle.Name = "lblTableTitle";
            lblTableTitle.Size = new Size(119, 25);
            lblTableTitle.TabIndex = 0;
            lblTableTitle.Text = "Classrooms List";
            pnlClassroomEditor.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnlClassroomEditor.BackColor = Color.Transparent;
            pnlClassroomEditor.BorderColor = Color.FromArgb(226, 232, 240);
            pnlClassroomEditor.BorderRadius = 8;
            pnlClassroomEditor.BorderThickness = 1;
            pnlClassroomEditor.Controls.Add(btnClearClassroomForm);
            pnlClassroomEditor.Controls.Add(btnDeleteClassroom);
            pnlClassroomEditor.Controls.Add(btnUpdateClassroom);
            pnlClassroomEditor.Controls.Add(btnAddClassroom);
            pnlClassroomEditor.Controls.Add(cmbRoomType);
            pnlClassroomEditor.Controls.Add(lblRoomType);
            pnlClassroomEditor.Controls.Add(txtCapacity);
            pnlClassroomEditor.Controls.Add(lblCapacity);
            pnlClassroomEditor.Controls.Add(txtClassroomNumber);
            pnlClassroomEditor.Controls.Add(lblClassroomNumber);
            pnlClassroomEditor.Controls.Add(txtClassroomId);
            pnlClassroomEditor.Controls.Add(lblClassroomId);
            pnlClassroomEditor.Controls.Add(lblEditorSubtitle);
            pnlClassroomEditor.Controls.Add(lblEditorTitle);
            pnlClassroomEditor.FillColor = Color.White;
            pnlClassroomEditor.Location = new Point(28, 24);
            pnlClassroomEditor.Name = "pnlClassroomEditor";
            pnlClassroomEditor.Size = new Size(948, 202);
            pnlClassroomEditor.TabIndex = 0;
            btnClearClassroomForm.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClearClassroomForm.BorderColor = Color.FromArgb(203, 213, 225);
            btnClearClassroomForm.BorderRadius = 8;
            btnClearClassroomForm.BorderThickness = 1;
            btnClearClassroomForm.Cursor = Cursors.Hand;
            btnClearClassroomForm.FillColor = Color.White;
            btnClearClassroomForm.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnClearClassroomForm.ForeColor = Color.FromArgb(51, 65, 85);
            btnClearClassroomForm.HoverState.FillColor = Color.FromArgb(248, 250, 252);
            btnClearClassroomForm.Location = new Point(816, 142);
            btnClearClassroomForm.Name = "btnClearClassroomForm";
            btnClearClassroomForm.Size = new Size(108, 38);
            btnClearClassroomForm.TabIndex = 13;
            btnClearClassroomForm.Text = "Clear";
            btnClearClassroomForm.Visible = false;
            btnDeleteClassroom.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnDeleteClassroom.BorderRadius = 8;
            btnDeleteClassroom.Cursor = Cursors.Hand;
            btnDeleteClassroom.FillColor = Color.FromArgb(220, 38, 38);
            btnDeleteClassroom.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnDeleteClassroom.ForeColor = Color.White;
            btnDeleteClassroom.HoverState.FillColor = Color.FromArgb(185, 28, 28);
            btnDeleteClassroom.Location = new Point(696, 142);
            btnDeleteClassroom.Name = "btnDeleteClassroom";
            btnDeleteClassroom.Size = new Size(108, 38);
            btnDeleteClassroom.TabIndex = 12;
            btnDeleteClassroom.Text = "Delete";
            btnUpdateClassroom.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnUpdateClassroom.BorderRadius = 8;
            btnUpdateClassroom.Cursor = Cursors.Hand;
            btnUpdateClassroom.FillColor = Color.FromArgb(14, 116, 144);
            btnUpdateClassroom.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnUpdateClassroom.ForeColor = Color.White;
            btnUpdateClassroom.HoverState.FillColor = Color.FromArgb(21, 94, 117);
            btnUpdateClassroom.Location = new Point(816, 98);
            btnUpdateClassroom.Name = "btnUpdateClassroom";
            btnUpdateClassroom.Size = new Size(108, 38);
            btnUpdateClassroom.TabIndex = 11;
            btnUpdateClassroom.Text = "Update";
            btnAddClassroom.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnAddClassroom.BorderRadius = 8;
            btnAddClassroom.Cursor = Cursors.Hand;
            btnAddClassroom.FillColor = Color.FromArgb(22, 163, 74);
            btnAddClassroom.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnAddClassroom.ForeColor = Color.White;
            btnAddClassroom.HoverState.FillColor = Color.FromArgb(21, 128, 61);
            btnAddClassroom.Location = new Point(696, 98);
            btnAddClassroom.Name = "btnAddClassroom";
            btnAddClassroom.Size = new Size(108, 38);
            btnAddClassroom.TabIndex = 10;
            btnAddClassroom.Text = "Add";
            cmbRoomType.BackColor = Color.Transparent;
            cmbRoomType.BorderColor = Color.FromArgb(203, 213, 225);
            cmbRoomType.BorderRadius = 8;
            cmbRoomType.DrawMode = DrawMode.OwnerDrawFixed;
            cmbRoomType.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbRoomType.FocusedColor = Color.FromArgb(37, 99, 235);
            cmbRoomType.FocusedState.BorderColor = Color.FromArgb(37, 99, 235);
            cmbRoomType.Font = new Font("Segoe UI", 10F);
            cmbRoomType.ForeColor = Color.FromArgb(15, 23, 42);
            cmbRoomType.HoverState.BorderColor = Color.FromArgb(59, 130, 246);
            cmbRoomType.ItemHeight = 36;
            cmbRoomType.Items.AddRange(new object[] { "Lecture", "Lab" });
            cmbRoomType.Location = new Point(402, 112);
            cmbRoomType.Name = "cmbRoomType";
            cmbRoomType.Size = new Size(190, 42);
            cmbRoomType.TabIndex = 9;
            lblRoomType.BackColor = Color.Transparent;
            lblRoomType.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            lblRoomType.ForeColor = Color.FromArgb(51, 65, 85);
            lblRoomType.Location = new Point(402, 87);
            lblRoomType.Name = "lblRoomType";
            lblRoomType.Size = new Size(72, 19);
            lblRoomType.TabIndex = 8;
            lblRoomType.Text = "Room Type";
            txtCapacity.BorderColor = Color.FromArgb(203, 213, 225);
            txtCapacity.BorderRadius = 8;
            txtCapacity.Cursor = Cursors.IBeam;
            txtCapacity.DefaultText = "";
            txtCapacity.DisabledState.BorderColor = Color.FromArgb(226, 232, 240);
            txtCapacity.DisabledState.FillColor = Color.FromArgb(248, 250, 252);
            txtCapacity.DisabledState.ForeColor = Color.FromArgb(100, 116, 139);
            txtCapacity.FocusedState.BorderColor = Color.FromArgb(37, 99, 235);
            txtCapacity.Font = new Font("Segoe UI", 10F);
            txtCapacity.ForeColor = Color.FromArgb(15, 23, 42);
            txtCapacity.HoverState.BorderColor = Color.FromArgb(59, 130, 246);
            txtCapacity.Location = new Point(270, 112);
            txtCapacity.Margin = new Padding(3, 4, 3, 4);
            txtCapacity.Name = "txtCapacity";
            txtCapacity.PlaceholderForeColor = Color.FromArgb(148, 163, 184);
            txtCapacity.PlaceholderText = "Capacity";
            txtCapacity.SelectedText = "";
            txtCapacity.Size = new Size(104, 42);
            txtCapacity.TabIndex = 7;
            lblCapacity.BackColor = Color.Transparent;
            lblCapacity.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            lblCapacity.ForeColor = Color.FromArgb(51, 65, 85);
            lblCapacity.Location = new Point(270, 87);
            lblCapacity.Name = "lblCapacity";
            lblCapacity.Size = new Size(54, 19);
            lblCapacity.TabIndex = 6;
            lblCapacity.Text = "Capacity";
            txtClassroomNumber.BorderColor = Color.FromArgb(203, 213, 225);
            txtClassroomNumber.BorderRadius = 8;
            txtClassroomNumber.Cursor = Cursors.IBeam;
            txtClassroomNumber.DefaultText = "";
            txtClassroomNumber.DisabledState.BorderColor = Color.FromArgb(226, 232, 240);
            txtClassroomNumber.DisabledState.FillColor = Color.FromArgb(248, 250, 252);
            txtClassroomNumber.DisabledState.ForeColor = Color.FromArgb(100, 116, 139);
            txtClassroomNumber.FocusedState.BorderColor = Color.FromArgb(37, 99, 235);
            txtClassroomNumber.Font = new Font("Segoe UI", 10F);
            txtClassroomNumber.ForeColor = Color.FromArgb(15, 23, 42);
            txtClassroomNumber.HoverState.BorderColor = Color.FromArgb(59, 130, 246);
            txtClassroomNumber.Location = new Point(184, 112);
            txtClassroomNumber.Margin = new Padding(3, 4, 3, 4);
            txtClassroomNumber.Name = "txtClassroomNumber";
            txtClassroomNumber.PlaceholderForeColor = Color.FromArgb(148, 163, 184);
            txtClassroomNumber.PlaceholderText = "Room No.";
            txtClassroomNumber.SelectedText = "";
            txtClassroomNumber.Size = new Size(60, 42);
            txtClassroomNumber.TabIndex = 5;
            lblClassroomNumber.BackColor = Color.Transparent;
            lblClassroomNumber.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            lblClassroomNumber.ForeColor = Color.FromArgb(51, 65, 85);
            lblClassroomNumber.Location = new Point(184, 87);
            lblClassroomNumber.Name = "lblClassroomNumber";
            lblClassroomNumber.Size = new Size(51, 19);
            lblClassroomNumber.TabIndex = 4;
            lblClassroomNumber.Text = "Room #";
            txtClassroomId.BorderColor = Color.FromArgb(226, 232, 240);
            txtClassroomId.BorderRadius = 8;
            txtClassroomId.Cursor = Cursors.IBeam;
            txtClassroomId.DefaultText = "";
            txtClassroomId.DisabledState.BorderColor = Color.FromArgb(226, 232, 240);
            txtClassroomId.DisabledState.FillColor = Color.FromArgb(248, 250, 252);
            txtClassroomId.DisabledState.ForeColor = Color.FromArgb(100, 116, 139);
            txtClassroomId.Enabled = true;
            txtClassroomId.FillColor = Color.White;
            txtClassroomId.Font = new Font("Segoe UI", 10F);
            txtClassroomId.ForeColor = Color.FromArgb(15, 23, 42);
            txtClassroomId.Location = new Point(24, 112);
            txtClassroomId.Margin = new Padding(3, 4, 3, 4);
            txtClassroomId.Name = "txtClassroomId";
            txtClassroomId.PlaceholderForeColor = Color.FromArgb(148, 163, 184);
            txtClassroomId.PlaceholderText = "Enter ID";
            txtClassroomId.SelectedText = "";
            txtClassroomId.Size = new Size(132, 42);
            txtClassroomId.TabIndex = 3;
            lblClassroomId.BackColor = Color.Transparent;
            lblClassroomId.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            lblClassroomId.ForeColor = Color.FromArgb(51, 65, 85);
            lblClassroomId.Location = new Point(24, 87);
            lblClassroomId.Name = "lblClassroomId";
            lblClassroomId.Size = new Size(83, 19);
            lblClassroomId.TabIndex = 2;
            lblClassroomId.Text = "Classroom ID";
            lblEditorSubtitle.BackColor = Color.Transparent;
            lblEditorSubtitle.Font = new Font("Segoe UI", 9F);
            lblEditorSubtitle.ForeColor = Color.FromArgb(100, 116, 139);
            lblEditorSubtitle.Location = new Point(24, 44);
            lblEditorSubtitle.Name = "lblEditorSubtitle";
            lblEditorSubtitle.Size = new Size(278, 17);
            lblEditorSubtitle.TabIndex = 1;
            lblEditorSubtitle.Text = "Prepare classroom details before applying an action.";
            lblEditorTitle.BackColor = Color.Transparent;
            lblEditorTitle.Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold);
            lblEditorTitle.ForeColor = Color.FromArgb(15, 23, 42);
            lblEditorTitle.Location = new Point(24, 18);
            lblEditorTitle.Name = "lblEditorTitle";
            lblEditorTitle.Size = new Size(138, 25);
            lblEditorTitle.TabIndex = 0;
            lblEditorTitle.Text = "Classroom Details";
            pnlHeader.Controls.Add(lblPageSubtitle);
            pnlHeader.Controls.Add(lblPageTitle);
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.FillColor = Color.White;
            pnlHeader.Location = new Point(0, 0);
            pnlHeader.Name = "pnlHeader";
            pnlHeader.Size = new Size(1004, 88);
            pnlHeader.TabIndex = 0;
            lblPageSubtitle.BackColor = Color.Transparent;
            lblPageSubtitle.Font = new Font("Segoe UI", 10F);
            lblPageSubtitle.ForeColor = Color.FromArgb(100, 116, 139);
            lblPageSubtitle.Location = new Point(32, 50);
            lblPageSubtitle.Name = "lblPageSubtitle";
            lblPageSubtitle.Size = new Size(353, 19);
            lblPageSubtitle.TabIndex = 1;
            lblPageSubtitle.Text = "Manage rooms, capacities, and classroom type information.";
            lblPageTitle.BackColor = Color.Transparent;
            lblPageTitle.Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold);
            lblPageTitle.ForeColor = Color.FromArgb(15, 23, 42);
            lblPageTitle.Location = new Point(32, 16);
            lblPageTitle.Name = "lblPageTitle";
            lblPageTitle.Size = new Size(278, 34);
            lblPageTitle.TabIndex = 0;
            lblPageTitle.Text = "Classrooms Management";
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(245, 247, 250);
            ClientSize = new Size(1244, 720);
            Controls.Add(pnlMain);
            Controls.Add(pnlSidebar);
            Font = new Font("Segoe UI", 9F);
            MinimumSize = new Size(980, 600);
            Name = "ClassroomsForm";
            Text = "Classrooms Management";
            pnlSidebar.ResumeLayout(false);
            pnlSidebar.PerformLayout();
            pnlMain.ResumeLayout(false);
            pnlWorkspace.ResumeLayout(false);
            pnlClassroomsTable.ResumeLayout(false);
            pnlClassroomsTable.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvClassrooms).EndInit();
            pnlClassroomEditor.ResumeLayout(false);
            pnlClassroomEditor.PerformLayout();
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
        private Guna.UI2.WinForms.Guna2Button btnNavigationSubjects = null!;
        private Guna.UI2.WinForms.Guna2Button btnNavigationClassrooms = null!;
        private Guna.UI2.WinForms.Guna2Button btnNavigationTimeSlots = null!;
        private Guna.UI2.WinForms.Guna2Button btnNavigationFaculty = null!;
        private Guna.UI2.WinForms.Guna2Button btnNavigationSchedules = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblSidebarFooter = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlMain = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlHeader = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblPageTitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblPageSubtitle = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlWorkspace = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlClassroomEditor = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblEditorTitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblEditorSubtitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblClassroomId = null!;
        private Guna.UI2.WinForms.Guna2TextBox txtClassroomId = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblClassroomNumber = null!;
        private Guna.UI2.WinForms.Guna2TextBox txtClassroomNumber = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblCapacity = null!;
        private Guna.UI2.WinForms.Guna2TextBox txtCapacity = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblRoomType = null!;
        private Guna.UI2.WinForms.Guna2ComboBox cmbRoomType = null!;
        private Guna.UI2.WinForms.Guna2Button btnAddClassroom = null!;
        private Guna.UI2.WinForms.Guna2Button btnUpdateClassroom = null!;
        private Guna.UI2.WinForms.Guna2Button btnDeleteClassroom = null!;
        private Guna.UI2.WinForms.Guna2Button btnClearClassroomForm = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlClassroomsTable = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblTableTitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblTableSubtitle = null!;
        private Guna.UI2.WinForms.Guna2DataGridView dgvClassrooms = null!;
        private System.Windows.Forms.DataGridViewTextBoxColumn colClassroomId = null!;
        private System.Windows.Forms.DataGridViewTextBoxColumn colClassroomNumber = null!;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCapacity = null!;
        private System.Windows.Forms.DataGridViewTextBoxColumn colRoomType = null!;
    }
}

