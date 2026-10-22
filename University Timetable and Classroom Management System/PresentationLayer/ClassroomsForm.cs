using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System
{
    public partial class ClassroomsForm : System.Windows.Forms.UserControl
    {
        private readonly ClassroomService classroomService = new();

        public ClassroomsForm()
        {
            InitializeComponent();
            if (DesignerEnvironment.IsDesignTime(this)) return;
            ArabicUi.Apply(this);
            ConfigureClassroomsGrid();
            ConfigureClassroomsEvents();
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (DesignerEnvironment.IsDesignTime(this) || IsDisposed || Disposing) return;
            await LoadClassroomsAsync();
            if (IsDisposed || Disposing) return;
            ClearClassroomForm();
        }


        private void ConfigureClassroomsGrid()
        {
            dgvClassrooms.AutoGenerateColumns = false;
            GridRowNumbers.Attach(dgvClassrooms, colRowNumber);
        }

        private void ConfigureClassroomsEvents()
        {
            dgvClassrooms.SelectionChanged += (_, _) => PopulateClassroomEditorFromSelection();

            btnAddClassroom.Click += async (_, _) => await AddClassroomAsync();
            btnUpdateClassroom.Click += async (_, _) => await UpdateClassroomAsync();
            btnDeleteClassroom.Click += async (_, _) => await DeleteClassroomAsync();
        }

        private async Task LoadClassroomsAsync()
        {
            if (IsDisposed || Disposing) return;

            SetClassroomActionsEnabled(false);

            try
            {
                var classrooms = await classroomService.GetAllAsync();
                if (IsDisposed || Disposing) return;
                dgvClassrooms.DataSource = classrooms;
                int lectureRoomCount = classrooms.Count(c => !ClassroomRules.IsLab(c));
                int labCount = classrooms.Count(ClassroomRules.IsLab);
                int sharedLabCount = classrooms.Count(ClassroomRules.IsSharedLab);
                lblTableTitle.Text = $"القاعات: {lectureRoomCount} نظرية و" +
                    $"{labCount} مختبرات (منها {sharedLabCount} مشتركة)";
                lblTableSubtitle.Text = $"قاعات المحاضرات: {lectureRoomCount} | " +
                    $"المختبرات: {labCount} (المشتركة للنظري والعملي: {sharedLabCount})";
                dgvClassrooms.ClearSelection();
            }
            catch (Exception ex)
            {
                ShowError("تعذر تحميل القاعات.", ex);
            }
            finally
            {
                SetClassroomActionsEnabled(true);
            }
        }

        private async Task AddClassroomAsync()
        {
            if (IsDisposed || Disposing) return;

            if (!TryBuildClassroom(out var classroom, requireId: false))
            {
                return;
            }

            await ExecuteClassroomActionAsync(
                async () => await classroomService.AddAsync(classroom),
                UiMessages.RecordAdded);
        }

        private async Task UpdateClassroomAsync()
        {
            if (IsDisposed || Disposing) return;

            if (!TryBuildClassroom(out var classroom))
            {
                return;
            }

            await ExecuteClassroomActionAsync(
                async () => await classroomService.UpdateAsync(classroom),
                UiMessages.RecordUpdated);
        }

        private async Task DeleteClassroomAsync()
        {
            if (IsDisposed || Disposing) return;

            if (!TryGetClassroomIdFromEditor(out int classroomId))
            {
                return;
            }

            var confirmation = UiMessages.ConfirmDeletion(this, "حذف القاعة");

            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            await ExecuteClassroomActionAsync(
                async () => await classroomService.DeleteAsync(classroomId),
                UiMessages.RecordDeleted);
        }

        private Task ExecuteClassroomActionAsync(Func<Task> action, string successMessage)
        {
            return EditorAction.RunAsync(this, action, LoadClassroomsAsync, SetClassroomActionsEnabled,
                () =>
                {
                    ClearClassroomForm();
                    ShowInformation(successMessage);
                },
                ex => ShowError("تعذر إتمام العملية على القاعة.", ex));
        }

        private bool TryBuildClassroom(out Classroom classroom, bool requireId = true)
        {
            classroom = new Classroom
            {
                ClassroomNumber = txtClassroomNumber.Text.Trim(),
                RoomType = ComboBoxHelper.GetSelectedPlainText(cmbRoomType)?.Trim()
            };

            var classroomId = 0;
            if (requireId && !TryGetClassroomIdFromEditor(out classroomId))
            {
                return false;
            }

            classroom.ClassroomID = requireId ? classroomId : 0;

            if (string.IsNullOrWhiteSpace(classroom.ClassroomNumber))
            {
                ShowInformation(UiMessages.RequiredFields);
                txtClassroomNumber.Focus();
                return false;
            }

            if (!int.TryParse(txtCapacity.Text, out int capacity) || capacity <= 0)
            {
                ShowInformation("يجب أن تكون السعة عدداً موجباً.");
                txtCapacity.Focus();
                return false;
            }

            classroom.Capacity = capacity;

            if (string.IsNullOrWhiteSpace(classroom.RoomType))
            {
                classroom.RoomType = null;
            }

            return true;
        }

        private bool TryGetClassroomIdFromEditor(out int classroomId)
        {
            if (int.TryParse(txtClassroomId.Text, out classroomId) && classroomId > 0)
            {
                return true;
            }

            ShowInformation("اختر قاعة من الجدول أولاً.");
            return false;
        }

        private void PopulateClassroomEditorFromSelection()
        {
            if (IsDisposed || Disposing) return;

            if (dgvClassrooms.CurrentRow?.DataBoundItem is not Classroom classroom)
            {
                return;
            }

            txtClassroomId.Text = classroom.ClassroomID.ToString();
            txtClassroomNumber.Text = classroom.ClassroomNumber;
            txtCapacity.Text = classroom.Capacity.ToString();
            ComboBoxHelper.SelectText(cmbRoomType, classroom.RoomType, addMissing: true);
        }

        private void ClearClassroomForm()
        {
            if (IsDisposed || Disposing) return;

            txtClassroomId.Text = "تلقائي";
            txtClassroomNumber.Clear();
            txtCapacity.Clear();
            cmbRoomType.SelectedIndex = -1;
            dgvClassrooms.ClearSelection();
            txtClassroomNumber.Focus();
        }

        private void SetClassroomActionsEnabled(bool enabled)
        {
            if (IsDisposed || Disposing) return;

            btnAddClassroom.Enabled = enabled;
            btnUpdateClassroom.Enabled = enabled;
            btnDeleteClassroom.Enabled = enabled;
            dgvClassrooms.Enabled = enabled;
        }

        private void ShowInformation(string message)
        {
            UiMessages.ShowInformation(this, message, "القاعات");
        }

        private void ShowError(string message, Exception ex)
        {
            UiMessages.ShowError(this, message, "القاعات", ex);
        }

    }
}
