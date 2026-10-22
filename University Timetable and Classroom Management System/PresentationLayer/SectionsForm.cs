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
            if (DesignerEnvironment.IsDesignTime(this)) return;
            ArabicUi.Apply(this);
            ConfigureSectionsGrid();
            ConfigureSectionsEvents();
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (DesignerEnvironment.IsDesignTime(this) || IsDisposed || Disposing) return;
            await RefreshSectionsAsync();
        }


        private void ConfigureSectionsGrid()
        {
            dgvSections.AutoGenerateColumns = false;
            GridRowNumbers.Attach(dgvSections, colRowNumber);
        }

        private void ConfigureSectionsEvents()
        {
            dgvSections.SelectionChanged += (_, _) => PopulateSectionEditorFromSelection();

            cmbStudyYear.SelectedIndexChanged += (_, _) => ApplyStudyYearRulesToEditor();
            cmbBranch.SelectedIndexChanged += (_, _) => ApplyBranchSelectionToSectionName();
            btnAddSection.Click += async (_, _) => await AddSectionAsync();
            btnUpdateSection.Click += async (_, _) => await UpdateSectionAsync();
            btnDeleteSection.Click += async (_, _) => await DeleteSectionAsync();
        }

        private async Task RefreshSectionsAsync()
        {
            if (IsDisposed || Disposing) return;

            SetSectionActionsEnabled(false);

            try
            {
                await LoadLookupsAsync();
                if (IsDisposed || Disposing) return;
                await LoadSectionsAsync();
                if (IsDisposed || Disposing) return;
                ClearSectionForm();
            }
            catch (Exception ex)
            {
                ShowError("تعذر تحديث الشعب.", ex);
            }
            finally
            {
                SetSectionActionsEnabled(true);
            }
        }

        private async Task LoadLookupsAsync()
        {
            if (IsDisposed || Disposing) return;

            studyYearsLookup = await studyYearService.GetAllAsync();
            if (IsDisposed || Disposing) return;
            branchesLookup = await branchService.GetAllAsync();
            if (IsDisposed || Disposing) return;

            BindCombo(cmbStudyYear, studyYearsLookup.Select(studyYear => new ComboOption(studyYear.StudyYearID, studyYear.YearName)));
            BindBranchCombo();
        }

        private async Task LoadSectionsAsync()
        {
            if (IsDisposed || Disposing) return;

            var sections = await sectionService.GetAllAsync();
            if (IsDisposed || Disposing) return;
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
            return AcademicStructureRules.IsGeneralSectionValid(section);
        }

        private async Task AddSectionAsync()
        {
            if (IsDisposed || Disposing) return;

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
            if (IsDisposed || Disposing) return;

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
            if (IsDisposed || Disposing) return;

            if (!TryGetSectionIdFromEditor(out int sectionId))
            {
                return;
            }

            var confirmation = UiMessages.ConfirmDeletion(this, "حذف الشعبة");

            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            await ExecuteSectionActionAsync(
                async () => await sectionService.DeleteAsync(sectionId),
                UiMessages.RecordDeleted);
        }

        private Task ExecuteSectionActionAsync(Func<Task> action, string successMessage)
        {
            return EditorAction.RunAsync(this, action, LoadSectionsAsync, SetSectionActionsEnabled,
                () =>
                {
                    ClearSectionForm();
                    ShowInformation(successMessage);
                },
                ex => ShowError("تعذر إتمام العملية على الشعبة.", ex));
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
                ShowInformation("يجب ألا يكون عدد الطلبة سالباً.");
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
                    ShowInformation($"أسماء الشعب المسموحة: {AcademicStructureRules.FormatAllowedSectionNames(section.StudyYearID)}.");
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

            ShowInformation("اختر شعبة من الجدول أولاً.");
            return false;
        }

        private void PopulateSectionEditorFromSelection()
        {
            if (IsDisposed || Disposing) return;

            if (dgvSections.CurrentRow?.DataBoundItem is not SectionRow row)
            {
                return;
            }

            PopulateSectionEditor(row);
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

        private void ClearSectionForm()
        {
            if (IsDisposed || Disposing) return;

            txtSectionId.Text = "تلقائي";
            txtSectionName.Clear();
            txtStudentCount.Clear();
            ClearCombo(cmbStudyYear);
            ClearCombo(cmbBranch);
            cmbBranch.Enabled = true;
            txtSectionName.PlaceholderText = "أدخل اسم الشعبة";
            dgvSections.ClearSelection();
            txtSectionName.Focus();
        }

        private void ApplyStudyYearRulesToEditor()
        {
            if (IsDisposed || Disposing) return;

            var studyYearId = GetSelectedRequiredId(cmbStudyYear);

            if (studyYearId <= 0)
            {
                cmbBranch.Enabled = true;
                txtSectionName.PlaceholderText = "أدخل اسم الشعبة";
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
            txtSectionName.PlaceholderText = "استخدم اسم الفرع";
            ApplyBranchSelectionToSectionName();
        }

        private void ApplyBranchSelectionToSectionName()
        {
            if (IsDisposed || Disposing) return;

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
            if (IsDisposed || Disposing) return;

            btnAddSection.Enabled = enabled;
            btnUpdateSection.Enabled = enabled;
            btnDeleteSection.Enabled = enabled;
            dgvSections.Enabled = enabled;
        }

        private void BindBranchCombo()
        {
            var branches = new List<ComboOption> { new(null, "عام / دون فرع") };
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
            UiMessages.ShowInformation(this, message, "الشعب");
        }

        private void ShowError(string message, Exception ex)
        {
            UiMessages.ShowError(this, message, "الشعب", ex);
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
                    BranchName = section.Branch?.BranchName ?? "عام"
                };
            }
        }

    }
}
