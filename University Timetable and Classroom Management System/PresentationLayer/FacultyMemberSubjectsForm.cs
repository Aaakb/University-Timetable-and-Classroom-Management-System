using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System
{
    public partial class FacultyMemberSubjectsForm : System.Windows.Forms.UserControl
    {
        private readonly FacultyMemberSubjectService assignmentService = new();
        private readonly FacultyMemberService facultyMemberService = new();
        private readonly SubjectService subjectService = new();

        private int? selectedFacultyMemberId;
        private int? selectedSubjectId;

        public FacultyMemberSubjectsForm()
        {
            InitializeComponent();
            ConfigureNavigation();
            ConfigureAssignmentsGrid();
            ConfigureAssignmentEvents();
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await RefreshAssignmentsAsync();
        }

        private void ConfigureNavigation()
        {
            FormNavigation.ConfigureSidebar(this, pnlSidebar, NavigationPage.FacultyAssignments);
        }

        private void ConfigureAssignmentsGrid()
        {
            dgvFacultyMemberSubjects.AutoGenerateColumns = false;
            dgvFacultyMemberSubjects.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            GridStyle.Apply(dgvFacultyMemberSubjects);
            colFacultyMemberId.DataPropertyName = nameof(AssignmentRow.FacultyMemberID);
            colFacultyMember.DataPropertyName = nameof(AssignmentRow.FacultyMemberName);
            colSubjectId.DataPropertyName = nameof(AssignmentRow.SubjectID);
            colSubject.DataPropertyName = nameof(AssignmentRow.SubjectName);
        }

        private void ConfigureAssignmentEvents()
        {
            dgvFacultyMemberSubjects.SelectionChanged += (_, _) => PopulateAssignmentEditorFromSelection();
            btnAddAssignment.Click += async (_, _) => await AddAssignmentAsync();
            btnUpdateAssignment.Click += async (_, _) => await UpdateAssignmentAsync();
            btnDeleteAssignment.Click += async (_, _) => await DeleteAssignmentAsync();
        }

        private async Task RefreshAssignmentsAsync()
        {
            SetAssignmentActionsEnabled(false);

            try
            {
                await LoadLookupsAsync();
                await LoadAssignmentsAsync();
                ClearAssignmentForm();
            }
            catch (Exception ex)
            {
                ShowError("Unable to refresh teaching assignments.", ex);
            }
            finally
            {
                SetAssignmentActionsEnabled(true);
            }
        }

        private async Task LoadLookupsAsync()
        {
            var facultyMembers = await facultyMemberService.GetAllAsync();
            var subjects = await subjectService.GetAllAsync();

            BindCombo(
                cmbFacultyMember,
                facultyMembers
                    .OrderBy(facultyMember => facultyMember.FullName)
                    .Select(facultyMember => new ComboOption(facultyMember.FacultyMemberID, facultyMember.FullName)));

            BindCombo(
                cmbSubject,
                subjects
                    .OrderBy(subject => subject.StudyYearID)
                    .ThenBy(subject => subject.BranchID ?? 0)
                    .ThenBy(subject => subject.SubjectName)
                    .Select(subject => new ComboOption(subject.SubjectID, FormatSubject(subject))));
        }

        private async Task LoadAssignmentsAsync()
        {
            var assignments = await assignmentService.GetAllAsync();
            dgvFacultyMemberSubjects.DataSource = assignments
                .Select(AssignmentRow.FromAssignment)
                .OrderBy(row => row.SubjectName)
                .ThenBy(row => row.FacultyMemberName)
                .ToList();
            dgvFacultyMemberSubjects.ClearSelection();
        }

        private async Task AddAssignmentAsync()
        {
            if (!TryBuildAssignment(out var assignment))
            {
                return;
            }

            await ExecuteAssignmentActionAsync(
                async () => await assignmentService.AddAsync(assignment),
                UiMessages.RecordAdded);
        }

        private async Task UpdateAssignmentAsync()
        {
            if (!selectedFacultyMemberId.HasValue || !selectedSubjectId.HasValue)
            {
                ShowInformation("Select a teaching assignment before updating.");
                return;
            }

            if (!TryBuildAssignment(out var assignment))
            {
                return;
            }

            await ExecuteAssignmentActionAsync(
                async () => await assignmentService.UpdateAsync(
                    selectedFacultyMemberId.Value,
                    selectedSubjectId.Value,
                    assignment),
                UiMessages.RecordUpdated);
        }

        private async Task DeleteAssignmentAsync()
        {
            if (!selectedFacultyMemberId.HasValue || !selectedSubjectId.HasValue)
            {
                ShowInformation("Select a teaching assignment before deleting.");
                return;
            }

            var confirmation = UiMessages.ConfirmDeletion(this, "Delete Teaching Assignment");

            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            await ExecuteAssignmentActionAsync(
                async () => await assignmentService.DeleteAsync(selectedFacultyMemberId.Value, selectedSubjectId.Value),
                UiMessages.RecordDeleted);
        }

        private async Task ExecuteAssignmentActionAsync(Func<Task> action, string successMessage)
        {
            SetAssignmentActionsEnabled(false);

            try
            {
                await action();
                await LoadAssignmentsAsync();
                ClearAssignmentForm();
                ShowInformation(successMessage);
            }
            catch (Exception ex)
            {
                ShowError("Unable to complete the teaching assignment operation.", ex);
            }
            finally
            {
                SetAssignmentActionsEnabled(true);
            }
        }

        private bool TryBuildAssignment(out FacultyMemberSubject assignment)
        {
            assignment = new FacultyMemberSubject
            {
                FacultyMemberID = GetSelectedRequiredId(cmbFacultyMember),
                SubjectID = GetSelectedRequiredId(cmbSubject)
            };

            if (assignment.FacultyMemberID <= 0)
            {
                ShowInformation(UiMessages.RequiredFields);
                cmbFacultyMember.Focus();
                return false;
            }

            if (assignment.SubjectID <= 0)
            {
                ShowInformation(UiMessages.RequiredFields);
                cmbSubject.Focus();
                return false;
            }

            return true;
        }

        private void PopulateAssignmentEditorFromSelection()
        {
            if (dgvFacultyMemberSubjects.CurrentRow?.DataBoundItem is not AssignmentRow row)
            {
                return;
            }

            selectedFacultyMemberId = row.FacultyMemberID;
            selectedSubjectId = row.SubjectID;
            SelectComboValue(cmbFacultyMember, row.FacultyMemberID);
            SelectComboValue(cmbSubject, row.SubjectID);
        }

        private void ClearAssignmentForm()
        {
            selectedFacultyMemberId = null;
            selectedSubjectId = null;
            ClearCombo(cmbFacultyMember);
            ClearCombo(cmbSubject);
            dgvFacultyMemberSubjects.ClearSelection();
        }

        private void SetAssignmentActionsEnabled(bool enabled)
        {
            btnAddAssignment.Enabled = enabled;
            btnUpdateAssignment.Enabled = enabled;
            btnDeleteAssignment.Enabled = enabled;
            dgvFacultyMemberSubjects.Enabled = enabled;
        }

        private static void BindCombo(Guna.UI2.WinForms.Guna2ComboBox combo, IEnumerable<ComboOption> options)
        {
            ComboBoxHelper.Bind(combo, options);
        }

        private static int GetSelectedRequiredId(Guna.UI2.WinForms.Guna2ComboBox combo)
        {
            return ComboBoxHelper.GetSelectedRequiredId(combo);
        }

        private static void SelectComboValue(Guna.UI2.WinForms.Guna2ComboBox combo, int id)
        {
            ComboBoxHelper.SelectValue(combo, id);
        }

        private static void ClearCombo(Guna.UI2.WinForms.Guna2ComboBox combo)
        {
            ComboBoxHelper.Clear(combo);
        }

        private static string FormatSubject(Subject subject)
        {
            var year = subject.StudyYear?.YearName ?? "Year";
            var branch = subject.Branch?.BranchName ?? "General";
            return $"{subject.SubjectName} - {year} - {branch}";
        }

        private void ShowInformation(string message)
        {
            UiMessages.ShowInformation(this, message, "Teaching Assignments");
        }

        private void ShowError(string message, Exception ex)
        {
            UiMessages.ShowError(this, message, "Teaching Assignments", ex);
        }

        private sealed class AssignmentRow
        {
            public int FacultyMemberID { get; init; }
            public string FacultyMemberName { get; init; } = string.Empty;
            public int SubjectID { get; init; }
            public string SubjectName { get; init; } = string.Empty;

            public static AssignmentRow FromAssignment(FacultyMemberSubject assignment)
            {
                return new AssignmentRow
                {
                    FacultyMemberID = assignment.FacultyMemberID,
                    FacultyMemberName = assignment.FacultyMember?.FullName ?? "-",
                    SubjectID = assignment.SubjectID,
                    SubjectName = assignment.Subject is null ? "-" : FormatSubject(assignment.Subject)
                };
            }
        }
        private void InitializeComponent()
        {
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle3 = new DataGridViewCellStyle();
            pnlSidebar = new Guna.UI2.WinForms.Guna2Panel();
            lblSidebarFooter = new Guna.UI2.WinForms.Guna2HtmlLabel();
            btnNavigationSchedules = new Guna.UI2.WinForms.Guna2Button();
            btnNavigationFacultyMembers = new Guna.UI2.WinForms.Guna2Button();
            btnNavigationFacultyAssignments = new Guna.UI2.WinForms.Guna2Button();
            btnNavigationTimeSlots = new Guna.UI2.WinForms.Guna2Button();
            btnNavigationClassrooms = new Guna.UI2.WinForms.Guna2Button();
            btnNavigationSubjects = new Guna.UI2.WinForms.Guna2Button();
            btnNavigationSections = new Guna.UI2.WinForms.Guna2Button();
            btnNavigationStudyYears = new Guna.UI2.WinForms.Guna2Button();
            btnNavigationBranches = new Guna.UI2.WinForms.Guna2Button();
            separatorSidebar = new Guna.UI2.WinForms.Guna2Separator();
            lblSidebarSubtitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblApplicationName = new Guna.UI2.WinForms.Guna2HtmlLabel();
            pnlMain = new Guna.UI2.WinForms.Guna2Panel();
            pnlWorkspace = new Guna.UI2.WinForms.Guna2Panel();
            pnlAssignmentsTable = new Guna.UI2.WinForms.Guna2Panel();
            dgvFacultyMemberSubjects = new Guna.UI2.WinForms.Guna2DataGridView();
            colFacultyMemberId = new DataGridViewTextBoxColumn();
            colFacultyMember = new DataGridViewTextBoxColumn();
            colSubjectId = new DataGridViewTextBoxColumn();
            colSubject = new DataGridViewTextBoxColumn();
            lblTableSubtitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblTableTitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            pnlAssignmentEditor = new Guna.UI2.WinForms.Guna2Panel();
            btnClearAssignmentForm = new Guna.UI2.WinForms.Guna2Button();
            btnDeleteAssignment = new Guna.UI2.WinForms.Guna2Button();
            btnUpdateAssignment = new Guna.UI2.WinForms.Guna2Button();
            btnAddAssignment = new Guna.UI2.WinForms.Guna2Button();
            cmbSubject = new Guna.UI2.WinForms.Guna2ComboBox();
            lblSubject = new Guna.UI2.WinForms.Guna2HtmlLabel();
            cmbFacultyMember = new Guna.UI2.WinForms.Guna2ComboBox();
            lblFacultyMember = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblEditorSubtitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblEditorTitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            pnlHeader = new Guna.UI2.WinForms.Guna2Panel();
            lblPageSubtitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblPageTitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            pnlSidebar.SuspendLayout();
            pnlMain.SuspendLayout();
            pnlWorkspace.SuspendLayout();
            pnlAssignmentsTable.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvFacultyMemberSubjects).BeginInit();
            pnlAssignmentEditor.SuspendLayout();
            pnlHeader.SuspendLayout();
            SuspendLayout();
            pnlSidebar.BackColor = Color.Transparent;
            pnlSidebar.Controls.Add(lblSidebarFooter);
            pnlSidebar.Controls.Add(btnNavigationSchedules);
            pnlSidebar.Controls.Add(btnNavigationFacultyMembers);
            pnlSidebar.Controls.Add(btnNavigationFacultyAssignments);
            pnlSidebar.Controls.Add(btnNavigationTimeSlots);
            pnlSidebar.Controls.Add(btnNavigationClassrooms);
            pnlSidebar.Controls.Add(btnNavigationSubjects);
            pnlSidebar.Controls.Add(btnNavigationSections);
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
            lblSidebarFooter.TabIndex = 13;
            lblSidebarFooter.Text = "Academic Scheduling Suite";
            btnNavigationSchedules.BorderRadius = 8;
            btnNavigationSchedules.Cursor = Cursors.Hand;
            btnNavigationSchedules.FillColor = Color.FromArgb(24, 38, 62);
            btnNavigationSchedules.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            btnNavigationSchedules.ForeColor = Color.FromArgb(226, 232, 240);
            btnNavigationSchedules.HoverState.FillColor = Color.FromArgb(36, 55, 86);
            btnNavigationSchedules.Location = new Point(24, 602);
            btnNavigationSchedules.Name = "btnNavigationSchedules";
            btnNavigationSchedules.Size = new Size(192, 44);
            btnNavigationSchedules.TabIndex = 12;
            btnNavigationSchedules.Text = "Schedules";
            btnNavigationSchedules.TextAlign = HorizontalAlignment.Left;
            btnNavigationSchedules.TextOffset = new Point(14, 0);
            btnNavigationFacultyMembers.BorderRadius = 8;
            btnNavigationFacultyMembers.Cursor = Cursors.Hand;
            btnNavigationFacultyMembers.FillColor = Color.FromArgb(24, 38, 62);
            btnNavigationFacultyMembers.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            btnNavigationFacultyMembers.ForeColor = Color.FromArgb(226, 232, 240);
            btnNavigationFacultyMembers.HoverState.FillColor = Color.FromArgb(36, 55, 86);
            btnNavigationFacultyMembers.Location = new Point(24, 546);
            btnNavigationFacultyMembers.Name = "btnNavigationFacultyMembers";
            btnNavigationFacultyMembers.Size = new Size(192, 44);
            btnNavigationFacultyMembers.TabIndex = 11;
            btnNavigationFacultyMembers.Text = "Faculty Members";
            btnNavigationFacultyMembers.TextAlign = HorizontalAlignment.Left;
            btnNavigationFacultyMembers.TextOffset = new Point(14, 0);
            btnNavigationFacultyAssignments.BorderRadius = 8;
            btnNavigationFacultyAssignments.Checked = true;
            btnNavigationFacultyAssignments.Cursor = Cursors.Hand;
            btnNavigationFacultyAssignments.Enabled = false;
            btnNavigationFacultyAssignments.FillColor = Color.FromArgb(37, 99, 235);
            btnNavigationFacultyAssignments.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            btnNavigationFacultyAssignments.ForeColor = Color.White;
            btnNavigationFacultyAssignments.HoverState.FillColor = Color.FromArgb(29, 78, 216);
            btnNavigationFacultyAssignments.Location = new Point(24, 490);
            btnNavigationFacultyAssignments.Name = "btnNavigationFacultyAssignments";
            btnNavigationFacultyAssignments.Size = new Size(192, 44);
            btnNavigationFacultyAssignments.TabIndex = 10;
            btnNavigationFacultyAssignments.Text = "Faculty Assignments";
            btnNavigationFacultyAssignments.TextAlign = HorizontalAlignment.Left;
            btnNavigationFacultyAssignments.TextOffset = new Point(14, 0);
            btnNavigationTimeSlots.BorderRadius = 8;
            btnNavigationTimeSlots.Cursor = Cursors.Hand;
            btnNavigationTimeSlots.FillColor = Color.FromArgb(24, 38, 62);
            btnNavigationTimeSlots.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            btnNavigationTimeSlots.ForeColor = Color.FromArgb(226, 232, 240);
            btnNavigationTimeSlots.HoverState.FillColor = Color.FromArgb(36, 55, 86);
            btnNavigationTimeSlots.Location = new Point(24, 434);
            btnNavigationTimeSlots.Name = "btnNavigationTimeSlots";
            btnNavigationTimeSlots.Size = new Size(192, 44);
            btnNavigationTimeSlots.TabIndex = 9;
            btnNavigationTimeSlots.Text = "Time Slots";
            btnNavigationTimeSlots.TextAlign = HorizontalAlignment.Left;
            btnNavigationTimeSlots.TextOffset = new Point(14, 0);
            btnNavigationClassrooms.BorderRadius = 8;
            btnNavigationClassrooms.Cursor = Cursors.Hand;
            btnNavigationClassrooms.FillColor = Color.FromArgb(24, 38, 62);
            btnNavigationClassrooms.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            btnNavigationClassrooms.ForeColor = Color.FromArgb(226, 232, 240);
            btnNavigationClassrooms.HoverState.FillColor = Color.FromArgb(36, 55, 86);
            btnNavigationClassrooms.Location = new Point(24, 378);
            btnNavigationClassrooms.Name = "btnNavigationClassrooms";
            btnNavigationClassrooms.Size = new Size(192, 44);
            btnNavigationClassrooms.TabIndex = 8;
            btnNavigationClassrooms.Text = "Classrooms";
            btnNavigationClassrooms.TextAlign = HorizontalAlignment.Left;
            btnNavigationClassrooms.TextOffset = new Point(14, 0);
            btnNavigationSubjects.BorderRadius = 8;
            btnNavigationSubjects.Cursor = Cursors.Hand;
            btnNavigationSubjects.FillColor = Color.FromArgb(24, 38, 62);
            btnNavigationSubjects.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            btnNavigationSubjects.ForeColor = Color.FromArgb(226, 232, 240);
            btnNavigationSubjects.HoverState.FillColor = Color.FromArgb(36, 55, 86);
            btnNavigationSubjects.Location = new Point(24, 322);
            btnNavigationSubjects.Name = "btnNavigationSubjects";
            btnNavigationSubjects.Size = new Size(192, 44);
            btnNavigationSubjects.TabIndex = 7;
            btnNavigationSubjects.Text = "Subjects";
            btnNavigationSubjects.TextAlign = HorizontalAlignment.Left;
            btnNavigationSubjects.TextOffset = new Point(14, 0);
            btnNavigationSections.BorderRadius = 8;
            btnNavigationSections.Cursor = Cursors.Hand;
            btnNavigationSections.FillColor = Color.FromArgb(24, 38, 62);
            btnNavigationSections.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            btnNavigationSections.ForeColor = Color.FromArgb(226, 232, 240);
            btnNavigationSections.HoverState.FillColor = Color.FromArgb(36, 55, 86);
            btnNavigationSections.Location = new Point(24, 266);
            btnNavigationSections.Name = "btnNavigationSections";
            btnNavigationSections.Size = new Size(192, 44);
            btnNavigationSections.TabIndex = 6;
            btnNavigationSections.Text = "Sections";
            btnNavigationSections.TextAlign = HorizontalAlignment.Left;
            btnNavigationSections.TextOffset = new Point(14, 0);
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
            pnlMain.Size = new Size(940, 720);
            pnlMain.TabIndex = 1;
            pnlWorkspace.Controls.Add(pnlAssignmentsTable);
            pnlWorkspace.Controls.Add(pnlAssignmentEditor);
            pnlWorkspace.Dock = DockStyle.Fill;
            pnlWorkspace.FillColor = Color.FromArgb(245, 247, 250);
            pnlWorkspace.Location = new Point(0, 88);
            pnlWorkspace.Name = "pnlWorkspace";
            pnlWorkspace.Padding = new Padding(28, 24, 28, 28);
            pnlWorkspace.Size = new Size(940, 632);
            pnlWorkspace.TabIndex = 1;
            pnlAssignmentsTable.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pnlAssignmentsTable.BackColor = Color.Transparent;
            pnlAssignmentsTable.BorderColor = Color.FromArgb(226, 232, 240);
            pnlAssignmentsTable.BorderRadius = 8;
            pnlAssignmentsTable.BorderThickness = 1;
            pnlAssignmentsTable.Controls.Add(dgvFacultyMemberSubjects);
            pnlAssignmentsTable.Controls.Add(lblTableSubtitle);
            pnlAssignmentsTable.Controls.Add(lblTableTitle);
            pnlAssignmentsTable.FillColor = Color.White;
            pnlAssignmentsTable.Location = new Point(28, 248);
            pnlAssignmentsTable.Name = "pnlAssignmentsTable";
            pnlAssignmentsTable.Size = new Size(884, 356);
            pnlAssignmentsTable.TabIndex = 1;
            dgvFacultyMemberSubjects.AllowUserToAddRows = false;
            dgvFacultyMemberSubjects.AllowUserToDeleteRows = false;
            dgvFacultyMemberSubjects.AllowUserToResizeRows = false;
            dataGridViewCellStyle1.BackColor = Color.FromArgb(248, 250, 252);
            dataGridViewCellStyle1.ForeColor = Color.FromArgb(30, 41, 59);
            dataGridViewCellStyle1.SelectionBackColor = Color.FromArgb(219, 234, 254);
            dataGridViewCellStyle1.SelectionForeColor = Color.FromArgb(30, 64, 175);
            dgvFacultyMemberSubjects.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            dgvFacultyMemberSubjects.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = Color.FromArgb(15, 23, 42);
            dataGridViewCellStyle2.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            dataGridViewCellStyle2.ForeColor = Color.White;
            dataGridViewCellStyle2.SelectionBackColor = Color.FromArgb(15, 23, 42);
            dataGridViewCellStyle2.SelectionForeColor = Color.White;
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.True;
            dgvFacultyMemberSubjects.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle2;
            dgvFacultyMemberSubjects.ColumnHeadersHeight = 44;
            dgvFacultyMemberSubjects.Columns.AddRange(new DataGridViewColumn[] { colFacultyMemberId, colFacultyMember, colSubjectId, colSubject });
            dataGridViewCellStyle3.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = Color.White;
            dataGridViewCellStyle3.Font = new Font("Segoe UI", 10F);
            dataGridViewCellStyle3.ForeColor = Color.FromArgb(30, 41, 59);
            dataGridViewCellStyle3.SelectionBackColor = Color.FromArgb(219, 234, 254);
            dataGridViewCellStyle3.SelectionForeColor = Color.FromArgb(30, 64, 175);
            dataGridViewCellStyle3.WrapMode = DataGridViewTriState.False;
            dgvFacultyMemberSubjects.DefaultCellStyle = dataGridViewCellStyle3;
            dgvFacultyMemberSubjects.GridColor = Color.FromArgb(226, 232, 240);
            dgvFacultyMemberSubjects.Location = new Point(24, 78);
            dgvFacultyMemberSubjects.MultiSelect = false;
            dgvFacultyMemberSubjects.Name = "dgvFacultyMemberSubjects";
            dgvFacultyMemberSubjects.ReadOnly = true;
            dgvFacultyMemberSubjects.RowHeadersVisible = false;
            dgvFacultyMemberSubjects.RowTemplate.Height = 42;
            dgvFacultyMemberSubjects.Size = new Size(836, 255);
            dgvFacultyMemberSubjects.TabIndex = 2;
            dgvFacultyMemberSubjects.ThemeStyle.AlternatingRowsStyle.BackColor = Color.FromArgb(248, 250, 252);
            dgvFacultyMemberSubjects.ThemeStyle.AlternatingRowsStyle.ForeColor = Color.FromArgb(30, 41, 59);
            dgvFacultyMemberSubjects.ThemeStyle.AlternatingRowsStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
            dgvFacultyMemberSubjects.ThemeStyle.AlternatingRowsStyle.SelectionForeColor = Color.FromArgb(30, 64, 175);
            dgvFacultyMemberSubjects.ThemeStyle.GridColor = Color.FromArgb(226, 232, 240);
            dgvFacultyMemberSubjects.ThemeStyle.HeaderStyle.BackColor = Color.FromArgb(15, 23, 42);
            dgvFacultyMemberSubjects.ThemeStyle.HeaderStyle.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            dgvFacultyMemberSubjects.ThemeStyle.HeaderStyle.HeaightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvFacultyMemberSubjects.ThemeStyle.HeaderStyle.Height = 44;
            dgvFacultyMemberSubjects.ThemeStyle.ReadOnly = true;
            dgvFacultyMemberSubjects.ThemeStyle.RowsStyle.Font = new Font("Segoe UI", 10F);
            dgvFacultyMemberSubjects.ThemeStyle.RowsStyle.ForeColor = Color.FromArgb(30, 41, 59);
            dgvFacultyMemberSubjects.ThemeStyle.RowsStyle.Height = 42;
            dgvFacultyMemberSubjects.ThemeStyle.RowsStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
            dgvFacultyMemberSubjects.ThemeStyle.RowsStyle.SelectionForeColor = Color.FromArgb(30, 64, 175);
            colFacultyMemberId.DataPropertyName = "FacultyMemberID";
            colFacultyMemberId.FillWeight = 45F;
            colFacultyMemberId.HeaderText = "Faculty ID";
            colFacultyMemberId.Name = "colFacultyMemberId";
            colFacultyMemberId.ReadOnly = true;
            colFacultyMember.FillWeight = 125F;
            colFacultyMember.HeaderText = "Faculty Member";
            colFacultyMember.Name = "colFacultyMember";
            colFacultyMember.ReadOnly = true;
            colSubjectId.DataPropertyName = "SubjectID";
            colSubjectId.FillWeight = 45F;
            colSubjectId.HeaderText = "Subject ID";
            colSubjectId.Name = "colSubjectId";
            colSubjectId.ReadOnly = true;
            colSubject.FillWeight = 125F;
            colSubject.HeaderText = "Subject";
            colSubject.Name = "colSubject";
            colSubject.ReadOnly = true;
            lblTableSubtitle.BackColor = Color.Transparent;
            lblTableSubtitle.Font = new Font("Segoe UI", 9F);
            lblTableSubtitle.ForeColor = Color.FromArgb(100, 116, 139);
            lblTableSubtitle.Location = new Point(24, 43);
            lblTableSubtitle.Name = "lblTableSubtitle";
            lblTableSubtitle.Size = new Size(279, 17);
            lblTableSubtitle.TabIndex = 1;
            lblTableSubtitle.Text = "Review assigned teaching responsibilities by subject.";
            lblTableTitle.BackColor = Color.Transparent;
            lblTableTitle.Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold);
            lblTableTitle.ForeColor = Color.FromArgb(15, 23, 42);
            lblTableTitle.Location = new Point(24, 18);
            lblTableTitle.Name = "lblTableTitle";
            lblTableTitle.Size = new Size(189, 25);
            lblTableTitle.TabIndex = 0;
            lblTableTitle.Text = "Faculty Assignments List";
            pnlAssignmentEditor.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnlAssignmentEditor.BackColor = Color.Transparent;
            pnlAssignmentEditor.BorderColor = Color.FromArgb(226, 232, 240);
            pnlAssignmentEditor.BorderRadius = 8;
            pnlAssignmentEditor.BorderThickness = 1;
            pnlAssignmentEditor.Controls.Add(btnClearAssignmentForm);
            pnlAssignmentEditor.Controls.Add(btnDeleteAssignment);
            pnlAssignmentEditor.Controls.Add(btnUpdateAssignment);
            pnlAssignmentEditor.Controls.Add(btnAddAssignment);
            pnlAssignmentEditor.Controls.Add(cmbSubject);
            pnlAssignmentEditor.Controls.Add(lblSubject);
            pnlAssignmentEditor.Controls.Add(cmbFacultyMember);
            pnlAssignmentEditor.Controls.Add(lblFacultyMember);
            pnlAssignmentEditor.Controls.Add(lblEditorSubtitle);
            pnlAssignmentEditor.Controls.Add(lblEditorTitle);
            pnlAssignmentEditor.FillColor = Color.White;
            pnlAssignmentEditor.Location = new Point(28, 24);
            pnlAssignmentEditor.Name = "pnlAssignmentEditor";
            pnlAssignmentEditor.Size = new Size(884, 202);
            pnlAssignmentEditor.TabIndex = 0;
            btnClearAssignmentForm.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClearAssignmentForm.BorderColor = Color.FromArgb(203, 213, 225);
            btnClearAssignmentForm.BorderRadius = 8;
            btnClearAssignmentForm.BorderThickness = 1;
            btnClearAssignmentForm.Cursor = Cursors.Hand;
            btnClearAssignmentForm.FillColor = Color.White;
            btnClearAssignmentForm.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnClearAssignmentForm.ForeColor = Color.FromArgb(51, 65, 85);
            btnClearAssignmentForm.HoverState.FillColor = Color.FromArgb(248, 250, 252);
            btnClearAssignmentForm.Location = new Point(752, 142);
            btnClearAssignmentForm.Name = "btnClearAssignmentForm";
            btnClearAssignmentForm.Size = new Size(108, 38);
            btnClearAssignmentForm.TabIndex = 9;
            btnClearAssignmentForm.Text = "Clear";
            btnClearAssignmentForm.Visible = false;
            btnDeleteAssignment.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnDeleteAssignment.BorderRadius = 8;
            btnDeleteAssignment.Cursor = Cursors.Hand;
            btnDeleteAssignment.FillColor = Color.FromArgb(220, 38, 38);
            btnDeleteAssignment.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnDeleteAssignment.ForeColor = Color.White;
            btnDeleteAssignment.HoverState.FillColor = Color.FromArgb(185, 28, 28);
            btnDeleteAssignment.Location = new Point(632, 142);
            btnDeleteAssignment.Name = "btnDeleteAssignment";
            btnDeleteAssignment.Size = new Size(108, 38);
            btnDeleteAssignment.TabIndex = 8;
            btnDeleteAssignment.Text = "Delete";
            btnUpdateAssignment.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnUpdateAssignment.BorderRadius = 8;
            btnUpdateAssignment.Cursor = Cursors.Hand;
            btnUpdateAssignment.FillColor = Color.FromArgb(14, 116, 144);
            btnUpdateAssignment.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnUpdateAssignment.ForeColor = Color.White;
            btnUpdateAssignment.HoverState.FillColor = Color.FromArgb(21, 94, 117);
            btnUpdateAssignment.Location = new Point(752, 98);
            btnUpdateAssignment.Name = "btnUpdateAssignment";
            btnUpdateAssignment.Size = new Size(108, 38);
            btnUpdateAssignment.TabIndex = 7;
            btnUpdateAssignment.Text = "Update";
            btnAddAssignment.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnAddAssignment.BorderRadius = 8;
            btnAddAssignment.Cursor = Cursors.Hand;
            btnAddAssignment.FillColor = Color.FromArgb(22, 163, 74);
            btnAddAssignment.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnAddAssignment.ForeColor = Color.White;
            btnAddAssignment.HoverState.FillColor = Color.FromArgb(21, 128, 61);
            btnAddAssignment.Location = new Point(632, 98);
            btnAddAssignment.Name = "btnAddAssignment";
            btnAddAssignment.Size = new Size(108, 38);
            btnAddAssignment.TabIndex = 6;
            btnAddAssignment.Text = "Add";
            cmbSubject.BackColor = Color.Transparent;
            cmbSubject.BorderColor = Color.FromArgb(203, 213, 225);
            cmbSubject.BorderRadius = 8;
            cmbSubject.DrawMode = DrawMode.OwnerDrawFixed;
            cmbSubject.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbSubject.FocusedColor = Color.FromArgb(37, 99, 235);
            cmbSubject.FocusedState.BorderColor = Color.FromArgb(37, 99, 235);
            cmbSubject.Font = new Font("Segoe UI", 10F);
            cmbSubject.ForeColor = Color.FromArgb(15, 23, 42);
            cmbSubject.HoverState.BorderColor = Color.FromArgb(59, 130, 246);
            cmbSubject.ItemHeight = 36;
            cmbSubject.Location = new Point(316, 112);
            cmbSubject.Name = "cmbSubject";
            cmbSubject.Size = new Size(276, 42);
            cmbSubject.TabIndex = 5;
            lblSubject.BackColor = Color.Transparent;
            lblSubject.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            lblSubject.ForeColor = Color.FromArgb(51, 65, 85);
            lblSubject.Location = new Point(316, 87);
            lblSubject.Name = "lblSubject";
            lblSubject.Size = new Size(47, 19);
            lblSubject.TabIndex = 4;
            lblSubject.Text = "Subject";
            cmbFacultyMember.BackColor = Color.Transparent;
            cmbFacultyMember.BorderColor = Color.FromArgb(203, 213, 225);
            cmbFacultyMember.BorderRadius = 8;
            cmbFacultyMember.DrawMode = DrawMode.OwnerDrawFixed;
            cmbFacultyMember.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbFacultyMember.FocusedColor = Color.FromArgb(37, 99, 235);
            cmbFacultyMember.FocusedState.BorderColor = Color.FromArgb(37, 99, 235);
            cmbFacultyMember.Font = new Font("Segoe UI", 10F);
            cmbFacultyMember.ForeColor = Color.FromArgb(15, 23, 42);
            cmbFacultyMember.HoverState.BorderColor = Color.FromArgb(59, 130, 246);
            cmbFacultyMember.ItemHeight = 36;
            cmbFacultyMember.Location = new Point(24, 112);
            cmbFacultyMember.Name = "cmbFacultyMember";
            cmbFacultyMember.Size = new Size(264, 42);
            cmbFacultyMember.TabIndex = 3;
            lblFacultyMember.BackColor = Color.Transparent;
            lblFacultyMember.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            lblFacultyMember.ForeColor = Color.FromArgb(51, 65, 85);
            lblFacultyMember.Location = new Point(24, 87);
            lblFacultyMember.Name = "lblFacultyMember";
            lblFacultyMember.Size = new Size(101, 19);
            lblFacultyMember.TabIndex = 2;
            lblFacultyMember.Text = "Faculty Member";
            lblEditorSubtitle.BackColor = Color.Transparent;
            lblEditorSubtitle.Font = new Font("Segoe UI", 9F);
            lblEditorSubtitle.ForeColor = Color.FromArgb(100, 116, 139);
            lblEditorSubtitle.Location = new Point(24, 44);
            lblEditorSubtitle.Name = "lblEditorSubtitle";
            lblEditorSubtitle.Size = new Size(323, 17);
            lblEditorSubtitle.TabIndex = 1;
            lblEditorSubtitle.Text = "Assign teaching responsibilities before generating schedules.";
            lblEditorTitle.BackColor = Color.Transparent;
            lblEditorTitle.Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold);
            lblEditorTitle.ForeColor = Color.FromArgb(15, 23, 42);
            lblEditorTitle.Location = new Point(24, 18);
            lblEditorTitle.Name = "lblEditorTitle";
            lblEditorTitle.Size = new Size(208, 25);
            lblEditorTitle.TabIndex = 0;
            lblEditorTitle.Text = "Faculty Assignment Details";
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
            lblPageSubtitle.Size = new Size(364, 19);
            lblPageSubtitle.TabIndex = 1;
            lblPageSubtitle.Text = "Manage faculty-to-subject assignments used by the timetable.";
            lblPageTitle.BackColor = Color.Transparent;
            lblPageTitle.Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold);
            lblPageTitle.ForeColor = Color.FromArgb(15, 23, 42);
            lblPageTitle.Location = new Point(32, 16);
            lblPageTitle.Name = "lblPageTitle";
            lblPageTitle.Size = new Size(377, 34);
            lblPageTitle.TabIndex = 0;
            lblPageTitle.Text = "Faculty Assignments Management";
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(245, 247, 250);
            ClientSize = new Size(1180, 720);
            Controls.Add(pnlMain);
            Controls.Add(pnlSidebar);
            Font = new Font("Segoe UI", 9F);
            MinimumSize = new Size(980, 600);
            Name = "FacultyMemberSubjectsForm";
            Text = "Faculty Assignments Management";
            pnlSidebar.ResumeLayout(false);
            pnlSidebar.PerformLayout();
            pnlMain.ResumeLayout(false);
            pnlWorkspace.ResumeLayout(false);
            pnlAssignmentsTable.ResumeLayout(false);
            pnlAssignmentsTable.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvFacultyMemberSubjects).EndInit();
            pnlAssignmentEditor.ResumeLayout(false);
            pnlAssignmentEditor.PerformLayout();
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
        private Guna.UI2.WinForms.Guna2Button btnNavigationSections = null!;
        private Guna.UI2.WinForms.Guna2Button btnNavigationSubjects = null!;
        private Guna.UI2.WinForms.Guna2Button btnNavigationClassrooms = null!;
        private Guna.UI2.WinForms.Guna2Button btnNavigationTimeSlots = null!;
        private Guna.UI2.WinForms.Guna2Button btnNavigationFacultyAssignments = null!;
        private Guna.UI2.WinForms.Guna2Button btnNavigationFacultyMembers = null!;
        private Guna.UI2.WinForms.Guna2Button btnNavigationSchedules = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblSidebarFooter = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlMain = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlHeader = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblPageTitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblPageSubtitle = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlWorkspace = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlAssignmentEditor = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblEditorTitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblEditorSubtitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblFacultyMember = null!;
        private Guna.UI2.WinForms.Guna2ComboBox cmbFacultyMember = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblSubject = null!;
        private Guna.UI2.WinForms.Guna2ComboBox cmbSubject = null!;
        private Guna.UI2.WinForms.Guna2Button btnAddAssignment = null!;
        private Guna.UI2.WinForms.Guna2Button btnUpdateAssignment = null!;
        private Guna.UI2.WinForms.Guna2Button btnDeleteAssignment = null!;
        private Guna.UI2.WinForms.Guna2Button btnClearAssignmentForm = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlAssignmentsTable = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblTableTitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblTableSubtitle = null!;
        private Guna.UI2.WinForms.Guna2DataGridView dgvFacultyMemberSubjects = null!;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFacultyMemberId = null!;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFacultyMember = null!;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSubjectId = null!;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSubject = null!;
    }
}

