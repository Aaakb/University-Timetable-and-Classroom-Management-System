using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;
using Guna.UI2.WinForms;

namespace University_Timetable_and_Classroom_Management_System
{
    public partial class FacultyMembersForm : System.Windows.Forms.UserControl
    {
        private readonly FacultyMemberService facultyMemberService = new();
        private readonly Guna2Button btnImportFaculty = new()
        {
            Name = "btnImportFaculty", Text = "استيراد Excel أو CSV", AccessibleName = "استيراد التدريسيين",
            Size = new Size(180, 44), Margin = new Padding(6, 4, 6, 4), BorderRadius = DesktopCorners.Button,
            FillColor = DesktopPalette.Primary, ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold)
        };
        private readonly CheckBox chkImportedFaculty = new()
        {
            Name = "chkImportedFaculty", Text = "عرض آخر دفعة مستوردة",
            AutoSize = true, Margin = new Padding(8, 14, 8, 4), Visible = false
        };
        private readonly Guna2Button btnExportFaculty = new()
        {
            Name = "btnExportFaculty", Text = "تصدير التدريسيين", Size = new Size(145, 44),
            Margin = new Padding(6, 4, 6, 4), BorderRadius = DesktopCorners.Button, BorderThickness = 1,
            BorderColor = DesktopPalette.Border, FillColor = Color.White, ForeColor = DesktopPalette.Header,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold), Cursor = Cursors.Hand, Enabled = false
        };
        private List<FacultyMember> facultyRows = [];
        private HashSet<int> lastImportedIds = [];
        private readonly Guna2Button btnChooseAcademicTitle = new()
        {
            Name = "btnChooseAcademicTitle", Text = "اختيار لقب", Size = new Size(100, 42),
            Margin = new Padding(4, 0, 0, 0), BorderRadius = DesktopCorners.Button,
            FillColor = DesktopPalette.SoftAccent, ForeColor = DesktopPalette.Ink,
            Font = new Font("Segoe UI Semibold", 10F), Enabled = false
        };
        private string[] academicTitles = [];
        private string? chosenAcademicTitle;
        private readonly ContextMenuStrip academicTitleMenu = new() { RightToLeft = RightToLeft.Yes };

        public FacultyMembersForm()
        {
            InitializeComponent();
            Disposed += (_, _) => academicTitleMenu.Dispose();
            using (new LayoutUpdateScope(this))
            {
                flowResultsHeader.WrapContents = true;
                flowResultsHeader.Controls.Add(btnImportFaculty);
                flowResultsHeader.Controls.Add(btnExportFaculty);
                flowResultsHeader.Controls.Add(chkImportedFaculty);
                layoutCmbAcademicTitle.ColumnCount = 2;
                layoutCmbAcademicTitle.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                layoutCmbAcademicTitle.SetColumnSpan(lblAcademicTitle, 2);
                layoutCmbAcademicTitle.Controls.Add(btnChooseAcademicTitle, 1, 1);
            }
            if (DesignerEnvironment.IsDesignTime(this)) return;
            ArabicUi.Apply(this);
            ConfigureFacultyMembersGrid();
            ConfigureFacultyMembersEvents();
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (DesignerEnvironment.IsDesignTime(this) || IsDisposed || Disposing) return;
            await LoadFacultyMembersAsync();
            if (IsDisposed || Disposing) return;
            ClearFacultyMemberForm();
        }


        private void ConfigureFacultyMembersGrid()
        {
            dgvFacultyMembers.AutoGenerateColumns = false;
            dgvFacultyMembers.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            GridRowNumbers.Attach(dgvFacultyMembers, colRowNumber);
        }

        private void ConfigureFacultyMembersEvents()
        {
            dgvFacultyMembers.SelectionChanged += (_, _) => PopulateFacultyMemberEditorFromSelection();

            btnAddFacultyMember.Click += async (_, _) => await AddFacultyMemberAsync();
            btnUpdateFacultyMember.Click += async (_, _) => await UpdateFacultyMemberAsync();
            btnDeleteFacultyMember.Click += async (_, _) => await DeleteFacultyMemberAsync();
            btnImportFaculty.Click += async (_, _) => await ImportFacultyAsync();
            btnExportFaculty.Click += async (_, _) => await ExportFacultyAsync();
            chkImportedFaculty.CheckedChanged += (_, _) => ShowFacultyRows();
            btnChooseAcademicTitle.Click += (_, _) => ChooseAcademicTitle();
            txtAcademicTitle.TextChanged += (_, _) =>
            {
                if (chosenAcademicTitle is { } title && txtAcademicTitle.Text != ArabicUi.Display(title))
                    chosenAcademicTitle = null;
            };
        }

        private async Task LoadFacultyMembersAsync()
        {
            if (IsDisposed || Disposing) return;

            SetFacultyMemberActionsEnabled(false);

            try
            {
                var facultyMembers = await facultyMemberService.GetAllAsync();
                if (IsDisposed || Disposing) return;
                facultyRows = facultyMembers;
                BindTitleOptions();
                ShowFacultyRows();
            }
            catch (Exception ex)
            {
                ShowError("تعذر تحميل التدريسيين.", ex);
            }
            finally
            {
                SetFacultyMemberActionsEnabled(true);
            }
        }

        private void ShowFacultyRows()
        {
            if (IsDisposed || Disposing) return;
            dgvFacultyMembers.DataSource = chkImportedFaculty.Checked
                ? facultyRows.Where(member => lastImportedIds.Contains(member.FacultyMemberID)).ToList()
                : facultyRows;
            btnExportFaculty.Enabled = dgvFacultyMembers.Enabled && dgvFacultyMembers.Rows.Count > 0;
            ClearFacultyMemberForm();
        }

        private void BindTitleOptions()
        {
            academicTitles = facultyRows.Select(member => member.AcademicTitle).OfType<string>()
                .Where(title => !string.IsNullOrWhiteSpace(title)).Distinct(StringComparer.Ordinal)
                .OrderBy(ArabicUi.Display).ToArray();
            btnChooseAcademicTitle.Enabled = academicTitles.Length > 0;
        }

        private void ChooseAcademicTitle()
        {
            while (academicTitleMenu.Items.Count > 0) academicTitleMenu.Items[0].Dispose();
            foreach (string title in academicTitles)
                academicTitleMenu.Items.Add(ArabicUi.Display(title), null, (_, _) => SetAcademicTitle(title));
            academicTitleMenu.Show(btnChooseAcademicTitle, new Point(0, btnChooseAcademicTitle.Height));
        }

        private async Task ImportFacultyAsync()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "اختر ملفًا بعمود «اسم التدريسي» وعمود «اللقب العلمي» الاختياري",
                Filter = "Excel وCSV (*.xlsx;*.csv)|*.xlsx;*.csv",
                CheckFileExists = true
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            SetFacultyMemberActionsEnabled(false);
            btnImportFaculty.Text = "جارٍ الاستيراد…";
            try
            {
                var (ids, skipped) = await facultyMemberService.ImportListAsync(dialog.FileName);
                if (IsDisposed || Disposing) return;
                if (ids.Count > 0)
                {
                    lastImportedIds = ids.ToHashSet();
                    chkImportedFaculty.Visible = true;
                    chkImportedFaculty.Checked = true;
                    await LoadFacultyMembersAsync();
                }
                ShowInformation($"أُضيف {ids.Count} تدريسيًا، وتم تجاوز {skipped} من الأسماء المكررة.");
            }
            catch (Exception ex) { ShowError("تعذر استيراد التدريسيين.", ex); }
            finally
            {
                if (!IsDisposed && !Disposing) btnImportFaculty.Text = "استيراد Excel أو CSV";
                SetFacultyMemberActionsEnabled(true);
            }
        }

        private async Task ExportFacultyAsync()
        {
            var rows = dgvFacultyMembers.Rows.Cast<DataGridViewRow>().Select(row => (FacultyMember)row.DataBoundItem)
                .Select(member => new RosterRow(member.FullName, ArabicUi.Display(member.AcademicTitle ?? ""))).ToArray();
            string title = chkImportedFaculty.Checked ? "التدريسيون - آخر دفعة مستوردة" : "قائمة التدريسيين";
            SetFacultyMemberActionsEnabled(false);
            try { await RosterExportDialog.ShowAsync(this, title, "اسم التدريسي", "اللقب العلمي", rows); }
            finally { SetFacultyMemberActionsEnabled(true); }
        }

        private async Task AddFacultyMemberAsync()
        {
            if (IsDisposed || Disposing) return;

            if (!TryBuildFacultyMember(out var facultyMember, requireId: false))
            {
                return;
            }

            await ExecuteFacultyMemberActionAsync(
                async () => await facultyMemberService.AddAsync(facultyMember),
                UiMessages.RecordAdded);
        }

        private async Task UpdateFacultyMemberAsync()
        {
            if (IsDisposed || Disposing) return;

            if (!TryBuildFacultyMember(out var facultyMember))
            {
                return;
            }

            await ExecuteFacultyMemberActionAsync(
                async () => await facultyMemberService.UpdateAsync(facultyMember),
                UiMessages.RecordUpdated);
        }

        private async Task DeleteFacultyMemberAsync()
        {
            if (IsDisposed || Disposing) return;

            if (!TryGetFacultyMemberIdFromEditor(out int facultyMemberId))
            {
                return;
            }

            var confirmation = UiMessages.ConfirmDeletion(this, "حذف التدريسي");

            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            await ExecuteFacultyMemberActionAsync(
                async () => await facultyMemberService.DeleteAsync(facultyMemberId),
                UiMessages.RecordDeleted);
        }

        private Task ExecuteFacultyMemberActionAsync(Func<Task> action, string successMessage)
        {
            return EditorAction.RunAsync(this, action, LoadFacultyMembersAsync, SetFacultyMemberActionsEnabled,
                () =>
                {
                    ClearFacultyMemberForm();
                    ShowInformation(successMessage);
                },
                ex => ShowError("تعذر إتمام العملية على التدريسي.", ex));
        }

        private bool TryBuildFacultyMember(out FacultyMember facultyMember, bool requireId = true)
        {
            facultyMember = new FacultyMember
            {
                FullName = txtFacultyMemberFullName.Text.Trim(),
                AcademicTitle = ReadAcademicTitle()
            };

            var facultyMemberId = 0;
            if (requireId && !TryGetFacultyMemberIdFromEditor(out facultyMemberId))
            {
                return false;
            }

            facultyMember.FacultyMemberID = requireId ? facultyMemberId : 0;

            if (string.IsNullOrWhiteSpace(facultyMember.FullName))
            {
                ShowInformation(UiMessages.RequiredFields);
                txtFacultyMemberFullName.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(facultyMember.AcademicTitle))
            {
                facultyMember.AcademicTitle = null;
            }

            return true;
        }

        private string ReadAcademicTitle()
        {
            string title = txtAcademicTitle.Text.Trim();
            return chosenAcademicTitle is { } stored && title == ArabicUi.Display(stored) ? stored : title;
        }

        private void SetAcademicTitle(string? title)
        {
            chosenAcademicTitle = title;
            txtAcademicTitle.Text = title is null ? "" : ArabicUi.Display(title);
        }

        private bool TryGetFacultyMemberIdFromEditor(out int facultyMemberId)
        {
            if (int.TryParse(txtFacultyMemberId.Text, out facultyMemberId) && facultyMemberId > 0)
            {
                return true;
            }

            ShowInformation("اختر تدريسياً من الجدول أولاً.");
            return false;
        }

        private void PopulateFacultyMemberEditorFromSelection()
        {
            if (IsDisposed || Disposing) return;

            if (dgvFacultyMembers.SelectedRows.Count != 1 ||
                dgvFacultyMembers.SelectedRows[0].DataBoundItem is not FacultyMember facultyMember)
            {
                return;
            }

            txtFacultyMemberId.Text = facultyMember.FacultyMemberID.ToString();
            txtFacultyMemberFullName.Text = facultyMember.FullName;
            SetAcademicTitle(facultyMember.AcademicTitle);
        }

        private void ClearFacultyMemberForm()
        {
            if (IsDisposed || Disposing) return;

            dgvFacultyMembers.ClearSelection();
            txtFacultyMemberId.Text = "تلقائي";
            txtFacultyMemberFullName.Clear();
            SetAcademicTitle(null);
            txtFacultyMemberFullName.Focus();
        }

        private void SetFacultyMemberActionsEnabled(bool enabled)
        {
            if (IsDisposed || Disposing) return;

            btnAddFacultyMember.Enabled = enabled;
            btnUpdateFacultyMember.Enabled = enabled;
            btnDeleteFacultyMember.Enabled = enabled;
            btnImportFaculty.Enabled = enabled;
            btnExportFaculty.Enabled = enabled && dgvFacultyMembers.Rows.Count > 0;
            chkImportedFaculty.Enabled = enabled;
            btnChooseAcademicTitle.Enabled = enabled && academicTitles.Length > 0;
            dgvFacultyMembers.Enabled = enabled;
        }

        private void ShowInformation(string message)
        {
            UiMessages.ShowInformation(this, message, "التدريسيون");
        }

        private void ShowError(string message, Exception ex)
        {
            UiMessages.ShowError(this, message, "التدريسيون", ex);
        }

        private void btnClearFacultyMemberForm_Click(object sender, EventArgs e)
        {
            ClearFacultyMemberForm();
        }
    }
}
