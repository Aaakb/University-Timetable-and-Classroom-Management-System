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
            InitializeAssignmentScopeEditor();
            if (DesignerEnvironment.IsDesignTime(this)) return;
            ArabicUi.Apply(this);
            ConfigureAssignmentsGrid();
            ConfigureAssignmentEvents();
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (DesignerEnvironment.IsDesignTime(this) || IsDisposed || Disposing) return;
            await RefreshAssignmentsAsync();
        }


        private void ConfigureAssignmentsGrid()
        {
            dgvFacultyMemberSubjects.AutoGenerateColumns = false;
            dgvFacultyMemberSubjects.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            GridRowNumbers.Attach(dgvFacultyMemberSubjects, colRowNumber);
            foreach (var (name, caption) in new[] { ("SemesterName", "الفصل الدراسي"), ("StudyYearName", "المرحلة"), ("BranchName", "التخصص"), ("SectionName", "الشعبة"), ("LectureTypeName", "نوع التكليف") })
                dgvFacultyMemberSubjects.Columns.Add(new DataGridViewTextBoxColumn { Name = "colAssignment" + name,
                    DataPropertyName = name, HeaderText = caption, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 75 });
        }

        private void ConfigureAssignmentEvents()
        {
            dgvFacultyMemberSubjects.SelectionChanged += (_, _) => PopulateAssignmentEditorFromSelection();
            btnAddAssignment.Click += async (_, _) => await AddAssignmentAsync();
            btnUpdateAssignment.Click += async (_, _) => await UpdateAssignmentAsync();
            btnDeleteAssignment.Click += async (_, _) => await DeleteAssignmentAsync();
            btnClearAssignmentForm.Click += (_, _) => ClearAssignmentForm();
            cmbAssignmentStudyYear.SelectedIndexChanged += (_, _) => ApplyAssignmentYearSelection();
            cmbAssignmentSemester.SelectedIndexChanged += (_, _) => ApplyAssignmentSemesterSelection();
            cmbAssignmentBranch.SelectedIndexChanged += (_, _) => ApplyAssignmentBranchSelection();
            cmbSubject.SelectedIndexChanged += (_, _) => { if (!bindingAssignmentScope) BindAssignmentSections(); };
        }

        private async Task RefreshAssignmentsAsync()
        {
            if (IsDisposed || Disposing) return;

            SetAssignmentActionsEnabled(false);

            try
            {
                await LoadLookupsAsync();
                if (IsDisposed || Disposing) return;
                await LoadAssignmentsAsync();
                if (IsDisposed || Disposing) return;
                ClearAssignmentForm();
            }
            catch (Exception ex)
            {
                ShowError("تعذر تحديث التكليفات التدريسية.", ex);
            }
            finally
            {
                SetAssignmentActionsEnabled(true);
            }
        }

        private async Task LoadLookupsAsync()
        {
            if (IsDisposed || Disposing) return;

            var facultyMembers = await facultyMemberService.GetAllAsync();
            if (IsDisposed || Disposing) return;
            var subjects = await subjectService.GetAllAsync();
            if (IsDisposed || Disposing) return;
            assignmentSubjects = subjects;
            assignmentSections = await new SectionService().GetAllAsync();
            if (IsDisposed || Disposing) return;
            assignmentBranches = await new BranchService().GetAllAsync();
            if (IsDisposed || Disposing) return;
            var years = await new StudyYearService().GetAllAsync();
            if (IsDisposed || Disposing) return;
            bindingAssignmentScope = true;
            BindCombo(cmbAssignmentStudyYear, years.Select(y => new ComboOption(y.StudyYearID, ArabicUi.Display(y.YearName))));

            BindCombo(
                cmbFacultyMember,
                facultyMembers
                    .OrderBy(facultyMember => facultyMember.FullName)
                    .Select(facultyMember => new ComboOption(facultyMember.FacultyMemberID, facultyMember.FullName)));

            bindingAssignmentScope = false;
            ApplyAssignmentYearSelection();
        }

        private async Task LoadAssignmentsAsync()
        {
            if (IsDisposed || Disposing) return;

            var assignments = await assignmentService.GetAllAsync();
            if (IsDisposed || Disposing) return;
            assignmentRows = assignments
                .Select(AssignmentRow.FromAssignment)
                .ToList();
            ApplyAssignmentRowsFilter();
        }

        private async Task AddAssignmentAsync()
        {
            if (IsDisposed || Disposing) return;

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
            if (IsDisposed || Disposing) return;

            if (!selectedFacultyMemberId.HasValue || !selectedSubjectId.HasValue)
            {
                ShowInformation("اختر تكليفاً تدريسياً قبل التعديل.");
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
            if (IsDisposed || Disposing) return;

            if (!selectedFacultyMemberId.HasValue || !selectedSubjectId.HasValue)
            {
                ShowInformation("اختر تكليفاً تدريسياً قبل الحذف.");
                return;
            }

            bool isDeletedSubject = dgvFacultyMemberSubjects.CurrentRow?.DataBoundItem is AssignmentRow { IsSubjectDeleted: true };
            var confirmation = isDeletedSubject
                ? UiMessages.Confirm(this, "هل تريد حذف تكليف هذه المادة المحذوفة؟ ستبقى محاضراتها المحفوظة بأسماء التدريسيين ومواعيدها الحالية.", "حذف التكليف التدريسي")
                : UiMessages.ConfirmDeletion(this, "حذف التكليف التدريسي");

            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            await ExecuteAssignmentActionAsync(
                async () => await assignmentService.DeleteAsync(selectedFacultyMemberId.Value, selectedSubjectId.Value),
                UiMessages.RecordDeleted);
        }

        private Task ExecuteAssignmentActionAsync(Func<Task> action, string successMessage)
        {
            return EditorAction.RunAsync(this, action, LoadAssignmentsAsync, SetAssignmentActionsEnabled,
                () =>
                {
                    ClearAssignmentForm();
                    ShowInformation(successMessage);
                },
                ex => ShowError("تعذر إتمام العملية على التكليف التدريسي.", ex));
        }

        private bool TryBuildAssignment(out FacultyMemberSubject assignment)
        {
            assignment = new FacultyMemberSubject
            {
                FacultyMemberID = GetSelectedRequiredId(cmbFacultyMember),
                SubjectID = GetSelectedRequiredId(cmbSubject),
                SectionID = ComboBoxHelper.GetSelectedOptionalId(cmbAssignmentSection),
                LectureType = cmbAssignmentLectureType.SelectedIndex switch { 1 => "Theory", 2 => "Practical", _ => "Both" }
            };

            if (GetSelectedRequiredId(cmbAssignmentStudyYear) <= 0 ||
                (AcademicStructureRules.UsesBranches(GetSelectedRequiredId(cmbAssignmentStudyYear)) &&
                    GetSelectedRequiredId(cmbAssignmentBranch) <= 0) || cmbAssignmentSection.SelectedIndex < 0)
            {
                ShowInformation("اختر المرحلة والتخصص والمادة والشعبة للتكليف.");
                return false;
            }

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
            if (bindingAssignmentScope || IsDisposed || Disposing) return;

            if (dgvFacultyMemberSubjects.SelectedRows.Count == 0 ||
                dgvFacultyMemberSubjects.CurrentRow?.DataBoundItem is not AssignmentRow row)
            {
                return;
            }

            selectedFacultyMemberId = row.FacultyMemberID;
            selectedSubjectId = row.SubjectID;
            bindingAssignmentScope = true;
            if (ComboBoxHelper.GetSelectedOptionalId(cmbAssignmentSemester).HasValue)
                SelectComboValue(cmbAssignmentSemester, row.SemesterNumber);
            SelectComboValue(cmbAssignmentStudyYear, row.StudyYearID);
            BindAssignmentBranches(row.BranchID);
            BindAssignmentSubjects(row.SubjectID);
            BindAssignmentSections(row.SectionID, preserveAll: true);
            cmbAssignmentLectureType.SelectedIndex = row.LectureType switch { "Theory" => 1, "Practical" => 2, _ => 0 };
            SelectComboValue(cmbFacultyMember, row.FacultyMemberID);
            bindingAssignmentScope = false;
        }

        private void ClearAssignmentForm()
        {
            if (IsDisposed || Disposing) return;

            selectedFacultyMemberId = null;
            selectedSubjectId = null;
            cmbAssignmentLectureType.SelectedIndex = 0;
            bindingAssignmentScope = true;
            ClearCombo(cmbFacultyMember);
            ClearCombo(cmbAssignmentStudyYear);
            BindAssignmentBranches();
            ClearCombo(cmbSubject);
            BindAssignmentSubjects();
            BindAssignmentSections();
            bindingAssignmentScope = false;
            dgvFacultyMemberSubjects.ClearSelection();
        }

        private void SetAssignmentActionsEnabled(bool enabled)
        {
            if (IsDisposed || Disposing) return;

            btnAddAssignment.Enabled = enabled;
            btnUpdateAssignment.Enabled = enabled;
            btnDeleteAssignment.Enabled = enabled;
            btnClearAssignmentForm.Enabled = enabled;
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
            var year = ArabicUi.Display(subject.StudyYear?.YearName ?? "المرحلة");
            var branch = subject.Branch?.BranchName ?? "عام";
            return $"{subject.SubjectName} - {year} - {branch}" + (subject.IsDeleted ? " — مادة محذوفة من القائمة" : "");
        }

        private void ShowInformation(string message)
        {
            UiMessages.ShowInformation(this, message, "التكليفات التدريسية");
        }

        private void ShowError(string message, Exception ex)
        {
            UiMessages.ShowError(this, message, "التكليفات التدريسية", ex);
        }

        private sealed class AssignmentRow
        {
            public int FacultyMemberID { get; init; }
            public string FacultyMemberName { get; init; } = string.Empty;
            public int SubjectID { get; init; }
            public string SubjectName { get; init; } = string.Empty;
            public int StudyYearID { get; init; }
            public int SemesterNumber { get; init; }
            public string SemesterName => SemesterNumber switch { 1 => "الأول", 2 => "الثاني", _ => "—" };
            public int? BranchID { get; init; }
            public int? SectionID { get; init; }
            public string StudyYearName { get; init; } = "";
            public string BranchName { get; init; } = "";
            public string SectionName { get; init; } = "";
            public string LectureType { get; init; } = "Both";
            public string LectureTypeName => LectureType switch { "Theory" => "نظري", "Practical" => "عملي", _ => "كلاهما" };
            public bool IsSubjectDeleted { get; init; }

            public static AssignmentRow FromAssignment(FacultyMemberSubject assignment)
            {
                return new AssignmentRow
                {
                    FacultyMemberID = assignment.FacultyMemberID,
                    FacultyMemberName = assignment.FacultyMember?.FullName ?? "-",
                    SubjectID = assignment.SubjectID,
                    StudyYearID = assignment.Subject?.StudyYearID ?? 0,
                    SemesterNumber = assignment.Subject?.SemesterNumber ?? 0,
                    BranchID = assignment.Subject?.BranchID,
                    SectionID = assignment.SectionID,
                    LectureType = assignment.LectureType,
                    StudyYearName = ArabicUi.Display(assignment.Subject?.StudyYear?.YearName ?? ""),
                    BranchName = assignment.Subject?.Branch?.BranchName ?? "عام",
                    SectionName = assignment.Section?.SectionName ?? "جميع الشعب المطابقة",
                    IsSubjectDeleted = assignment.Subject?.IsDeleted == true,
                    SubjectName = assignment.Subject?.SubjectName ?? "-"
                };
            }
        }

    }
}
