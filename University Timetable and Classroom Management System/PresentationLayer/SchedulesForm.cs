using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System
{
    public partial class SchedulesForm : System.Windows.Forms.UserControl
    {
        private readonly ScheduleService scheduleService = new();
        private readonly SchedulePdfExportService schedulePdfExportService = new();
        private readonly SubjectService subjectService = new();
        private readonly FacultyMemberService facultyMemberService = new();
        private readonly FacultyMemberSubjectService facultyMemberSubjectService = new();
        private readonly ClassroomService classroomService = new();
        private readonly TimeSlotService timeSlotService = new();
        private readonly StudyYearService studyYearService = new();
        private readonly BranchService branchService = new();
        private readonly SectionService sectionService = new();
        private readonly ScheduleFilterService scheduleFilterService = new();

        private List<ScheduleRow> scheduleRows = [];
        private List<Subject> subjectsLookup = [];
        private List<FacultyMember> facultyMembersLookup = [];
        private List<FacultyMemberSubject> facultySubjectAssignmentsLookup = [];
        private List<StudyYear> studyYearsLookup = [];
        private List<Branch> branchesLookup = [];
        private List<Section> sectionsLookup = [];
        private bool isUpdatingScheduleLookups;
        private bool isTimetableView;
        private int scheduleEditorLoadVersion;

        private bool IsScheduleUiUnavailable => IsDisposed || Disposing;

        public SchedulesForm()
        {
            InitializeComponent();
            btnGenerateSchedule.Text = "توليد جدول جديد";
            if (DesignerEnvironment.IsDesignTime(this)) return;
            ArabicUi.Apply(this);
            ConfigureScheduleGrid();
            ConfigureScheduleEvents();
            ConfigureTimetableEvents();
            pnlWorkspace.Resize += (_, _) => ResizeScheduleWorkspace();
            ResizeScheduleWorkspace();
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (DesignerEnvironment.IsDesignTime(this) || IsScheduleUiUnavailable) return;

            try
            {
                await LoadFormAsync();
            }
            catch (Exception ex)
            {
                ShowError("تعذر تحميل شاشة الجدول الدراسي.", ex);
            }
        }


        private void ConfigureScheduleEvents()
        {
            btnGenerateSchedule.Click += async (_, _) => await GenerateScheduleAsync();
            btnBackupTimetable.Click += async (_, _) => await SaveTimetableBackupAsync();
            btnRestoreTimetable.Click += async (_, _) => await RestoreTimetableBackupAsync();
            dgvSchedules.CurrentCellDirtyStateChanged += async (_, _) => await SaveScheduleLockCheckboxAsync();
            btnAddSchedule.Click += async (_, _) => await AddScheduleAsync();
            btnUpdateSchedule.Click += async (_, _) => await UpdateScheduleAsync();
            btnDeleteSchedule.Click += async (_, _) => await DeleteScheduleAsync();
            btnClearScheduleForm.Click += (_, _) => ClearScheduleForm();
            btnExportSchedulePdf.Click += async (_, _) => await ExportSchedulePdfAsync();
            btnScheduleGridView.Click += (_, _) => ShowScheduleGridView();
            btnScheduleTimetableView.Click += (_, _) => ShowScheduleTimetableView();

            dgvSchedules.SelectionChanged += async (_, _) => await PopulateScheduleEditorFromSelectionAsync();
            cmbSubject.SelectedIndexChanged += (_, _) => ApplySubjectSelection();
            cmbStudyYear.SelectedIndexChanged += (_, _) => ApplyStudyYearSelection();
            cmbBranch.SelectedIndexChanged += (_, _) => ApplyBranchSelection();
            cmbSection.SelectedIndexChanged += (_, _) => ApplySectionSelection();
            cmbFacultyFilter.SelectedIndexChanged += (_, _) => ApplyScheduleFilters();
            cmbSectionFilter.SelectedIndexChanged += (_, _) => ApplySectionFilterSelection();
            cmbStudyYearFilter.SelectedIndexChanged += (_, _) => ApplyStudyYearFilterSelection();
            cmbSemesterFilter.SelectedIndexChanged += (_, _) => ApplyScheduleFilters();
            cmbGroupFilter.SelectedIndexChanged += (_, _) => ApplyScheduleFilters();
            cmbLectureType.SelectedIndexChanged += (_, _) => ApplyLectureTypeSelection();
        }

        private void ShowInformation(string message)
        {
            if (IsScheduleUiUnavailable) return;
            UiMessages.ShowInformation(this, message, "الجدول الدراسي");
        }



        private void ShowError(string message, Exception ex)
        {
            if (IsScheduleUiUnavailable) return;
            UiMessages.ShowError(this, message, "الجدول الدراسي", ex);
        }

    }
}
