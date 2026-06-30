using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System
{
    public partial class TimeSlotsForm : System.Windows.Forms.UserControl
    {
        private readonly TimeSlotService timeSlotService = new();

        public TimeSlotsForm()
        {
            InitializeComponent();
            ConfigureAutoIdField();
            ConfigureNavigation();
            ConfigureTimeSlotsGrid();
            ConfigureTimeSlotsEvents();
        }

        private void ConfigureAutoIdField()
        {
            txtTimeSlotId.ReadOnly = true;
            txtTimeSlotId.TabStop = false;
            txtTimeSlotId.PlaceholderText = "Auto";
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await LoadTimeSlotsAsync();
            ClearTimeSlotForm();
        }

        private void ConfigureNavigation()
        {
            FormNavigation.ConfigureSidebar(this, pnlSidebar, NavigationPage.TimeSlots);
        }

        private void ConfigureTimeSlotsGrid()
        {
            dgvTimeSlots.AutoGenerateColumns = false;
            dgvTimeSlots.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            GridStyle.Apply(dgvTimeSlots);
            dgvTimeSlots.CellFormatting += FormatTimeSlotCell;
        }

        private void ConfigureTimeSlotsEvents()
        {
            dgvTimeSlots.SelectionChanged += (_, _) => PopulateTimeSlotEditorFromSelection();
            txtTimeSlotId.Leave += async (_, _) => await PopulateTimeSlotEditorFromEnteredIdAsync();
            btnAddTimeSlot.Click += async (_, _) => await AddTimeSlotAsync();
            btnUpdateTimeSlot.Click += async (_, _) => await UpdateTimeSlotAsync();
            btnDeleteTimeSlot.Click += async (_, _) => await DeleteTimeSlotAsync();
        }

        private async Task LoadTimeSlotsAsync()
        {
            SetTimeSlotActionsEnabled(false);

            try
            {
                var timeSlots = await timeSlotService.GetAllAsync();
                dgvTimeSlots.DataSource = timeSlots;
                dgvTimeSlots.ClearSelection();
            }
            catch (Exception ex)
            {
                ShowError("Unable to load time slots.", ex);
            }
            finally
            {
                SetTimeSlotActionsEnabled(true);
            }
        }

        private async Task AddTimeSlotAsync()
        {
            if (!TryBuildTimeSlot(out var timeSlot, requireId: false))
            {
                return;
            }

            await ExecuteTimeSlotActionAsync(
                async () => await timeSlotService.AddAsync(timeSlot),
                UiMessages.RecordAdded);
        }

        private async Task UpdateTimeSlotAsync()
        {
            if (!TryBuildTimeSlot(out var timeSlot))
            {
                return;
            }

            await ExecuteTimeSlotActionAsync(
                async () => await timeSlotService.UpdateAsync(timeSlot),
                UiMessages.RecordUpdated);
        }

        private async Task DeleteTimeSlotAsync()
        {
            if (!TryGetTimeSlotIdFromEditor(out int timeSlotId))
            {
                return;
            }

            var confirmation = UiMessages.ConfirmDeletion(this, "Delete Time Slot");

            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            await ExecuteTimeSlotActionAsync(
                async () => await timeSlotService.DeleteAsync(timeSlotId),
                UiMessages.RecordDeleted);
        }

        private async Task ExecuteTimeSlotActionAsync(Func<Task> action, string successMessage)
        {
            SetTimeSlotActionsEnabled(false);

            try
            {
                await action();
                await LoadTimeSlotsAsync();
                ClearTimeSlotForm();
                ShowInformation(successMessage);
            }
            catch (Exception ex)
            {
                ShowError("Unable to complete the time slot operation.", ex);
            }
            finally
            {
                SetTimeSlotActionsEnabled(true);
            }
        }

        private bool TryBuildTimeSlot(out TimeSlot timeSlot, bool requireId = true)
        {
            timeSlot = new TimeSlot
            {
                StartTime = ToMinutePrecision(dtpStartTime.Value),
                EndTime = ToMinutePrecision(dtpEndTime.Value),
                IsBreak = false
            };

            var timeSlotId = 0;
            if (requireId && !TryGetTimeSlotIdFromEditor(out timeSlotId))
            {
                return false;
            }

            timeSlot.TimeSlotID = requireId ? timeSlotId : 0;

            if (timeSlot.EndTime > timeSlot.StartTime)
            {
                return true;
            }

            ShowInformation("End time must be after start time.");
            dtpEndTime.Focus();
            return false;
        }

        private bool TryGetTimeSlotIdFromEditor(out int timeSlotId)
        {
            if (int.TryParse(txtTimeSlotId.Text, out timeSlotId) && timeSlotId > 0)
            {
                return true;
            }

            ShowInformation("Select a time slot row first.");
            return false;
        }

        private void PopulateTimeSlotEditorFromSelection()
        {
            if (dgvTimeSlots.CurrentRow?.DataBoundItem is not TimeSlot timeSlot)
            {
                return;
            }

            txtTimeSlotId.Text = timeSlot.TimeSlotID.ToString();
            dtpStartTime.Value = DateTime.Today.Add(timeSlot.StartTime);
            dtpEndTime.Value = DateTime.Today.Add(timeSlot.EndTime);
        }

        private async Task PopulateTimeSlotEditorFromEnteredIdAsync()
        {
            if (!int.TryParse(txtTimeSlotId.Text, out int timeSlotId) || timeSlotId <= 0)
            {
                return;
            }

            try
            {
                var timeSlot = await timeSlotService.GetByIdAsync(timeSlotId);

                if (timeSlot is null)
                {
                    return;
                }

                dtpStartTime.Value = DateTime.Today.Add(timeSlot.StartTime);
                dtpEndTime.Value = DateTime.Today.Add(timeSlot.EndTime);
                SelectTimeSlotRow(timeSlot.TimeSlotID);
            }
            catch (Exception ex)
            {
                ShowError("Unable to load time slot details.", ex);
            }
        }

        private void SelectTimeSlotRow(int timeSlotId)
        {
            foreach (DataGridViewRow row in dgvTimeSlots.Rows)
            {
                if (row.DataBoundItem is not TimeSlot timeSlot || timeSlot.TimeSlotID != timeSlotId)
                {
                    continue;
                }

                row.Selected = true;
                dgvTimeSlots.CurrentCell = row.Cells[0];
                break;
            }
        }

        private void ClearTimeSlotForm()
        {
            txtTimeSlotId.Text = "Auto";
            dtpStartTime.Value = DateTime.Today.AddHours(9);
            dtpEndTime.Value = DateTime.Today.AddHours(10).AddMinutes(30);
            dgvTimeSlots.ClearSelection();
            dtpStartTime.Focus();
        }

        private void SetTimeSlotActionsEnabled(bool enabled)
        {
            btnAddTimeSlot.Enabled = enabled;
            btnUpdateTimeSlot.Enabled = enabled;
            btnDeleteTimeSlot.Enabled = enabled;
            dgvTimeSlots.Enabled = enabled;
        }

        private void FormatTimeSlotCell(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            if ((dgvTimeSlots.Columns[e.ColumnIndex] == colStartTime ||
                 dgvTimeSlots.Columns[e.ColumnIndex] == colEndTime) &&
                e.Value is TimeSpan time)
            {
                e.Value = TimeDisplay.Format(time);
                e.FormattingApplied = true;
                return;
            }
        }

        private static TimeSpan ToMinutePrecision(DateTime value)
        {
            return new TimeSpan(value.Hour, value.Minute, 0);
        }

        private void ShowInformation(string message)
        {
            UiMessages.ShowInformation(this, message, "Time Slots");
        }

        private void ShowError(string message, Exception ex)
        {
            UiMessages.ShowError(this, message, "Time Slots", ex);
        }
        private void InitializeComponent()
        {
            pnlSidebar = new Guna.UI2.WinForms.Guna2Panel();
            pnlMain = new Guna.UI2.WinForms.Guna2Panel();
            pnlHeader = new Guna.UI2.WinForms.Guna2Panel();
            lblPageTitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblPageSubtitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            pnlWorkspace = new Guna.UI2.WinForms.Guna2Panel();
            pnlTimeSlotEditor = new Guna.UI2.WinForms.Guna2Panel();
            lblEditorTitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblEditorSubtitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblTimeSlotId = new Guna.UI2.WinForms.Guna2HtmlLabel();
            txtTimeSlotId = new Guna.UI2.WinForms.Guna2TextBox();
            lblStartTime = new Guna.UI2.WinForms.Guna2HtmlLabel();
            dtpStartTime = new Guna.UI2.WinForms.Guna2DateTimePicker();
            lblEndTime = new Guna.UI2.WinForms.Guna2HtmlLabel();
            dtpEndTime = new Guna.UI2.WinForms.Guna2DateTimePicker();
            btnAddTimeSlot = new Guna.UI2.WinForms.Guna2Button();
            btnUpdateTimeSlot = new Guna.UI2.WinForms.Guna2Button();
            btnDeleteTimeSlot = new Guna.UI2.WinForms.Guna2Button();
            pnlTimeSlotsTable = new Guna.UI2.WinForms.Guna2Panel();
            lblTableTitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            lblTableSubtitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            dgvTimeSlots = new Guna.UI2.WinForms.Guna2DataGridView();
            colTimeSlotId = new DataGridViewTextBoxColumn();
            colStartTime = new DataGridViewTextBoxColumn();
            colEndTime = new DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)dgvTimeSlots).BeginInit();
            SuspendLayout();

            pnlSidebar.Name = "pnlSidebar";
            pnlSidebar.Dock = DockStyle.Left;
            pnlSidebar.Size = new Size(0, 720);
            pnlSidebar.Visible = false;

            pnlMain.Name = "pnlMain";
            pnlMain.Dock = DockStyle.Fill;
            pnlMain.FillColor = Color.FromArgb(245, 247, 250);
            pnlMain.Controls.Add(pnlWorkspace);
            pnlMain.Controls.Add(pnlHeader);

            pnlHeader.Name = "pnlHeader";
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 88;
            pnlHeader.FillColor = Color.White;
            pnlHeader.Controls.Add(lblPageTitle);
            pnlHeader.Controls.Add(lblPageSubtitle);

            lblPageTitle.Name = "lblPageTitle";
            lblPageTitle.BackColor = Color.Transparent;
            lblPageTitle.Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold);
            lblPageTitle.ForeColor = Color.FromArgb(15, 23, 42);
            lblPageTitle.Location = new Point(32, 16);
            lblPageTitle.Text = "Time Slots Management";

            lblPageSubtitle.Name = "lblPageSubtitle";
            lblPageSubtitle.BackColor = Color.Transparent;
            lblPageSubtitle.Font = new Font("Segoe UI", 10F);
            lblPageSubtitle.ForeColor = Color.FromArgb(100, 116, 139);
            lblPageSubtitle.Location = new Point(32, 50);
            lblPageSubtitle.Text = "Manage lecture times with 10-minute gaps between sessions.";

            pnlWorkspace.Name = "pnlWorkspace";
            pnlWorkspace.Dock = DockStyle.Fill;
            pnlWorkspace.FillColor = Color.FromArgb(245, 247, 250);
            pnlWorkspace.Padding = new Padding(28, 24, 28, 28);
            pnlWorkspace.Controls.Add(pnlTimeSlotsTable);
            pnlWorkspace.Controls.Add(pnlTimeSlotEditor);

            pnlTimeSlotEditor.Name = "pnlTimeSlotEditor";
            pnlTimeSlotEditor.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnlTimeSlotEditor.BorderColor = Color.FromArgb(226, 232, 240);
            pnlTimeSlotEditor.BorderRadius = 8;
            pnlTimeSlotEditor.BorderThickness = 1;
            pnlTimeSlotEditor.FillColor = Color.White;
            pnlTimeSlotEditor.Location = new Point(28, 24);
            pnlTimeSlotEditor.Size = new Size(1084, 190);
            pnlTimeSlotEditor.Controls.Add(lblEditorTitle);
            pnlTimeSlotEditor.Controls.Add(lblEditorSubtitle);
            pnlTimeSlotEditor.Controls.Add(lblTimeSlotId);
            pnlTimeSlotEditor.Controls.Add(txtTimeSlotId);
            pnlTimeSlotEditor.Controls.Add(lblStartTime);
            pnlTimeSlotEditor.Controls.Add(dtpStartTime);
            pnlTimeSlotEditor.Controls.Add(lblEndTime);
            pnlTimeSlotEditor.Controls.Add(dtpEndTime);
            pnlTimeSlotEditor.Controls.Add(btnAddTimeSlot);
            pnlTimeSlotEditor.Controls.Add(btnUpdateTimeSlot);
            pnlTimeSlotEditor.Controls.Add(btnDeleteTimeSlot);

            lblEditorTitle.Name = "lblEditorTitle";
            lblEditorTitle.BackColor = Color.Transparent;
            lblEditorTitle.Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold);
            lblEditorTitle.ForeColor = Color.FromArgb(15, 23, 42);
            lblEditorTitle.Location = new Point(24, 18);
            lblEditorTitle.Text = "Time Slot Details";

            lblEditorSubtitle.Name = "lblEditorSubtitle";
            lblEditorSubtitle.BackColor = Color.Transparent;
            lblEditorSubtitle.Font = new Font("Segoe UI", 9F);
            lblEditorSubtitle.ForeColor = Color.FromArgb(100, 116, 139);
            lblEditorSubtitle.Location = new Point(24, 44);
            lblEditorSubtitle.Text = "Every lecture slot must leave a 10-minute gap before the next one.";

            lblTimeSlotId.Name = "lblTimeSlotId";
            lblTimeSlotId.BackColor = Color.Transparent;
            lblTimeSlotId.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            lblTimeSlotId.ForeColor = Color.FromArgb(51, 65, 85);
            lblTimeSlotId.Location = new Point(24, 92);
            lblTimeSlotId.Text = "Time Slot ID";

            txtTimeSlotId.Name = "txtTimeSlotId";
            txtTimeSlotId.BorderRadius = 8;
            txtTimeSlotId.BorderColor = Color.FromArgb(203, 213, 225);
            txtTimeSlotId.FillColor = Color.White;
            txtTimeSlotId.Font = new Font("Segoe UI", 10F);
            txtTimeSlotId.ForeColor = Color.FromArgb(15, 23, 42);
            txtTimeSlotId.Location = new Point(24, 116);
            txtTimeSlotId.PlaceholderText = "Auto";
            txtTimeSlotId.Size = new Size(140, 42);

            lblStartTime.Name = "lblStartTime";
            lblStartTime.BackColor = Color.Transparent;
            lblStartTime.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            lblStartTime.ForeColor = Color.FromArgb(51, 65, 85);
            lblStartTime.Location = new Point(190, 92);
            lblStartTime.Text = "Start Time";

            dtpStartTime.Name = "dtpStartTime";
            dtpStartTime.BorderRadius = 8;
            dtpStartTime.Checked = true;
            dtpStartTime.CustomFormat = "hh:mm tt";
            dtpStartTime.FillColor = Color.White;
            dtpStartTime.Font = new Font("Segoe UI", 10F);
            dtpStartTime.Format = DateTimePickerFormat.Custom;
            dtpStartTime.Location = new Point(190, 116);
            dtpStartTime.ShowUpDown = true;
            dtpStartTime.Size = new Size(128, 42);

            lblEndTime.Name = "lblEndTime";
            lblEndTime.BackColor = Color.Transparent;
            lblEndTime.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            lblEndTime.ForeColor = Color.FromArgb(51, 65, 85);
            lblEndTime.Location = new Point(344, 92);
            lblEndTime.Text = "End Time";

            dtpEndTime.Name = "dtpEndTime";
            dtpEndTime.BorderRadius = 8;
            dtpEndTime.Checked = true;
            dtpEndTime.CustomFormat = "hh:mm tt";
            dtpEndTime.FillColor = Color.White;
            dtpEndTime.Font = new Font("Segoe UI", 10F);
            dtpEndTime.Format = DateTimePickerFormat.Custom;
            dtpEndTime.Location = new Point(344, 116);
            dtpEndTime.ShowUpDown = true;
            dtpEndTime.Size = new Size(128, 42);

            btnAddTimeSlot.Name = "btnAddTimeSlot";
            btnAddTimeSlot.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnAddTimeSlot.BorderRadius = 8;
            btnAddTimeSlot.Cursor = Cursors.Hand;
            btnAddTimeSlot.FillColor = Color.FromArgb(22, 163, 74);
            btnAddTimeSlot.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnAddTimeSlot.ForeColor = Color.White;
            btnAddTimeSlot.Location = new Point(736, 116);
            btnAddTimeSlot.Size = new Size(104, 38);
            btnAddTimeSlot.Text = "Add";

            btnUpdateTimeSlot.Name = "btnUpdateTimeSlot";
            btnUpdateTimeSlot.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnUpdateTimeSlot.BorderRadius = 8;
            btnUpdateTimeSlot.Cursor = Cursors.Hand;
            btnUpdateTimeSlot.FillColor = Color.FromArgb(37, 99, 235);
            btnUpdateTimeSlot.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnUpdateTimeSlot.ForeColor = Color.White;
            btnUpdateTimeSlot.Location = new Point(854, 116);
            btnUpdateTimeSlot.Size = new Size(104, 38);
            btnUpdateTimeSlot.Text = "Update";

            btnDeleteTimeSlot.Name = "btnDeleteTimeSlot";
            btnDeleteTimeSlot.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnDeleteTimeSlot.BorderRadius = 8;
            btnDeleteTimeSlot.Cursor = Cursors.Hand;
            btnDeleteTimeSlot.FillColor = Color.FromArgb(220, 38, 38);
            btnDeleteTimeSlot.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnDeleteTimeSlot.ForeColor = Color.White;
            btnDeleteTimeSlot.Location = new Point(972, 116);
            btnDeleteTimeSlot.Size = new Size(88, 38);
            btnDeleteTimeSlot.Text = "Delete";

            pnlTimeSlotsTable.Name = "pnlTimeSlotsTable";
            pnlTimeSlotsTable.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pnlTimeSlotsTable.BorderColor = Color.FromArgb(226, 232, 240);
            pnlTimeSlotsTable.BorderRadius = 8;
            pnlTimeSlotsTable.BorderThickness = 1;
            pnlTimeSlotsTable.FillColor = Color.White;
            pnlTimeSlotsTable.Location = new Point(28, 236);
            pnlTimeSlotsTable.Size = new Size(1084, 456);
            pnlTimeSlotsTable.Controls.Add(lblTableTitle);
            pnlTimeSlotsTable.Controls.Add(lblTableSubtitle);
            pnlTimeSlotsTable.Controls.Add(dgvTimeSlots);

            lblTableTitle.Name = "lblTableTitle";
            lblTableTitle.BackColor = Color.Transparent;
            lblTableTitle.Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold);
            lblTableTitle.ForeColor = Color.FromArgb(15, 23, 42);
            lblTableTitle.Location = new Point(24, 18);
            lblTableTitle.Text = "Time Slots List";

            lblTableSubtitle.Name = "lblTableSubtitle";
            lblTableSubtitle.BackColor = Color.Transparent;
            lblTableSubtitle.Font = new Font("Segoe UI", 9F);
            lblTableSubtitle.ForeColor = Color.FromArgb(100, 116, 139);
            lblTableSubtitle.Location = new Point(24, 44);
            lblTableSubtitle.Text = "Only lecture slots are shown; break time is handled as spacing.";

            dgvTimeSlots.Name = "dgvTimeSlots";
            dgvTimeSlots.AllowUserToAddRows = false;
            dgvTimeSlots.AllowUserToDeleteRows = false;
            dgvTimeSlots.AllowUserToResizeRows = false;
            dgvTimeSlots.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvTimeSlots.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvTimeSlots.BackgroundColor = Color.White;
            dgvTimeSlots.BorderStyle = BorderStyle.None;
            dgvTimeSlots.ColumnHeadersHeight = 44;
            dgvTimeSlots.Columns.AddRange(new DataGridViewColumn[] { colTimeSlotId, colStartTime, colEndTime });
            dgvTimeSlots.Location = new Point(24, 78);
            dgvTimeSlots.MultiSelect = false;
            dgvTimeSlots.ReadOnly = true;
            dgvTimeSlots.RowHeadersVisible = false;
            dgvTimeSlots.RowTemplate.Height = 42;
            dgvTimeSlots.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvTimeSlots.Size = new Size(1036, 354);

            colTimeSlotId.DataPropertyName = "TimeSlotID";
            colTimeSlotId.FillWeight = 45F;
            colTimeSlotId.HeaderText = "Time Slot ID";
            colTimeSlotId.Name = "colTimeSlotId";
            colTimeSlotId.ReadOnly = true;

            colStartTime.DataPropertyName = "StartTime";
            colStartTime.FillWeight = 85F;
            colStartTime.HeaderText = "Start Time";
            colStartTime.Name = "colStartTime";
            colStartTime.ReadOnly = true;

            colEndTime.DataPropertyName = "EndTime";
            colEndTime.FillWeight = 85F;
            colEndTime.HeaderText = "End Time";
            colEndTime.Name = "colEndTime";
            colEndTime.ReadOnly = true;

            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(245, 247, 250);
            Controls.Add(pnlMain);
            Controls.Add(pnlSidebar);
            Font = new Font("Segoe UI", 9F);
            MinimumSize = new Size(980, 600);
            Name = "TimeSlotsForm";
            Size = new Size(1180, 720);
            ((System.ComponentModel.ISupportInitialize)dgvTimeSlots).EndInit();
            ResumeLayout(false);
        }

        private Guna.UI2.WinForms.Guna2Panel pnlSidebar = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlMain = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlHeader = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblPageTitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblPageSubtitle = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlWorkspace = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlTimeSlotEditor = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblEditorTitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblEditorSubtitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblTimeSlotId = null!;
        private Guna.UI2.WinForms.Guna2TextBox txtTimeSlotId = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblStartTime = null!;
        private Guna.UI2.WinForms.Guna2DateTimePicker dtpStartTime = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblEndTime = null!;
        private Guna.UI2.WinForms.Guna2DateTimePicker dtpEndTime = null!;
        private Guna.UI2.WinForms.Guna2Button btnAddTimeSlot = null!;
        private Guna.UI2.WinForms.Guna2Button btnUpdateTimeSlot = null!;
        private Guna.UI2.WinForms.Guna2Button btnDeleteTimeSlot = null!;
        private Guna.UI2.WinForms.Guna2Panel pnlTimeSlotsTable = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblTableTitle = null!;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblTableSubtitle = null!;
        private Guna.UI2.WinForms.Guna2DataGridView dgvTimeSlots = null!;
        private DataGridViewTextBoxColumn colTimeSlotId = null!;
        private DataGridViewTextBoxColumn colStartTime = null!;
        private DataGridViewTextBoxColumn colEndTime = null!;
    }
}

