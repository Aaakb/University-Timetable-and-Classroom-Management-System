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
            if (DesignerEnvironment.IsDesignTime(this)) return;
            ArabicUi.Apply(this);
            ConfigureTimeSlotsGrid();
            ConfigureTimeSlotsEvents();
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (DesignerEnvironment.IsDesignTime(this) || IsDisposed || Disposing) return;
            await LoadTimeSlotsAsync();
            if (IsDisposed || Disposing) return;
            ClearTimeSlotForm();
        }


        private void ConfigureTimeSlotsGrid()
        {
            dgvTimeSlots.AutoGenerateColumns = false;
            GridRowNumbers.Attach(dgvTimeSlots, colRowNumber);
            dgvTimeSlots.CellFormatting += FormatTimeSlotCell;
        }

        private void ConfigureTimeSlotsEvents()
        {
            dgvTimeSlots.SelectionChanged += (_, _) => PopulateTimeSlotEditorFromSelection();

            btnAddTimeSlot.Click += async (_, _) => await AddTimeSlotAsync();
            btnUpdateTimeSlot.Click += async (_, _) => await UpdateTimeSlotAsync();
            btnDeleteTimeSlot.Click += async (_, _) => await DeleteTimeSlotAsync();
        }

        private async Task LoadTimeSlotsAsync()
        {
            if (IsDisposed || Disposing) return;

            SetTimeSlotActionsEnabled(false);

            try
            {
                var timeSlots = await timeSlotService.GetAllAsync();
                if (IsDisposed || Disposing) return;
                dgvTimeSlots.DataSource = timeSlots;
                dgvTimeSlots.ClearSelection();
            }
            catch (Exception ex)
            {
                ShowError("تعذر تحميل الفترات الدراسية.", ex);
            }
            finally
            {
                SetTimeSlotActionsEnabled(true);
            }
        }

        private async Task AddTimeSlotAsync()
        {
            if (IsDisposed || Disposing) return;

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
            if (IsDisposed || Disposing) return;

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
            if (IsDisposed || Disposing) return;

            if (!TryGetTimeSlotIdFromEditor(out int timeSlotId))
            {
                return;
            }

            var confirmation = UiMessages.ConfirmDeletion(this, "حذف الفترة الدراسية");

            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            await ExecuteTimeSlotActionAsync(
                async () => await timeSlotService.DeleteAsync(timeSlotId),
                UiMessages.RecordDeleted);
        }

        private Task ExecuteTimeSlotActionAsync(Func<Task> action, string successMessage)
        {
            return EditorAction.RunAsync(this, action, LoadTimeSlotsAsync, SetTimeSlotActionsEnabled,
                () =>
                {
                    ClearTimeSlotForm();
                    ShowInformation(successMessage);
                },
                ex => ShowError("تعذر إتمام العملية على الفترة الدراسية.", ex));
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

            try
            {
                TimeSlotService.ValidateLectureRange(timeSlot.StartTime, timeSlot.EndTime);
                return true;
            }
            catch (ArgumentException ex)
            {
                UiMessages.ShowWarning(this, ex.Message, "الفترات الدراسية");
                dtpStartTime.Focus();
                return false;
            }
        }

        private bool TryGetTimeSlotIdFromEditor(out int timeSlotId)
        {
            if (int.TryParse(txtTimeSlotId.Text, out timeSlotId) && timeSlotId > 0)
            {
                return true;
            }

            ShowInformation("اختر فترة دراسية من الجدول أولاً.");
            return false;
        }

        private void PopulateTimeSlotEditorFromSelection()
        {
            if (IsDisposed || Disposing) return;

            if (dgvTimeSlots.CurrentRow?.DataBoundItem is not TimeSlot timeSlot)
            {
                return;
            }

            txtTimeSlotId.Text = timeSlot.TimeSlotID.ToString();
            dtpStartTime.Value = DateTime.Today.Add(timeSlot.StartTime);
            dtpEndTime.Value = DateTime.Today.Add(timeSlot.EndTime);
        }

        private void ClearTimeSlotForm()
        {
            if (IsDisposed || Disposing) return;

            txtTimeSlotId.Text = "تلقائي";
            var period = TimeSlotService.DefaultLecturePeriod;
            dtpStartTime.Value = DateTime.Today.Add(period.Start);
            dtpEndTime.Value = DateTime.Today.Add(period.End);
            dgvTimeSlots.ClearSelection();
            dtpStartTime.Focus();
        }

        private void SetTimeSlotActionsEnabled(bool enabled)
        {
            if (IsDisposed || Disposing) return;

            btnAddTimeSlot.Enabled = enabled;
            btnUpdateTimeSlot.Enabled = enabled;
            btnDeleteTimeSlot.Enabled = enabled;
            dgvTimeSlots.Enabled = enabled;
        }

        private void FormatTimeSlotCell(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (IsDisposed || Disposing) return;

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
            UiMessages.ShowInformation(this, message, "الفترات الدراسية");
        }

        private void ShowError(string message, Exception ex)
        {
            UiMessages.ShowError(this, message, "الفترات الدراسية", ex);
        }

    }
}
