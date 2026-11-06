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
            AddAcademicNavigation();
            ArabicUi.Apply(this);
            ConfigureStudyYearsGrid();
            ConfigureStudyYearsEvents();
        }

        private void AddAcademicNavigation()
        {
            using var layout = new LayoutUpdateScope(this);
            var host = new Panel { Name = "academicPageHost", Dock = DockStyle.Fill };
            AcademicPage currentPage = AcademicPage.Stages;
            SectionsForm? sections = null;
            bool refreshStudentGroups = false;
            Func<AcademicPage, Task> navigate = _ => Task.CompletedTask;
            var students = new StudentListPage(AcademicPageNavigation.Create(AcademicPage.Students, page => navigate(page)))
            { Dock = DockStyle.Fill, Visible = false };
            host.Controls.Add(pnlWorkspace);
            host.Controls.Add(students);
            pnlMain.Controls.Add(host);
            host.BringToFront();
            AcademicPageNavigation.PlaceBelowEditor(layoutWorkspace, pnlStudyYearEditor, pnlStudyYearsTable,
                AcademicPageNavigation.Create(AcademicPage.Stages, page => navigate(page)));

            lblTableSubtitle.Text = "اضغط اسم المرحلة لفتح طلبتها. للتعديل، اختر رقم صفها.";
            colYearName.HeaderText = "اسم المرحلة · عرض الطلبة";
            colYearName.DefaultCellStyle.ForeColor = DesktopPalette.Primary;
            colYearName.DefaultCellStyle.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Underline);
            dgvStudyYears.CellMouseEnter += (_, e) =>
                dgvStudyYears.Cursor = e.RowIndex >= 0 && e.ColumnIndex == colYearName.Index ? Cursors.Hand : Cursors.Default;
            dgvStudyYears.MouseLeave += (_, _) => dgvStudyYears.Cursor = Cursors.Default;
            dgvStudyYears.CellClick += async (_, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == colYearName.Index &&
                    dgvStudyYears.Rows[e.RowIndex].DataBoundItem is StudyYear { StudyYearID: >= 1 and <= 4 } year)
                    await OpenStudentsAsync(year.StudyYearID, null);
            };
            dgvStudyYears.KeyDown += async (_, e) =>
            {
                if (e.KeyCode != Keys.Enter || dgvStudyYears.CurrentCell?.OwningColumn != colYearName ||
                    dgvStudyYears.CurrentRow?.DataBoundItem is not StudyYear { StudyYearID: >= 1 and <= 4 } year) return;
                e.SuppressKeyPress = true;
                await OpenStudentsAsync(year.StudyYearID, null);
            };
            async Task OpenStudentsAsync(int stage, int? group)
            {
                var loading = students.OpenStageAsync(stage, group);
                await NavigateAsync(AcademicPage.Students);
                await loading;
            }
            async Task NavigateAsync(AcademicPage page)
            {
                if (IsDisposed || Disposing || currentPage == page) return;
                currentPage = page;
                using (new LayoutUpdateScope(host))
                {
                    if (page == AcademicPage.Sections && sections is null)
                    {
                        sections = new SectionsForm { Dock = DockStyle.Fill, Visible = false };
                        sections.UseAcademicNavigation(AcademicPageNavigation.Create(AcademicPage.Sections, NavigateAsync),
                            (stage, group) => OpenStudentsAsync(stage, group));
                        host.Controls.Add(sections);
                    }
                    pnlWorkspace.Visible = page == AcademicPage.Stages;
                    students.Visible = page == AcademicPage.Students;
                    if (sections is not null) sections.Visible = page == AcademicPage.Sections;
                }
                if (page == AcademicPage.Sections)
                {
                    refreshStudentGroups = true;
                }
                else if (page == AcademicPage.Students)
                {
                    bool refresh = refreshStudentGroups;
                    refreshStudentGroups = false;
                    await students.LoadOnceAsync(refresh);
                }
            }
            navigate = NavigateAsync;
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
            dgvStudyYears.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
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
