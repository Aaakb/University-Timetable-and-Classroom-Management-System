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
            if (DesignerEnvironment.IsDesignTime(this)) return;
            ArabicUi.Apply(this);
            ConfigureStudyYearsGrid();
            ConfigureStudyYearsEvents();
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (DesignerEnvironment.IsDesignTime(this) || IsDisposed || Disposing) return;
            await LoadStudyYearsAsync();
            if (IsDisposed || Disposing) return;
            ClearStudyYearForm();
        }


        private void ConfigureStudyYearsGrid()
        {
            dgvStudyYears.AutoGenerateColumns = false;
            GridRowNumbers.Attach(dgvStudyYears, colRowNumber);
        }

        private void ConfigureStudyYearsEvents()
        {
            dgvStudyYears.SelectionChanged += (_, _) => PopulateStudyYearEditorFromSelection();

            btnAddStudyYear.Click += async (_, _) => await AddStudyYearAsync();
            btnUpdateStudyYear.Click += async (_, _) => await UpdateStudyYearAsync();
            btnDeleteStudyYear.Click += async (_, _) => await DeleteStudyYearAsync();
        }

        private async Task LoadStudyYearsAsync()
        {
            if (IsDisposed || Disposing) return;

            SetStudyYearActionsEnabled(false);

            try
            {
                var studyYears = await studyYearService.GetAllAsync();
                if (IsDisposed || Disposing) return;
                dgvStudyYears.DataSource = studyYears;
                dgvStudyYears.ClearSelection();
            }
            catch (Exception ex)
            {
                ShowError("تعذر تحميل المراحل الدراسية.", ex);
            }
            finally
            {
                SetStudyYearActionsEnabled(true);
            }
        }

        private async Task AddStudyYearAsync()
        {
            if (IsDisposed || Disposing) return;

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
            if (IsDisposed || Disposing) return;

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
            if (IsDisposed || Disposing) return;

            if (!TryGetStudyYearIdFromEditor(out int studyYearId))
            {
                return;
            }

            var confirmation = UiMessages.ConfirmDeletion(this, "حذف المرحلة الدراسية");

            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            await ExecuteStudyYearActionAsync(
                async () => await studyYearService.DeleteAsync(studyYearId),
                UiMessages.RecordDeleted);
        }

        private Task ExecuteStudyYearActionAsync(Func<Task> action, string successMessage)
        {
            return EditorAction.RunAsync(this, action, LoadStudyYearsAsync, SetStudyYearActionsEnabled,
                () =>
                {
                    ClearStudyYearForm();
                    ShowInformation(successMessage);
                },
                ex => ShowError("تعذر إتمام العملية على المرحلة الدراسية.", ex));
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

            ShowInformation("اختر مرحلة دراسية من الجدول أولاً.");
            return false;
        }

        private void PopulateStudyYearEditorFromSelection()
        {
            if (IsDisposed || Disposing) return;

            if (dgvStudyYears.CurrentRow?.DataBoundItem is not StudyYear studyYear)
            {
                return;
            }

            txtStudyYearId.Text = studyYear.StudyYearID.ToString();
            txtYearName.Text = studyYear.YearName;
        }

        private void ClearStudyYearForm()
        {
            if (IsDisposed || Disposing) return;

            txtStudyYearId.Text = "تلقائي";
            txtYearName.Clear();
            dgvStudyYears.ClearSelection();
            txtYearName.Focus();
        }

        private void SetStudyYearActionsEnabled(bool enabled)
        {
            if (IsDisposed || Disposing) return;

            btnAddStudyYear.Enabled = enabled;
            btnUpdateStudyYear.Enabled = enabled;
            btnDeleteStudyYear.Enabled = enabled;
            dgvStudyYears.Enabled = enabled;
        }

        private void ShowInformation(string message)
        {
            UiMessages.ShowInformation(this, message, "المراحل الدراسية");
        }

        private void ShowError(string message, Exception ex)
        {
            UiMessages.ShowError(this, message, "المراحل الدراسية", ex);
        }

    }
}
