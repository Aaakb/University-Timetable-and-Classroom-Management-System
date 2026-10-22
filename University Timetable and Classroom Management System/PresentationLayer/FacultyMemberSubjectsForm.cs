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
            GridRowNumbers.Attach(dgvFacultyMemberSubjects, colRowNumber);
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
            if (IsDisposed || Disposing) return;

            var assignments = await assignmentService.GetAllAsync();
            if (IsDisposed || Disposing) return;
            dgvFacultyMemberSubjects.DataSource = assignments
                .Select(AssignmentRow.FromAssignment)
                .OrderBy(row => row.SubjectName)
                .ThenBy(row => row.FacultyMemberName)
                .ToList();
            dgvFacultyMemberSubjects.ClearSelection();
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

        private async Task ExecuteAssignmentActionAsync(Func<Task> action, string successMessage)
        {
            if (IsDisposed || Disposing) return;

            SetAssignmentActionsEnabled(false);

            try
            {
                await action();
                if (IsDisposed || Disposing) return;
                await LoadAssignmentsAsync();
                if (IsDisposed || Disposing) return;
                ClearAssignmentForm();
                ShowInformation(successMessage);
            }
            catch (Exception ex)
            {
                ShowError("تعذر إتمام العملية على التكليف التدريسي.", ex);
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
            if (IsDisposed || Disposing) return;

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
            if (IsDisposed || Disposing) return;

            selectedFacultyMemberId = null;
            selectedSubjectId = null;
            ClearCombo(cmbFacultyMember);
            ClearCombo(cmbSubject);
            dgvFacultyMemberSubjects.ClearSelection();
        }

        private void SetAssignmentActionsEnabled(bool enabled)
        {
            if (IsDisposed || Disposing) return;

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
            public bool IsSubjectDeleted { get; init; }

            public static AssignmentRow FromAssignment(FacultyMemberSubject assignment)
            {
                return new AssignmentRow
                {
                    FacultyMemberID = assignment.FacultyMemberID,
                    FacultyMemberName = assignment.FacultyMember?.FullName ?? "-",
                    SubjectID = assignment.SubjectID,
                    IsSubjectDeleted = assignment.Subject?.IsDeleted == true,
                    SubjectName = assignment.Subject is null ? "-" : FormatSubject(assignment.Subject)
                };
            }
        }

    }
}
