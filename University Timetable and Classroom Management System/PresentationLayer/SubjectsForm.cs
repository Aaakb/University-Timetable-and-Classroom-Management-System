using System.Globalization;
using Guna.UI2.WinForms;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System
{
    public partial class SubjectsForm : System.Windows.Forms.UserControl
    {
        private readonly SubjectService subjectService = new();
        private readonly StudyYearService studyYearService = new();
        private readonly BranchService branchService = new();

        private List<StudyYear> studyYearsLookup = [];
        private List<Branch> branchesLookup = [];
        private List<SubjectRow> subjectRows = [];
        private bool isUpdatingSubjectFilters;
        private bool updatingLectureCounts;
        private bool theoreticalCountIsManual;
        private bool practicalCountIsManual;
        private double legacyTheoreticalHours;
        private double legacyPracticalHours;

        public SubjectsForm()
        {
            InitializeComponent();
            if (DesignerEnvironment.IsDesignTime(this)) return;
            ArabicUi.Apply(this);
            ConfigureSubjectsGrid();
            ConfigureSubjectsEvents();
            ResetLectureCounts();
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (DesignerEnvironment.IsDesignTime(this) || IsDisposed || Disposing) return;
            await RefreshSubjectsAsync();
        }


        private void ConfigureSubjectsGrid()
        {
            dgvSubjects.AutoGenerateColumns = false;
            GridRowNumbers.Attach(dgvSubjects, colRowNumber);
        }

        private void ConfigureSubjectsEvents()
        {
            dgvSubjects.SelectionChanged += (_, _) => PopulateSubjectEditorFromSelection();

            btnAddSubject.Click += async (_, _) => await AddSubjectAsync();
            btnUpdateSubject.Click += async (_, _) => await UpdateSubjectAsync();
            btnDeleteSubject.Click += async (_, _) => await DeleteSubjectAsync();
            cmbStudyYear.SelectedIndexChanged += (_, _) => ApplyStudyYearRulesToEditor();
            cmbStudyYearFilter.SelectedIndexChanged += (_, _) => ApplyStudyYearFilterSelection();
            cmbBranchFilter.SelectedIndexChanged += (_, _) => ApplySubjectFilters();
            txtTheoreticalLectureCount.TextChanged += (_, _) =>
            {
                if (!updatingLectureCounts) theoreticalCountIsManual = !string.IsNullOrWhiteSpace(txtTheoreticalLectureCount.Text);
            };
            txtPracticalLectureCount.TextChanged += (_, _) =>
            {
                if (!updatingLectureCounts) practicalCountIsManual = !string.IsNullOrWhiteSpace(txtPracticalLectureCount.Text);
            };
            linkResetLectureCounts.LinkClicked += (_, _) => ResetLectureCounts();
        }

        private async Task RefreshSubjectsAsync()
        {
            if (IsDisposed || Disposing) return;

            SetSubjectActionsEnabled(false);

            try
            {
                await LoadLookupsAsync();
                if (IsDisposed || Disposing) return;
                await LoadSubjectsAsync();
                if (IsDisposed || Disposing) return;
                ClearSubjectForm();
            }
            catch (Exception ex)
            {
                ShowError("تعذر تحديث المواد.", ex);
            }
            finally
            {
                SetSubjectActionsEnabled(true);
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
            BindSubjectFilterLookups();
        }

        private async Task LoadSubjectsAsync()
        {
            if (IsDisposed || Disposing) return;

            var subjects = await subjectService.GetAllAsync();
            if (IsDisposed || Disposing) return;
            subjectRows = subjects
                .Select(SubjectRow.FromSubject)
                .OrderBy(row => AcademicStructureRules.GetStudyYearOrder(row.StudyYearName))
                .ThenBy(row => row.BranchID ?? 0)
                .ThenBy(row => row.SemesterNumber)
                .ThenBy(row => row.SubjectID)
                .ThenBy(row => row.SubjectName)
                .ToList();

            ApplyStudyYearFilterSelection();
        }

        private void ApplyStudyYearFilterSelection()
        {
            if (IsDisposed || Disposing) return;

            if (isUpdatingSubjectFilters)
            {
                return;
            }

            var selectedStudyYearId = GetSelectedOptionalId(cmbStudyYearFilter);
            var selectedBranchId = GetSelectedOptionalId(cmbBranchFilter);

            isUpdatingSubjectFilters = true;
            BindBranchFilterCombo(selectedStudyYearId, selectedBranchId);
            isUpdatingSubjectFilters = false;

            ApplySubjectFilters();
        }

        private void ApplySubjectFilters()
        {
            if (IsDisposed || Disposing) return;

            if (isUpdatingSubjectFilters)
            {
                return;
            }

            var selectedStudyYearId = GetSelectedOptionalId(cmbStudyYearFilter);
            var selectedBranchId = GetSelectedOptionalId(cmbBranchFilter);

            dgvSubjects.DataSource = subjectRows
                .Where(row => !selectedStudyYearId.HasValue || row.StudyYearID == selectedStudyYearId.Value)
                .Where(row => !selectedBranchId.HasValue || row.BranchID == selectedBranchId.Value)
                .ToList();
            dgvSubjects.ClearSelection();
        }

        private async Task AddSubjectAsync()
        {
            if (IsDisposed || Disposing) return;

            if (!TryBuildSubject(out var subject, requireId: false))
            {
                return;
            }

            await ExecuteSubjectActionAsync(
                async () => await subjectService.AddAsync(subject),
                UiMessages.RecordAdded);
        }

        private async Task UpdateSubjectAsync()
        {
            if (IsDisposed || Disposing) return;

            if (!TryBuildSubject(out var subject))
            {
                return;
            }

            await ExecuteSubjectActionAsync(
                async () => await subjectService.UpdateAsync(subject),
                UiMessages.RecordUpdated);
        }

        private async Task DeleteSubjectAsync()
        {
            if (IsDisposed || Disposing) return;

            if (!TryGetSubjectIdFromEditor(out int subjectId))
            {
                return;
            }

            var confirmation = UiMessages.Confirm(this,
                "حذف المادة من القائمة ومن التوليد الجديد؟ ستبقى تكليفاتها ومحاضراتها المحفوظة دون حذف.", "حذف المادة");

            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            await ExecuteSubjectActionAsync(
                async () => await subjectService.DeleteAsync(subjectId),
                UiMessages.RecordDeleted);
        }

        private Task ExecuteSubjectActionAsync(Func<Task> action, string successMessage)
        {
            return EditorAction.RunAsync(this, action, LoadSubjectsAsync, SetSubjectActionsEnabled,
                () =>
                {
                    ClearSubjectForm();
                    ShowInformation(successMessage);
                },
                ex => ShowError("تعذر إتمام العملية على المادة.", ex));
        }

        private bool TryBuildSubject(out Subject subject, bool requireId = true)
        {
            subject = new Subject
            {
                SubjectName = txtSubjectName.Text.Trim(),
                StudyYearID = GetSelectedRequiredId(cmbStudyYear),
                BranchID = GetSelectedOptionalId(cmbBranch),
            };

            var subjectId = 0;
            if (requireId && !TryGetSubjectIdFromEditor(out subjectId))
            {
                return false;
            }

            subject.SubjectID = requireId ? subjectId : 0;

            if (string.IsNullOrWhiteSpace(subject.SubjectName))
            {
                ShowInformation(UiMessages.RequiredFields);
                txtSubjectName.Focus();
                return false;
            }

            if (subject.StudyYearID <= 0)
            {
                ShowInformation(UiMessages.RequiredFields);
                cmbStudyYear.Focus();
                return false;
            }

            if (AcademicStructureRules.UsesGeneralSections(subject.StudyYearID))
            {
                subject.BranchID = null;
            }

            if (AcademicStructureRules.UsesBranches(subject.StudyYearID) && !subject.BranchID.HasValue)
            {
                ShowInformation(UiMessages.RequiredFields);
                cmbBranch.Focus();
                return false;
            }

            if (!int.TryParse(cmbSemester.Text, out int semester) || semester <= 0)
            {
                ShowInformation(UiMessages.RequiredFields);
                cmbSemester.Focus();
                return false;
            }

            if (!TryReadLectureCount(txtTheoreticalLectureCount, out int theoreticalCount) ||
                !TryReadLectureCount(txtPracticalLectureCount, out int practicalCount))
            {
                return false;
            }

            subject.SemesterNumber = semester;
            var original = subjectRows.FirstOrDefault(row => row.SubjectID == subjectId);
            subject.TheoreticalHours = original?.TheoreticalHours ?? 0;
            subject.PracticalHours = original?.PracticalHours ?? 0;
            subject.TheoreticalLectureCount = theoreticalCountIsManual ? theoreticalCount : null;
            subject.PracticalLectureCount = practicalCountIsManual ? practicalCount : null;
            subject.CreditUnits = original?.CreditUnits ?? 0;
            return true;
        }

        private bool TryGetSubjectIdFromEditor(out int subjectId)
        {
            if (int.TryParse(txtSubjectId.Text, out subjectId) && subjectId > 0)
            {
                return true;
            }

            ShowInformation("اختر مادة من الجدول أولاً.");
            txtSubjectId.Focus();
            return false;
        }

        private void PopulateSubjectEditorFromSelection()
        {
            if (IsDisposed || Disposing) return;

            if (dgvSubjects.CurrentRow?.DataBoundItem is not SubjectRow row)
            {
                return;
            }

            PopulateSubjectEditor(row);
        }

        private void PopulateSubjectEditor(SubjectRow row)
        {
            txtSubjectId.Text = row.SubjectID.ToString();
            txtSubjectName.Text = row.SubjectName;
            SelectComboValue(cmbStudyYear, row.StudyYearID);
            SelectComboValue(cmbBranch, row.BranchID);
            ApplyStudyYearRulesToEditor();
            SelectComboText(cmbSemester, row.SemesterNumber.ToString());
            updatingLectureCounts = true;
            try
            {
                legacyTheoreticalHours = row.TheoreticalHours;
                legacyPracticalHours = row.PracticalHours;
                theoreticalCountIsManual = row.TheoreticalLectureOverride.HasValue;
                practicalCountIsManual = row.PracticalLectureOverride.HasValue;
                txtTheoreticalLectureCount.Text = row.TheoreticalLectureCount.ToString(CultureInfo.InvariantCulture);
                txtPracticalLectureCount.Text = row.PracticalLectureCount.ToString(CultureInfo.InvariantCulture);
            }
            finally { updatingLectureCounts = false; }
        }

        private void ClearSubjectForm()
        {
            if (IsDisposed || Disposing) return;

            txtSubjectId.Text = "تلقائي";
            txtSubjectName.Clear();
            ClearCombo(cmbStudyYear);
            ClearCombo(cmbBranch);
            cmbBranch.Enabled = true;
            ClearCombo(cmbSemester);
            legacyTheoreticalHours = legacyPracticalHours = 0;
            ResetLectureCounts();
            dgvSubjects.ClearSelection();
            txtSubjectName.Focus();
        }

        private void ApplyStudyYearRulesToEditor()
        {
            if (IsDisposed || Disposing) return;

            var studyYearId = GetSelectedRequiredId(cmbStudyYear);

            if (studyYearId <= 0)
            {
                cmbBranch.Enabled = true;
                return;
            }

            if (AcademicStructureRules.UsesGeneralSections(studyYearId))
            {
                SelectComboValue(cmbBranch, null);
                cmbBranch.Enabled = false;
                return;
            }

            cmbBranch.Enabled = true;
        }

        private void ResetLectureCounts()
        {
            theoreticalCountIsManual = practicalCountIsManual = false;
            UpdateAutomaticLectureCounts();
        }

        private void UpdateAutomaticLectureCounts()
        {
            if (updatingLectureCounts || IsDisposed || Disposing) return;
            var defaults = new Subject { TheoreticalHours = legacyTheoreticalHours, PracticalHours = legacyPracticalHours };
            updatingLectureCounts = true;
            try
            {
                if (!theoreticalCountIsManual)
                    txtTheoreticalLectureCount.Text = defaults.DefaultTheoreticalLectureCount.ToString(CultureInfo.InvariantCulture);
                if (!practicalCountIsManual)
                    txtPracticalLectureCount.Text = defaults.DefaultPracticalLectureCount.ToString(CultureInfo.InvariantCulture);
            }
            finally { updatingLectureCounts = false; }
        }

        private bool TryReadLectureCount(Guna2TextBox editor, out int value)
        {
            if (int.TryParse(editor.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) &&
                value is >= 0 and <= Subject.MaximumWeeklyLectureCount) return true;
            ShowInformation($"أدخل عدداً صحيحاً من 0 إلى {Subject.MaximumWeeklyLectureCount} للمحاضرات الأسبوعية. العدد العملي يخص كل مجموعة.");
            editor.Focus();
            return false;
        }

        private void SetSubjectActionsEnabled(bool enabled)
        {
            if (IsDisposed || Disposing) return;

            btnAddSubject.Enabled = enabled;
            btnUpdateSubject.Enabled = enabled;
            btnDeleteSubject.Enabled = enabled;
            dgvSubjects.Enabled = enabled;
        }

        private void BindBranchCombo()
        {
            var branches = new List<ComboOption> { new(null, "عام / جميع الفروع") };
            branches.AddRange(branchesLookup.Select(branch => new ComboOption(branch.BranchID, branch.BranchName)));
            BindCombo(cmbBranch, branches);
        }

        private void BindSubjectFilterLookups()
        {
            isUpdatingSubjectFilters = true;

            var studyYears = new List<ComboOption> { new(null, "جميع المراحل") };
            studyYears.AddRange(studyYearsLookup.Select(studyYear => new ComboOption(studyYear.StudyYearID, studyYear.YearName)));
            BindCombo(cmbStudyYearFilter, studyYears);
            cmbStudyYearFilter.SelectedIndex = 0;

            BindBranchFilterCombo(null, null);

            isUpdatingSubjectFilters = false;
        }

        private void BindBranchFilterCombo(int? studyYearId, int? selectedBranchId)
        {
            var availableBranchIds = subjectRows
                .Where(row => !studyYearId.HasValue || row.StudyYearID == studyYearId.Value)
                .Select(row => row.BranchID)
                .ToHashSet();

            var branches = new List<ComboOption> { new(null, "جميع الفروع") };
            branches.AddRange(branchesLookup
                .Where(branch => availableBranchIds.Count == 0 || availableBranchIds.Contains(branch.BranchID))
                .Select(branch => new ComboOption(branch.BranchID, branch.BranchName)));

            BindCombo(cmbBranchFilter, branches);

            if (selectedBranchId.HasValue && branches.Any(branch => branch.Id == selectedBranchId.Value))
            {
                SelectComboValue(cmbBranchFilter, selectedBranchId);
            }
            else
            {
                cmbBranchFilter.SelectedIndex = 0;
            }
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

        private static void SelectComboText(Guna.UI2.WinForms.Guna2ComboBox combo, string text)
        {
            ComboBoxHelper.SelectText(combo, text, addMissing: true);
        }

        private static void ClearCombo(Guna.UI2.WinForms.Guna2ComboBox combo)
        {
            ComboBoxHelper.Clear(combo);
        }

        private void ShowInformation(string message)
        {
            UiMessages.ShowInformation(this, message, "المواد");
        }

        private void ShowError(string message, Exception ex)
        {
            UiMessages.ShowError(this, message, "المواد", ex);
        }

        private sealed class SubjectRow
        {
            public int SubjectID { get; init; }
            public string SubjectName { get; init; } = string.Empty;
            public int StudyYearID { get; init; }
            public int? BranchID { get; init; }
            public string StudyYearName { get; init; } = string.Empty;
            public string BranchName { get; init; } = string.Empty;
            public int SemesterNumber { get; init; }
            public double TheoreticalHours { get; init; }
            public double PracticalHours { get; init; }
            public double CreditUnits { get; init; }
            public int? TheoreticalLectureOverride { get; init; }
            public int? PracticalLectureOverride { get; init; }
            public int TheoreticalLectureCount { get; init; }
            public int PracticalLectureCount { get; init; }

            public static SubjectRow FromSubject(Subject subject)
            {
                return new SubjectRow
                {
                    SubjectID = subject.SubjectID,
                    SubjectName = subject.SubjectName,
                    StudyYearID = subject.StudyYearID,
                    BranchID = subject.BranchID,
                    StudyYearName = subject.StudyYear?.YearName ?? "-",
                    BranchName = subject.Branch?.BranchName ?? "عام",
                    SemesterNumber = subject.SemesterNumber,
                    TheoreticalHours = subject.TheoreticalHours,
                    PracticalHours = subject.PracticalHours,
                    CreditUnits = subject.CreditUnits,
                    TheoreticalLectureOverride = subject.TheoreticalLectureCount,
                    PracticalLectureOverride = subject.PracticalLectureCount,
                    TheoreticalLectureCount = subject.EffectiveTheoreticalLectureCount,
                    PracticalLectureCount = subject.EffectivePracticalLectureCount,
                };
            }
        }

    }
}
