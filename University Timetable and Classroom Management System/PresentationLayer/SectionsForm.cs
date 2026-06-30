using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System
{
    public partial class SectionsForm : System.Windows.Forms.UserControl
    {
        private readonly SectionService sectionService = new();
        private readonly StudyYearService studyYearService = new();
        private readonly BranchService branchService = new();

        private List<StudyYear> studyYearsLookup = [];
        private List<Branch> branchesLookup = [];

        public SectionsForm()
        {
            InitializeComponent();
            ConfigureAutoIdField();
            ConfigureNavigation();
            ConfigureSectionsGrid();
            ConfigureSectionsEvents();
        }

        private void ConfigureAutoIdField()
        {
            txtSectionId.ReadOnly = true;
            txtSectionId.TabStop = false;
            txtSectionId.PlaceholderText = "Auto";
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await RefreshSectionsAsync();
        }

        private void ConfigureNavigation()
        {
            FormNavigation.ConfigureSidebar(this, pnlSidebar, NavigationPage.Sections);
        }

        private void ConfigureSectionsGrid()
        {
            dgvSections.AutoGenerateColumns = false;
            dgvSections.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            GridStyle.Apply(dgvSections);
            colSectionId.DataPropertyName = nameof(SectionRow.SectionID);
            colSectionName.DataPropertyName = nameof(SectionRow.SectionName);
            colStudentCount.DataPropertyName = nameof(SectionRow.StudentCount);
            colStudyYear.DataPropertyName = nameof(SectionRow.StudyYearName);
            colBranch.DataPropertyName = nameof(SectionRow.BranchName);
        }

        private void ConfigureSectionsEvents()
        {
            dgvSections.SelectionChanged += (_, _) => PopulateSectionEditorFromSelection();
            txtSectionId.Leave += async (_, _) => await PopulateSectionEditorFromEnteredIdAsync();
            cmbStudyYear.SelectedIndexChanged += (_, _) => ApplyStudyYearRulesToEditor();
            cmbBranch.SelectedIndexChanged += (_, _) => ApplyBranchSelectionToSectionName();
            btnAddSection.Click += async (_, _) => await AddSectionAsync();
            btnUpdateSection.Click += async (_, _) => await UpdateSectionAsync();
            btnDeleteSection.Click += async (_, _) => await DeleteSectionAsync();
        }

        private async Task RefreshSectionsAsync()
        {
            SetSectionActionsEnabled(false);

            try
            {
                await LoadLookupsAsync();
                await LoadSectionsAsync();
                ClearSectionForm();
            }
            catch (Exception ex)
            {
                ShowError("Unable to refresh sections.", ex);
            }
            finally
            {
                SetSectionActionsEnabled(true);
            }
        }

        private async Task LoadLookupsAsync()
        {
            studyYearsLookup = await studyYearService.GetAllAsync();
            branchesLookup = await branchService.GetAllAsync();

            BindCombo(cmbStudyYear, studyYearsLookup.Select(studyYear => new ComboOption(studyYear.StudyYearID, studyYear.YearName)));
            BindBranchCombo();
        }

        private async Task LoadSectionsAsync()
        {
            var sections = await sectionService.GetAllAsync();
            dgvSections.DataSource = sections
                .Where(SectionShouldBeDisplayed)
                .Select(SectionRow.FromSection)
                .OrderBy(row => row.StudyYearID)
                .ThenBy(row => row.BranchID ?? 0)
                .ThenBy(row => row.SectionName)
                .ToList();
            dgvSections.ClearSelection();
        }

        private static bool SectionShouldBeDisplayed(Section section)
        {
            if (AcademicStructureRules.UsesGeneralSections(section.StudyYearID))
            {
                return !section.BranchID.HasValue &&
                    AcademicStructureRules.GetAllowedSectionNames(section.StudyYearID)
                        .Contains(section.SectionName.Trim(), StringComparer.OrdinalIgnoreCase);
            }

            return true;
        }

        private async Task AddSectionAsync()
        {
            if (!TryBuildSection(out var section, requireId: false))
            {
                return;
            }

            await ExecuteSectionActionAsync(
                async () => await sectionService.AddAsync(section),
                UiMessages.RecordAdded);
        }

        private async Task UpdateSectionAsync()
        {
            if (!TryBuildSection(out var section))
            {
                return;
            }

            await ExecuteSectionActionAsync(
                async () => await sectionService.UpdateAsync(section),
                UiMessages.RecordUpdated);
        }

        private async Task DeleteSectionAsync()
        {
            if (!TryGetSectionIdFromEditor(out int sectionId))
            {
                return;
            }

            var confirmation = UiMessages.ConfirmDeletion(this, "Delete Section");

            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            await ExecuteSectionActionAsync(
                async () => await sectionService.DeleteAsync(sectionId),
                UiMessages.RecordDeleted);
        }

        private async Task ExecuteSectionActionAsync(Func<Task> action, string successMessage)
        {
            SetSectionActionsEnabled(false);

            try
            {
                await action();
                await LoadSectionsAsync();
                ClearSectionForm();
                ShowInformation(successMessage);
            }
            catch (Exception ex)
            {
                ShowError("Unable to complete the section operation.", ex);
            }
            finally
            {
                SetSectionActionsEnabled(true);
            }
        }

        private bool TryBuildSection(out Section section, bool requireId = true)
        {
            section = new Section
            {
                SectionName = txtSectionName.Text.Trim(),
                StudyYearID = GetSelectedRequiredId(cmbStudyYear),
                BranchID = GetSelectedOptionalId(cmbBranch)
            };

            var sectionId = 0;
            if (requireId && !TryGetSectionIdFromEditor(out sectionId))
            {
                return false;
            }

            section.SectionID = requireId ? sectionId : 0;

            if (string.IsNullOrWhiteSpace(section.SectionName))
            {
                ShowInformation(UiMessages.RequiredFields);
                txtSectionName.Focus();
                return false;
            }

            if (!int.TryParse(txtStudentCount.Text, out int studentCount) || studentCount < 0)
            {
                ShowInformation("Student count must be zero or greater.");
                txtStudentCount.Focus();
                return false;
            }

            if (section.StudyYearID <= 0)
            {
                ShowInformation(UiMessages.RequiredFields);
                cmbStudyYear.Focus();
                return false;
            }

            if (AcademicStructureRules.UsesGeneralSections(section.StudyYearID))
            {
                section.BranchID = null;
                var allowedNames = AcademicStructureRules.GetAllowedSectionNames(section.StudyYearID);

                if (!allowedNames.Contains(section.SectionName, StringComparer.OrdinalIgnoreCase))
                {
                    ShowInformation($"Allowed section names are: {AcademicStructureRules.FormatAllowedSectionNames(section.StudyYearID)}.");
                    txtSectionName.Focus();
                    return false;
                }
            }

            if (AcademicStructureRules.UsesBranches(section.StudyYearID) && !section.BranchID.HasValue)
            {
                ShowInformation(UiMessages.RequiredFields);
                cmbBranch.Focus();
                return false;
            }

            section.StudentCount = studentCount;
            return true;
        }

        private bool TryGetSectionIdFromEditor(out int sectionId)
        {
            if (int.TryParse(txtSectionId.Text, out sectionId) && sectionId > 0)
            {
                return true;
            }

            ShowInformation("Select a section row first.");
            return false;
        }

        private void PopulateSectionEditorFromSelection()
        {
            if (dgvSections.CurrentRow?.DataBoundItem is not SectionRow row)
            {
                return;
            }

            PopulateSectionEditor(row);
        }

        private async Task PopulateSectionEditorFromEnteredIdAsync()
        {
            if (!int.TryParse(txtSectionId.Text, out int sectionId) || sectionId <= 0)
            {
                return;
            }

            try
            {
                var section = await sectionService.GetByIdAsync(sectionId);

                if (section is null)
                {
                    return;
                }

                PopulateSectionEditor(SectionRow.FromSection(section));
                SelectSectionRow(section.SectionID);
            }
            catch (Exception ex)
            {
                ShowError("Unable to load section details.", ex);
            }
        }

        private void PopulateSectionEditor(SectionRow row)
        {
            txtSectionId.Text = row.SectionID.ToString();
            txtSectionName.Text = row.SectionName;
            txtStudentCount.Text = row.StudentCount.ToString();
            SelectComboValue(cmbStudyYear, row.StudyYearID);
            SelectComboValue(cmbBranch, row.BranchID);
            ApplyStudyYearRulesToEditor();
        }

        private void SelectSectionRow(int sectionId)
        {
            foreach (DataGridViewRow row in dgvSections.Rows)
            {
                if (row.DataBoundItem is not SectionRow section || section.SectionID != sectionId)
                {
                    continue;
                }

                row.Selected = true;
                dgvSections.CurrentCell = row.Cells[0];
                break;
            }
        }

        private void ClearSectionForm()
        {
            txtSectionId.Text = "Auto";
            txtSectionName.Clear();
            txtStudentCount.Clear();
            ClearCombo(cmbStudyYear);
            ClearCombo(cmbBranch);
            cmbBranch.Enabled = true;
            txtSectionName.PlaceholderText = "Enter section name";
            dgvSections.ClearSelection();
            txtSectionName.Focus();
        }

        private void ApplyStudyYearRulesToEditor()
        {
            var studyYearId = GetSelectedRequiredId(cmbStudyYear);

            if (studyYearId <= 0)
            {
                cmbBranch.Enabled = true;
                txtSectionName.PlaceholderText = "Enter section name";
                return;
            }

            if (AcademicStructureRules.UsesGeneralSections(studyYearId))
            {
                SelectComboValue(cmbBranch, null);
                cmbBranch.Enabled = false;
                txtSectionName.PlaceholderText = AcademicStructureRules.FormatAllowedSectionNames(studyYearId);
                return;
            }

            cmbBranch.Enabled = true;
            txtSectionName.PlaceholderText = "Use branch name";
            ApplyBranchSelectionToSectionName();
        }

        private void ApplyBranchSelectionToSectionName()
        {
            var studyYearId = GetSelectedRequiredId(cmbStudyYear);

            if (!AcademicStructureRules.UsesBranches(studyYearId) ||
                cmbBranch.SelectedItem is not ComboOption branch ||
                !branch.Id.HasValue ||
                !string.IsNullOrWhiteSpace(txtSectionName.Text))
            {
                return;
            }

            txtSectionName.Text = branch.Text;
        }

        private void SetSectionActionsEnabled(bool enabled)
        {
            btnAddSection.Enabled = enabled;
            btnUpdateSection.Enabled = enabled;
            btnDeleteSection.Enabled = enabled;
            dgvSections.Enabled = enabled;
        }

        private void BindBranchCombo()
        {
            var branches = new List<ComboOption> { new(null, "General / no branch") };
            branches.AddRange(branchesLookup.Select(branch => new ComboOption(branch.BranchID, branch.BranchName)));
            BindCombo(cmbBranch, branches);
        }

        private static void BindCombo(Guna.UI2.WinForms.Guna2ComboBox combo, IEnumerable<ComboOption> options)
        {
            ComboBoxHelper.Bind(combo, options);
        }

        private static int GetSelectedRequiredId(Guna.UI2.WinForms.Guna2ComboBox combo)
        {
            return ComboBoxHelper.GetSelectedRequiredId(combo);
        }

        private static int? GetSelectedOptionalId(Guna.UI2.WinForms.Guna2ComboBox combo)
        {
            return ComboBoxHelper.GetSelectedOptionalId(combo);
        }

        private static void SelectComboValue(Guna.UI2.WinForms.Guna2ComboBox combo, int? id)
        {
            ComboBoxHelper.SelectValue(combo, id);
        }

        private static void ClearCombo(Guna.UI2.WinForms.Guna2ComboBox combo)
        {
            ComboBoxHelper.Clear(combo);
        }

        private void ShowInformation(string message)
        {
            UiMessages.ShowInformation(this, message, "Sections");
        }

        private void ShowError(string message, Exception ex)
        {
            UiMessages.ShowError(this, message, "Sections", ex);
        }

        private sealed class SectionRow
        {
            public int SectionID { get; init; }
            public string SectionName { get; init; } = string.Empty;
            public int StudentCount { get; init; }
            public int StudyYearID { get; init; }
            public int? BranchID { get; init; }
            public string StudyYearName { get; init; } = string.Empty;
            public string BranchName { get; init; } = string.Empty;

            public static SectionRow FromSection(Section section)
            {
                return new SectionRow
                {
                    SectionID = section.SectionID,
                    SectionName = section.SectionName,
                    StudentCount = section.StudentCount,
                    StudyYearID = section.StudyYearID,
                    BranchID = section.BranchID,
                    StudyYearName = section.StudyYear?.YearName ?? "-",
                    BranchName = section.Branch?.BranchName ?? "General"
                };
            }
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
            pnlSectionsTable = new Guna.UI2.WinForms.Guna2Panel();
            dgvSections = new Guna.UI2.WinForms.Guna2DataGridView();
            colSectionId = new System.Windows.Forms.DataGridViewTextBoxColumn();
            colSectionName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            colStudentCount = new System.Windows.Forms.DataGridViewTextBoxColumn();
            colStudyYear = new System.Windows.Forms.DataGridViewTextBoxColumn();
            colBranch = new System.Windows.Forms.DataGridViewTextBoxColumn();
            lblTableSubtitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblTableTitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            pnlSectionEditor = new Guna.UI2.WinForms.Guna2Panel();
            btnClearSectionForm = new Guna.UI2.WinForms.Guna2Button();
            btnDeleteSection = new Guna.UI2.WinForms.Guna2Button();
            btnUpdateSection = new Guna.UI2.WinForms.Guna2Button();
            btnAddSection = new Guna.UI2.WinForms.Guna2Button();
            cmbBranch = new Guna.UI2.WinForms.Guna2ComboBox();
            lblBranch = new Guna.UI2.WinForms.Guna2HtmlLabel();
            cmbStudyYear = new Guna.UI2.WinForms.Guna2ComboBox();
            lblStudyYear = new Guna.UI2.WinForms.Guna2HtmlLabel();
            txtStudentCount = new Guna.UI2.WinForms.Guna2TextBox();
            lblStudentCount = new Guna.UI2.WinForms.Guna2HtmlLabel();
            txtSectionName = new Guna.UI2.WinForms.Guna2TextBox();
            lblSectionName = new Guna.UI2.WinForms.Guna2HtmlLabel();
            txtSectionId = new Guna.UI2.WinForms.Guna2TextBox();
            lblSectionId = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblEditorSubtitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblEditorTitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            pnlHeader = new Guna.UI2.WinForms.Guna2Panel();
            lblPageSubtitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblPageTitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            pnlSidebar.SuspendLayout();
            pnlMain.SuspendLayout();
            pnlWorkspace.SuspendLayout();
            pnlSectionsTable.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvSections).BeginInit();
            pnlSectionEditor.SuspendLayout();
            pnlHeader.SuspendLayout();
            SuspendLayout();
            pnlSidebar.BackColor = System.Drawing.Color.Transparent;
            pnlSidebar.Controls.Add(lblSidebarFooter);
            pnlSidebar.Controls.Add(btnNavigationSchedules);
            pnlSidebar.Controls.Add(btnNavigationFaculty);
            pnlSidebar.Controls.Add(btnNavigationTimeSlots);
            pnlSidebar.Controls.Add(btnNavigationClassrooms);
            pnlSidebar.Controls.Add(btnNavigationSubjects);
            pnlSidebar.Controls.Add(btnNavigationSections);
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
            lblSidebarFooter.TabIndex = 12;
            lblSidebarFooter.Text = "Academic Scheduling Suite";
            btnNavigationSchedules.BorderRadius = 8;
            btnNavigationSchedules.Cursor = System.Windows.Forms.Cursors.Hand;
            btnNavigationSchedules.FillColor = System.Drawing.Color.FromArgb(24, 38, 62);
            btnNavigationSchedules.Font = new System.Drawing.Font("Segoe UI Semibold", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnNavigationSchedules.ForeColor = System.Drawing.Color.FromArgb(226, 232, 240);
            btnNavigationSchedules.HoverState.FillColor = System.Drawing.Color.FromArgb(36, 55, 86);
            btnNavigationSchedules.Location = new System.Drawing.Point(24, 546);
            btnNavigationSchedules.Name = "btnNavigationSchedules";
            btnNavigationSchedules.Size = new System.Drawing.Size(192, 44);
            btnNavigationSchedules.TabIndex = 11;
            btnNavigationSchedules.Text = "Schedules";
            btnNavigationSchedules.TextAlign = System.Windows.Forms.HorizontalAlignment.Left;
            btnNavigationSchedules.TextOffset = new System.Drawing.Point(14, 0);
            btnNavigationFaculty.BorderRadius = 8;
            btnNavigationFaculty.Cursor = System.Windows.Forms.Cursors.Hand;
            btnNavigationFaculty.FillColor = System.Drawing.Color.FromArgb(24, 38, 62);
            btnNavigationFaculty.Font = new System.Drawing.Font("Segoe UI Semibold", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnNavigationFaculty.ForeColor = System.Drawing.Color.FromArgb(226, 232, 240);
            btnNavigationFaculty.HoverState.FillColor = System.Drawing.Color.FromArgb(36, 55, 86);
            btnNavigationFaculty.Location = new System.Drawing.Point(24, 490);
            btnNavigationFaculty.Name = "btnNavigationFaculty";
            btnNavigationFaculty.Size = new System.Drawing.Size(192, 44);
            btnNavigationFaculty.TabIndex = 10;
            btnNavigationFaculty.Text = "Faculty Members";
            btnNavigationFaculty.TextAlign = System.Windows.Forms.HorizontalAlignment.Left;
            btnNavigationFaculty.TextOffset = new System.Drawing.Point(14, 0);
            btnNavigationTimeSlots.BorderRadius = 8;
            btnNavigationTimeSlots.Cursor = System.Windows.Forms.Cursors.Hand;
            btnNavigationTimeSlots.FillColor = System.Drawing.Color.FromArgb(24, 38, 62);
            btnNavigationTimeSlots.Font = new System.Drawing.Font("Segoe UI Semibold", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnNavigationTimeSlots.ForeColor = System.Drawing.Color.FromArgb(226, 232, 240);
            btnNavigationTimeSlots.HoverState.FillColor = System.Drawing.Color.FromArgb(36, 55, 86);
            btnNavigationTimeSlots.Location = new System.Drawing.Point(24, 434);
            btnNavigationTimeSlots.Name = "btnNavigationTimeSlots";
            btnNavigationTimeSlots.Size = new System.Drawing.Size(192, 44);
            btnNavigationTimeSlots.TabIndex = 9;
            btnNavigationTimeSlots.Text = "Time Slots";
            btnNavigationTimeSlots.TextAlign = System.Windows.Forms.HorizontalAlignment.Left;
            btnNavigationTimeSlots.TextOffset = new System.Drawing.Point(14, 0);
            btnNavigationClassrooms.BorderRadius = 8;
            btnNavigationClassrooms.Cursor = System.Windows.Forms.Cursors.Hand;
            btnNavigationClassrooms.FillColor = System.Drawing.Color.FromArgb(24, 38, 62);
            btnNavigationClassrooms.Font = new System.Drawing.Font("Segoe UI Semibold", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnNavigationClassrooms.ForeColor = System.Drawing.Color.FromArgb(226, 232, 240);
            btnNavigationClassrooms.HoverState.FillColor = System.Drawing.Color.FromArgb(36, 55, 86);
            btnNavigationClassrooms.Location = new System.Drawing.Point(24, 378);
            btnNavigationClassrooms.Name = "btnNavigationClassrooms";
            btnNavigationClassrooms.Size = new System.Drawing.Size(192, 44);
            btnNavigationClassrooms.TabIndex = 8;
            btnNavigationClassrooms.Text = "Classrooms";
            btnNavigationClassrooms.TextAlign = System.Windows.Forms.HorizontalAlignment.Left;
            btnNavigationClassrooms.TextOffset = new System.Drawing.Point(14, 0);
            btnNavigationSubjects.BorderRadius = 8;
            btnNavigationSubjects.Cursor = System.Windows.Forms.Cursors.Hand;
            btnNavigationSubjects.FillColor = System.Drawing.Color.FromArgb(24, 38, 62);
            btnNavigationSubjects.Font = new System.Drawing.Font("Segoe UI Semibold", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnNavigationSubjects.ForeColor = System.Drawing.Color.FromArgb(226, 232, 240);
            btnNavigationSubjects.HoverState.FillColor = System.Drawing.Color.FromArgb(36, 55, 86);
            btnNavigationSubjects.Location = new System.Drawing.Point(24, 322);
            btnNavigationSubjects.Name = "btnNavigationSubjects";
            btnNavigationSubjects.Size = new System.Drawing.Size(192, 44);
            btnNavigationSubjects.TabIndex = 7;
            btnNavigationSubjects.Text = "Subjects";
            btnNavigationSubjects.TextAlign = System.Windows.Forms.HorizontalAlignment.Left;
            btnNavigationSubjects.TextOffset = new System.Drawing.Point(14, 0);
            btnNavigationSections.BorderRadius = 8;
            btnNavigationSections.Checked = true;
            btnNavigationSections.Cursor = System.Windows.Forms.Cursors.Hand;
            btnNavigationSections.Enabled = false;
            btnNavigationSections.FillColor = System.Drawing.Color.FromArgb(37, 99, 235);
            btnNavigationSections.Font = new System.Drawing.Font("Segoe UI Semibold", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnNavigationSections.ForeColor = System.Drawing.Color.White;
            btnNavigationSections.HoverState.FillColor = System.Drawing.Color.FromArgb(29, 78, 216);
            btnNavigationSections.Location = new System.Drawing.Point(24, 266);
            btnNavigationSections.Name = "btnNavigationSections";
            btnNavigationSections.Size = new System.Drawing.Size(192, 44);
            btnNavigationSections.TabIndex = 6;
            btnNavigationSections.Text = "Sections";
            btnNavigationSections.TextAlign = System.Windows.Forms.HorizontalAlignment.Left;
            btnNavigationSections.TextOffset = new System.Drawing.Point(14, 0);
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
            pnlWorkspace.Controls.Add(pnlSectionsTable);
            pnlWorkspace.Controls.Add(pnlSectionEditor);
            pnlWorkspace.Dock = System.Windows.Forms.DockStyle.Fill;
            pnlWorkspace.FillColor = System.Drawing.Color.FromArgb(245, 247, 250);
            pnlWorkspace.Location = new System.Drawing.Point(0, 88);
            pnlWorkspace.Name = "pnlWorkspace";
            pnlWorkspace.Padding = new System.Windows.Forms.Padding(28, 24, 28, 28);
            pnlWorkspace.Size = new System.Drawing.Size(940, 632);
            pnlWorkspace.TabIndex = 1;
            pnlSectionsTable.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            pnlSectionsTable.BackColor = System.Drawing.Color.Transparent;
            pnlSectionsTable.BorderColor = System.Drawing.Color.FromArgb(226, 232, 240);
            pnlSectionsTable.BorderRadius = 8;
            pnlSectionsTable.BorderThickness = 1;
            pnlSectionsTable.Controls.Add(dgvSections);
            pnlSectionsTable.Controls.Add(lblTableSubtitle);
            pnlSectionsTable.Controls.Add(lblTableTitle);
            pnlSectionsTable.FillColor = System.Drawing.Color.White;
            pnlSectionsTable.Location = new System.Drawing.Point(28, 296);
            pnlSectionsTable.Name = "pnlSectionsTable";
            pnlSectionsTable.Size = new System.Drawing.Size(884, 308);
            pnlSectionsTable.TabIndex = 1;
            dgvSections.AllowUserToAddRows = false;
            dgvSections.AllowUserToDeleteRows = false;
            dgvSections.AllowUserToResizeRows = false;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.FromArgb(248, 250, 252);
            dataGridViewCellStyle1.ForeColor = System.Drawing.Color.FromArgb(30, 41, 59);
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.Color.FromArgb(219, 234, 254);
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.Color.FromArgb(30, 64, 175);
            dgvSections.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            dgvSections.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            dgvSections.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dgvSections.BackgroundColor = System.Drawing.Color.White;
            dgvSections.BorderStyle = System.Windows.Forms.BorderStyle.None;
            dgvSections.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleHorizontal;
            dgvSections.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.Color.FromArgb(15, 23, 42);
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Segoe UI Semibold", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            dataGridViewCellStyle2.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.Color.FromArgb(15, 23, 42);
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            dgvSections.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle2;
            dgvSections.ColumnHeadersHeight = 44;
            dgvSections.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvSections.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { colSectionId, colSectionName, colStudentCount, colStudyYear, colBranch });
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.Color.White;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            dataGridViewCellStyle3.ForeColor = System.Drawing.Color.FromArgb(30, 41, 59);
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.Color.FromArgb(219, 234, 254);
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.Color.FromArgb(30, 64, 175);
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            dgvSections.DefaultCellStyle = dataGridViewCellStyle3;
            dgvSections.EnableHeadersVisualStyles = false;
            dgvSections.GridColor = System.Drawing.Color.FromArgb(226, 232, 240);
            dgvSections.Location = new System.Drawing.Point(24, 78);
            dgvSections.MultiSelect = false;
            dgvSections.Name = "dgvSections";
            dgvSections.ReadOnly = true;
            dgvSections.RowHeadersVisible = false;
            dgvSections.RowTemplate.Height = 42;
            dgvSections.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            dgvSections.Size = new System.Drawing.Size(836, 204);
            dgvSections.TabIndex = 2;
            colSectionId.DataPropertyName = "SectionID";
            colSectionId.FillWeight = 45F;
            colSectionId.HeaderText = "Section ID";
            colSectionId.Name = "colSectionId";
            colSectionId.ReadOnly = true;
            colSectionName.DataPropertyName = "SectionName";
            colSectionName.FillWeight = 110F;
            colSectionName.HeaderText = "Section Name";
            colSectionName.Name = "colSectionName";
            colSectionName.ReadOnly = true;
            colStudentCount.DataPropertyName = "StudentCount";
            colStudentCount.FillWeight = 65F;
            colStudentCount.HeaderText = "Students";
            colStudentCount.Name = "colStudentCount";
            colStudentCount.ReadOnly = true;
            colStudyYear.DataPropertyName = "StudyYearID";
            colStudyYear.FillWeight = 85F;
            colStudyYear.HeaderText = "Study Year";
            colStudyYear.Name = "colStudyYear";
            colStudyYear.ReadOnly = true;
            colBranch.DataPropertyName = "BranchID";
            colBranch.FillWeight = 85F;
            colBranch.HeaderText = "Branch";
            colBranch.Name = "colBranch";
            colBranch.ReadOnly = true;
            lblTableSubtitle.BackColor = System.Drawing.Color.Transparent;
            lblTableSubtitle.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblTableSubtitle.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            lblTableSubtitle.Location = new System.Drawing.Point(24, 43);
            lblTableSubtitle.Name = "lblTableSubtitle";
            lblTableSubtitle.Size = new System.Drawing.Size(272, 17);
            lblTableSubtitle.TabIndex = 1;
            lblTableSubtitle.Text = "Review and select academic section records.";
            lblTableTitle.BackColor = System.Drawing.Color.Transparent;
            lblTableTitle.Font = new System.Drawing.Font("Segoe UI Semibold", 13F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblTableTitle.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            lblTableTitle.Location = new System.Drawing.Point(24, 18);
            lblTableTitle.Name = "lblTableTitle";
            lblTableTitle.Size = new System.Drawing.Size(105, 25);
            lblTableTitle.TabIndex = 0;
            lblTableTitle.Text = "Sections List";
            pnlSectionEditor.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            pnlSectionEditor.BackColor = System.Drawing.Color.Transparent;
            pnlSectionEditor.BorderColor = System.Drawing.Color.FromArgb(226, 232, 240);
            pnlSectionEditor.BorderRadius = 8;
            pnlSectionEditor.BorderThickness = 1;
            pnlSectionEditor.Controls.Add(btnClearSectionForm);
            pnlSectionEditor.Controls.Add(btnDeleteSection);
            pnlSectionEditor.Controls.Add(btnUpdateSection);
            pnlSectionEditor.Controls.Add(btnAddSection);
            pnlSectionEditor.Controls.Add(cmbBranch);
            pnlSectionEditor.Controls.Add(lblBranch);
            pnlSectionEditor.Controls.Add(cmbStudyYear);
            pnlSectionEditor.Controls.Add(lblStudyYear);
            pnlSectionEditor.Controls.Add(txtStudentCount);
            pnlSectionEditor.Controls.Add(lblStudentCount);
            pnlSectionEditor.Controls.Add(txtSectionName);
            pnlSectionEditor.Controls.Add(lblSectionName);
            pnlSectionEditor.Controls.Add(txtSectionId);
            pnlSectionEditor.Controls.Add(lblSectionId);
            pnlSectionEditor.Controls.Add(lblEditorSubtitle);
            pnlSectionEditor.Controls.Add(lblEditorTitle);
            pnlSectionEditor.FillColor = System.Drawing.Color.White;
            pnlSectionEditor.Location = new System.Drawing.Point(28, 24);
            pnlSectionEditor.Name = "pnlSectionEditor";
            pnlSectionEditor.Size = new System.Drawing.Size(884, 250);
            pnlSectionEditor.TabIndex = 0;
            btnClearSectionForm.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnClearSectionForm.BorderColor = System.Drawing.Color.FromArgb(203, 213, 225);
            btnClearSectionForm.BorderRadius = 8;
            btnClearSectionForm.BorderThickness = 1;
            btnClearSectionForm.Cursor = System.Windows.Forms.Cursors.Hand;
            btnClearSectionForm.FillColor = System.Drawing.Color.White;
            btnClearSectionForm.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnClearSectionForm.ForeColor = System.Drawing.Color.FromArgb(51, 65, 85);
            btnClearSectionForm.HoverState.FillColor = System.Drawing.Color.FromArgb(248, 250, 252);
            btnClearSectionForm.Location = new System.Drawing.Point(752, 142);
            btnClearSectionForm.Name = "btnClearSectionForm";
            btnClearSectionForm.Size = new System.Drawing.Size(108, 38);
            btnClearSectionForm.TabIndex = 15;
            btnClearSectionForm.Text = "Clear";
            btnClearSectionForm.Visible = false;
            btnDeleteSection.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnDeleteSection.BorderRadius = 8;
            btnDeleteSection.Cursor = System.Windows.Forms.Cursors.Hand;
            btnDeleteSection.FillColor = System.Drawing.Color.FromArgb(220, 38, 38);
            btnDeleteSection.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnDeleteSection.ForeColor = System.Drawing.Color.White;
            btnDeleteSection.HoverState.FillColor = System.Drawing.Color.FromArgb(185, 28, 28);
            btnDeleteSection.Location = new System.Drawing.Point(632, 142);
            btnDeleteSection.Name = "btnDeleteSection";
            btnDeleteSection.Size = new System.Drawing.Size(108, 38);
            btnDeleteSection.TabIndex = 14;
            btnDeleteSection.Text = "Delete";
            btnUpdateSection.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnUpdateSection.BorderRadius = 8;
            btnUpdateSection.Cursor = System.Windows.Forms.Cursors.Hand;
            btnUpdateSection.FillColor = System.Drawing.Color.FromArgb(14, 116, 144);
            btnUpdateSection.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnUpdateSection.ForeColor = System.Drawing.Color.White;
            btnUpdateSection.HoverState.FillColor = System.Drawing.Color.FromArgb(21, 94, 117);
            btnUpdateSection.Location = new System.Drawing.Point(752, 98);
            btnUpdateSection.Name = "btnUpdateSection";
            btnUpdateSection.Size = new System.Drawing.Size(108, 38);
            btnUpdateSection.TabIndex = 13;
            btnUpdateSection.Text = "Update";
            btnAddSection.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnAddSection.BorderRadius = 8;
            btnAddSection.Cursor = System.Windows.Forms.Cursors.Hand;
            btnAddSection.FillColor = System.Drawing.Color.FromArgb(22, 163, 74);
            btnAddSection.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnAddSection.ForeColor = System.Drawing.Color.White;
            btnAddSection.HoverState.FillColor = System.Drawing.Color.FromArgb(21, 128, 61);
            btnAddSection.Location = new System.Drawing.Point(632, 98);
            btnAddSection.Name = "btnAddSection";
            btnAddSection.Size = new System.Drawing.Size(108, 38);
            btnAddSection.TabIndex = 12;
            btnAddSection.Text = "Add";
            cmbBranch.BackColor = System.Drawing.Color.Transparent;
            cmbBranch.BorderColor = System.Drawing.Color.FromArgb(203, 213, 225);
            cmbBranch.BorderRadius = 8;
            cmbBranch.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            cmbBranch.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbBranch.FocusedColor = System.Drawing.Color.FromArgb(37, 99, 235);
            cmbBranch.FocusedState.BorderColor = System.Drawing.Color.FromArgb(37, 99, 235);
            cmbBranch.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            cmbBranch.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            cmbBranch.HoverState.BorderColor = System.Drawing.Color.FromArgb(59, 130, 246);
            cmbBranch.ItemHeight = 36;
            cmbBranch.Location = new System.Drawing.Point(214, 182);
            cmbBranch.Name = "cmbBranch";
            cmbBranch.Size = new System.Drawing.Size(160, 42);
            cmbBranch.TabIndex = 11;
            lblBranch.BackColor = System.Drawing.Color.Transparent;
            lblBranch.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblBranch.ForeColor = System.Drawing.Color.FromArgb(51, 65, 85);
            lblBranch.Location = new System.Drawing.Point(214, 157);
            lblBranch.Name = "lblBranch";
            lblBranch.Size = new System.Drawing.Size(45, 19);
            lblBranch.TabIndex = 10;
            lblBranch.Text = "Branch";
            cmbStudyYear.BackColor = System.Drawing.Color.Transparent;
            cmbStudyYear.BorderColor = System.Drawing.Color.FromArgb(203, 213, 225);
            cmbStudyYear.BorderRadius = 8;
            cmbStudyYear.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            cmbStudyYear.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbStudyYear.FocusedColor = System.Drawing.Color.FromArgb(37, 99, 235);
            cmbStudyYear.FocusedState.BorderColor = System.Drawing.Color.FromArgb(37, 99, 235);
            cmbStudyYear.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            cmbStudyYear.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            cmbStudyYear.HoverState.BorderColor = System.Drawing.Color.FromArgb(59, 130, 246);
            cmbStudyYear.ItemHeight = 36;
            cmbStudyYear.Location = new System.Drawing.Point(24, 182);
            cmbStudyYear.Name = "cmbStudyYear";
            cmbStudyYear.Size = new System.Drawing.Size(160, 42);
            cmbStudyYear.TabIndex = 9;
            lblStudyYear.BackColor = System.Drawing.Color.Transparent;
            lblStudyYear.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblStudyYear.ForeColor = System.Drawing.Color.FromArgb(51, 65, 85);
            lblStudyYear.Location = new System.Drawing.Point(24, 157);
            lblStudyYear.Name = "lblStudyYear";
            lblStudyYear.Size = new System.Drawing.Size(69, 19);
            lblStudyYear.TabIndex = 8;
            lblStudyYear.Text = "Study Year";
            txtStudentCount.BorderColor = System.Drawing.Color.FromArgb(203, 213, 225);
            txtStudentCount.BorderRadius = 8;
            txtStudentCount.Cursor = System.Windows.Forms.Cursors.IBeam;
            txtStudentCount.DefaultText = "";
            txtStudentCount.DisabledState.BorderColor = System.Drawing.Color.FromArgb(226, 232, 240);
            txtStudentCount.DisabledState.FillColor = System.Drawing.Color.FromArgb(248, 250, 252);
            txtStudentCount.DisabledState.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            txtStudentCount.FocusedState.BorderColor = System.Drawing.Color.FromArgb(37, 99, 235);
            txtStudentCount.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtStudentCount.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            txtStudentCount.HoverState.BorderColor = System.Drawing.Color.FromArgb(59, 130, 246);
            txtStudentCount.Location = new System.Drawing.Point(402, 112);
            txtStudentCount.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            txtStudentCount.Name = "txtStudentCount";
            txtStudentCount.PasswordChar = '\0';
            txtStudentCount.PlaceholderForeColor = System.Drawing.Color.FromArgb(148, 163, 184);
            txtStudentCount.PlaceholderText = "Students";
            txtStudentCount.SelectedText = "";
            txtStudentCount.Size = new System.Drawing.Size(190, 42);
            txtStudentCount.TabIndex = 7;
            lblStudentCount.BackColor = System.Drawing.Color.Transparent;
            lblStudentCount.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblStudentCount.ForeColor = System.Drawing.Color.FromArgb(51, 65, 85);
            lblStudentCount.Location = new System.Drawing.Point(402, 87);
            lblStudentCount.Name = "lblStudentCount";
            lblStudentCount.Size = new System.Drawing.Size(89, 19);
            lblStudentCount.TabIndex = 6;
            lblStudentCount.Text = "Student Count";
            txtSectionName.BorderColor = System.Drawing.Color.FromArgb(203, 213, 225);
            txtSectionName.BorderRadius = 8;
            txtSectionName.Cursor = System.Windows.Forms.Cursors.IBeam;
            txtSectionName.DefaultText = "";
            txtSectionName.DisabledState.BorderColor = System.Drawing.Color.FromArgb(226, 232, 240);
            txtSectionName.DisabledState.FillColor = System.Drawing.Color.FromArgb(248, 250, 252);
            txtSectionName.DisabledState.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            txtSectionName.FocusedState.BorderColor = System.Drawing.Color.FromArgb(37, 99, 235);
            txtSectionName.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtSectionName.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            txtSectionName.HoverState.BorderColor = System.Drawing.Color.FromArgb(59, 130, 246);
            txtSectionName.Location = new System.Drawing.Point(184, 112);
            txtSectionName.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            txtSectionName.Name = "txtSectionName";
            txtSectionName.PasswordChar = '\0';
            txtSectionName.PlaceholderForeColor = System.Drawing.Color.FromArgb(148, 163, 184);
            txtSectionName.PlaceholderText = "Enter section name";
            txtSectionName.SelectedText = "";
            txtSectionName.Size = new System.Drawing.Size(190, 42);
            txtSectionName.TabIndex = 5;
            lblSectionName.BackColor = System.Drawing.Color.Transparent;
            lblSectionName.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblSectionName.ForeColor = System.Drawing.Color.FromArgb(51, 65, 85);
            lblSectionName.Location = new System.Drawing.Point(184, 87);
            lblSectionName.Name = "lblSectionName";
            lblSectionName.Size = new System.Drawing.Size(86, 19);
            lblSectionName.TabIndex = 4;
            lblSectionName.Text = "Section Name";
            txtSectionId.BorderColor = System.Drawing.Color.FromArgb(226, 232, 240);
            txtSectionId.BorderRadius = 8;
            txtSectionId.Cursor = System.Windows.Forms.Cursors.IBeam;
            txtSectionId.DefaultText = "";
            txtSectionId.DisabledState.BorderColor = System.Drawing.Color.FromArgb(226, 232, 240);
            txtSectionId.DisabledState.FillColor = System.Drawing.Color.FromArgb(248, 250, 252);
            txtSectionId.DisabledState.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            txtSectionId.Enabled = true;
            txtSectionId.FillColor = System.Drawing.Color.White;
            txtSectionId.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtSectionId.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            txtSectionId.Location = new System.Drawing.Point(24, 112);
            txtSectionId.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            txtSectionId.Name = "txtSectionId";
            txtSectionId.PasswordChar = '\0';
            txtSectionId.PlaceholderForeColor = System.Drawing.Color.FromArgb(148, 163, 184);
            txtSectionId.PlaceholderText = "Enter ID";
            txtSectionId.SelectedText = "";
            txtSectionId.Size = new System.Drawing.Size(132, 42);
            txtSectionId.TabIndex = 3;
            lblSectionId.BackColor = System.Drawing.Color.Transparent;
            lblSectionId.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblSectionId.ForeColor = System.Drawing.Color.FromArgb(51, 65, 85);
            lblSectionId.Location = new System.Drawing.Point(24, 87);
            lblSectionId.Name = "lblSectionId";
            lblSectionId.Size = new System.Drawing.Size(64, 19);
            lblSectionId.TabIndex = 2;
            lblSectionId.Text = "Section ID";
            lblEditorSubtitle.BackColor = System.Drawing.Color.Transparent;
            lblEditorSubtitle.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblEditorSubtitle.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            lblEditorSubtitle.Location = new System.Drawing.Point(24, 44);
            lblEditorSubtitle.Name = "lblEditorSubtitle";
            lblEditorSubtitle.Size = new System.Drawing.Size(284, 17);
            lblEditorSubtitle.TabIndex = 1;
            lblEditorSubtitle.Text = "Prepare section details before applying an action.";
            lblEditorTitle.BackColor = System.Drawing.Color.Transparent;
            lblEditorTitle.Font = new System.Drawing.Font("Segoe UI Semibold", 13F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblEditorTitle.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            lblEditorTitle.Location = new System.Drawing.Point(24, 18);
            lblEditorTitle.Name = "lblEditorTitle";
            lblEditorTitle.Size = new System.Drawing.Size(125, 25);
            lblEditorTitle.TabIndex = 0;
            lblEditorTitle.Text = "Section Details";
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
            lblPageSubtitle.Size = new System.Drawing.Size(394, 19);
            lblPageSubtitle.TabIndex = 1;
            lblPageSubtitle.Text = "Manage academic sections by study year, branch, and capacity.";
            lblPageTitle.BackColor = System.Drawing.Color.Transparent;
            lblPageTitle.Font = new System.Drawing.Font("Segoe UI Semibold", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblPageTitle.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            lblPageTitle.Location = new System.Drawing.Point(32, 16);
            lblPageTitle.Name = "lblPageTitle";
            lblPageTitle.Size = new System.Drawing.Size(265, 34);
            lblPageTitle.TabIndex = 0;
            lblPageTitle.Text = "Sections Management";
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(245, 247, 250);
            ClientSize = new System.Drawing.Size(1180, 720);
            Controls.Add(pnlMain);
            Controls.Add(pnlSidebar);
            Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            MinimumSize = new System.Drawing.Size(980, 600);
            Name = "SectionsForm";
            Text = "Sections Management";
            pnlSidebar.ResumeLayout(false);
            pnlSidebar.PerformLayout();
            pnlMain.ResumeLayout(false);
            pnlWorkspace.ResumeLayout(false);
            pnlSectionsTable.ResumeLayout(false);
            pnlSectionsTable.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvSections).EndInit();
            pnlSectionEditor.ResumeLayout(false);
            pnlSectionEditor.PerformLayout();
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
        private Guna.UI2.WinForms.Guna2Button btnNavigationFaculty = null!;
        private Guna.UI2.WinForms.Guna2Button btnNavigationSchedules = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblSidebarFooter = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlMain = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlHeader = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblPageTitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblPageSubtitle = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlWorkspace = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlSectionEditor = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblEditorTitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblEditorSubtitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblSectionId = null!;
        private Guna.UI2.WinForms.Guna2TextBox txtSectionId = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblSectionName = null!;
        private Guna.UI2.WinForms.Guna2TextBox txtSectionName = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblStudentCount = null!;
        private Guna.UI2.WinForms.Guna2TextBox txtStudentCount = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblStudyYear = null!;
        private Guna.UI2.WinForms.Guna2ComboBox cmbStudyYear = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblBranch = null!;
        private Guna.UI2.WinForms.Guna2ComboBox cmbBranch = null!;
        private Guna.UI2.WinForms.Guna2Button btnAddSection = null!;
        private Guna.UI2.WinForms.Guna2Button btnUpdateSection = null!;
        private Guna.UI2.WinForms.Guna2Button btnDeleteSection = null!;
        private Guna.UI2.WinForms.Guna2Button btnClearSectionForm = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlSectionsTable = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblTableTitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblTableSubtitle = null!;
        private Guna.UI2.WinForms.Guna2DataGridView dgvSections = null!;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSectionId = null!;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSectionName = null!;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStudentCount = null!;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStudyYear = null!;
        private System.Windows.Forms.DataGridViewTextBoxColumn colBranch = null!;
    }
}

