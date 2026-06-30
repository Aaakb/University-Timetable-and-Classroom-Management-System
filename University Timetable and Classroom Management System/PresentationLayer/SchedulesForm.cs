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
        private readonly BindingSource scheduleBindingSource = new();

        private List<ScheduleRow> scheduleRows = [];
        private List<Subject> subjectsLookup = [];
        private List<FacultyMember> facultyMembersLookup = [];
        private List<FacultyMemberSubject> facultySubjectAssignmentsLookup = [];
        private List<StudyYear> studyYearsLookup = [];
        private List<Branch> branchesLookup = [];
        private List<Section> sectionsLookup = [];
        private bool isUpdatingScheduleLookups;
        private Guna.UI2.WinForms.Guna2ComboBox cmbSemesterFilter = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblSemesterFilter = null!;
        private Guna.UI2.WinForms.Guna2ComboBox cmbGroupFilter = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblGroupFilter = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblPageTitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblPageSubtitle = null!;
        private Guna.UI2.WinForms.Guna2ComboBox cmbLectureType = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblLectureType = null!;
        private Guna.UI2.WinForms.Guna2ComboBox cmbGroupName = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblGroupName = null!;
        private Guna.UI2.WinForms.Guna2Button btnScheduleGridView = null!;
        private Guna.UI2.WinForms.Guna2Button btnScheduleTimetableView = null!;
        private Panel pnlTimetableHost = null!;
        private TableLayoutPanel tblScheduleTimetable = null!;
        private readonly ToolTip scheduleToolTip = new();
        private bool isTimetableView;

        public SchedulesForm()
        {
            InitializeComponent();
            ConfigureScheduleFilterControls();
            ConfigureLectureTypeAndGroupControls();
            ConfigureSchedulePageHeader();
            ConfigureScheduleLayoutEnhancements();
            ConfigureScheduleCommands();
            ConfigureNavigation();
            ConfigureScheduleGrid();
            ConfigureScheduleTimetableView();
            ConfigureScheduleEvents();
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            try
            {
                await LoadFormAsync();
            }
            catch (Exception ex)
            {
                ShowError("Unable to load schedule form.", ex);
            }
        }

        private void ConfigureNavigation()
        {
            FormNavigation.ConfigureSidebar(this, pnlSidebar, NavigationPage.Schedules);
        }

        private void ConfigureScheduleCommands()
        {
            btnGenerateSchedule.Text = "Generate Timetable";
            btnClearScheduleForm.Text = "Clear Form";
            lblEditorTitle.Text = "Manual Schedule Entry";
            lblTableTitle.Text = "Timetable Records";
            lblFacultyMember.Text = "Teacher";
            lblLectureType.Text = "Type";
            lblDayOfWeek.Text = "Day";
            lblTimeSlot.Text = "Time";
            lblClassroom.Text = "Room";
            lblStudyYear.Text = "Year";
            lblSemesterFilter.Text = "Semester";
            lblStudyYearFilter.Text = "Year";
            lblSectionFilter.Text = "Section";
            lblGroupFilter.Text = "Group";
            lblFacultyFilter.Text = "Faculty";

            btnGenerateSchedule.FillColor = Color.FromArgb(234, 88, 12);
            btnGenerateSchedule.HoverState.FillColor = Color.FromArgb(194, 65, 12);
            btnGenerateSchedule.Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold);
        }

        private static Guna.UI2.WinForms.Guna2Panel CreateScheduleGroupPanel(string title, Point location, Size size)
        {
            var panel = new Guna.UI2.WinForms.Guna2Panel
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent,
                BorderColor = Color.FromArgb(226, 232, 240),
                BorderRadius = 8,
                BorderThickness = 1,
                FillColor = Color.FromArgb(248, 250, 252),
                Location = location,
                Size = size
            };

            panel.Controls.Add(new Guna.UI2.WinForms.Guna2HtmlLabel
            {
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(24, 38, 62),
                Location = new Point(16, 5),
                Text = title
            });

            return panel;
        }

        private static Guna.UI2.WinForms.Guna2Panel CreateCommandGroupPanel(Point location, Size size)
        {
            return new Guna.UI2.WinForms.Guna2Panel
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent,
                FillColor = Color.Transparent,
                Location = location,
                Size = size
            };
        }

        private static Guna.UI2.WinForms.Guna2HtmlLabel CreateEditorLabel(
            string text,
            Point location,
            string name)
        {
            return new Guna.UI2.WinForms.Guna2HtmlLabel
            {
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(51, 65, 85),
                Location = location,
                Name = name,
                Size = new Size(90, 19),
                Text = text
            };
        }

        private static Guna.UI2.WinForms.Guna2ComboBox CreateEditorComboBox(Point location, string name)
        {
            var combo = new Guna.UI2.WinForms.Guna2ComboBox
            {
                BackColor = Color.Transparent,
                BorderColor = Color.FromArgb(203, 213, 225),
                BorderRadius = 8,
                DrawMode = DrawMode.OwnerDrawFixed,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FocusedColor = Color.FromArgb(37, 99, 235),
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(15, 23, 42),
                HoverState = { BorderColor = Color.FromArgb(59, 130, 246) },
                ItemHeight = 36,
                Location = location,
                Name = name,
                Size = new Size(160, 42)
            };

            combo.FocusedState.BorderColor = Color.FromArgb(37, 99, 235);
            return combo;
        }

        private void ConfigureScheduleEvents()
        {
            btnGenerateSchedule.Click += async (_, _) => await GenerateScheduleAsync();
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
            UiMessages.ShowInformation(this, message, "Schedule");
        }

        private void ShowError(string message, Exception ex)
        {
            UiMessages.ShowError(this, message, "Schedule", ex);
        }

    }
}
